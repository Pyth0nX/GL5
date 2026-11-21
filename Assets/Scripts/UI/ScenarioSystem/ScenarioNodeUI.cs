using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Generates and caches clean procedural UI sprites (9-sliced rounded rectangles and triangular arrowheads)
/// so the ScenarioSystem flowchart renders crisply without external texture dependencies.
/// </summary>
public static class ScenarioProceduralSprites
{
    private static Sprite _roundedRectSprite;
    private static Sprite _triangleArrowSprite;

    /// <summary>
    /// Returns a 9-sliced rounded rectangle sprite with smooth anti-aliased corners.
    /// </summary>
    public static Sprite GetRoundedRectSprite()
    {
        if (_roundedRectSprite != null)
        {
            return _roundedRectSprite;
        }

        const int size = 64;
        const float radius = 14f;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "ScenarioRoundedRect",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;

                float dx = 0f;
                if (px < radius) dx = radius - px;
                else if (px > size - radius) dx = px - (size - radius);

                float dy = 0f;
                if (py < radius) dy = radius - py;
                else if (py > size - radius) dy = py - (size - radius);

                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(radius + 0.5f - dist);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        Vector4 border = new Vector4(radius + 2f, radius + 2f, radius + 2f, radius + 2f);
        _roundedRectSprite = Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            border);

        return _roundedRectSprite;
    }

    /// <summary>
    /// Returns a right-pointing filled triangle sprite used for directed connection arrowheads.
    /// </summary>
    public static Sprite GetTriangleArrowSprite()
    {
        if (_triangleArrowSprite != null)
        {
            return _triangleArrowSprite;
        }

        const int width = 32;
        const int height = 32;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "ScenarioArrowHead",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color[] pixels = new Color[width * height];
        float halfH = height * 0.5f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float normalizedX = (x + 0.5f) / width;
                float maxHalfSpanAtX = (1f - normalizedX) * halfH;
                float distFromCenterY = Mathf.Abs((y + 0.5f) - halfH);
                float alpha = Mathf.Clamp01(maxHalfSpanAtX + 0.6f - distFromCenterY);
                pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        _triangleArrowSprite = Sprite.Create(
            tex,
            new Rect(0, 0, width, height),
            new Vector2(0.5f, 0.5f),
            100f);

        return _triangleArrowSprite;
    }
}

/// <summary>
/// Centralized color tokens for the ScenarioSystem flowchart, arrows, node states, and modals.
/// </summary>
public static class ScenarioVisualTheme
{
    public static readonly Color CurrentRunDecisionFill = new Color(0.18f, 0.34f, 0.38f, 1f);
    public static readonly Color CurrentRunEndingFill = new Color(0.28f, 0.25f, 0.14f, 1f);
    public static readonly Color CurrentRunBorder = new Color(0.96f, 0.82f, 0.34f, 1f);
    public static readonly Color CurrentRunAccent = new Color(0.96f, 0.82f, 0.34f, 1f);
    public static readonly Color CurrentRunArrowColor = new Color(0.96f, 0.82f, 0.34f, 0.96f);

    public static readonly Color PreviousRunFill = new Color(0.24f, 0.21f, 0.34f, 0.96f);
    public static readonly Color PreviousRunBorder = new Color(0.62f, 0.55f, 0.88f, 0.92f);
    public static readonly Color PreviousRunAccent = new Color(0.66f, 0.58f, 0.94f, 1f);
    public static readonly Color PreviousRunTextColor = new Color(0.88f, 0.86f, 0.96f, 0.95f);
    public static readonly Color PreviousRunArrowColor = new Color(0.58f, 0.52f, 0.82f, 0.78f);

    public static readonly Color UndiscoveredFill = new Color(0.22f, 0.22f, 0.25f, 0.90f);
    public static readonly Color UndiscoveredAccent = new Color(0.62f, 0.64f, 0.68f, 1f);
    public static readonly Color UndiscoveredTextColor = new Color(0.75f, 0.75f, 0.78f, 0.85f);
    public static readonly Color UndiscoveredArrowColor = new Color(0.45f, 0.46f, 0.52f, 0.65f);

    public static readonly Color HoverBorderColor = new Color(1f, 0.92f, 0.50f, 1f);
    public static readonly Color HoverFillTint = new Color(1.16f, 1.16f, 1.16f, 1f);

    public static readonly Color BackdropColor = new Color(0.03f, 0.03f, 0.05f, 0.78f);
    public static readonly Color ModalCardBgColor = new Color(0.13f, 0.14f, 0.18f, 0.99f);
    public static readonly Color ModalImageFrameColor = new Color(0.18f, 0.20f, 0.26f, 1f);

    public static readonly Color EndingDividerColor = new Color(0.82f, 0.84f, 0.88f, 0.72f);
    public static readonly Color EndingHeaderColor = new Color(0.96f, 0.85f, 0.42f, 1f);
    public static readonly Color ColumnHeaderColor = new Color(0.78f, 0.80f, 0.84f, 0.92f);

    public static Color GetVisitAccentColor(ScenarioNodeVisitState visitState)
    {
        return visitState switch
        {
            ScenarioNodeVisitState.CurrentRun => CurrentRunAccent,
            ScenarioNodeVisitState.PreviousRun => PreviousRunAccent,
            _ => UndiscoveredAccent
        };
    }
}

/// <summary>
/// Shared procedural UI builders for rounded cards, modal backdrops, and action buttons in ScenarioSystem.
/// </summary>
public static class ScenarioUiFactory
{
    public static RectTransform ConfigureFullScreenBackdrop(
        GameObject host,
        Color color,
        UnityEngine.Events.UnityAction onBackdropClick = null)
    {
        RectTransform rect = host.GetComponent<RectTransform>();
        if (rect == null) rect = host.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image img = host.GetComponent<Image>();
        if (img == null) img = host.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = true;

        if (onBackdropClick != null)
        {
            Button btn = host.GetComponent<Button>();
            if (btn == null) btn = host.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.RemoveListener(onBackdropClick);
            btn.onClick.AddListener(onBackdropClick);
        }

        return rect;
    }

    public static RectTransform CreateRoundedCard(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Color bgColor,
        Color outlineColor,
        float outlineThickness,
        out Image background,
        out UnityEngine.UI.Outline outline)
    {
        GameObject cardObj = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Image),
            typeof(UnityEngine.UI.Outline));
        cardObj.transform.SetParent(parent, false);

        RectTransform rect = cardObj.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        background = cardObj.GetComponent<Image>();
        background.sprite = ScenarioProceduralSprites.GetRoundedRectSprite();
        background.type = Image.Type.Sliced;
        background.color = bgColor;
        background.raycastTarget = true;

        outline = cardObj.GetComponent<UnityEngine.UI.Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(outlineThickness, -outlineThickness);

        return rect;
    }

    public static Button CreateRoundedButton(
        Transform parent,
        string name,
        string label,
        Color bgColor,
        Color textColor,
        float fontSize,
        FontStyles fontStyle,
        UnityEngine.Events.UnityAction onClick,
        out TextMeshProUGUI labelText)
    {
        GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(parent, false);

        Image img = btnObj.GetComponent<Image>();
        img.sprite = ScenarioProceduralSprites.GetRoundedRectSprite();
        img.type = Image.Type.Sliced;
        img.color = bgColor;

        GameObject txtObj = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtObj.transform.SetParent(btnObj.transform, false);
        RectTransform txtRect = txtObj.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.offsetMin = new Vector2(8f, 2f);
        txtRect.offsetMax = new Vector2(-8f, -2f);

        labelText = txtObj.GetComponent<TextMeshProUGUI>();
        labelText.text = label;
        labelText.fontSize = fontSize;
        labelText.fontStyle = fontStyle;
        labelText.color = textColor;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.raycastTarget = false;

        Button btn = btnObj.GetComponent<Button>();
        if (onClick != null)
        {
            btn.onClick.RemoveListener(onClick);
            btn.onClick.AddListener(onClick);
        }
        return btn;
    }
}

/// <summary>
/// Represents a single interactive decision or ending node inside the ScenarioSystem XY flowchart.
/// Supports hover highlighting, click-to-open detail inspection, and drag/scroll pass-through for XY navigation.
/// </summary>
public class ScenarioNodeUI : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerClickHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IScrollHandler
{
    [SerializeField] private RectTransform _rectTransform;
    [SerializeField] private Image _backgroundImage;
    [SerializeField] private UnityEngine.UI.Outline _outline;
    [SerializeField] private TextMeshProUGUI _labelText;

    [Header("Custom Sprites (Optional — falls back to procedural if unassigned)")]
    [Tooltip("Optional custom normal sprite assigned directly on the node prefab.")]
    [SerializeField] private Sprite _customNormalSprite;

    [Tooltip("Optional custom hover sprite assigned directly on the node prefab.")]
    [SerializeField] private Sprite _customHoverSprite;

    [Tooltip("If true, custom sprites will still be tinted with the visit-state colors. If false, custom sprites display their native colors (Color.white).")]
    [SerializeField] private bool _useCustomSpriteColor = false;

    private ScenarioOptionNodeData _nodeData;
    private Action<ScenarioNodeUI> _onHoverEnter;
    private Action<ScenarioNodeUI> _onHoverExit;
    private Action<ScenarioNodeUI> _onClicked;
    private Sprite _activeNormalSprite;
    private Sprite _activeHoverSprite;
    private bool _isCustomSpriteActive;

    private ScrollRect _parentScrollRect;
    private Vector2 _pointerDownScreenPos;
    private bool _isDragging;

    public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = GetComponent<RectTransform>());
    public ScenarioOptionNodeData NodeData => _nodeData;

    /// <summary>
    /// Binds internal UI component references when instantiating or building the node hierarchy.
    /// </summary>
    public void ConfigureReferences(
        RectTransform rectTransform,
        Image backgroundImage,
        UnityEngine.UI.Outline outline,
        TextMeshProUGUI labelText)
    {
        _rectTransform = rectTransform;
        _backgroundImage = backgroundImage;
        _outline = outline;
        _labelText = labelText;
    }

    /// <summary>
    /// Populates the node with decision or ending data, resolves custom or procedural sprites, and registers callbacks.
    /// </summary>
    public void Initialize(
        ScenarioOptionNodeData data,
        Action<ScenarioNodeUI> onHoverEnter,
        Action<ScenarioNodeUI> onHoverExit,
        Sprite fallbackCustomSprite = null,
        Sprite customHoverSprite = null,
        bool tintCustomSprites = false,
        Action<ScenarioNodeUI> onClicked = null)
    {
        _nodeData = data;
        _onHoverEnter = onHoverEnter;
        _onHoverExit = onHoverExit;
        _onClicked = onClicked;

        if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
        if (_backgroundImage == null) _backgroundImage = GetComponent<Image>();
        if (_outline == null) _outline = GetComponent<UnityEngine.UI.Outline>();
        if (_labelText == null) _labelText = GetComponentInChildren<TextMeshProUGUI>(true);
        _parentScrollRect = GetComponentInParent<ScrollRect>();

        // Resolve normal sprite: per-node customSprite -> fallbackCustomSprite -> _customNormalSprite -> procedural
        Sprite selectedSprite = data != null && data.customSprite != null
            ? data.customSprite
            : (fallbackCustomSprite != null ? fallbackCustomSprite : _customNormalSprite);

        _isCustomSpriteActive = (selectedSprite != null);
        _activeNormalSprite = _isCustomSpriteActive
            ? selectedSprite
            : ScenarioProceduralSprites.GetRoundedRectSprite();

        _activeHoverSprite = customHoverSprite != null
            ? customHoverSprite
            : _customHoverSprite;

        ApplyBaseVisualState(tintCustomSprites);

        if (_labelText != null && data != null)
        {
            _labelText.raycastTarget = false;
            _labelText.text = PlayerNameService.FormatText(data.GetDisplayLabel());

            if (data.IsDiscovered)
            {
                _labelText.color = data.visitState == ScenarioNodeVisitState.PreviousRun
                    ? ScenarioVisualTheme.PreviousRunTextColor
                    : Color.white;
                _labelText.alignment = TextAlignmentOptions.TopLeft;
                _labelText.fontSize = data.isEndingNode ? 14.5f : 13.5f;
                _labelText.fontStyle = data.visitState == ScenarioNodeVisitState.CurrentRun || data.isEndingNode
                    ? FontStyles.Bold
                    : FontStyles.Normal;
            }
            else
            {
                _labelText.color = ScenarioVisualTheme.UndiscoveredTextColor;
                _labelText.alignment = TextAlignmentOptions.Center;
                _labelText.fontSize = 24f;
                _labelText.fontStyle = FontStyles.Normal;
            }
        }
    }

    private Color GetBaseFillColor()
    {
        if (_nodeData == null) return ScenarioVisualTheme.UndiscoveredFill;

        return _nodeData.visitState switch
        {
            ScenarioNodeVisitState.CurrentRun => _nodeData.isEndingNode
                ? ScenarioVisualTheme.CurrentRunEndingFill
                : ScenarioVisualTheme.CurrentRunDecisionFill,
            ScenarioNodeVisitState.PreviousRun => ScenarioVisualTheme.PreviousRunFill,
            _ => ScenarioVisualTheme.UndiscoveredFill
        };
    }

    private void ApplyBaseVisualState(bool tintCustomSprites = false)
    {
        if (_backgroundImage != null)
        {
            _backgroundImage.sprite = _activeNormalSprite;
            _backgroundImage.type = (_activeNormalSprite != null && _activeNormalSprite.border != Vector4.zero)
                ? Image.Type.Sliced
                : Image.Type.Simple;
            _backgroundImage.raycastTarget = true;

            if (_isCustomSpriteActive && !tintCustomSprites && !_useCustomSpriteColor)
            {
                _backgroundImage.color = Color.white;
            }
            else
            {
                _backgroundImage.color = GetBaseFillColor();
            }
        }

        if (_outline != null && _nodeData != null)
        {
            switch (_nodeData.visitState)
            {
                case ScenarioNodeVisitState.CurrentRun:
                    _outline.enabled = true;
                    _outline.effectColor = ScenarioVisualTheme.CurrentRunBorder;
                    _outline.effectDistance = new Vector2(3.5f, -3.5f);
                    break;

                case ScenarioNodeVisitState.PreviousRun:
                    _outline.enabled = true;
                    _outline.effectColor = ScenarioVisualTheme.PreviousRunBorder;
                    _outline.effectDistance = new Vector2(2f, -2f);
                    break;

                default:
                    _outline.enabled = false;
                    _outline.effectColor = ScenarioVisualTheme.CurrentRunBorder;
                    _outline.effectDistance = new Vector2(2f, -2f);
                    break;
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_backgroundImage != null)
        {
            if (_activeHoverSprite != null)
            {
                _backgroundImage.sprite = _activeHoverSprite;
            }

            if (!_isCustomSpriteActive || _useCustomSpriteColor)
            {
                Color baseColor = GetBaseFillColor();
                _backgroundImage.color = new Color(
                    Mathf.Min(1f, baseColor.r * ScenarioVisualTheme.HoverFillTint.r + 0.06f),
                    Mathf.Min(1f, baseColor.g * ScenarioVisualTheme.HoverFillTint.g + 0.06f),
                    Mathf.Min(1f, baseColor.b * ScenarioVisualTheme.HoverFillTint.b + 0.06f),
                    baseColor.a);
            }
        }

        if (_outline != null)
        {
            _outline.enabled = true;
            _outline.effectColor = ScenarioVisualTheme.HoverBorderColor;
        }

        if (_rectTransform != null)
        {
            _rectTransform.localScale = new Vector3(1.05f, 1.05f, 1f);
        }

        _onHoverEnter?.Invoke(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ApplyBaseVisualState();

        if (_rectTransform != null)
        {
            _rectTransform.localScale = Vector3.one;
        }

        _onHoverExit?.Invoke(this);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _pointerDownScreenPos = eventData.position;
        _isDragging = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || _isDragging)
        {
            return;
        }

        if (Vector2.Distance(_pointerDownScreenPos, eventData.position) <= 10f)
        {
            _onClicked?.Invoke(this);
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _isDragging = true;
        if (_parentScrollRect == null) _parentScrollRect = GetComponentInParent<ScrollRect>();
        _parentScrollRect?.OnBeginDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_parentScrollRect == null) _parentScrollRect = GetComponentInParent<ScrollRect>();
        _parentScrollRect?.OnDrag(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_parentScrollRect == null) _parentScrollRect = GetComponentInParent<ScrollRect>();
        _parentScrollRect?.OnEndDrag(eventData);
        _isDragging = false;
    }

    public void OnScroll(PointerEventData eventData)
    {
        ScenarioSystemUI systemUI = GetComponentInParent<ScenarioSystemUI>();
        if (systemUI != null)
        {
            systemUI.OnScroll(eventData);
        }
    }

    /// <summary>
    /// Computes the world-space intersection on this node's perimeter toward a target world position
    /// so 2D angled branches connect crisply to any edge (left, right, top, bottom).
    /// </summary>
    public Vector3 GetPerimeterConnectionWorldPoint(Vector3 towardWorldPoint)
    {
        Rect r = RectTransform.rect;
        Vector3 localTarget = RectTransform.InverseTransformPoint(towardWorldPoint);
        Vector2 dir = new Vector2(localTarget.x - r.center.x, localTarget.y - r.center.y);
        if (dir.sqrMagnitude < 0.0001f)
        {
            return RectTransform.TransformPoint(r.center);
        }

        float halfW = Mathf.Max(1f, (r.width * 0.5f) - 2f);
        float halfH = Mathf.Max(1f, (r.height * 0.5f) - 2f);

        float scaleX = Mathf.Abs(dir.x) > 0.0001f ? halfW / Mathf.Abs(dir.x) : float.MaxValue;
        float scaleY = Mathf.Abs(dir.y) > 0.0001f ? halfH / Mathf.Abs(dir.y) : float.MaxValue;
        float t = Mathf.Min(scaleX, scaleY);

        Vector2 localEdge = r.center + dir * t;
        return RectTransform.TransformPoint(new Vector3(localEdge.x, localEdge.y, 0f));
    }

    /// <summary>
    /// Returns the world-space anchor point on the left side of this node where incoming arrows terminate.
    /// </summary>
    public Vector3 GetLeftConnectionWorldPoint(float verticalOffsetNormalized = 0f)
    {
        Rect r = RectTransform.rect;
        Vector3 localPoint = new Vector3(r.xMin + 4f, r.center.y + (r.height * 0.28f * verticalOffsetNormalized), 0f);
        return RectTransform.TransformPoint(localPoint);
    }

    /// <summary>
    /// Returns the world-space anchor point on the right side of this node where outgoing arrows originate.
    /// </summary>
    public Vector3 GetRightConnectionWorldPoint(float verticalOffsetNormalized = 0f)
    {
        Rect r = RectTransform.rect;
        Vector3 localPoint = new Vector3(r.xMax - 4f, r.center.y + (r.height * 0.28f * verticalOffsetNormalized), 0f);
        return RectTransform.TransformPoint(localPoint);
    }
}
