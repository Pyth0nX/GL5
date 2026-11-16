using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Reduces CPU load caused by high-poly skinned characters in view (e.g. multiple VRoid NPCs)
/// without reducing mesh vertices or visual silhouette quality.
/// Scales SkinnedMeshRenderer flags and staggers/throttles offscreen & background NPC Animator updates
/// according to the active QualitySettings level (Low, Medium, High).
/// </summary>
[DisallowMultipleComponent]
public class CharacterPerformanceOptimizer : MonoBehaviour
{
    private const float LowCloseDistanceSqr = 5.5f * 5.5f;
    private const float LowCloseUpdateInterval = 1f / 24f;
    private const float LowFarUpdateInterval = 1f / 15f;

    private const float MediumFullRateDistanceSqr = 3.5f * 3.5f;
    private const float MediumMidDistanceSqr = 9f * 9f;
    private const float MediumMidUpdateInterval = 1f / 30f;
    private const float MediumFarUpdateInterval = 1f / 20f;

    private const float HighFullRateDistanceSqr = 11f * 11f;
    private const float HighFarUpdateInterval = 1f / 30f;

    private const float MaxAccumulatedDeltaTime = 0.12f;
    private const int LowMaxAnimatorsUpdatedPerFrame = 4;
    private const int MediumMaxAnimatorsUpdatedPerFrame = 6;
    private const int HighMaxAnimatorsUpdatedPerFrame = 8;

    private sealed class TrackedCharacterAnimator
    {
        public Animator Animator;
        public Transform Root;
        public Renderer[] Renderers;
        public NPCConversationReaction Reaction;
        public float Accumulator;
        public bool WasVisible;
    }

    private sealed class TrackedSkinnedMesh
    {
        public SkinnedMeshRenderer Renderer;
        public ShadowCastingMode OriginalShadowCastingMode;
        public bool IsOutlineHelper;
        public bool IsFaceMesh;
    }

    private readonly List<TrackedCharacterAnimator> _trackedAnimators = new List<TrackedCharacterAnimator>();
    private readonly List<TrackedSkinnedMesh> _trackedSkinnedMeshes = new List<TrackedSkinnedMesh>();

    private int _currentQualityIndex = 1;
    private int _updateCursor;
    private Camera _cachedMainCamera;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (GraphicsSettingsManager.Instance != null)
        {
            GraphicsSettingsManager.Instance.OnQualityLevelChanged -= HandleQualityChanged;
            GraphicsSettingsManager.Instance.OnQualityLevelChanged += HandleQualityChanged;
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (GraphicsSettingsManager.Instance != null)
        {
            GraphicsSettingsManager.Instance.OnQualityLevelChanged -= HandleQualityChanged;
        }

        RestoreAllAnimators();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _cachedMainCamera = null;
        RefreshAndApply(QualitySettings.GetQualityLevel());
    }

    /// <summary>
    /// Handles quality level switches triggered by GraphicsSettingsManager.
    /// </summary>
    public void HandleQualityChanged(int qualityIndex)
    {
        RefreshAndApply(qualityIndex);
    }

    /// <summary>
    /// Rescans active scene characters and applies CPU/rendering optimizations for the given quality tier.
    /// </summary>
    public void RefreshAndApply(int qualityIndex)
    {
        _currentQualityIndex = qualityIndex;
        RebuildSceneCaches();
        ApplySkinnedMeshSettings(qualityIndex);
        ApplyAnimatorSettings(qualityIndex);
    }

    private void RebuildSceneCaches()
    {
        Dictionary<SkinnedMeshRenderer, ShadowCastingMode> previousShadows =
            new Dictionary<SkinnedMeshRenderer, ShadowCastingMode>(_trackedSkinnedMeshes.Count);

        for (int i = 0; i < _trackedSkinnedMeshes.Count; i++)
        {
            TrackedSkinnedMesh item = _trackedSkinnedMeshes[i];
            if (item.Renderer != null && !previousShadows.ContainsKey(item.Renderer))
            {
                previousShadows.Add(item.Renderer, item.OriginalShadowCastingMode);
            }
        }

        _trackedSkinnedMeshes.Clear();
        SkinnedMeshRenderer[] allSmrs = UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include);
        for (int i = 0; i < allSmrs.Length; i++)
        {
            SkinnedMeshRenderer smr = allSmrs[i];
            if (smr == null)
            {
                continue;
            }

            ShadowCastingMode originalShadow = previousShadows.TryGetValue(smr, out ShadowCastingMode cached)
                ? cached
                : smr.shadowCastingMode;

            bool isHelper = smr.gameObject.name == "[OutlineHelper]";
            bool isFace = smr.gameObject.name.IndexOf("Face", StringComparison.OrdinalIgnoreCase) >= 0;

            _trackedSkinnedMeshes.Add(new TrackedSkinnedMesh
            {
                Renderer = smr,
                OriginalShadowCastingMode = originalShadow,
                IsOutlineHelper = isHelper,
                IsFaceMesh = isFace
            });
        }

        _trackedAnimators.Clear();
        Animator[] allAnimators = UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsInactive.Include);
        int phaseIndex = 0;
        for (int i = 0; i < allAnimators.Length; i++)
        {
            Animator anim = allAnimators[i];
            if (anim == null || anim.runtimeAnimatorController == null)
            {
                continue;
            }

            // Never throttle the player or non-character animators (UI, doors, props without skinned meshes)
            if (anim.GetComponentInParent<PlayerController>() != null)
            {
                continue;
            }

            SkinnedMeshRenderer[] childSmrs = anim.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (childSmrs == null || childSmrs.Length == 0)
            {
                continue;
            }

            // Cache only character meshes (ignore [OutlineHelper] children)
            List<Renderer> characterRenderers = new List<Renderer>(childSmrs.Length);
            for (int j = 0; j < childSmrs.Length; j++)
            {
                if (childSmrs[j] != null && childSmrs[j].gameObject.name != "[OutlineHelper]")
                {
                    characterRenderers.Add(childSmrs[j]);
                }
            }

            if (characterRenderers.Count == 0)
            {
                continue;
            }

            _trackedAnimators.Add(new TrackedCharacterAnimator
            {
                Animator = anim,
                Root = anim.transform,
                Renderers = characterRenderers.ToArray(),
                Reaction = anim.GetComponentInParent<NPCConversationReaction>(),
                // Stagger initial phase so visible characters distribute across frames instead of spiking together
                Accumulator = (phaseIndex++ % 4) * 0.012f,
                WasVisible = false
            });
        }

        _updateCursor = 0;
    }

    private void ApplySkinnedMeshSettings(int qualityIndex)
    {
        for (int i = 0; i < _trackedSkinnedMeshes.Count; i++)
        {
            TrackedSkinnedMesh item = _trackedSkinnedMeshes[i];
            SkinnedMeshRenderer smr = item.Renderer;
            if (smr == null)
            {
                continue;
            }

            smr.updateWhenOffscreen = false;
            smr.allowOcclusionWhenDynamic = true;
            // Motion Blur / TAA motion vectors are disabled across presets, so skip per-vertex motion vector skinning
            smr.skinnedMotionVectors = false;
            smr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

            if (item.IsOutlineHelper)
            {
                smr.shadowCastingMode = ShadowCastingMode.Off;
                smr.quality = SkinQuality.Bone1;
                continue;
            }

            if (qualityIndex <= 0)
            {
                // Low: skip shadow submission and reflection probe CPU culling
                smr.shadowCastingMode = ShadowCastingMode.Off;
                smr.reflectionProbeUsage = ReflectionProbeUsage.Off;
                smr.quality = SkinQuality.Auto;
            }
            else if (qualityIndex == 1)
            {
                // Medium: single-probe lookup and skip shadow casting on inner Face submeshes
                smr.reflectionProbeUsage = ReflectionProbeUsage.Simple;
                smr.shadowCastingMode = item.IsFaceMesh ? ShadowCastingMode.Off : item.OriginalShadowCastingMode;
                smr.quality = SkinQuality.Auto;
            }
            else
            {
                // High: blended probes and full character shadows
                smr.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                smr.shadowCastingMode = item.OriginalShadowCastingMode;
                smr.quality = SkinQuality.Auto;
            }
        }
    }

    private void ApplyAnimatorSettings(int qualityIndex)
    {
        for (int i = 0; i < _trackedAnimators.Count; i++)
        {
            TrackedCharacterAnimator entry = _trackedAnimators[i];
            if (entry.Animator == null)
            {
                continue;
            }

            entry.Animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            entry.Accumulator = (i % 4) * 0.01f;
        }
    }

    private void RestoreAllAnimators()
    {
        for (int i = 0; i < _trackedAnimators.Count; i++)
        {
            if (_trackedAnimators[i].Animator != null)
            {
                _trackedAnimators[i].Animator.enabled = true;
            }
        }
    }

    private void LateUpdate()
    {
        if (_trackedAnimators.Count == 0)
        {
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f)
        {
            return;
        }

        Transform referenceTransform = ResolveReferenceTransform();
        bool hasRef = referenceTransform != null;
        Vector3 refPos = hasRef ? referenceTransform.position : Vector3.zero;

        bool isLowQuality = _currentQualityIndex <= 0;
        bool isMediumQuality = _currentQualityIndex == 1;
        float fullRateDistSqr = isMediumQuality ? MediumFullRateDistanceSqr : HighFullRateDistanceSqr;
        int maxUpdatesPerFrame = isLowQuality
            ? LowMaxAnimatorsUpdatedPerFrame
            : (isMediumQuality ? MediumMaxAnimatorsUpdatedPerFrame : HighMaxAnimatorsUpdatedPerFrame);

        int updatesThisFrame = 0;
        int totalCount = _trackedAnimators.Count;

        for (int step = 0; step < totalCount; step++)
        {
            int idx = (_updateCursor + step) % totalCount;
            TrackedCharacterAnimator entry = _trackedAnimators[idx];
            Animator anim = entry.Animator;

            if (anim == null || !anim.gameObject.activeInHierarchy)
            {
                continue;
            }

            // Active conversation participants always animate at full frame rate
            if (entry.Reaction != null && entry.Reaction.IsConversationActive)
            {
                if (!anim.enabled)
                {
                    anim.enabled = true;
                }
                entry.Accumulator = 0f;
                continue;
            }

            bool isVisible = IsAnyRendererVisible(entry.Renderers);
            if (!isVisible)
            {
                if (anim.enabled)
                {
                    anim.enabled = false;
                }
                entry.WasVisible = false;
                continue;
            }

            float sqrDistance = hasRef && entry.Root != null
                ? (entry.Root.position - refPos).sqrMagnitude
                : 0f;

            // Medium keeps only immediate foreground characters at full rate; High keeps nearby ones at full rate
            if (!isLowQuality && hasRef && sqrDistance <= fullRateDistSqr)
            {
                if (!anim.enabled)
                {
                    anim.enabled = true;
                }
                entry.Accumulator = 0f;
                entry.WasVisible = true;
                continue;
            }

            if (anim.enabled)
            {
                anim.enabled = false;
            }

            entry.Accumulator += dt;

            float targetInterval;
            if (isLowQuality)
            {
                targetInterval = hasRef && sqrDistance > LowCloseDistanceSqr ? LowFarUpdateInterval : LowCloseUpdateInterval;
            }
            else if (isMediumQuality)
            {
                targetInterval = hasRef && sqrDistance > MediumMidDistanceSqr ? MediumFarUpdateInterval : MediumMidUpdateInterval;
            }
            else
            {
                targetInterval = HighFarUpdateInterval;
            }

            bool justBecameVisible = !entry.WasVisible;
            bool isDue = justBecameVisible || entry.Accumulator >= targetInterval;

            if (!isDue)
            {
                continue;
            }

            if (!justBecameVisible && updatesThisFrame >= maxUpdatesPerFrame)
            {
                // Spread remaining due animators across subsequent frames to keep CPU frame times flat
                continue;
            }

            float clampedStep = Mathf.Min(entry.Accumulator, MaxAccumulatedDeltaTime);
            entry.Accumulator = 0f;
            entry.WasVisible = true;

            anim.Update(clampedStep);
            updatesThisFrame++;
            _updateCursor = (idx + 1) % totalCount;
        }
    }

    private static bool IsAnyRendererVisible(Renderer[] renderers)
    {
        if (renderers == null)
        {
            return false;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].enabled && renderers[i].isVisible)
            {
                return true;
            }
        }

        return false;
    }

    private Transform ResolveReferenceTransform()
    {
        if (_cachedMainCamera == null || !_cachedMainCamera.isActiveAndEnabled)
        {
            _cachedMainCamera = Camera.main;
        }

        if (_cachedMainCamera != null)
        {
            return _cachedMainCamera.transform;
        }

        return PlayerController.Instance != null ? PlayerController.Instance.transform : null;
    }
}
