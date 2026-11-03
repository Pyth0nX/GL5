using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Reusable, high-performance UI component for pointer hover and navigation feedback.
/// Supports smooth scale punch (inner viewport or whole element), procedural border highlight
/// (SolidInside, SolidOutside, CornerBrackets), color tinting, click feedback, and audio.
/// Fully compatible with both StartScreen app buttons and GalleryPhotoItem thumbnails.
/// </summary>
[DisallowMultipleComponent]
public class UIHoverFeedback : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    ISelectHandler,
    IDeselectHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    public enum BorderStyle
    {
        SolidInside = 0,
        SolidOutside = 1,
        CornerBrackets = 2,
        Solid = 0 // Backward-compatible alias
    }

    [Header("1. Settings Override")]
    [Tooltip("If enabled, this element retains its own Inspector settings and ignores global style overrides from GalleryManager.")]
    [SerializeField] private bool _overrideGlobalSettings = false;

    [Header("2. Scale Feedback")]
    [Tooltip("Whether to scale the target on hover/selection.")]
    [SerializeField] private bool _enableScale = true;

    [Tooltip("Target scale multiplier on hover (e.g. 1.10 = 10% larger, 1.04 = subtle zoom).")]
    [Range(0.8f, 1.5f)]
    [SerializeField] private float _hoverScaleMultiplier = 1.10f;

    [Tooltip("Target scale multiplier when clicked/pressed (e.g. 0.96 for tactile press feedback).")]
    [Range(0.8f, 1.2f)]
    [SerializeField] private float _pressedScaleMultiplier = 0.96f;

    [Tooltip("Duration in seconds for the scale transition.")]
    [Range(0.01f, 0.4f)]
    [SerializeField] private float _scaleDuration = 0.12f;

    [Tooltip("Target RectTransform to scale. If null, scales this GameObject's RectTransform. In gallery thumbnails, points to PhotoViewport for inner zoom.")]
    [SerializeField] private RectTransform _targetScaleTransform;

    [Header("3. Outline / Border Feedback")]
    [Tooltip("Whether to show an outline/border around the button on hover/selection.")]
    [SerializeField] private bool _enableBorder = false;

    [Tooltip("Border color on hover.")]
    [SerializeField] private Color _borderColor = new Color(0.25f, 0.85f, 1f, 1f);

    [Tooltip("Border thickness in UI pixels.")]
    [Range(1f, 16f)]
    [SerializeField] private float _borderThickness = 3f;

    [Tooltip("Padding/offset distance from button edges in UI pixels (used when style is custom or solid).")]
    [SerializeField] private float _borderPadding = 0f;

    [Tooltip("Visual style of the border: SolidInside (flush inside), SolidOutside (projected outward), or CornerBrackets (camera viewfinder corners).")]
    [SerializeField] private BorderStyle _borderStyle = BorderStyle.SolidInside;

    [Tooltip("Corner bracket segment length ratio (0.15 - 0.48) when using CornerBrackets.")]
    [Range(0.15f, 0.48f)]
    [SerializeField] private float _cornerBracketRatio = 0.28f;

    [Tooltip("Fade transition duration for the border.")]
    [Range(0.01f, 0.3f)]
    [SerializeField] private float _borderFadeDuration = 0.08f;

    [Header("4. Color Tint Feedback")]
    [Tooltip("Whether to tint the target graphic on hover.")]
    [SerializeField] private bool _enableColorTint = false;

    [Tooltip("Target color when hovered.")]
    [SerializeField] private Color _hoverTintColor = new Color(1.15f, 1.15f, 1.15f, 1f);

    [Tooltip("Duration for the color tint transition.")]
    [Range(0.01f, 0.3f)]
    [SerializeField] private float _colorFadeDuration = 0.10f;

    [Tooltip("Target Graphic to tint. If null, attempts to find an Image/Graphic on this GameObject.")]
    [SerializeField] private Graphic _targetGraphic;

    [Header("5. Audio Feedback (Optional)")]
    [Tooltip("Whether to play a sound on hover.")]
    [SerializeField] private bool _enableHoverSound = false;

    [Tooltip("Audio clip to play when cursor enters or element is selected.")]
    [SerializeField] private AudioClip _hoverSoundClip;

    [Tooltip("Optional AudioSource. If null, plays via AudioSource.PlayClipAtPoint.")]
    [SerializeField] private AudioSource _audioSource;

    [Header("6. Custom UnityEvents")]
    [SerializeField] private UnityEvent _onHoverEnter;
    [SerializeField] private UnityEvent _onHoverExit;

    private Vector3 _initialScale = Vector3.one;
    private Color _initialColor = Color.white;
    private bool _isHovered = false;
    private bool _isPressed = false;
    private bool _hasCapturedInitialState = false;
    private bool _geometryDirty = true;

    private float _currentAlpha = 0f;
    private float _targetAlpha = 0f;

    private Coroutine _scaleCoroutine;
    private Coroutine _colorCoroutine;

    // Procedural border segments
    private RectTransform _borderContainer;
    private CanvasGroup _borderCanvasGroup;
    private readonly List<Image> _borderSegments = new List<Image>(8);

    #region Public Properties

    public bool OverrideGlobalSettings
    {
        get => _overrideGlobalSettings;
        set => _overrideGlobalSettings = value;
    }

    public bool EnableScale
    {
        get => _enableScale;
        set => _enableScale = value;
    }

    public float HoverScaleMultiplier
    {
        get => _hoverScaleMultiplier;
        set => _hoverScaleMultiplier = value;
    }

    public bool EnableBorder
    {
        get => _enableBorder;
        set
        {
            _enableBorder = value;
            if (_enableBorder)
            {
                EnsureBorderStructure();
            }
            else if (_borderContainer != null)
            {
                _borderContainer.gameObject.SetActive(false);
            }
        }
    }

    public Color BorderColor
    {
        get => _borderColor;
        set
        {
            _borderColor = value;
            ApplyBorderColor(_borderColor);
        }
    }

    public float BorderThickness
    {
        get => _borderThickness;
        set
        {
            _borderThickness = Mathf.Max(1f, value);
            _geometryDirty = true;
            if (_enableBorder)
            {
                RebuildBorderLayout();
            }
        }
    }

    public float BorderPadding
    {
        get => _borderPadding;
        set
        {
            _borderPadding = value;
            _geometryDirty = true;
            if (_enableBorder)
            {
                RebuildBorderLayout();
            }
        }
    }

    public BorderStyle StyleOfBorder
    {
        get => _borderStyle;
        set
        {
            _borderStyle = value;
            _geometryDirty = true;
            if (_enableBorder)
            {
                RebuildBorderLayout();
            }
        }
    }

    public float CornerBracketRatio
    {
        get => _cornerBracketRatio;
        set
        {
            _cornerBracketRatio = Mathf.Clamp(value, 0.15f, 0.48f);
            _geometryDirty = true;
            if (_enableBorder)
            {
                RebuildBorderLayout();
            }
        }
    }

    public RectTransform TargetScaleTransform => _targetScaleTransform;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        CaptureInitialState();
        if (_enableBorder)
        {
            EnsureBorderStructure();
        }
        SetHighlightedImmediate(false);
    }

    private void OnEnable()
    {
        CaptureInitialState();
        SetHighlightedImmediate(false);
    }

    private void OnDisable()
    {
        ResetVisualsInstant();
    }

    private void OnRectTransformDimensionsChange()
    {
        _geometryDirty = true;
        if (_borderContainer != null && (_currentAlpha > 0.001f || _targetAlpha > 0.001f))
        {
            RebuildBorderLayout();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _borderThickness = Mathf.Max(1f, _borderThickness);
        _cornerBracketRatio = Mathf.Clamp(_cornerBracketRatio, 0.15f, 0.48f);
        _geometryDirty = true;
        if (Application.isPlaying && _borderContainer != null)
        {
            RebuildBorderLayout();
        }
    }
#endif

    private void Update()
    {
        if (_borderCanvasGroup == null) return;

        if (_geometryDirty && (_targetAlpha > 0f || _currentAlpha > 0f))
        {
            RebuildBorderLayout();
        }

        if (!Mathf.Approximately(_currentAlpha, _targetAlpha))
        {
            if (_borderFadeDuration <= 0.001f)
            {
                _currentAlpha = _targetAlpha;
            }
            else
            {
                float step = Time.unscaledDeltaTime / _borderFadeDuration;
                _currentAlpha = Mathf.MoveTowards(_currentAlpha, _targetAlpha, step);
            }

            _borderCanvasGroup.alpha = _currentAlpha;

            if (_currentAlpha <= 0.001f && _targetAlpha <= 0.001f && _borderContainer != null)
            {
                _borderContainer.gameObject.SetActive(false);
            }
        }
    }

    #endregion

    #region Gallery Compatibility Methods

    /// <summary>
    /// Explicitly designates the transform to scale on hover (e.g. PhotoViewport).
    /// </summary>
    public void SetScaleTarget(RectTransform target)
    {
        _targetScaleTransform = target;
        if (_targetScaleTransform != null)
        {
            _initialScale = _targetScaleTransform.localScale;
            _hasCapturedInitialState = true;
        }
    }

    /// <summary>
    /// Injects global style settings from GalleryManager unless _overrideGlobalSettings is enabled.
    /// </summary>
    public void ApplyStyleFromManager(
        Color color,
        float thickness,
        GalleryBorderStyle style,
        float cornerRatio,
        bool enableScale,
        float hoverScale,
        float fadeDuration)
    {
        if (_overrideGlobalSettings) return;

        _borderColor = color;
        _borderThickness = Mathf.Max(1f, thickness);
        _borderStyle = (BorderStyle)(int)style;
        _cornerBracketRatio = Mathf.Clamp(cornerRatio, 0.15f, 0.48f);
        _enableScale = enableScale;
        _hoverScaleMultiplier = hoverScale;
        _scaleDuration = Mathf.Max(0.01f, fadeDuration);
        _borderFadeDuration = Mathf.Max(0.01f, fadeDuration);
        _enableBorder = true;

        _geometryDirty = true;
        if (_borderContainer != null)
        {
            RebuildBorderLayout();
        }
    }

    /// <summary>
    /// Injects global style settings from GalleryManager using UIHoverFeedback.BorderStyle.
    /// </summary>
    public void ApplyStyleFromManager(
        Color color,
        float thickness,
        BorderStyle style,
        float cornerRatio,
        bool enableScale,
        float hoverScale,
        float fadeDuration)
    {
        if (_overrideGlobalSettings) return;

        _borderColor = color;
        _borderThickness = Mathf.Max(1f, thickness);
        _borderStyle = style;
        _cornerBracketRatio = Mathf.Clamp(cornerRatio, 0.15f, 0.48f);
        _enableScale = enableScale;
        _hoverScaleMultiplier = hoverScale;
        _scaleDuration = Mathf.Max(0.01f, fadeDuration);
        _borderFadeDuration = Mathf.Max(0.01f, fadeDuration);
        _enableBorder = true;

        _geometryDirty = true;
        if (_borderContainer != null)
        {
            RebuildBorderLayout();
        }
    }

    /// <summary>
    /// Instantly snaps highlight state on or off without animation transitions.
    /// </summary>
    public void SetHighlightedImmediate(bool highlighted)
    {
        _isHovered = highlighted;
        _isPressed = false;
        _targetAlpha = highlighted ? 1f : 0f;
        _currentAlpha = _targetAlpha;

        if (_borderCanvasGroup != null)
        {
            _borderCanvasGroup.alpha = _currentAlpha;
        }

        if (_borderContainer != null)
        {
            _borderContainer.gameObject.SetActive(highlighted);
        }

        if (_targetScaleTransform != null && _hasCapturedInitialState)
        {
            float mult = highlighted ? _hoverScaleMultiplier : 1f;
            _targetScaleTransform.localScale = _enableScale
                ? new Vector3(_initialScale.x * mult, _initialScale.y * mult, _initialScale.z * mult)
                : _initialScale;
        }

        if (_targetGraphic != null && _hasCapturedInitialState)
        {
            _targetGraphic.color = (highlighted && _enableColorTint) ? _hoverTintColor : _initialColor;
        }
    }

    public void SetBorderColor(Color color)
    {
        _borderColor = color;
        ApplyBorderColor(_borderColor);
    }

    public void SetBorderThickness(float thickness)
    {
        _borderThickness = Mathf.Max(1f, thickness);
        _geometryDirty = true;
        if (_enableBorder)
        {
            RebuildBorderLayout();
        }
    }

    public void SetBorderStyle(GalleryBorderStyle style)
    {
        _borderStyle = (BorderStyle)(int)style;
        _geometryDirty = true;
        if (_enableBorder)
        {
            RebuildBorderLayout();
        }
    }

    public void SetBorderStyle(BorderStyle style)
    {
        _borderStyle = style;
        _geometryDirty = true;
        if (_enableBorder)
        {
            RebuildBorderLayout();
        }
    }

    public void SetScaleMultiplier(float multiplier)
    {
        _hoverScaleMultiplier = multiplier;
    }

    public void SetScaleDuration(float duration)
    {
        _scaleDuration = Mathf.Max(0.01f, duration);
    }

    public void SetBorderFadeDuration(float duration)
    {
        _borderFadeDuration = Mathf.Max(0.01f, duration);
    }

    #endregion

    #region State Capture and Reset

    private void CaptureInitialState()
    {
        if (_targetScaleTransform == null)
        {
            _targetScaleTransform = GetComponent<RectTransform>();
        }

        if (_targetGraphic == null)
        {
            _targetGraphic = GetComponent<Graphic>();
        }

        if (_targetScaleTransform != null && !_hasCapturedInitialState)
        {
            _initialScale = _targetScaleTransform.localScale;
            if (_targetGraphic != null)
            {
                _initialColor = _targetGraphic.color;
            }
            _hasCapturedInitialState = true;
        }
    }

    private void ResetVisualsInstant()
    {
        _isHovered = false;
        _isPressed = false;
        _targetAlpha = 0f;
        _currentAlpha = 0f;

        if (_scaleCoroutine != null)
        {
            StopCoroutine(_scaleCoroutine);
            _scaleCoroutine = null;
        }

        if (_colorCoroutine != null)
        {
            StopCoroutine(_colorCoroutine);
            _colorCoroutine = null;
        }

        if (_targetScaleTransform != null && _hasCapturedInitialState)
        {
            _targetScaleTransform.localScale = _initialScale;
        }

        if (_borderCanvasGroup != null)
        {
            _borderCanvasGroup.alpha = 0f;
        }

        if (_borderContainer != null)
        {
            _borderContainer.gameObject.SetActive(false);
        }

        if (_targetGraphic != null && _hasCapturedInitialState)
        {
            _targetGraphic.color = _initialColor;
        }
    }

    #endregion

    #region Pointer & Selection Handlers

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;
        TriggerFeedbackState();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        _isPressed = false;
        TriggerFeedbackState();
    }

    public void OnSelect(BaseEventData eventData)
    {
        _isHovered = true;
        TriggerFeedbackState();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        _isHovered = false;
        _isPressed = false;
        TriggerFeedbackState();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _isPressed = true;
        TriggerFeedbackState();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _isPressed = false;
        TriggerFeedbackState();
    }

    private void TriggerFeedbackState()
    {
        if (!isActiveAndEnabled) return;

        if (_isHovered)
        {
            // Scale feedback
            if (_enableScale && _targetScaleTransform != null)
            {
                float mult = _isPressed ? _pressedScaleMultiplier : _hoverScaleMultiplier;
                Vector3 target = new Vector3(
                    _initialScale.x * mult,
                    _initialScale.y * mult,
                    _initialScale.z * mult
                );
                StartScaleAnimation(target, _scaleDuration);
            }

            // Border feedback
            if (_enableBorder)
            {
                EnsureBorderStructure();
                _targetAlpha = 1f;
                if (_borderContainer != null)
                {
                    _borderContainer.gameObject.SetActive(true);
                }
            }

            // Color tint feedback
            if (_enableColorTint && _targetGraphic != null)
            {
                StartColorAnimation(_hoverTintColor, _colorFadeDuration);
            }

            // Audio feedback
            if (_enableHoverSound && _hoverSoundClip != null && !_isPressed)
            {
                PlayHoverAudio();
            }

            _onHoverEnter?.Invoke();
        }
        else
        {
            // Reset scale
            if (_enableScale && _targetScaleTransform != null)
            {
                StartScaleAnimation(_initialScale, _scaleDuration);
            }

            // Reset border
            if (_enableBorder)
            {
                _targetAlpha = 0f;
            }

            // Reset color
            if (_enableColorTint && _targetGraphic != null)
            {
                StartColorAnimation(_initialColor, _colorFadeDuration);
            }

            _onHoverExit?.Invoke();
        }
    }

    #endregion

    #region Scale Animation

    private void StartScaleAnimation(Vector3 targetScale, float duration)
    {
        if (_scaleCoroutine != null)
        {
            StopCoroutine(_scaleCoroutine);
        }
        _scaleCoroutine = StartCoroutine(AnimateScaleRoutine(targetScale, duration));
    }

    private IEnumerator AnimateScaleRoutine(Vector3 targetScale, float duration)
    {
        if (_targetScaleTransform == null) yield break;

        Vector3 startScale = _targetScaleTransform.localScale;
        if (duration <= 0.001f)
        {
            _targetScaleTransform.localScale = targetScale;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float ease = Mathf.Sin(t * Mathf.PI * 0.5f);
            _targetScaleTransform.localScale = Vector3.LerpUnclamped(startScale, targetScale, ease);
            yield return null;
        }

        _targetScaleTransform.localScale = targetScale;
        _scaleCoroutine = null;
    }

    #endregion

    #region Border Generation & Layout

    private void EnsureBorderStructure()
    {
        if (_borderContainer != null && _borderCanvasGroup != null && _borderSegments.Count == 8)
        {
            return;
        }

        Transform existing = transform.Find("HoverBorderOverlay");
        if (existing != null)
        {
            _borderContainer = existing as RectTransform;
        }
        else
        {
            GameObject overlayGO = new GameObject("HoverBorderOverlay", typeof(RectTransform), typeof(CanvasGroup));
            overlayGO.transform.SetParent(transform, false);
            _borderContainer = overlayGO.GetComponent<RectTransform>();
        }

        _borderContainer.anchorMin = Vector2.zero;
        _borderContainer.anchorMax = Vector2.one;
        _borderContainer.pivot = new Vector2(0.5f, 0.5f);
        _borderContainer.offsetMin = Vector2.zero;
        _borderContainer.offsetMax = Vector2.zero;
        _borderContainer.localScale = Vector3.one;
        _borderContainer.SetAsLastSibling();

        _borderCanvasGroup = _borderContainer.GetComponent<CanvasGroup>();
        if (_borderCanvasGroup == null)
        {
            _borderCanvasGroup = _borderContainer.gameObject.AddComponent<CanvasGroup>();
        }
        _borderCanvasGroup.blocksRaycasts = false;
        _borderCanvasGroup.interactable = false;

        _borderSegments.Clear();
        for (int i = 0; i < _borderContainer.childCount; i++)
        {
            Image img = _borderContainer.GetChild(i).GetComponent<Image>();
            if (img != null)
            {
                _borderSegments.Add(img);
            }
        }

        while (_borderSegments.Count < 8)
        {
            int idx = _borderSegments.Count;
            GameObject segGO = new GameObject($"BorderSeg_{idx}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            segGO.transform.SetParent(_borderContainer, false);
            Image segImg = segGO.GetComponent<Image>();
            segImg.raycastTarget = false;
            segImg.maskable = true;
            _borderSegments.Add(segImg);
        }

        _geometryDirty = true;
        RebuildBorderLayout();
    }

    private void RebuildBorderLayout()
    {
        if (_borderContainer == null || _borderSegments.Count < 8) return;

        _geometryDirty = false;
        float t = Mathf.Max(1f, _borderThickness);

        for (int i = 0; i < _borderSegments.Count; i++)
        {
            _borderSegments[i].color = _borderColor;
            _borderSegments[i].raycastTarget = false;
        }

        switch (_borderStyle)
        {
            case BorderStyle.SolidInside:
                ConfigureSolidRectSegments(offsetOutward: 0f, thickness: t);
                break;

            case BorderStyle.SolidOutside:
                ConfigureSolidRectSegments(offsetOutward: t + _borderPadding, thickness: t);
                break;

            case BorderStyle.CornerBrackets:
                ConfigureCornerBracketSegments(thickness: t);
                break;
        }
    }

    private void ConfigureSolidRectSegments(float offsetOutward, float thickness)
    {
        for (int i = 0; i < 8; i++)
        {
            _borderSegments[i].gameObject.SetActive(i < 4);
        }

        // Top
        RectTransform top = _borderSegments[0].rectTransform;
        top.anchorMin = new Vector2(0f, 1f);
        top.anchorMax = new Vector2(1f, 1f);
        top.pivot = new Vector2(0.5f, 1f);
        top.offsetMin = new Vector2(-offsetOutward, offsetOutward - thickness);
        top.offsetMax = new Vector2(offsetOutward, offsetOutward);

        // Bottom
        RectTransform bottom = _borderSegments[1].rectTransform;
        bottom.anchorMin = new Vector2(0f, 0f);
        bottom.anchorMax = new Vector2(1f, 0f);
        bottom.pivot = new Vector2(0.5f, 0f);
        bottom.offsetMin = new Vector2(-offsetOutward, -offsetOutward);
        bottom.offsetMax = new Vector2(offsetOutward, -offsetOutward + thickness);

        // Left
        RectTransform left = _borderSegments[2].rectTransform;
        left.anchorMin = new Vector2(0f, 0f);
        left.anchorMax = new Vector2(0f, 1f);
        left.pivot = new Vector2(0f, 0.5f);
        left.offsetMin = new Vector2(-offsetOutward, -offsetOutward + thickness);
        left.offsetMax = new Vector2(-offsetOutward + thickness, offsetOutward - thickness);

        // Right
        RectTransform right = _borderSegments[3].rectTransform;
        right.anchorMin = new Vector2(1f, 0f);
        right.anchorMax = new Vector2(1f, 1f);
        right.pivot = new Vector2(1f, 0.5f);
        right.offsetMin = new Vector2(offsetOutward - thickness, -offsetOutward + thickness);
        right.offsetMax = new Vector2(offsetOutward, offsetOutward - thickness);
    }

    private void ConfigureCornerBracketSegments(float thickness)
    {
        RectTransform selfRect = transform as RectTransform;
        float w = (selfRect != null && selfRect.rect.width > 4f) ? selfRect.rect.width : 64f;
        float h = (selfRect != null && selfRect.rect.height > 4f) ? selfRect.rect.height : 64f;

        float armX = Mathf.Max(thickness * 2f, w * _cornerBracketRatio);
        float armY = Mathf.Max(thickness * 2f, h * _cornerBracketRatio);
        float innerArmY = Mathf.Max(thickness, armY - thickness);

        for (int i = 0; i < 8; i++)
        {
            _borderSegments[i].gameObject.SetActive(true);
        }

        // Top-Left
        SetAnchoredBox(_borderSegments[0].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(armX, thickness));
        SetAnchoredBox(_borderSegments[1].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -thickness), new Vector2(thickness, innerArmY));

        // Top-Right
        SetAnchoredBox(_borderSegments[2].rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(armX, thickness));
        SetAnchoredBox(_borderSegments[3].rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), new Vector2(thickness, innerArmY));

        // Bottom-Left
        SetAnchoredBox(_borderSegments[4].rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(armX, thickness));
        SetAnchoredBox(_borderSegments[5].rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, thickness), new Vector2(thickness, innerArmY));

        // Bottom-Right
        SetAnchoredBox(_borderSegments[6].rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(armX, thickness));
        SetAnchoredBox(_borderSegments[7].rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, thickness), new Vector2(thickness, innerArmY));
    }

    private static void SetAnchoredBox(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
    }

    private void ApplyBorderColor(Color color)
    {
        for (int i = 0; i < _borderSegments.Count; i++)
        {
            if (_borderSegments[i] != null)
            {
                _borderSegments[i].color = color;
            }
        }
    }

    #endregion

    #region Color Tint Animation

    private void StartColorAnimation(Color targetColor, float duration)
    {
        if (_colorCoroutine != null)
        {
            StopCoroutine(_colorCoroutine);
        }
        _colorCoroutine = StartCoroutine(AnimateColorRoutine(targetColor, duration));
    }

    private IEnumerator AnimateColorRoutine(Color targetColor, float duration)
    {
        if (_targetGraphic == null) yield break;

        Color startColor = _targetGraphic.color;
        if (duration <= 0.001f)
        {
            _targetGraphic.color = targetColor;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _targetGraphic.color = Color.Lerp(startColor, targetColor, t);
            yield return null;
        }

        _targetGraphic.color = targetColor;
        _colorCoroutine = null;
    }

    #endregion

    #region Audio Feedback

    private void PlayHoverAudio()
    {
        if (_audioSource != null && _hoverSoundClip != null)
        {
            _audioSource.PlayOneShot(_hoverSoundClip);
        }
        else if (_hoverSoundClip != null)
        {
            AudioSource.PlayClipAtPoint(_hoverSoundClip, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
        }
    }

    #endregion
}
