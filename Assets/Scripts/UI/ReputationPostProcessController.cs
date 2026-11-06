using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Customizable post-processing parameters for a single ReputationTier state.
/// Exposed in the Unity Inspector with sliders so designers can balance aesthetics without editing code.
/// </summary>
[Serializable]
public struct ReputationTierVisualPreset
{
    [Tooltip("Vignette intensity (0 = none, higher values darken screen edges).")]
    [Range(0f, 1f)]
    public float vignetteIntensity;

    [Tooltip("Vignette border smoothness (higher values produce a softer falloff).")]
    [Range(0.01f, 1f)]
    public float vignetteSmoothness;

    [Tooltip("Tint color applied to the vignette border.")]
    public Color vignetteColor;

    [Tooltip("Color saturation adjustment (-100 = grayscale, 0 = neutral, +100 = vivid).")]
    [Range(-100f, 100f)]
    public float saturation;

    [Tooltip("Color contrast adjustment (-100 = flat, 0 = neutral, +100 = high contrast).")]
    [Range(-100f, 100f)]
    public float contrast;

    public static ReputationTierVisualPreset CreateDefaultVeryLow()
    {
        return new ReputationTierVisualPreset
        {
            vignetteIntensity = 0.45f,
            vignetteSmoothness = 0.45f,
            vignetteColor = new Color(0.05f, 0.02f, 0.04f, 1f),
            saturation = -55f,
            contrast = 25f
        };
    }

    public static ReputationTierVisualPreset CreateDefaultLow()
    {
        return new ReputationTierVisualPreset
        {
            vignetteIntensity = 0.28f,
            vignetteSmoothness = 0.35f,
            vignetteColor = new Color(0.04f, 0.03f, 0.05f, 1f),
            saturation = -25f,
            contrast = 12f
        };
    }

    public static ReputationTierVisualPreset CreateDefaultNeutral()
    {
        return new ReputationTierVisualPreset
        {
            vignetteIntensity = 0f,
            vignetteSmoothness = 0.25f,
            vignetteColor = Color.black,
            saturation = 0f,
            contrast = 0f
        };
    }

    public static ReputationTierVisualPreset CreateDefaultHigh()
    {
        return new ReputationTierVisualPreset
        {
            vignetteIntensity = 0f,
            vignetteSmoothness = 0.25f,
            vignetteColor = Color.black,
            saturation = 10f,
            contrast = 4f
        };
    }

    public static ReputationTierVisualPreset CreateDefaultVeryHigh()
    {
        return new ReputationTierVisualPreset
        {
            vignetteIntensity = 0f,
            vignetteSmoothness = 0.25f,
            vignetteColor = Color.black,
            saturation = 20f,
            contrast = 8f
        };
    }

    /// <summary>
    /// Smoothly interpolates between two visual presets using an evaluated curve factor.
    /// </summary>
    public static ReputationTierVisualPreset Lerp(
        in ReputationTierVisualPreset from,
        in ReputationTierVisualPreset to,
        float t)
    {
        return new ReputationTierVisualPreset
        {
            vignetteIntensity = Mathf.Clamp01(Mathf.LerpUnclamped(from.vignetteIntensity, to.vignetteIntensity, t)),
            vignetteSmoothness = Mathf.Clamp(Mathf.LerpUnclamped(from.vignetteSmoothness, to.vignetteSmoothness, t), 0.01f, 1f),
            vignetteColor = Color.LerpUnclamped(from.vignetteColor, to.vignetteColor, t),
            saturation = Mathf.Clamp(Mathf.LerpUnclamped(from.saturation, to.saturation, t), -100f, 100f),
            contrast = Mathf.Clamp(Mathf.LerpUnclamped(from.contrast, to.contrast, t), -100f, 100f)
        };
    }

    /// <summary>
    /// Checks whether two visual presets have effectively identical parameter values.
    /// </summary>
    public bool ApproximatelyEquals(in ReputationTierVisualPreset other)
    {
        const float epsilon = 0.0005f;
        return Mathf.Abs(vignetteIntensity - other.vignetteIntensity) <= epsilon &&
               Mathf.Abs(vignetteSmoothness - other.vignetteSmoothness) <= epsilon &&
               Mathf.Abs(saturation - other.saturation) <= epsilon &&
               Mathf.Abs(contrast - other.contrast) <= epsilon &&
               Mathf.Abs(vignetteColor.r - other.vignetteColor.r) <= epsilon &&
               Mathf.Abs(vignetteColor.g - other.vignetteColor.g) <= epsilon &&
               Mathf.Abs(vignetteColor.b - other.vignetteColor.b) <= epsilon &&
               Mathf.Abs(vignetteColor.a - other.vignetteColor.a) <= epsilon;
    }
}

/// <summary>
/// Modular, weakly-coupled URP post-processing controller that applies visual feedback
/// (Vignette and Color Adjustments) based on the current 5-level ReputationTier.
/// Safe to disable or remove at any time without affecting ReputationManager or core UI systems.
/// </summary>
[DisallowMultipleComponent]
public class ReputationPostProcessController : MonoBehaviour
{
    [Header("General Toggle")]
    [Tooltip("Master runtime toggle to enable or disable reputation visual feedback on the fly.")]
    [SerializeField] private bool _enableVisualFeedback = true;

    [Tooltip("Automatically enables post-processing rendering on the active Main Camera when visual feedback is active.")]
    [SerializeField] private bool _ensureCameraPostProcessing = true;

    [Header("Post-Processing Volume")]
    [Tooltip("Target URP Volume controlled by this module. Automatically resolved on this GameObject if left unassigned.")]
    [SerializeField] private Volume _postProcessVolume;

    [Header("Transition Settings")]
    [Tooltip("Duration in seconds to smoothly interpolate between reputation visual states.")]
    [SerializeField, Range(0.05f, 10f)] private float _transitionDuration = 1.25f;

    [Tooltip("Easing curve used during Lerp transitions between tier presets.")]
    [SerializeField] private AnimationCurve _transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("Uses unscaled delta time so visual transitions remain smooth even when Time.timeScale is 0.")]
    [SerializeField] private bool _useUnscaledTime = true;

    [Header("Tier Visual Presets (5 Levels)")]
    [Tooltip("Visual preset for VeryLow reputation (severe anxiety: strong vignette, heavy desaturation, stark contrast).")]
    [SerializeField] private ReputationTierVisualPreset _veryLowPreset = ReputationTierVisualPreset.CreateDefaultVeryLow();

    [Tooltip("Visual preset for Low reputation (mild anxiety: moderate vignette, desaturated, slightly elevated contrast).")]
    [SerializeField] private ReputationTierVisualPreset _lowPreset = ReputationTierVisualPreset.CreateDefaultLow();

    [Tooltip("Visual preset for Neutral reputation (balanced baseline aesthetic).")]
    [SerializeField] private ReputationTierVisualPreset _neutralPreset = ReputationTierVisualPreset.CreateDefaultNeutral();

    [Tooltip("Visual preset for High reputation (calm confidence: clean borders, gently warmer saturation).")]
    [SerializeField] private ReputationTierVisualPreset _highPreset = ReputationTierVisualPreset.CreateDefaultHigh();

    [Tooltip("Visual preset for VeryHigh reputation (strong security: clean borders, vibrant saturation and crisp contrast).")]
    [SerializeField] private ReputationTierVisualPreset _veryHighPreset = ReputationTierVisualPreset.CreateDefaultVeryHigh();

    [Header("Live Inspector Preview (Optional)")]
    [Tooltip("When enabled, overrides the ReputationManager tier to preview and balance the selected tier live in Play Mode.")]
    [SerializeField] private bool _usePreviewTier = false;

    [Tooltip("Target tier to preview when Use Preview Tier is active.")]
    [SerializeField] private ReputationTier _previewTier = ReputationTier.Neutral;

    private ReputationManager _boundReputationManager;
    private VolumeProfile _runtimeProfile;
    private Vignette _vignette;
    private ColorAdjustments _colorAdjustments;

    private ReputationTier _trackedReputationTier = ReputationTier.Neutral;
    private ReputationTier _activeTargetTier = ReputationTier.Neutral;
    private ReputationTierVisualPreset _currentVisualState;
    private ReputationTierVisualPreset _transitionStartState;
    private ReputationTierVisualPreset _targetVisualState;

    private float _transitionElapsed = 0f;
    private bool _isTransitioning = false;
    private bool _isInitialized = false;
    private bool _loggedMissingVolumeWarning = false;

    /// <summary>
    /// Gets or sets whether the reputation post-processing visual feedback is enabled at runtime.
    /// </summary>
    public bool EnableVisualFeedback
    {
        get => _enableVisualFeedback;
        set => SetVisualFeedbackEnabled(value);
    }

    /// <summary>
    /// Current target tier driving the post-processing controller.
    /// </summary>
    public ReputationTier ActiveTargetTier => _activeTargetTier;

    private void Awake()
    {
        InitializeVolumeComponents();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        TryBindToReputationManager();

        if (_isInitialized)
        {
            SyncTargetTier(immediate: false);
        }
    }

    private void Start()
    {
        TryBindToReputationManager();
        EnsureActiveCameraPostProcessing();
        SyncTargetTier(immediate: true);
        _isInitialized = true;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        UnbindFromReputationManager();
    }

    private void OnDestroy()
    {
        UnbindFromReputationManager();

        if (_runtimeProfile != null)
        {
            Destroy(_runtimeProfile);
            _runtimeProfile = null;
        }
    }

    private void OnValidate()
    {
        _transitionDuration = Mathf.Max(0.05f, _transitionDuration);

        if (_transitionCurve == null || _transitionCurve.length == 0)
        {
            _transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        if (!Application.isPlaying || !_isInitialized)
        {
            return;
        }

        if (_enableVisualFeedback)
        {
            EnsureActiveCameraPostProcessing();
        }

        ReputationTier desiredTier = GetDesiredTier();
        if (desiredTier != _activeTargetTier)
        {
            StartTransitionToTier(desiredTier);
        }
        else
        {
            // Live Inspector slider balancing: update target and apply directly if idle
            _targetVisualState = GetPresetForTier(_activeTargetTier);
            if (!_isTransitioning)
            {
                _currentVisualState = _targetVisualState;
                ApplyVisualStateToVolume(_currentVisualState);
            }
        }
    }

    private void Update()
    {
        if (_postProcessVolume == null || _vignette == null || _colorAdjustments == null)
        {
            return;
        }

        // Re-check weak binding if ReputationManager was initialized after this component
        if (_boundReputationManager == null)
        {
            TryBindToReputationManager();
        }

        ReputationTier desiredTier = GetDesiredTier();
        if (desiredTier != _activeTargetTier)
        {
            StartTransitionToTier(desiredTier);
        }

        float deltaTime = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        UpdateVolumeWeight(deltaTime);

        if (_isTransitioning)
        {
            AdvancePresetTransition(deltaTime);
        }
    }

    /// <summary>
    /// Enables or disables the post-processing visual feedback smoothly at runtime.
    /// </summary>
    public void SetVisualFeedbackEnabled(bool isEnabled)
    {
        _enableVisualFeedback = isEnabled;
        if (_enableVisualFeedback)
        {
            EnsureActiveCameraPostProcessing();
        }
    }

    /// <summary>
    /// Flips the runtime visual feedback toggle on or off.
    /// </summary>
    public void ToggleVisualFeedback()
    {
        SetVisualFeedbackEnabled(!_enableVisualFeedback);
    }

    /// <summary>
    /// Public listener compatible with UnityEvent&lt;ReputationTier&gt; for optional Inspector wiring.
    /// </summary>
    public void OnReputationTierChanged(ReputationTier newTier)
    {
        HandleReputationTierChanged(newTier);
    }

    /// <summary>
    /// Returns the configured visual preset corresponding to the specified tier.
    /// </summary>
    public ReputationTierVisualPreset GetPresetForTier(ReputationTier tier)
    {
        return tier switch
        {
            ReputationTier.VeryLow => _veryLowPreset,
            ReputationTier.Low => _lowPreset,
            ReputationTier.High => _highPreset,
            ReputationTier.VeryHigh => _veryHighPreset,
            _ => _neutralPreset
        };
    }

    private ReputationTier GetDesiredTier()
    {
        return _usePreviewTier ? _previewTier : _trackedReputationTier;
    }

    private void InitializeVolumeComponents()
    {
        if (_postProcessVolume == null)
        {
            TryGetComponent(out _postProcessVolume);
        }

        if (_postProcessVolume == null)
        {
            if (!_loggedMissingVolumeWarning)
            {
                Debug.LogWarning("[ReputationPostProcessController] No URP Volume component assigned or found on GameObject. Visual feedback remains inactive.");
                _loggedMissingVolumeWarning = true;
            }
            return;
        }

        // Clone the shared profile at runtime so asset files on disk are never dirtied during Play Mode
        if (_postProcessVolume.sharedProfile != null)
        {
            _runtimeProfile = Instantiate(_postProcessVolume.sharedProfile);
            _runtimeProfile.name = $"{_postProcessVolume.sharedProfile.name} (Runtime Clone)";
        }
        else
        {
            _runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _runtimeProfile.name = "ReputationFeedbackProfile (Runtime Generated)";
        }

        _postProcessVolume.profile = _runtimeProfile;

        if (!_runtimeProfile.TryGet(out _vignette))
        {
            _vignette = _runtimeProfile.Add<Vignette>(overrides: true);
        }

        if (!_runtimeProfile.TryGet(out _colorAdjustments))
        {
            _colorAdjustments = _runtimeProfile.Add<ColorAdjustments>(overrides: true);
        }

        _vignette.active = true;
        _vignette.intensity.overrideState = true;
        _vignette.smoothness.overrideState = true;
        _vignette.color.overrideState = true;

        _colorAdjustments.active = true;
        _colorAdjustments.saturation.overrideState = true;
        _colorAdjustments.contrast.overrideState = true;

        _postProcessVolume.weight = _enableVisualFeedback ? 1f : 0f;
    }

    private void TryBindToReputationManager()
    {
        if (_boundReputationManager != null)
        {
            return;
        }

        // Use FindAnyObjectByType instead of ReputationManager.Instance to avoid spawning a manager if deleted
        ReputationManager foundManager = FindAnyObjectByType<ReputationManager>(FindObjectsInactive.Include);
        if (foundManager == null)
        {
            return;
        }

        _boundReputationManager = foundManager;
        _boundReputationManager.OnReputationTierChanged += HandleReputationTierChanged;
        _boundReputationManager.OnReputationChanged += HandleReputationValueChanged;

        _trackedReputationTier = _boundReputationManager.CurrentTier;
    }

    private void UnbindFromReputationManager()
    {
        if (_boundReputationManager == null)
        {
            return;
        }

        _boundReputationManager.OnReputationTierChanged -= HandleReputationTierChanged;
        _boundReputationManager.OnReputationChanged -= HandleReputationValueChanged;
        _boundReputationManager = null;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryBindToReputationManager();
        EnsureActiveCameraPostProcessing();
        SyncTargetTier(immediate: false);
    }

    private void HandleReputationValueChanged(int newValue)
    {
        if (_boundReputationManager == null)
        {
            return;
        }

        ReputationTier evaluatedTier = _boundReputationManager.CurrentTier;
        if (evaluatedTier != _trackedReputationTier)
        {
            HandleReputationTierChanged(evaluatedTier);
        }
    }

    private void HandleReputationTierChanged(ReputationTier newTier)
    {
        _trackedReputationTier = newTier;

        ReputationTier desiredTier = GetDesiredTier();
        if (desiredTier != _activeTargetTier || !_isInitialized)
        {
            StartTransitionToTier(desiredTier);
        }
    }

    private void SyncTargetTier(bool immediate)
    {
        if (_boundReputationManager != null)
        {
            _trackedReputationTier = _boundReputationManager.CurrentTier;
        }

        ReputationTier desiredTier = GetDesiredTier();
        if (immediate)
        {
            _activeTargetTier = desiredTier;
            _targetVisualState = GetPresetForTier(desiredTier);
            _currentVisualState = _targetVisualState;
            _transitionStartState = _currentVisualState;
            _isTransitioning = false;
            _transitionElapsed = 0f;

            if (_postProcessVolume != null)
            {
                _postProcessVolume.weight = _enableVisualFeedback ? 1f : 0f;
            }

            ApplyVisualStateToVolume(_currentVisualState);
        }
        else if (desiredTier != _activeTargetTier)
        {
            StartTransitionToTier(desiredTier);
        }
    }

    private void StartTransitionToTier(ReputationTier targetTier)
    {
        _activeTargetTier = targetTier;
        _transitionStartState = _currentVisualState;
        _targetVisualState = GetPresetForTier(targetTier);
        _transitionElapsed = 0f;

        if (_transitionDuration <= 0.0001f || _currentVisualState.ApproximatelyEquals(_targetVisualState))
        {
            _currentVisualState = _targetVisualState;
            _isTransitioning = false;
            ApplyVisualStateToVolume(_currentVisualState);
            return;
        }

        _isTransitioning = true;
    }

    private void AdvancePresetTransition(float deltaTime)
    {
        float duration = Mathf.Max(0.05f, _transitionDuration);
        _transitionElapsed += Mathf.Max(0f, deltaTime);

        float normalizedTime = Mathf.Clamp01(_transitionElapsed / duration);
        float curvedFactor = _transitionCurve != null
            ? _transitionCurve.Evaluate(normalizedTime)
            : normalizedTime;

        _currentVisualState = ReputationTierVisualPreset.Lerp(_transitionStartState, _targetVisualState, curvedFactor);
        ApplyVisualStateToVolume(_currentVisualState);

        if (normalizedTime >= 1f)
        {
            _currentVisualState = _targetVisualState;
            _isTransitioning = false;
            ApplyVisualStateToVolume(_currentVisualState);
        }
    }

    private void UpdateVolumeWeight(float deltaTime)
    {
        float targetWeight = _enableVisualFeedback ? 1f : 0f;
        if (Mathf.Approximately(_postProcessVolume.weight, targetWeight))
        {
            _postProcessVolume.weight = targetWeight;
            return;
        }

        float fadeSpeed = 1f / Mathf.Max(0.05f, _transitionDuration);
        _postProcessVolume.weight = Mathf.MoveTowards(_postProcessVolume.weight, targetWeight, fadeSpeed * Mathf.Max(0f, deltaTime));
    }

    private void ApplyVisualStateToVolume(in ReputationTierVisualPreset state)
    {
        if (_vignette == null || _colorAdjustments == null)
        {
            return;
        }

        _vignette.intensity.value = Mathf.Clamp01(state.vignetteIntensity);
        _vignette.smoothness.value = Mathf.Clamp(state.vignetteSmoothness, 0.01f, 1f);
        _vignette.color.value = state.vignetteColor;

        _colorAdjustments.saturation.value = Mathf.Clamp(state.saturation, -100f, 100f);
        _colorAdjustments.contrast.value = Mathf.Clamp(state.contrast, -100f, 100f);
    }

    private void EnsureActiveCameraPostProcessing()
    {
        if (!_enableVisualFeedback || !_ensureCameraPostProcessing)
        {
            return;
        }

        Camera activeCamera = Camera.main;
        if (activeCamera == null)
        {
            return;
        }

        if (activeCamera.TryGetComponent(out UniversalAdditionalCameraData cameraData) && !cameraData.renderPostProcessing)
        {
            cameraData.renderPostProcessing = true;
        }
    }

    #region Inspector Debug Helpers
    [ContextMenu("Debug: Preview VeryLow")]
    private void DebugPreviewVeryLow()
    {
        _usePreviewTier = true;
        _previewTier = ReputationTier.VeryLow;
        if (Application.isPlaying) StartTransitionToTier(ReputationTier.VeryLow);
    }

    [ContextMenu("Debug: Preview Low")]
    private void DebugPreviewLow()
    {
        _usePreviewTier = true;
        _previewTier = ReputationTier.Low;
        if (Application.isPlaying) StartTransitionToTier(ReputationTier.Low);
    }

    [ContextMenu("Debug: Preview Neutral")]
    private void DebugPreviewNeutral()
    {
        _usePreviewTier = true;
        _previewTier = ReputationTier.Neutral;
        if (Application.isPlaying) StartTransitionToTier(ReputationTier.Neutral);
    }

    [ContextMenu("Debug: Preview High")]
    private void DebugPreviewHigh()
    {
        _usePreviewTier = true;
        _previewTier = ReputationTier.High;
        if (Application.isPlaying) StartTransitionToTier(ReputationTier.High);
    }

    [ContextMenu("Debug: Preview VeryHigh")]
    private void DebugPreviewVeryHigh()
    {
        _usePreviewTier = true;
        _previewTier = ReputationTier.VeryHigh;
        if (Application.isPlaying) StartTransitionToTier(ReputationTier.VeryHigh);
    }

    [ContextMenu("Debug: Disable Preview Override")]
    private void DebugDisablePreview()
    {
        _usePreviewTier = false;
        if (Application.isPlaying) SyncTargetTier(immediate: false);
    }
    #endregion
}
