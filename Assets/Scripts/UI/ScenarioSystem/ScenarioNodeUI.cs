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
/// Represents a single interactive decision or ending node inside the ScenarioSystem flowchart.
/// Handles pointer hover events to display the contextual annotation tooltip.
/// </summary>
public class ScenarioNodeUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
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

    [Tooltip("If true, custom sprites will still be tinted with the taken/untaken grey colors. If false, custom sprites display their native colors (Color.white).")]
    [SerializeField] private bool _useCustomSpriteColor = false;

    private ScenarioOptionNodeData _nodeData;
    private Action<ScenarioNodeUI> _onHoverEnter;
    private Action<ScenarioNodeUI> _onHoverExit;
    private Sprite _activeNormalSprite;
    private Sprite _activeHoverSprite;
    private bool _isCustomSpriteActive;

    // Visual palette inspired by ScenarioSystemConcept.png
    private static readonly Color TakenNodeColor = new Color(0.465f, 0.465f, 0.465f, 1f);
    private static readonly Color UntakenNodeColor = new Color(0.42f, 0.42f, 0.42f, 0.94f);
    private static readonly Color HoverNodeColor = new Color(0.56f, 0.56f, 0.56f, 1f);
    private static readonly Color TakenEndingBorderColor = new Color(0.651f, 0.675f, 0.259f, 0.95f);
    private static readonly Color HoverBorderColor = new Color(0.651f, 0.675f, 0.259f, 1f);

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
    /// Populates the node with decision or ending data, resolves custom or procedural sprites, and registers hover callbacks.
    /// </summary>
    public void Initialize(
        ScenarioOptionNodeData data,
        Action<ScenarioNodeUI> onHoverEnter,
        Action<ScenarioNodeUI> onHoverExit,
        Sprite fallbackCustomSprite = null,
        Sprite customHoverSprite = null,
        bool tintCustomSprites = false)
    {
        _nodeData = data;
        _onHoverEnter = onHoverEnter;
        _onHoverExit = onHoverExit;

        if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
        if (_backgroundImage == null) _backgroundImage = GetComponent<Image>();
        if (_outline == null) _outline = GetComponent<UnityEngine.UI.Outline>();
        if (_labelText == null) _labelText = GetComponentInChildren<TextMeshProUGUI>(true);

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
                _backgroundImage.color = data.isTaken ? TakenNodeColor : UntakenNodeColor;
            }
        }

        if (_outline != null)
        {
            _outline.enabled = data.isEndingNode && data.isTaken;
            _outline.effectColor = TakenEndingBorderColor;
            _outline.effectDistance = new Vector2(2f, -2f);
        }

        if (_labelText != null)
        {
            _labelText.raycastTarget = false;
            _labelText.text = data.GetDisplayLabel();
            _labelText.color = Color.white;

            if (data.isTaken)
            {
                // Taken nodes show their text top-left aligned like "Decision Made" in ScenarioSystemConcept.png
                _labelText.alignment = TextAlignmentOptions.TopLeft;
                _labelText.fontSize = data.isEndingNode ? 14.5f : 13.5f;
                _labelText.fontStyle = data.isEndingNode ? FontStyles.Bold : FontStyles.Normal;
            }
            else
            {
                // Untaken nodes display "???" centered inside the rounded square
                _labelText.alignment = TextAlignmentOptions.Center;
                _labelText.fontSize = 24f;
                _labelText.fontStyle = FontStyles.Normal;
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
                _backgroundImage.color = HoverNodeColor;
            }
        }

        if (_outline != null)
        {
            _outline.enabled = true;
            _outline.effectColor = HoverBorderColor;
        }

        if (_rectTransform != null)
        {
            _rectTransform.localScale = new Vector3(1.04f, 1.04f, 1f);
        }

        _onHoverEnter?.Invoke(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_backgroundImage != null && _nodeData != null)
        {
            if (_activeNormalSprite != null)
            {
                _backgroundImage.sprite = _activeNormalSprite;
            }

            if (_isCustomSpriteActive && !_useCustomSpriteColor)
            {
                _backgroundImage.color = Color.white;
            }
            else
            {
                _backgroundImage.color = _nodeData.isTaken ? TakenNodeColor : UntakenNodeColor;
            }
        }

        if (_outline != null && _nodeData != null)
        {
            _outline.enabled = _nodeData.isEndingNode && _nodeData.isTaken;
            _outline.effectColor = TakenEndingBorderColor;
        }

        if (_rectTransform != null)
        {
            _rectTransform.localScale = Vector3.one;
        }

        _onHoverExit?.Invoke(this);
    }

    /// <summary>
    /// Returns the world-space anchor point on the left side of this node where incoming arrows terminate.
    /// </summary>
    public Vector3 GetLeftConnectionWorldPoint(float verticalOffsetNormalized = 0f)
    {
        Rect r = RectTransform.rect;
        Vector3 localPoint = new Vector3(r.xMin + 6f, r.center.y + (r.height * 0.28f * verticalOffsetNormalized), 0f);
        return RectTransform.TransformPoint(localPoint);
    }

    /// <summary>
    /// Returns the world-space anchor point on the right side of this node where outgoing arrows originate.
    /// </summary>
    public Vector3 GetRightConnectionWorldPoint(float verticalOffsetNormalized = 0f)
    {
        Rect r = RectTransform.rect;
        Vector3 localPoint = new Vector3(r.xMax - 6f, r.center.y + (r.height * 0.28f * verticalOffsetNormalized), 0f);
        return RectTransform.TransformPoint(localPoint);
    }
}
