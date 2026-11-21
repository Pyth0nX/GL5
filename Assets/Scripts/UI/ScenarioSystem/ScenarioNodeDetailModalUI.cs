using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the centered 70%-screen node inspection modal displayed when clicking a node
/// in the ScenarioSystem XY flowchart.
/// Layout: left panel shows a representative situation image (or clean placeholder),
/// right panel shows the situation title, choice status subtitle, and scrollable description.
/// </summary>
public class ScenarioNodeDetailModalUI : MonoBehaviour
{
    private RectTransform _backdropRect;
    private RectTransform _cardRect;
    private Image _cardBackground;
    private UnityEngine.UI.Outline _cardOutline;

    private Image _imageFrameBg;
    private UnityEngine.UI.Outline _imageFrameOutline;
    private Image _situationImage;
    private GameObject _placeholderGroup;
    private TextMeshProUGUI _placeholderText;

    private TextMeshProUGUI _titleText;
    private TextMeshProUGUI _statusSubtitleText;
    private Image _dividerImage;
    private TextMeshProUGUI _descriptionText;
    private ScrollRect _descriptionScrollRect;
    private Button _closeButton;

    private bool _isBuilt;

    public bool IsOpen => gameObject != null && gameObject.activeSelf;

    /// <summary>
    /// Ensures the modal UI hierarchy exists and is stretched cleanly over the ScenarioSystem root.
    /// Safe to invoke both at runtime and from Editor prefab builders.
    /// </summary>
    public void EnsureBuilt(Transform parent)
    {
        if (parent != null && transform.parent != parent)
        {
            transform.SetParent(parent, false);
        }

        if (_isBuilt && _cardRect != null && _titleText != null && _descriptionText != null)
        {
            return;
        }

        _backdropRect = ScenarioUiFactory.ConfigureFullScreenBackdrop(
            gameObject,
            ScenarioVisualTheme.BackdropColor,
            Hide);

        // Centered 70% x 70% modal card (anchors 0.15 -> 0.85 on both axes)
        _cardRect = ScenarioUiFactory.CreateRoundedCard(
            transform,
            "DetailCard",
            new Vector2(0.15f, 0.15f),
            new Vector2(0.85f, 0.85f),
            ScenarioVisualTheme.ModalCardBgColor,
            ScenarioVisualTheme.CurrentRunAccent,
            2.5f,
            out _cardBackground,
            out _cardOutline);

        // Left Situation Image Panel (normalized 4%..43% horizontal, 8%..90% vertical)
        RectTransform frameRect = ScenarioUiFactory.CreateRoundedCard(
            _cardRect,
            "ImageFrame",
            new Vector2(0.04f, 0.08f),
            new Vector2(0.43f, 0.90f),
            ScenarioVisualTheme.ModalImageFrameColor,
            new Color(1f, 1f, 1f, 0.14f),
            1.5f,
            out _imageFrameBg,
            out _imageFrameOutline);
        _imageFrameBg.raycastTarget = false;

        GameObject sitImgObj = new GameObject("SituationImage", typeof(RectTransform), typeof(Image));
        sitImgObj.transform.SetParent(frameRect, false);
        RectTransform sitImgRect = sitImgObj.GetComponent<RectTransform>();
        sitImgRect.anchorMin = Vector2.zero;
        sitImgRect.anchorMax = Vector2.one;
        sitImgRect.offsetMin = new Vector2(10f, 10f);
        sitImgRect.offsetMax = new Vector2(-10f, -10f);

        _situationImage = sitImgObj.GetComponent<Image>();
        _situationImage.preserveAspect = true;
        _situationImage.raycastTarget = false;

        // Clean fallback placeholder inside ImageFrame when no situation sprite is assigned yet
        _placeholderGroup = new GameObject("PlaceholderOverlay", typeof(RectTransform));
        _placeholderGroup.transform.SetParent(frameRect, false);
        RectTransform phRect = _placeholderGroup.GetComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero;
        phRect.anchorMax = Vector2.one;
        phRect.offsetMin = new Vector2(20f, 20f);
        phRect.offsetMax = new Vector2(-20f, -20f);

        GameObject phTextObj = new GameObject("PlaceholderText", typeof(RectTransform), typeof(TextMeshProUGUI));
        phTextObj.transform.SetParent(_placeholderGroup.transform, false);
        RectTransform phTextRect = phTextObj.GetComponent<RectTransform>();
        phTextRect.anchorMin = Vector2.zero;
        phTextRect.anchorMax = Vector2.one;
        phTextRect.offsetMin = Vector2.zero;
        phTextRect.offsetMax = Vector2.zero;

        _placeholderText = phTextObj.GetComponent<TextMeshProUGUI>();
        _placeholderText.fontSize = 20f;
        _placeholderText.color = new Color(0.75f, 0.78f, 0.85f, 0.85f);
        _placeholderText.alignment = TextAlignmentOptions.Center;
        _placeholderText.textWrappingMode = TextWrappingModes.Normal;
        _placeholderText.raycastTarget = false;

        // Right Content Column (normalized 46%..95% horizontal, 8%..90% vertical)
        GameObject rightColObj = new GameObject("RightContent", typeof(RectTransform));
        rightColObj.transform.SetParent(_cardRect, false);
        RectTransform rightRect = rightColObj.GetComponent<RectTransform>();
        rightRect.anchorMin = new Vector2(0.46f, 0.08f);
        rightRect.anchorMax = new Vector2(0.95f, 0.90f);
        rightRect.offsetMin = Vector2.zero;
        rightRect.offsetMax = Vector2.zero;

        // Top-right Close button inside card (kept clear of title text)
        _closeButton = CreateCloseButton(_cardRect);

        // Situation Title (top of right column, leaves right margin for X button)
        GameObject titleObj = new GameObject("SituationTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleObj.transform.SetParent(rightColObj.transform, false);
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.82f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = new Vector2(0f, 0f);
        titleRect.offsetMax = new Vector2(-56f, 0f);

        _titleText = titleObj.GetComponent<TextMeshProUGUI>();
        _titleText.fontSize = 32f;
        _titleText.fontStyle = FontStyles.Bold;
        _titleText.color = Color.white;
        _titleText.alignment = TextAlignmentOptions.TopLeft;
        _titleText.textWrappingMode = TextWrappingModes.Normal;
        _titleText.overflowMode = TextOverflowModes.Ellipsis;
        _titleText.raycastTarget = false;

        // Choice + Playthrough Status Subtitle
        GameObject subtitleObj = new GameObject("StatusSubtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
        subtitleObj.transform.SetParent(rightColObj.transform, false);
        RectTransform subRect = subtitleObj.GetComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0f, 0.72f);
        subRect.anchorMax = new Vector2(1f, 0.82f);
        subRect.offsetMin = Vector2.zero;
        subRect.offsetMax = new Vector2(-10f, 0f);

        _statusSubtitleText = subtitleObj.GetComponent<TextMeshProUGUI>();
        _statusSubtitleText.fontSize = 18f;
        _statusSubtitleText.fontStyle = FontStyles.Bold;
        _statusSubtitleText.color = ScenarioVisualTheme.CurrentRunAccent;
        _statusSubtitleText.alignment = TextAlignmentOptions.TopLeft;
        _statusSubtitleText.textWrappingMode = TextWrappingModes.Normal;
        _statusSubtitleText.raycastTarget = false;

        // Accent horizontal divider
        GameObject dividerObj = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        dividerObj.transform.SetParent(rightColObj.transform, false);
        RectTransform divRect = dividerObj.GetComponent<RectTransform>();
        divRect.anchorMin = new Vector2(0f, 0.69f);
        divRect.anchorMax = new Vector2(1f, 0.695f);
        divRect.offsetMin = Vector2.zero;
        divRect.offsetMax = Vector2.zero;
        _dividerImage = dividerObj.GetComponent<Image>();
        _dividerImage.color = new Color(1f, 1f, 1f, 0.18f);
        _dividerImage.raycastTarget = false;

        // Scrollable Annotation Description
        GameObject scrollObj = new GameObject(
            "DescriptionScroll",
            typeof(RectTransform),
            typeof(RectMask2D),
            typeof(ScrollRect));
        scrollObj.transform.SetParent(rightColObj.transform, false);
        RectTransform scrollRectTransform = scrollObj.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(0f, 0f);
        scrollRectTransform.anchorMax = new Vector2(1f, 0.66f);
        scrollRectTransform.offsetMin = Vector2.zero;
        scrollRectTransform.offsetMax = Vector2.zero;

        GameObject contentObj = new GameObject(
            "Content",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        contentObj.transform.SetParent(scrollObj.transform, false);
        RectTransform contentRect = contentObj.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup vlg = contentObj.GetComponent<VerticalLayoutGroup>();
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(0, 8, 4, 16);

        ContentSizeFitter csf = contentObj.GetComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject descObj = new GameObject("DescriptionText", typeof(RectTransform), typeof(TextMeshProUGUI));
        descObj.transform.SetParent(contentObj.transform, false);

        _descriptionText = descObj.GetComponent<TextMeshProUGUI>();
        _descriptionText.fontSize = 22f;
        _descriptionText.color = new Color(0.92f, 0.93f, 0.96f, 1f);
        _descriptionText.alignment = TextAlignmentOptions.TopLeft;
        _descriptionText.textWrappingMode = TextWrappingModes.Normal;
        _descriptionText.lineSpacing = 6f;
        _descriptionText.raycastTarget = false;

        _descriptionScrollRect = scrollObj.GetComponent<ScrollRect>();
        _descriptionScrollRect.viewport = scrollRectTransform;
        _descriptionScrollRect.content = contentRect;
        _descriptionScrollRect.horizontal = false;
        _descriptionScrollRect.vertical = true;
        _descriptionScrollRect.movementType = ScrollRect.MovementType.Clamped;
        _descriptionScrollRect.scrollSensitivity = 30f;

        _isBuilt = true;
        Hide();
    }

    /// <summary>
    /// Populates and reveals the 70% detail modal for the clicked scenario node.
    /// </summary>
    public void ShowForNode(ScenarioOptionNodeData data, Sprite resolvedSituationSprite)
    {
        if (data == null)
        {
            Hide();
            return;
        }

        EnsureBuilt(transform.parent);

        bool discovered = data.IsDiscovered;
        Color accent = ScenarioVisualTheme.GetVisitAccentColor(data.visitState);

        if (_cardOutline != null)
        {
            _cardOutline.effectColor = accent;
        }

        if (_dividerImage != null)
        {
            _dividerImage.color = new Color(accent.r, accent.g, accent.b, 0.45f);
        }

        if (_titleText != null)
        {
            if (discovered)
            {
                string situationTitle = !string.IsNullOrWhiteSpace(data.stageTitle)
                    ? data.stageTitle
                    : $"Day {data.storyDay} Decision";
                _titleText.text = PlayerNameService.FormatText(situationTitle);
            }
            else
            {
                _titleText.text = "Undiscovered Path";
            }
        }

        if (_statusSubtitleText != null)
        {
            if (discovered)
            {
                string cleanChoice = PlayerNameService.FormatText(data.GetDisplayLabel()).Replace('\n', ' ');
                string badge = data.visitState == ScenarioNodeVisitState.CurrentRun
                    ? "Current Playthrough"
                    : "Previous Playthrough";
                _statusSubtitleText.text = $"{cleanChoice}  •  {badge}";
            }
            else
            {
                _statusSubtitleText.text = "Not yet experienced in this save slot";
            }
            _statusSubtitleText.color = accent;
        }

        if (_descriptionText != null)
        {
            _descriptionText.text = discovered
                ? PlayerNameService.FormatText(data.GetDisplayAnnotation())
                : "You have not taken this branch yet.\n\nStart another playthrough after finishing the campaign to explore different choices and uncover what happens along this path.";
        }

        // Only reveal custom situation artwork for discovered nodes so untaken spoilers stay hidden
        Sprite spriteToShow = discovered ? resolvedSituationSprite : null;
        if (_situationImage != null)
        {
            _situationImage.sprite = spriteToShow;
            _situationImage.enabled = (spriteToShow != null);
        }

        if (_placeholderGroup != null)
        {
            _placeholderGroup.SetActive(spriteToShow == null);
        }

        if (_placeholderText != null && spriteToShow == null)
        {
            if (!discovered)
            {
                _placeholderText.fontSize = 54f;
                _placeholderText.fontStyle = FontStyles.Normal;
                _placeholderText.text = "???";
            }
            else
            {
                _placeholderText.fontSize = 26f;
                _placeholderText.fontStyle = FontStyles.Bold;
                _placeholderText.text = PlayerNameService.FormatText(data.stageTitle);
            }
        }

        if (_imageFrameBg != null)
        {
            _imageFrameBg.color = spriteToShow != null
                ? new Color(0.09f, 0.10f, 0.13f, 1f)
                : Color.Lerp(ScenarioVisualTheme.ModalImageFrameColor, accent, 0.12f);
        }

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (_descriptionScrollRect != null && _descriptionScrollRect.content != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_descriptionScrollRect.content);
            _descriptionScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    public void Hide()
    {
        if (gameObject != null && gameObject.activeSelf)
        {
            gameObject.SetActive(false);
        }
    }

    private Button CreateCloseButton(Transform cardParent)
    {
        Button btn = ScenarioUiFactory.CreateRoundedButton(
            cardParent,
            "CloseModalButton",
            "X",
            new Color(0.28f, 0.29f, 0.34f, 1f),
            Color.white,
            20f,
            FontStyles.Bold,
            Hide,
            out _);

        RectTransform rect = btn.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-18f, -18f);
        rect.sizeDelta = new Vector2(42f, 42f);
        return btn;
    }
}
