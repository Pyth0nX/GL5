using UnityEngine;
using TMPro;

public class ObjectivesUI : MonoBehaviour
{
    [Header("UI Components")]
    [Tooltip("The TextMeshPro text element that will show the objective description.")]
    [SerializeField] private TextMeshProUGUI _objectiveText;

    [Tooltip("The GameObject representing the whole objectives panel (so it can be hidden if there are no objectives).")]
    [SerializeField] private GameObject _panel;

    [Tooltip("Optional background RectTransform resized to wrap the objective text. Auto-detected if omitted.")]
    [SerializeField] private RectTransform _backgroundRect;

    [Tooltip("Optional title RectTransform (e.g. 'Current Objective'). Auto-detected if omitted.")]
    [SerializeField] private RectTransform _titleRect;

    [Header("Dynamic Box Sizing")]
    [SerializeField, Min(120f)] private float _minBoxWidth = 360f;
    [SerializeField, Min(200f)] private float _maxBoxWidth = 680f;
    [SerializeField, Min(60f)] private float _minBoxHeight = 110f;
    [SerializeField, Min(0f)] private float _horizontalPadding = 28f;
    [SerializeField, Min(0f)] private float _topPadding = 18f;
    [SerializeField, Min(0f)] private float _titleToTextSpacing = 10f;
    [SerializeField, Min(0f)] private float _bottomPadding = 20f;

    private TextMeshProUGUI _titleText;
    private bool _layoutInitialized;
    private bool _isStandaloneRootCanvas;
    private Vector2 _standaloneTopLeftAnchoredPos;

    private void Awake()
    {
        EnsureLayoutReferences();
    }

    private void Start()
    {
        EnsureLayoutReferences();

        // Subscribe to the objectives manager event
        if (ObjectivesManager.Instance != null)
        {
            ObjectivesManager.Instance.OnObjectiveUpdated += UpdateUI;
            // Manually update UI once to ensure it doesn't say "New Text"
            UpdateUI(ObjectivesManager.Instance.GetCurrentObjective(), ObjectivesManager.Instance.GetCurrentProgress());
        }
        else
        {
            Debug.LogWarning("[ObjectivesUI] ObjectivesManager instance not found!");
        }
    }

    private void OnDestroy()
    {
        // Always unsubscribe to prevent memory leaks
        if (ObjectivesManager.Instance != null)
        {
            ObjectivesManager.Instance.OnObjectiveUpdated -= UpdateUI;
        }
    }

    private void EnsureLayoutReferences()
    {
        if (_layoutInitialized)
        {
            return;
        }
        _layoutInitialized = true;

        Transform searchRoot = _panel != null ? _panel.transform : transform;

        if (_backgroundRect == null)
        {
            Transform bg = searchRoot.Find("BackGround") ?? searchRoot.Find("Background");
            if (bg != null)
            {
                _backgroundRect = bg as RectTransform;
            }
        }

        if (_titleRect == null)
        {
            Transform title = searchRoot.Find("Title");
            if (title != null)
            {
                _titleRect = title as RectTransform;
            }
        }

        if (_titleRect != null)
        {
            _titleText = _titleRect.GetComponent<TextMeshProUGUI>();
        }

        RectTransform panelRect = _panel != null ? _panel.transform as RectTransform : transform as RectTransform;
        Canvas rootCanvas = panelRect != null ? panelRect.GetComponent<Canvas>() : null;
        _isStandaloneRootCanvas = rootCanvas != null && rootCanvas.isRootCanvas;

        if (_isStandaloneRootCanvas && _backgroundRect != null)
        {
            Vector2 oldPivot = _backgroundRect.pivot;
            Vector2 size = _backgroundRect.rect.size;
            _standaloneTopLeftAnchoredPos = new Vector2(
                _backgroundRect.anchoredPosition.x - oldPivot.x * size.x,
                _backgroundRect.anchoredPosition.y + (1f - oldPivot.y) * size.y);
            _backgroundRect.pivot = new Vector2(0f, 1f);
            _backgroundRect.anchoredPosition = _standaloneTopLeftAnchoredPos;
        }
    }

    private void UpdateUI(Objective currentObj, int progress)
    {
        EnsureLayoutReferences();

        if (currentObj == null || !currentObj.IsActive())
        {
            if (_panel != null) _panel.SetActive(false);
            if (_objectiveText != null) _objectiveText.text = "";
            return;
        }

        // Ensure the panel is visible if we have an active objective
        if (_panel != null && !_panel.activeSelf)
        {
            _panel.SetActive(true);
        }

        string description = PlayerNameService.FormatText(currentObj.Description());

        // If the target count is greater than 1, we show a progress counter like (1/3)
        if (_objectiveText != null)
        {
            _objectiveText.text = currentObj.TargetCount() > 1
                ? $"{description} ({progress}/{currentObj.TargetCount()})"
                : description;

            ResizeBoxToFitText();
        }
    }

    private void ResizeBoxToFitText()
    {
        if (_objectiveText == null)
        {
            return;
        }

        _objectiveText.textWrappingMode = TextWrappingModes.Normal;
        _objectiveText.overflowMode = TextOverflowModes.Overflow;
        _objectiveText.margin = Vector4.zero;

        float titleWidth = 0f;
        float titleHeight = 0f;
        if (_titleText != null)
        {
            _titleText.margin = Vector4.zero;
            Vector2 titlePref = _titleText.GetPreferredValues(_titleText.text);
            titleWidth = titlePref.x;
            titleHeight = Mathf.Max(36f, titlePref.y);
        }
        else if (_titleRect != null)
        {
            titleHeight = Mathf.Max(36f, _titleRect.rect.height);
        }

        Vector2 singleLinePref = _objectiveText.GetPreferredValues(_objectiveText.text);
        float maxAllowedWidth = Mathf.Max(_minBoxWidth, _maxBoxWidth);
        float contentWidth = Mathf.Max(titleWidth, singleLinePref.x);
        float targetWidth = Mathf.Clamp(contentWidth + _horizontalPadding * 2f, _minBoxWidth, maxAllowedWidth);
        float textAreaWidth = Mathf.Max(80f, targetWidth - _horizontalPadding * 2f);

        Vector2 wrappedPref = _objectiveText.GetPreferredValues(_objectiveText.text, textAreaWidth, 0f);
        float textHeight = Mathf.Max(32f, wrappedPref.y);

        float headerBlockHeight = titleHeight > 0f ? titleHeight + _titleToTextSpacing : 0f;
        float targetHeight = Mathf.Max(_minBoxHeight, _topPadding + headerBlockHeight + textHeight + _bottomPadding);

        RectTransform panelRect = _panel != null ? _panel.transform as RectTransform : transform as RectTransform;
        if (!_isStandaloneRootCanvas && panelRect != null)
        {
            panelRect.sizeDelta = new Vector2(targetWidth, targetHeight);
            if (_backgroundRect != null && _backgroundRect != panelRect)
            {
                _backgroundRect.anchorMin = Vector2.zero;
                _backgroundRect.anchorMax = Vector2.one;
                _backgroundRect.pivot = new Vector2(0.5f, 0.5f);
                _backgroundRect.offsetMin = Vector2.zero;
                _backgroundRect.offsetMax = Vector2.zero;
            }
        }
        else if (_backgroundRect != null)
        {
            _backgroundRect.pivot = new Vector2(0f, 1f);
            _backgroundRect.anchoredPosition = _standaloneTopLeftAnchoredPos;
            _backgroundRect.sizeDelta = new Vector2(targetWidth, targetHeight);
        }

        Vector2 originOffset = _isStandaloneRootCanvas && _backgroundRect != null
            ? _standaloneTopLeftAnchoredPos
            : Vector2.zero;
        Vector2 originAnchor = _isStandaloneRootCanvas && _backgroundRect != null
            ? _backgroundRect.anchorMin
            : new Vector2(0f, 1f);

        if (_titleRect != null)
        {
            _titleRect.anchorMin = originAnchor;
            _titleRect.anchorMax = originAnchor;
            _titleRect.pivot = new Vector2(0f, 1f);
            _titleRect.anchoredPosition = originOffset + new Vector2(_horizontalPadding, -_topPadding);
            _titleRect.sizeDelta = new Vector2(textAreaWidth, titleHeight);
        }

        RectTransform textRect = _objectiveText.rectTransform;
        textRect.anchorMin = originAnchor;
        textRect.anchorMax = originAnchor;
        textRect.pivot = new Vector2(0f, 1f);
        textRect.anchoredPosition = originOffset + new Vector2(_horizontalPadding, -(_topPadding + headerBlockHeight));
        textRect.sizeDelta = new Vector2(textAreaWidth, textHeight);
    }
}
