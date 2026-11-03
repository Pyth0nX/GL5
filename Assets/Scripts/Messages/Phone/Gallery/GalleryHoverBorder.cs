using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Reusable visual UGUI component that draws a configurable highlight border
/// when hovering with the cursor or selecting an item with pointer/keyboard/gamepad.
/// Allows editing color, thickness, style (inside, outside, or corners), and micro-scaling from the Inspector.
/// </summary>
[DisallowMultipleComponent]
public class GalleryHoverBorder : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [Header("Border Configuration")]
    [Tooltip("If enabled, this element will retain its own Inspector settings and ignore global styles from GalleryManager.")]
    [SerializeField] private bool _overrideGlobalSettings = false;

    [Tooltip("Border color on cursor hover or navigation focus.")]
    [SerializeField] private Color _borderColor = new Color(0.25f, 0.85f, 1f, 1f);

    [Tooltip("Border thickness in UI pixels.")]
    [Range(1f, 16f)]
    [SerializeField] private float _borderThickness = 3f;

    [Tooltip("Geometric border style (SolidInside, SolidOutside, or CornerBrackets).")]
    [SerializeField] private GalleryBorderStyle _borderStyle = GalleryBorderStyle.SolidInside;

    [Tooltip("Relative corner arm length ratio (0.15 to 0.48) when CornerBrackets style is selected.")]
    [Range(0.15f, 0.48f)]
    [SerializeField] private float _cornerLengthRatio = 0.28f;

    [Header("Animation and Extra Feedback")]
    [Tooltip("Smooth fade duration on enter/exit hover (0 = instant).")]
    [Range(0f, 0.3f)]
    [SerializeField] private float _fadeDuration = 0.08f;

    [Tooltip("Applies a subtle visual scale to the inner container on hover without altering the parent GridLayoutGroup.")]
    [SerializeField] private bool _enableHoverScale = true;

    [Tooltip("Hover scale factor when Enable Hover Scale is enabled.")]
    [Range(1f, 1.15f)]
    [SerializeField] private float _hoverScale = 1.03f;

    [Tooltip("Optional Transform scaled on hover. If null, only the border animates without scaling the grid item.")]
    [SerializeField] private RectTransform _scaleTarget;

    private RectTransform _overlayRoot;
    private CanvasGroup _overlayCanvasGroup;
    private readonly List<Image> _segmentImages = new List<Image>(8);

    private bool _isHovered;
    private bool _isSelected;
    private float _currentAlpha;
    private float _targetAlpha;
    private bool _geometryDirty = true;

    public bool OverrideGlobalSettings => _overrideGlobalSettings;

    private void Awake()
    {
        EnsureOverlayBuilt();
        SetHighlightedImmediate(false);
    }

    private void OnEnable()
    {
        _isHovered = false;
        _isSelected = false;
        SetHighlightedImmediate(false);
    }

    private void OnDisable()
    {
        _isHovered = false;
        _isSelected = false;
        SetHighlightedImmediate(false);
    }

    private void OnRectTransformDimensionsChange()
    {
        _geometryDirty = true;
        if (_overlayRoot != null && (_currentAlpha > 0.001f || _targetAlpha > 0.001f))
        {
            RebuildSegmentsLayout();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _borderThickness = Mathf.Max(1f, _borderThickness);
        _geometryDirty = true;
        if (Application.isPlaying && _overlayRoot != null)
        {
            RebuildSegmentsLayout();
        }
    }
#endif

    private void Update()
    {
        if (_overlayCanvasGroup == null) return;

        if (_geometryDirty && (_targetAlpha > 0f || _currentAlpha > 0f))
        {
            RebuildSegmentsLayout();
        }

        if (!Mathf.Approximately(_currentAlpha, _targetAlpha))
        {
            if (_fadeDuration <= 0.001f)
            {
                _currentAlpha = _targetAlpha;
            }
            else
            {
                float step = Time.unscaledDeltaTime / _fadeDuration;
                _currentAlpha = Mathf.MoveTowards(_currentAlpha, _targetAlpha, step);
            }

            _overlayCanvasGroup.alpha = _currentAlpha;
            UpdateScaleVisual(_currentAlpha);
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
        _borderStyle = style;
        _cornerLengthRatio = Mathf.Clamp(cornerRatio, 0.15f, 0.48f);
        _enableHoverScale = enableScale;
        _hoverScale = Mathf.Max(1f, hoverScale);
        _fadeDuration = Mathf.Max(0f, fadeDuration);

        _geometryDirty = true;
        EnsureOverlayBuilt();
        RebuildSegmentsLayout();
    }

    /// <summary>
    /// Defines the child RectTransform that receives micro-scaling on hover without affecting the parent GridLayoutGroup.
    /// </summary>
    public void SetScaleTarget(RectTransform target)
    {
        _scaleTarget = target;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _isHovered = true;
        RefreshTargetHighlight();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovered = false;
        RefreshTargetHighlight();
    }

    public void OnSelect(BaseEventData eventData)
    {
        _isSelected = true;
        RefreshTargetHighlight();
    }

    public void OnDeselect(BaseEventData eventData)
    {
        _isSelected = false;
        RefreshTargetHighlight();
    }

    private void RefreshTargetHighlight()
    {
        EnsureOverlayBuilt();
        if (_geometryDirty)
        {
            RebuildSegmentsLayout();
        }

        _overlayRoot.SetAsLastSibling();
        _targetAlpha = (_isHovered || _isSelected) ? 1f : 0f;

        if (_fadeDuration <= 0.001f)
        {
            _currentAlpha = _targetAlpha;
            _overlayCanvasGroup.alpha = _currentAlpha;
            UpdateScaleVisual(_currentAlpha);
        }
    }

    private void SetHighlightedImmediate(bool highlighted)
    {
        _targetAlpha = highlighted ? 1f : 0f;
        _currentAlpha = _targetAlpha;

        if (_overlayCanvasGroup != null)
        {
            _overlayCanvasGroup.alpha = _currentAlpha;
        }

        UpdateScaleVisual(_currentAlpha);
    }

    private void UpdateScaleVisual(float normalizedFactor)
    {
        if (_scaleTarget == null || _scaleTarget == transform) return;

        if (_enableHoverScale)
        {
            float s = Mathf.Lerp(1f, _hoverScale, normalizedFactor);
            _scaleTarget.localScale = new Vector3(s, s, 1f);
        }
        else
        {
            _scaleTarget.localScale = Vector3.one;
        }
    }

    private void EnsureOverlayBuilt()
    {
        if (_overlayRoot != null && _overlayCanvasGroup != null && _segmentImages.Count == 8)
        {
            return;
        }

        Transform existing = transform.Find("HoverBorderOverlay");
        if (existing != null)
        {
            _overlayRoot = existing as RectTransform;
        }
        else
        {
            GameObject overlayGO = new GameObject("HoverBorderOverlay", typeof(RectTransform), typeof(CanvasGroup));
            overlayGO.transform.SetParent(transform, false);
            _overlayRoot = overlayGO.GetComponent<RectTransform>();
        }

        _overlayRoot.anchorMin = Vector2.zero;
        _overlayRoot.anchorMax = Vector2.one;
        _overlayRoot.pivot = new Vector2(0.5f, 0.5f);
        _overlayRoot.offsetMin = Vector2.zero;
        _overlayRoot.offsetMax = Vector2.zero;
        _overlayRoot.localScale = Vector3.one;
        _overlayRoot.SetAsLastSibling();

        _overlayCanvasGroup = _overlayRoot.GetComponent<CanvasGroup>();
        if (_overlayCanvasGroup == null)
        {
            _overlayCanvasGroup = _overlayRoot.gameObject.AddComponent<CanvasGroup>();
        }
        _overlayCanvasGroup.blocksRaycasts = false;
        _overlayCanvasGroup.interactable = false;

        _segmentImages.Clear();
        for (int i = 0; i < _overlayRoot.childCount; i++)
        {
            Image img = _overlayRoot.GetChild(i).GetComponent<Image>();
            if (img != null)
            {
                _segmentImages.Add(img);
            }
        }

        while (_segmentImages.Count < 8)
        {
            int idx = _segmentImages.Count;
            GameObject segGO = new GameObject($"BorderSeg_{idx}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            segGO.transform.SetParent(_overlayRoot, false);
            Image segImg = segGO.GetComponent<Image>();
            segImg.raycastTarget = false;
            segImg.maskable = true;
            _segmentImages.Add(segImg);
        }

        _geometryDirty = true;
        RebuildSegmentsLayout();
    }

    private void RebuildSegmentsLayout()
    {
        if (_overlayRoot == null || _segmentImages.Count < 8) return;

        _geometryDirty = false;
        float t = Mathf.Max(1f, _borderThickness);

        for (int i = 0; i < _segmentImages.Count; i++)
        {
            _segmentImages[i].color = _borderColor;
            _segmentImages[i].raycastTarget = false;
        }

        switch (_borderStyle)
        {
            case GalleryBorderStyle.SolidInside:
                ConfigureSolidRectSegments(offsetOutward: 0f, thickness: t);
                break;

            case GalleryBorderStyle.SolidOutside:
                ConfigureSolidRectSegments(offsetOutward: t, thickness: t);
                break;

            case GalleryBorderStyle.CornerBrackets:
                ConfigureCornerBracketSegments(thickness: t);
                break;
        }
    }

    private void ConfigureSolidRectSegments(float offsetOutward, float thickness)
    {
        // Use 4 segments for the 4 sides (preventing double alpha blending on corners)
        for (int i = 0; i < 8; i++)
        {
            _segmentImages[i].gameObject.SetActive(i < 4);
        }

        // Top (covers top corners end-to-end)
        RectTransform top = _segmentImages[0].rectTransform;
        top.anchorMin = new Vector2(0f, 1f);
        top.anchorMax = new Vector2(1f, 1f);
        top.pivot = new Vector2(0.5f, 1f);
        top.offsetMin = new Vector2(-offsetOutward, offsetOutward - thickness);
        top.offsetMax = new Vector2(offsetOutward, offsetOutward);

        // Bottom (covers bottom corners end-to-end)
        RectTransform bottom = _segmentImages[1].rectTransform;
        bottom.anchorMin = new Vector2(0f, 0f);
        bottom.anchorMax = new Vector2(1f, 0f);
        bottom.pivot = new Vector2(0.5f, 0f);
        bottom.offsetMin = new Vector2(-offsetOutward, -offsetOutward);
        bottom.offsetMax = new Vector2(offsetOutward, -offsetOutward + thickness);

        // Left (between Top and Bottom without overlapping)
        RectTransform left = _segmentImages[2].rectTransform;
        left.anchorMin = new Vector2(0f, 0f);
        left.anchorMax = new Vector2(0f, 1f);
        left.pivot = new Vector2(0f, 0.5f);
        left.offsetMin = new Vector2(-offsetOutward, -offsetOutward + thickness);
        left.offsetMax = new Vector2(-offsetOutward + thickness, offsetOutward - thickness);

        // Right (between Top and Bottom without overlapping)
        RectTransform right = _segmentImages[3].rectTransform;
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

        float armX = Mathf.Max(thickness * 2f, w * _cornerLengthRatio);
        float armY = Mathf.Max(thickness * 2f, h * _cornerLengthRatio);
        float innerArmY = Mathf.Max(thickness, armY - thickness);

        for (int i = 0; i < 8; i++)
        {
            _segmentImages[i].gameObject.SetActive(true);
        }

        // Top-Left (horizontal + vertical)
        SetAnchoredBox(_segmentImages[0].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(armX, thickness));
        SetAnchoredBox(_segmentImages[1].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -thickness), new Vector2(thickness, innerArmY));

        // Top-Right (horizontal + vertical)
        SetAnchoredBox(_segmentImages[2].rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(armX, thickness));
        SetAnchoredBox(_segmentImages[3].rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -thickness), new Vector2(thickness, innerArmY));

        // Bottom-Left (horizontal + vertical)
        SetAnchoredBox(_segmentImages[4].rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), Vector2.zero, new Vector2(armX, thickness));
        SetAnchoredBox(_segmentImages[5].rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, thickness), new Vector2(thickness, innerArmY));

        // Bottom-Right (horizontal + vertical)
        SetAnchoredBox(_segmentImages[6].rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(armX, thickness));
        SetAnchoredBox(_segmentImages[7].rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, thickness), new Vector2(thickness, innerArmY));
    }

    private static void SetAnchoredBox(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 anchoredPos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
    }
}
