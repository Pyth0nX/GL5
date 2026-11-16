using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Modal dialog that prompts the player to choose the protagonist's name when starting a New Game
/// on an empty save slot. Supports either Inspector-assigned UI references or automatic runtime
/// UI construction matching the active menu typography and sprite styling.
/// </summary>
public class NewGameNameModalUI : MonoBehaviour
{
    [Header("Optional Manual UI References")]
    [SerializeField] private GameObject _modalRoot;
    [SerializeField] private TMP_InputField _nameInputField;
    [SerializeField] private Button _confirmButton;
    [SerializeField] private Button _cancelButton;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _subtitleText;
    [SerializeField] private TMP_Text _validationText;

    [Header("Labels")]
    [SerializeField] private string _titleLabel = "CHOOSE YOUR NAME";
    [SerializeField] private string _subtitleLabel = "Enter the protagonist's name for this playthrough:";
    [SerializeField] private string _placeholderLabel = "Enter name...";
    [SerializeField] private string _emptyValidationMessage = "Please enter a name to continue.";

    private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.65f);
    private static readonly Color CardColor = new Color(0.24f, 0.24f, 0.24f, 1f);
    private static readonly Color CardBorderColor = new Color(0.36f, 0.36f, 0.36f, 0.9f);
    private static readonly Color InputSurfaceColor = new Color(0.14f, 0.14f, 0.14f, 1f);
    private static readonly Color InputBorderNormalColor = new Color(0.34f, 0.34f, 0.34f, 1f);
    private static readonly Color AccentColor = new Color(0.17f, 0.83f, 0.78f, 1f);
    private static readonly Color AccentSelectionColor = new Color(0.17f, 0.83f, 0.78f, 0.30f);
    private static readonly Color SubtitleColor = new Color(0.80f, 0.80f, 0.80f, 1f);
    private static readonly Color InputTextColor = new Color(0.95f, 0.95f, 0.95f, 1f);
    private static readonly Color PlaceholderColor = new Color(0.55f, 0.55f, 0.55f, 1f);
    private static readonly Color ValidationColor = new Color(0.95f, 0.42f, 0.42f, 1f);
    private static readonly Color PrimaryButtonBgColor = new Color(0.96f, 0.96f, 0.96f, 1f);
    private static readonly Color PrimaryButtonTextColor = new Color(0.18f, 0.18f, 0.18f, 1f);
    private static readonly Color SecondaryButtonBgColor = new Color(0.14f, 0.14f, 0.14f, 1f);
    private static readonly Color SecondaryButtonTextColor = new Color(0.92f, 0.92f, 0.92f, 1f);

    private int _pendingSlotIndex = -1;
    private Action<int, string> _onConfirmed;
    private Action _onCancelled;
    private bool _listenersHooked = false;
    private UnityEngine.UI.Outline _inputOutline;

    public bool IsOpen => _modalRoot != null && _modalRoot.activeSelf;

    private void Awake()
    {
        if (_modalRoot != null)
        {
            HookListeners();
            _modalRoot.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        UnhookListeners();
    }

    private void Update()
    {
        if (!IsOpen) return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cancel();
        }
    }

    /// <summary>
    /// Opens the character-name prompt for the specified save slot.
    /// </summary>
    public void Open(int slotIndex, Action<int, string> onConfirmed, Action onCancelled = null)
    {
        EnsureBuilt();
        HookListeners();

        _pendingSlotIndex = slotIndex;
        _onConfirmed = onConfirmed;
        _onCancelled = onCancelled;

        if (_titleText != null) _titleText.text = _titleLabel;
        if (_subtitleText != null) _subtitleText.text = _subtitleLabel;
        if (_validationText != null)
        {
            _validationText.text = string.Empty;
            _validationText.gameObject.SetActive(false);
        }

        if (_nameInputField != null)
        {
            _nameInputField.characterLimit = PlayerNameService.MaxNameLength;
            _nameInputField.SetTextWithoutNotify(string.Empty);
        }

        _modalRoot.SetActive(true);
        _modalRoot.transform.SetAsLastSibling();

        if (_nameInputField != null)
        {
            _nameInputField.Select();
            _nameInputField.ActivateInputField();
        }
    }

    /// <summary>
    /// Closes the modal without starting a new game.
    /// </summary>
    public void Close()
    {
        if (_modalRoot != null)
        {
            _modalRoot.SetActive(false);
        }

        _pendingSlotIndex = -1;
        _onConfirmed = null;
        _onCancelled = null;
    }

    public void Confirm()
    {
        string rawInput = _nameInputField != null ? _nameInputField.text : string.Empty;
        if (!PlayerNameService.TryCleanUserInput(rawInput, out string cleanedName))
        {
            if (_validationText != null)
            {
                _validationText.text = _emptyValidationMessage;
                _validationText.gameObject.SetActive(true);
            }

            if (_nameInputField != null)
            {
                _nameInputField.Select();
                _nameInputField.ActivateInputField();
            }
            return;
        }

        int slot = _pendingSlotIndex;
        Action<int, string> callback = _onConfirmed;
        Close();
        callback?.Invoke(slot, cleanedName);
    }

    public void Cancel()
    {
        Action callback = _onCancelled;
        Close();
        callback?.Invoke();
    }

    private void HandleInputChanged(string value)
    {
        if (_validationText != null && _validationText.gameObject.activeSelf &&
            PlayerNameService.TryCleanUserInput(value, out _))
        {
            _validationText.gameObject.SetActive(false);
        }
    }

    private void HandleInputSubmit(string _)
    {
        if (IsOpen)
        {
            Confirm();
        }
    }

    private void HandleInputSelected(string _)
    {
        if (_inputOutline != null)
        {
            _inputOutline.effectColor = AccentColor;
        }
    }

    private void HandleInputDeselected(string _)
    {
        if (_inputOutline != null)
        {
            _inputOutline.effectColor = InputBorderNormalColor;
        }
    }

    private void HookListeners()
    {
        if (_listenersHooked) return;
        _listenersHooked = true;

        if (_confirmButton != null)
        {
            _confirmButton.onClick.AddListener(Confirm);
        }

        if (_cancelButton != null)
        {
            _cancelButton.onClick.AddListener(Cancel);
        }

        if (_nameInputField != null)
        {
            _nameInputField.onValueChanged.AddListener(HandleInputChanged);
            _nameInputField.onSubmit.AddListener(HandleInputSubmit);
            _nameInputField.onSelect.AddListener(HandleInputSelected);
            _nameInputField.onDeselect.AddListener(HandleInputDeselected);
        }
    }

    private void UnhookListeners()
    {
        if (!_listenersHooked) return;
        _listenersHooked = false;

        if (_confirmButton != null)
        {
            _confirmButton.onClick.RemoveListener(Confirm);
        }

        if (_cancelButton != null)
        {
            _cancelButton.onClick.RemoveListener(Cancel);
        }

        if (_nameInputField != null)
        {
            _nameInputField.onValueChanged.RemoveListener(HandleInputChanged);
            _nameInputField.onSubmit.RemoveListener(HandleInputSubmit);
            _nameInputField.onSelect.RemoveListener(HandleInputSelected);
            _nameInputField.onDeselect.RemoveListener(HandleInputDeselected);
        }
    }

    private void EnsureBuilt()
    {
        if (_modalRoot != null && _nameInputField != null && _confirmButton != null && _cancelButton != null)
        {
            return;
        }

        Canvas rootCanvas = GetComponentInParent<Canvas>();
        Transform parentTransform = rootCanvas != null ? rootCanvas.transform : transform;
        TMP_FontAsset sharedFont = ResolveMenuFont(transform) ?? ResolveMenuFont(parentTransform);
        Sprite roundedSprite = ResolveRoundedSprite(transform) ?? ResolveRoundedSprite(parentTransform);

        // Full-screen overlay canvas and backdrop matching SettingsPanel / DebugPanel
        GameObject backdropGO = new GameObject(
            "NewGameNameModal",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(GraphicRaycaster),
            typeof(CanvasRenderer),
            typeof(Image));
        backdropGO.layer = parentTransform.gameObject.layer;
        backdropGO.transform.SetParent(parentTransform, false);

        Canvas modalCanvas = backdropGO.GetComponent<Canvas>();
        modalCanvas.overrideSorting = true;
        modalCanvas.sortingOrder = 500;

        RectTransform backdropRect = backdropGO.GetComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;

        Image backdropImg = backdropGO.GetComponent<Image>();
        backdropImg.color = BackdropColor;
        backdropImg.raycastTarget = true;

        // Centered charcoal dialog panel matching SettingsPanel and DebugPanel
        GameObject cardGO = new GameObject("DialogPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Outline), typeof(VerticalLayoutGroup));
        cardGO.layer = backdropGO.layer;
        cardGO.transform.SetParent(backdropGO.transform, false);

        RectTransform cardRect = cardGO.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(560f, 360f);
        cardRect.anchoredPosition = Vector2.zero;

        Image cardImg = cardGO.GetComponent<Image>();
        cardImg.sprite = roundedSprite;
        cardImg.type = roundedSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        cardImg.color = CardColor;

        UnityEngine.UI.Outline cardOutline = cardGO.GetComponent<UnityEngine.UI.Outline>();
        cardOutline.effectColor = CardBorderColor;
        cardOutline.effectDistance = new Vector2(1.5f, -1.5f);

        VerticalLayoutGroup cardLayout = cardGO.GetComponent<VerticalLayoutGroup>();
        cardLayout.padding = new RectOffset(36, 36, 30, 30);
        cardLayout.spacing = 14f;
        cardLayout.childAlignment = TextAnchor.UpperCenter;
        cardLayout.childControlWidth = true;
        cardLayout.childControlHeight = false;
        cardLayout.childForceExpandWidth = true;
        cardLayout.childForceExpandHeight = false;

        _titleText = CreateLabel("TitleText", cardGO.transform, _titleLabel, 34f, FontStyles.Bold, Color.white, sharedFont, 44f);
        _subtitleText = CreateLabel("SubtitleText", cardGO.transform, _subtitleLabel, 19f, FontStyles.Normal, SubtitleColor, sharedFont, 46f);

        _nameInputField = CreateInputField(cardGO.transform, sharedFont, roundedSprite);

        _validationText = CreateLabel("ValidationText", cardGO.transform, string.Empty, 17f, FontStyles.Normal, ValidationColor, sharedFont, 24f);
        _validationText.gameObject.SetActive(false);

        // Action button row matching SettingsPanel / DebugPanel contrast hierarchy
        GameObject buttonRowGO = new GameObject("ButtonsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        buttonRowGO.layer = cardGO.layer;
        buttonRowGO.transform.SetParent(cardGO.transform, false);
        RectTransform rowRect = buttonRowGO.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, 52f);

        HorizontalLayoutGroup rowLayout = buttonRowGO.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 16f;
        rowLayout.childAlignment = TextAnchor.MiddleCenter;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = true;

        _cancelButton = CreateButton("CancelButton", buttonRowGO.transform, "Cancel", SecondaryButtonBgColor, SecondaryButtonTextColor, sharedFont, roundedSprite);
        _confirmButton = CreateButton("ConfirmButton", buttonRowGO.transform, "Start Game", PrimaryButtonBgColor, PrimaryButtonTextColor, sharedFont, roundedSprite);

        _modalRoot = backdropGO;
        _modalRoot.SetActive(false);
    }

    private TMP_InputField CreateInputField(Transform parent, TMP_FontAsset font, Sprite roundedSprite)
    {
        GameObject inputGO = new GameObject("NameInputField", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Outline), typeof(TMP_InputField));
        inputGO.layer = parent.gameObject.layer;
        inputGO.transform.SetParent(parent, false);

        RectTransform inputRect = inputGO.GetComponent<RectTransform>();
        inputRect.sizeDelta = new Vector2(0f, 56f);

        Image bgImg = inputGO.GetComponent<Image>();
        bgImg.sprite = roundedSprite;
        bgImg.type = roundedSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        bgImg.color = InputSurfaceColor;

        _inputOutline = inputGO.GetComponent<UnityEngine.UI.Outline>();
        _inputOutline.effectColor = InputBorderNormalColor;
        _inputOutline.effectDistance = new Vector2(1.25f, -1.25f);

        GameObject textAreaGO = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        textAreaGO.layer = inputGO.layer;
        textAreaGO.transform.SetParent(inputGO.transform, false);
        RectTransform textAreaRect = textAreaGO.GetComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(18f, 8f);
        textAreaRect.offsetMax = new Vector2(-18f, -8f);

        TextMeshProUGUI placeholderTmp = CreateLabel("Placeholder", textAreaGO.transform, _placeholderLabel, 20f, FontStyles.Normal, PlaceholderColor, font, 40f);
        StretchFull(placeholderTmp.rectTransform);
        placeholderTmp.alignment = TextAlignmentOptions.MidlineLeft;
        placeholderTmp.raycastTarget = false;

        TextMeshProUGUI textTmp = CreateLabel("Text", textAreaGO.transform, string.Empty, 22f, FontStyles.Bold, InputTextColor, font, 40f);
        StretchFull(textTmp.rectTransform);
        textTmp.alignment = TextAlignmentOptions.MidlineLeft;
        textTmp.richText = false;
        textTmp.textWrappingMode = TextWrappingModes.NoWrap;

        TMP_InputField inputField = inputGO.GetComponent<TMP_InputField>();
        inputField.targetGraphic = bgImg;
        inputField.textViewport = textAreaRect;
        inputField.textComponent = textTmp;
        inputField.placeholder = placeholderTmp;
        if (font != null)
        {
            inputField.fontAsset = font;
        }
        inputField.characterLimit = PlayerNameService.MaxNameLength;
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.customCaretColor = true;
        inputField.caretColor = AccentColor;
        inputField.caretWidth = 2;
        inputField.selectionColor = AccentSelectionColor;

        return inputField;
    }

    private static TextMeshProUGUI CreateLabel(
        string name,
        Transform parent,
        string text,
        float fontSize,
        FontStyles style,
        Color color,
        TMP_FontAsset font,
        float height)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0f, height);

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        if (font != null)
        {
            tmp.font = font;
        }
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        return tmp;
    }

    private static Button CreateButton(
        string name,
        Transform parent,
        string label,
        Color bgColor,
        Color textColor,
        TMP_FontAsset font,
        Sprite roundedSprite)
    {
        GameObject btnGO = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(UIHoverFeedback));
        btnGO.layer = parent.gameObject.layer;
        btnGO.transform.SetParent(parent, false);

        Image img = btnGO.GetComponent<Image>();
        img.sprite = roundedSprite;
        img.type = roundedSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        img.color = bgColor;

        Button btn = btnGO.GetComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
        colors.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        colors.selectedColor = Color.white;
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        colors.fadeDuration = 0.1f;
        btn.colors = colors;

        TextMeshProUGUI labelTmp = CreateLabel("Text (TMP)", btnGO.transform, label, 21f, FontStyles.Bold, textColor, font, 0f);
        StretchFull(labelTmp.rectTransform);
        labelTmp.raycastTarget = false;

        return btn;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static TMP_FontAsset ResolveMenuFont(Transform scope)
    {
        if (scope != null)
        {
            TMP_Text localText = scope.GetComponentInChildren<TMP_Text>(true);
            if (localText != null && localText.font != null)
            {
                return localText.font;
            }
        }

        TMP_Text anyText = FindAnyObjectByType<TMP_Text>(FindObjectsInactive.Include);
        return anyText != null ? anyText.font : TMP_Settings.defaultFontAsset;
    }

    private static Sprite ResolveRoundedSprite(Transform scope)
    {
        if (scope != null)
        {
            SaveSlotUI slot = scope.GetComponentInChildren<SaveSlotUI>(true);
            if (slot != null)
            {
                Image slotImage = slot.GetComponent<Image>();
                if (slotImage != null && slotImage.sprite != null)
                {
                    return slotImage.sprite;
                }
            }

            Button anyBtn = scope.GetComponentInChildren<Button>(true);
            if (anyBtn != null && anyBtn.image != null && anyBtn.image.sprite != null)
            {
                return anyBtn.image.sprite;
            }
        }

        SaveSlotUI fallbackSlot = FindAnyObjectByType<SaveSlotUI>(FindObjectsInactive.Include);
        if (fallbackSlot != null)
        {
            Image fallbackImg = fallbackSlot.GetComponent<Image>();
            if (fallbackImg != null && fallbackImg.sprite != null)
            {
                return fallbackImg.sprite;
            }
        }

        return null;
    }
}
