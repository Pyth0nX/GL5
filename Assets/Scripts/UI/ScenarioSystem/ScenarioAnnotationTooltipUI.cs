using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the olive-green hover annotation box displayed when the player hovers over a node
/// in the ScenarioSystem flowchart. Shows detailed decision context for taken nodes and "???" for untaken nodes.
/// </summary>
public class ScenarioAnnotationTooltipUI : MonoBehaviour
{
    [SerializeField] private RectTransform _rootRect;
    [SerializeField] private Image _backgroundImage;
    [SerializeField] private TextMeshProUGUI _annotationText;
    [SerializeField] private CanvasGroup _canvasGroup;

    [Header("Custom Visual Sprites (Optional)")]
    [Tooltip("Optional custom background sprite for the annotation tooltip box. If unassigned, procedural rounded rect is used.")]
    [SerializeField] private Sprite _customTooltipSprite;

    [Tooltip("If true, retains the olive-green tint even when a custom sprite is assigned. If false, uses Color.white for custom sprite art.")]
    [SerializeField] private bool _useCustomSpriteColor = false;

    // Olive-green color matching ScenarioSystemConcept.png (#A6AC42)
    private static readonly Color TooltipOliveColor = new Color(0.651f, 0.675f, 0.259f, 0.98f);

    private void Awake()
    {
        EnsureReferences();
        HideImmediate();
    }

    /// <summary>
    /// Ensures all internal UI references are bound and styled according to the concept visual design.
    /// </summary>
    public void ConfigureReferences(
        RectTransform rootRect,
        Image backgroundImage,
        TextMeshProUGUI annotationText,
        CanvasGroup canvasGroup)
    {
        _rootRect = rootRect;
        _backgroundImage = backgroundImage;
        _annotationText = annotationText;
        _canvasGroup = canvasGroup;
        ApplyVisualStyle();
    }

    private void EnsureReferences()
    {
        if (_rootRect == null) _rootRect = GetComponent<RectTransform>();
        if (_backgroundImage == null) _backgroundImage = GetComponent<Image>();
        if (_annotationText == null) _annotationText = GetComponentInChildren<TextMeshProUGUI>(true);
        if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
        ApplyVisualStyle();
    }

    public void SetCustomSprite(Sprite sprite, bool useColor = false)
    {
        _customTooltipSprite = sprite;
        _useCustomSpriteColor = useColor;
        ApplyVisualStyle();
    }

    private void ApplyVisualStyle()
    {
        if (_backgroundImage != null)
        {
            Sprite spriteToUse = _customTooltipSprite != null
                ? _customTooltipSprite
                : ScenarioProceduralSprites.GetRoundedRectSprite();

            _backgroundImage.sprite = spriteToUse;
            _backgroundImage.type = (spriteToUse != null && spriteToUse.border != Vector4.zero)
                ? Image.Type.Sliced
                : Image.Type.Simple;
            _backgroundImage.color = (_customTooltipSprite != null && !_useCustomSpriteColor)
                ? Color.white
                : TooltipOliveColor;
            _backgroundImage.raycastTarget = false;
        }

        if (_annotationText != null)
        {
            _annotationText.raycastTarget = false;
            _annotationText.color = Color.white;
            _annotationText.alignment = TextAlignmentOptions.TopLeft;
            _annotationText.textWrappingMode = TextWrappingModes.Normal;
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }
    }

    /// <summary>
    /// Displays the annotation box anchored near the hovered node, automatically clamping it inside the viewport.
    /// </summary>
    public void ShowForNode(ScenarioNodeUI nodeUI, RectTransform viewportContainer)
    {
        if (nodeUI == null || nodeUI.NodeData == null)
        {
            HideImmediate();
            return;
        }

        EnsureReferences();

        ScenarioOptionNodeData data = nodeUI.NodeData;
        string noteText = data.GetDisplayAnnotation();

        if (_annotationText != null)
        {
            _annotationText.text = noteText;
            _annotationText.fontSize = data.isTaken ? 15f : 20f;
        }

        // Adapt tooltip box size to matched concept proportions (square-ish rounded note card)
        Vector2 boxSize = data.isTaken ? new Vector2(235f, 185f) : new Vector2(180f, 155f);
        if (_rootRect != null)
        {
            _rootRect.sizeDelta = boxSize;
        }

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
        }

        PositionNearNode(nodeUI.RectTransform, viewportContainer, boxSize);
    }

    /// <summary>
    /// Hides the annotation box immediately when the pointer exits a node.
    /// </summary>
    public void HideImmediate()
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 0f;
        }

        if (gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    private void PositionNearNode(RectTransform nodeRect, RectTransform containerRect, Vector2 boxSize)
    {
        if (_rootRect == null || nodeRect == null || containerRect == null)
        {
            return;
        }

        // Convert the node's world position into the tooltip parent container's local space
        Vector3 nodeWorldPos = nodeRect.TransformPoint(nodeRect.rect.center);
        Vector2 localNodeCenter = containerRect.InverseTransformPoint(nodeWorldPos);

        // Place the note slightly overlapping the bottom-left or top-left corner of the node, matching ScenarioSystemConcept.png
        Vector2 offset = new Vector2(-boxSize.x * 0.35f, -nodeRect.rect.height * 0.45f - boxSize.y * 0.45f);
        Vector2 targetCenter = localNodeCenter + offset;

        Rect bounds = containerRect.rect;
        float padding = 18f;

        // If placing below goes beneath the bottom edge of the viewport, place above-left of the node instead (like the top note in Concept)
        if (targetCenter.y - (boxSize.y * 0.5f) < bounds.yMin + padding)
        {
            targetCenter.y = localNodeCenter.y + (nodeRect.rect.height * 0.45f) + (boxSize.y * 0.45f);
        }

        // Clamp horizontally and vertically inside the visible viewport
        float minX = bounds.xMin + (boxSize.x * 0.5f) + padding;
        float maxX = bounds.xMax - (boxSize.x * 0.5f) - padding;
        float minY = bounds.yMin + (boxSize.y * 0.5f) + padding;
        float maxY = bounds.yMax - (boxSize.y * 0.5f) - padding;

        if (minX <= maxX) targetCenter.x = Mathf.Clamp(targetCenter.x, minX, maxX);
        if (minY <= maxY) targetCenter.y = Mathf.Clamp(targetCenter.y, minY, maxY);

        _rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        _rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        _rootRect.pivot = new Vector2(0.5f, 0.5f);
        _rootRect.anchoredPosition = targetCenter;
    }
}
