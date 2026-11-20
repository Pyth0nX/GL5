using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Scalable reputation-driven material color controller.
/// Updates any configured Material assets (e.g. M_Stripe) based on the player's Reputation / Safety level:
/// - Neutral: preserves each material's original color unchanged.
/// - Safer (High / VeryHigh): progressively warmer and more vivid tones.
/// - Riskier (Low / VeryLow): progressively colder and darker tones.
/// Safely restores original material asset colors on destroy/exit Play Mode.
/// </summary>
[DisallowMultipleComponent]
public class ReputationMaterialController : MonoBehaviour
{
    [Serializable]
    public class ReputationMaterialEntry
    {
        [Tooltip("Target Material asset whose color will react to Reputation / Safety.")]
        public Material targetMaterial;

        [Tooltip("Shader color property to update (URP Lit uses _BaseColor; falls back to _Color automatically).")]
        public string colorPropertyName = "_BaseColor";

        [Tooltip("If true, captures the material's current color at startup as its Neutral baseline.")]
        public bool captureNeutralFromMaterial = true;

        [Tooltip("Used as the Neutral color when Capture Neutral From Material is disabled.")]
        public Color neutralColor = new Color(0.10110356f, 0.2746425f, 0.4528302f, 1f);

        [Tooltip("Enable to override the shared warm/cold ramp with per-material custom tier colors.")]
        public bool useCustomTierColors = false;

        public Color customVeryLowColor = DefaultVeryLowColor;
        public Color customLowColor = DefaultLowColor;
        public Color customHighColor = DefaultHighColor;
        public Color customVeryHighColor = DefaultVeryHighColor;

        [NonSerialized] public bool hasCapturedOriginal;
        [NonSerialized] public bool hasBaseColorProp;
        [NonSerialized] public bool hasLegacyColorProp;
        [NonSerialized] public Color originalBaseColor;
        [NonSerialized] public Color originalLegacyColor;
        [NonSerialized] public Color currentAppliedColor;
        [NonSerialized] public Color transitionStartColor;
        [NonSerialized] public Color transitionTargetColor;
    }

    public static readonly Color DefaultVeryLowColor = new Color(0.02f, 0.04f, 0.12f, 1f);  // Frigid, very dark midnight indigo
    public static readonly Color DefaultLowColor = new Color(0.05f, 0.12f, 0.28f, 1f);      // Colder and darker steel blue
    public static readonly Color DefaultHighColor = new Color(0.98f, 0.78f, 0.14f, 1f);     // Warm vivid golden-yellow
    public static readonly Color DefaultVeryHighColor = new Color(1.00f, 0.88f, 0.12f, 1f); // Very warm, vivid sunlit yellow

    [Header("Target Materials")]
    [Tooltip("Materials driven by Reputation / Safety. Add M_Stripe or any future materials here.")]
    [SerializeField] private List<ReputationMaterialEntry> _materialTargets = new List<ReputationMaterialEntry>();

    [Header("Shared Safe / Risky Palette")]
    [Tooltip("Warm & vivid color reached at maximum safety (Reputation = 100 / VeryHigh).")]
    [SerializeField] private Color _veryHighColor = DefaultVeryHighColor;

    [Tooltip("Warm & vivid color for High safety (Reputation >= High threshold).")]
    [SerializeField] private Color _highColor = DefaultHighColor;

    [Tooltip("Cold & dark color for Low safety (Reputation <= Low threshold).")]
    [SerializeField] private Color _lowColor = DefaultLowColor;

    [Tooltip("Very cold & dark color reached at minimum safety (Reputation = 0 / VeryLow).")]
    [SerializeField] private Color _veryLowColor = DefaultVeryLowColor;

    [Header("Transition Settings")]
    [Tooltip("If true, smoothly interpolates across the exact 0..100 reputation score. If false, snaps targets per 5-tier bracket.")]
    [SerializeField] private bool _continuousInterpolation = true;

    [Tooltip("Duration in seconds to smoothly transition material colors when reputation changes.")]
    [SerializeField, Range(0.05f, 5f)] private float _transitionDuration = 1.0f;

    [Tooltip("Uses unscaled time so material color transitions remain smooth even while UI menus pause timeScale.")]
    [SerializeField] private bool _useUnscaledTime = true;

    [Header("Live Inspector Preview (Optional)")]
    [SerializeField] private bool _usePreviewReputation = false;
    [SerializeField, Range(0, 100)] private int _previewReputation = 50;

    private ReputationManager _boundManager;
    private bool _isTransitioning;
    private float _transitionElapsed;

    private void Awake()
    {
        CaptureInitialMaterialStates();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryBindToReputationManager();
        SyncToCurrentReputation(immediate: true);
    }

    private void Start()
    {
        TryBindToReputationManager();
        SyncToCurrentReputation(immediate: true);
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnbindFromReputationManager();
    }

    private void OnDestroy()
    {
        UnbindFromReputationManager();
        RestoreOriginalMaterialColors();
    }

    private void OnValidate()
    {
        if (Application.isPlaying && isActiveAndEnabled)
        {
            CaptureInitialMaterialStates();
            SyncToCurrentReputation(immediate: false);
        }
    }

    private void Update()
    {
        if (!_isTransitioning)
        {
            return;
        }

        float dt = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        _transitionElapsed += dt;
        float t = _transitionDuration > 0.0001f
            ? Mathf.Clamp01(_transitionElapsed / _transitionDuration)
            : 1f;
        float eased = Mathf.SmoothStep(0f, 1f, t);

        for (int i = 0; i < _materialTargets.Count; i++)
        {
            ReputationMaterialEntry entry = _materialTargets[i];
            if (entry == null || entry.targetMaterial == null) continue;

            Color blended = Color.LerpUnclamped(entry.transitionStartColor, entry.transitionTargetColor, eased);
            ApplyColorToMaterial(entry, blended);
        }

        if (t >= 1f)
        {
            _isTransitioning = false;
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryBindToReputationManager();
        SyncToCurrentReputation(immediate: false);
    }

    private void TryBindToReputationManager()
    {
        if (_boundManager != null)
        {
            return;
        }

        ReputationManager found = GetComponent<ReputationManager>();
        if (found == null)
        {
            found = FindAnyObjectByType<ReputationManager>(FindObjectsInactive.Include);
        }

        if (found == null)
        {
            return;
        }

        _boundManager = found;
        _boundManager.OnReputationChanged += HandleReputationChanged;
        _boundManager.OnReputationTierChanged += HandleReputationTierChanged;
    }

    private void UnbindFromReputationManager()
    {
        if (_boundManager == null)
        {
            return;
        }

        _boundManager.OnReputationChanged -= HandleReputationChanged;
        _boundManager.OnReputationTierChanged -= HandleReputationTierChanged;
        _boundManager = null;
    }

    private void HandleReputationChanged(int newValue)
    {
        SyncToCurrentReputation(immediate: false);
    }

    private void HandleReputationTierChanged(ReputationTier newTier)
    {
        SyncToCurrentReputation(immediate: false);
    }

    public void SyncToCurrentReputation(bool immediate)
    {
        CaptureInitialMaterialStates();

        int repValue = GetEffectiveReputationValue();

        bool anyChanged = false;
        for (int i = 0; i < _materialTargets.Count; i++)
        {
            ReputationMaterialEntry entry = _materialTargets[i];
            if (entry == null || entry.targetMaterial == null) continue;

            Color targetColor = EvaluateTargetColor(entry, repValue);
            entry.transitionStartColor = entry.hasCapturedOriginal ? entry.currentAppliedColor : targetColor;
            entry.transitionTargetColor = targetColor;

            if (immediate || _transitionDuration <= 0.0001f)
            {
                ApplyColorToMaterial(entry, targetColor);
            }
            else if (!ApproximatelyEqualColor(entry.currentAppliedColor, targetColor))
            {
                anyChanged = true;
            }
        }

        if (immediate || _transitionDuration <= 0.0001f)
        {
            _isTransitioning = false;
            _transitionElapsed = 0f;
        }
        else if (anyChanged)
        {
            _transitionElapsed = 0f;
            _isTransitioning = true;
        }
    }

    private int GetEffectiveReputationValue()
    {
        if (_usePreviewReputation)
        {
            return Mathf.Clamp(_previewReputation, 0, 100);
        }

        if (_boundManager != null)
        {
            return _boundManager.CurrentReputation;
        }

        return 50;
    }

    private Color EvaluateTargetColor(ReputationMaterialEntry entry, int reputationValue)
    {
        Color neutral = entry.captureNeutralFromMaterial && entry.hasCapturedOriginal
            ? entry.originalBaseColor
            : entry.neutralColor;

        Color veryLow = entry.useCustomTierColors ? entry.customVeryLowColor : _veryLowColor;
        Color low = entry.useCustomTierColors ? entry.customLowColor : _lowColor;
        Color high = entry.useCustomTierColors ? entry.customHighColor : _highColor;
        Color veryHigh = entry.useCustomTierColors ? entry.customVeryHighColor : _veryHighColor;

        int minRep = _boundManager != null ? _boundManager.MinReputation : 0;
        int maxRep = _boundManager != null ? _boundManager.MaxReputation : 100;
        int veryLowMax = _boundManager != null ? _boundManager.VeryLowTierMaxThreshold : 20;
        int lowMax = _boundManager != null ? _boundManager.LowTierMaxThreshold : 40;
        int neutralCenter = _boundManager != null ? _boundManager.InitialReputation : 50;
        int highMin = _boundManager != null ? _boundManager.HighTierMinThreshold : 60;
        int veryHighMin = _boundManager != null ? _boundManager.VeryHighTierMinThreshold : 80;

        if (!_continuousInterpolation)
        {
            if (reputationValue <= veryLowMax) return veryLow;
            if (reputationValue <= lowMax) return low;
            if (reputationValue >= veryHighMin) return veryHigh;
            if (reputationValue >= highMin) return high;
            return neutral;
        }

        // Keep exact Neutral band (between Low and High thresholds, centered at 50) or smoothly ramp outside/within.
        // At 50 (default Neutral), returns exact Neutral color; below 50 gets colder/darker, above 50 gets warmer/more vivid.
        if (reputationValue == neutralCenter)
        {
            return neutral;
        }

        if (reputationValue < neutralCenter)
        {
            float lowAnchor = Mathf.Lerp(veryLowMax, lowMax, 0.5f); // ~30
            if (reputationValue >= lowAnchor)
            {
                float t = Mathf.InverseLerp(neutralCenter, lowAnchor, reputationValue);
                return Color.Lerp(neutral, low, t);
            }
            else
            {
                float t = Mathf.InverseLerp(lowAnchor, minRep, reputationValue);
                return Color.Lerp(low, veryLow, t);
            }
        }

        float highAnchor = Mathf.Lerp(highMin, veryHighMin, 0.5f); // ~70
        if (reputationValue <= highAnchor)
        {
            float t = Mathf.InverseLerp(neutralCenter, highAnchor, reputationValue);
            return Color.Lerp(neutral, high, t);
        }
        else
        {
            float t = Mathf.InverseLerp(highAnchor, maxRep, reputationValue);
            return Color.Lerp(high, veryHigh, t);
        }
    }

    private void CaptureInitialMaterialStates()
    {
        if (_materialTargets == null) return;

        for (int i = 0; i < _materialTargets.Count; i++)
        {
            ReputationMaterialEntry entry = _materialTargets[i];
            if (entry == null || entry.targetMaterial == null || entry.hasCapturedOriginal) continue;

            Material mat = entry.targetMaterial;
            string prop = string.IsNullOrWhiteSpace(entry.colorPropertyName) ? "_BaseColor" : entry.colorPropertyName.Trim();

            entry.hasBaseColorProp = mat.HasProperty(prop);
            entry.hasLegacyColorProp = mat.HasProperty("_Color");

            if (entry.hasBaseColorProp)
            {
                entry.originalBaseColor = mat.GetColor(prop);
            }
            else if (entry.hasLegacyColorProp)
            {
                entry.originalBaseColor = mat.GetColor("_Color");
            }
            else
            {
                entry.originalBaseColor = entry.neutralColor;
            }

            if (entry.hasLegacyColorProp)
            {
                entry.originalLegacyColor = mat.GetColor("_Color");
            }

            if (entry.captureNeutralFromMaterial)
            {
                entry.neutralColor = entry.originalBaseColor;
            }

            entry.currentAppliedColor = entry.originalBaseColor;
            entry.transitionStartColor = entry.originalBaseColor;
            entry.transitionTargetColor = entry.originalBaseColor;
            entry.hasCapturedOriginal = true;
        }
    }

    private void RestoreOriginalMaterialColors()
    {
        if (_materialTargets == null) return;

        for (int i = 0; i < _materialTargets.Count; i++)
        {
            ReputationMaterialEntry entry = _materialTargets[i];
            if (entry == null || entry.targetMaterial == null || !entry.hasCapturedOriginal) continue;

            string prop = string.IsNullOrWhiteSpace(entry.colorPropertyName) ? "_BaseColor" : entry.colorPropertyName.Trim();
            if (entry.hasBaseColorProp)
            {
                entry.targetMaterial.SetColor(prop, entry.originalBaseColor);
            }
            if (entry.hasLegacyColorProp)
            {
                entry.targetMaterial.SetColor("_Color", entry.originalLegacyColor);
            }
        }
    }

    private static void ApplyColorToMaterial(ReputationMaterialEntry entry, Color color)
    {
        Material mat = entry.targetMaterial;
        string prop = string.IsNullOrWhiteSpace(entry.colorPropertyName) ? "_BaseColor" : entry.colorPropertyName.Trim();

        if (entry.hasBaseColorProp)
        {
            mat.SetColor(prop, color);
        }
        if (entry.hasLegacyColorProp)
        {
            mat.SetColor("_Color", color);
        }
        entry.currentAppliedColor = color;
    }

    private static bool ApproximatelyEqualColor(Color a, Color b)
    {
        const float eps = 0.001f;
        return Mathf.Abs(a.r - b.r) <= eps &&
               Mathf.Abs(a.g - b.g) <= eps &&
               Mathf.Abs(a.b - b.b) <= eps &&
               Mathf.Abs(a.a - b.a) <= eps;
    }
}
