using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Reduces CPU/GPU load caused by high-poly skinned characters (e.g. multiple VRoid NPCs)
/// without reducing mesh vertices or causing visual chopping/clipping during fast camera pans.
/// Scales SkinnedMeshRenderer shadow/probe flags according to the active QualitySettings level (Low, Medium, High).
/// </summary>
[DisallowMultipleComponent]
public class CharacterPerformanceOptimizer : MonoBehaviour
{
    private sealed class TrackedSkinnedMesh
    {
        public SkinnedMeshRenderer Renderer;
        public ShadowCastingMode OriginalShadowCastingMode;
        public bool IsOutlineHelper;
        public bool IsFaceMesh;
    }

    private readonly List<Animator> _trackedAnimators = new List<Animator>();
    private readonly List<TrackedSkinnedMesh> _trackedSkinnedMeshes = new List<TrackedSkinnedMesh>();

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
        RebuildSceneCaches();
        ApplySkinnedMeshSettings(qualityIndex);
        ApplyAnimatorSettings();
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
        for (int i = 0; i < allAnimators.Length; i++)
        {
            Animator anim = allAnimators[i];
            if (anim == null || anim.runtimeAnimatorController == null)
            {
                continue;
            }

            // Never alter the player or non-character animators (UI, doors, props without skinned meshes)
            if (anim.GetComponentInParent<PlayerController>() != null)
            {
                continue;
            }

            SkinnedMeshRenderer[] childSmrs = anim.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (childSmrs == null || childSmrs.Length == 0)
            {
                continue;
            }

            _trackedAnimators.Add(anim);
        }
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

            // Keep character meshes updated and exempt from dynamic occlusion culling so fast camera turns never clip or stutter them.
            smr.updateWhenOffscreen = !item.IsOutlineHelper;
            smr.allowOcclusionWhenDynamic = false;
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

    private void ApplyAnimatorSettings()
    {
        for (int i = 0; i < _trackedAnimators.Count; i++)
        {
            Animator anim = _trackedAnimators[i];
            if (anim == null)
            {
                continue;
            }

            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.enabled = true;
        }
    }

    private void RestoreAllAnimators()
    {
        for (int i = 0; i < _trackedAnimators.Count; i++)
        {
            if (_trackedAnimators[i] != null)
            {
                _trackedAnimators[i].enabled = true;
            }
        }
    }
}
