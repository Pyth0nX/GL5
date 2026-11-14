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

    private int _pendingSlotIndex = -1;
    private Action<int, string> _onConfirmed;
    private Action _onCancelled;
    private bool _listenersHooked = false;

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

        // Full-screen dimmed backdrop to focus attention and block clicks behind the modal
        GameObject backdropGO = new GameObject("NewGameNameModal", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        backdropGO.layer = parentTransform.gameObject.layer;
        backdropGO.transform.SetParent(parentTransform, false);

        RectTransform backdropRect = backdropGO.GetComponent<RectTransform>();
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = Vector2.zero;
        backdropRect.offsetMax = Vector2.zero;

        Image backdropImg = backdropGO.GetComponent<Image>();
        backdropImg.color = new Color(0.04f, 0.05f, 0.09f, 0.72f);
        backdropImg.raycastTarget = true;

        // Centered card
        GameObject cardGO = new GameObject("ModalCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup));
        cardGO.layer = backdropGO.layer;
        cardGO.transform.SetParent(backdropGO.transform, false);

        RectTransform cardRect = cardGO.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(560f, 340f);
        cardRect.anchoredPosition = Vector2.zero;

        Image cardImg = cardGO.GetComponent<Image>();
        cardImg.sprite = roundedSprite;
        cardImg.type = roundedSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        cardImg.color = new Color(0.11f, 0.13f, 0.19f, 0.98f);

        VerticalLayoutGroup cardLayout = cardGO.GetComponent<VerticalLayoutGroup>();
        cardLayout.padding = new RectOffset(32, 32, 28, 28);
        cardLayout.spacing = 14f;
        cardLayout.childAlignment = TextAnchor.UpperCenter;
        cardLayout.childControlWidth = true;
        cardLayout.childControlHeight = false;
        cardLayout.childForceExpandWidth = true;
        cardLayout.childForceExpandHeight = false;

        _titleText = CreateLabel("TitleText", cardGO.transform, _titleLabel, 30f, FontStyles.Bold, Color.white, sharedFont, 40f);
        _subtitleText = CreateLabel("SubtitleText", cardGO.transform, _subtitleLabel, 20f, FontStyles.Normal, new Color(0.84f, 0.87f, 0.94f, 1f), sharedFont, 32f);

        _nameInputField = CreateInputField(cardGO.transform, sharedFont, roundedSprite);

        _validationText = CreateLabel("ValidationText", cardGO.transform, string.Empty, 17f, FontStyles.Italic, new Color(1f, 0.45f, 0.45f, 1f), sharedFont, 24f);
        _validationText.gameObject.SetActive(false);

        // Button row
        GameObject buttonRowGO = new GameObject("ButtonsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        buttonRowGO.layer = cardGO.layer;
        buttonRowGO.transform.SetParent(cardGO.transform, false);
        RectTransform rowRect = buttonRowGO.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, 54f);

        HorizontalLayoutGroup rowLayout = buttonRowGO.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 18f;
        rowLayout.childAlignment = TextAnchor.MiddleCenter;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = true;
        rowLayout.childForceExpandHeight = true;

        _cancelButton = CreateButton("CancelButton", buttonRowGO.transform, "Cancel", new Color(0.28f, 0.31f, 0.39f, 1f), Color.white, sharedFont, roundedSprite);
        _confirmButton = CreateButton("ConfirmButton", buttonRowGO.transform, "Start Game", new Color(0.20f, 0.56f, 0.92f, 1f), Color.white, sharedFont, roundedSprite);

        _modalRoot = backdropGO;
        _modalRoot.SetActive(false);
    }

    private TMP_InputField CreateInputField(Transform parent, TMP_FontAsset font, Sprite roundedSprite)
    {
        GameObject inputGO = new GameObject("NameInputField", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField));
        inputGO.layer = parent.gameObject.layer;
        inputGO.transform.SetParent(parent, false);

        RectTransform inputRect = inputGO.GetComponent<RectTransform>();
        inputRect.sizeDelta = new Vector2(0f, 56f);

        Image bgImg = inputGO.GetComponent<Image>();
        bgImg.sprite = roundedSprite;
        bgImg.type = roundedSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        bgImg.color = new Color(0.96f, 0.97f, 0.99f, 1f);

        GameObject textAreaGO = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        textAreaGO.layer = inputGO.layer;
        textAreaGO.transform.SetParent(inputGO.transform, false);
        RectTransform textAreaRect = textAreaGO.GetComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(16f, 8f);
        textAreaRect.offsetMax = new Vector2(-16f, -8f);

        TextMeshProUGUI placeholderTmp = CreateLabel("Placeholder", textAreaGO.transform, _placeholderLabel, 22f, FontStyles.Italic, new Color(0.50f, 0.53f, 0.60f, 0.85f), font, 40f);
        StretchFull(placeholderTmp.rectTransform);
        placeholderTmp.alignment = TextAlignmentOptions.MidlineLeft;
        placeholderTmp.raycastTarget = false;

        TextMeshProUGUI textTmp = CreateLabel("Text", textAreaGO.transform, string.Empty, 24f, FontStyles.Bold, new Color(0.10f, 0.12f, 0.18f, 1f), font, 40f);
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
        inputField.caretColor = new Color(0.12f, 0.15f, 0.24f, 1f);
        inputField.caretWidth = 2;
        inputField.selectionColor = new Color(0.20f, 0.56f, 0.92f, 0.35f);

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
        GameObject btnGO = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btnGO.layer = parent.gameObject.layer;
        btnGO.transform.SetParent(parent, false);

        Image img = btnGO.GetComponent<Image>();
        img.sprite = roundedSprite;
        img.type = roundedSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        img.color = bgColor;

        Button btn = btnGO.GetComponent<Button>();
        btn.targetGraphic = img;

        TextMeshProUGUI labelTmp = CreateLabel("Text (TMP)", btnGO.transform, label, 22f, FontStyles.Bold, textColor, font, 0f);
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

        return null;
    }
}
