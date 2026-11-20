using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Manages the bottom interactive bar inside the phone Messaging screen:
/// - Mode A (Free Text): fixed-size scrollable multi-line TMP_InputField + SEND button that classifies
///   English replies into Safe (0), Neutral (1), or Risky (2) branches.
/// - Mode B (Two Options): two compact choice buttons replacing the input bar for lore/standard turns.
/// - Blocked Send: clicking SEND or pressing Enter outside Mode A triggers a random inner monologue.
/// </summary>
public class DialogueButton : MonoBehaviour
{
    private const float NpcMessageDelaySeconds = 1.0f;
    private const float ScriptedPlayerLineDelaySeconds = 0.25f;
    private const float PhoneAdvanceCooldownSeconds = 0.08f;

    private static readonly Color InputSurfaceColor = new Color(0.20f, 0.21f, 0.23f, 1f);
    private static readonly Color InputTextColor = new Color(0.95f, 0.96f, 0.98f, 1f);
    private static readonly Color InputPlaceholderColor = new Color(0.74f, 0.78f, 0.84f, 0.85f);
    private static readonly Color InputActiveBorderColor = new Color(0.22f, 0.76f, 0.42f, 1f);
    private static readonly Color InputBlockedBorderColor = new Color(0.55f, 0.58f, 0.62f, 1f);
    private static readonly Color SendActiveColor = Color.white;
    private static readonly Color OptionButtonColor = new Color(0.18f, 0.46f, 0.76f, 1f);

    private static readonly string[] BlockedSendMonologues =
    {
        "I shouldn't send anything right now.",
        "Better not text them right now.",
        "I don't really have anything to say right now.",
        "There's no point in sending a message right now.",
        "I should just leave this chat alone for now.",
        "Maybe it's better if I don't text them right now.",
        "Not now... I shouldn't be texting them."
    };

    [Header("Phone Input Bar UI (Prefab References)")]
    [SerializeField] private GameObject _freeTextContainer;
    [SerializeField] private TMP_InputField _inputField;
    [SerializeField] private TextMeshProUGUI _placeholderText;
    [SerializeField] private Button _sendButton;
    [SerializeField] private Image _sendButtonImage;
    [SerializeField] private Image _sendButtonIconImage;

    private Button _legacyButton;
    private Image _legacyImage;
    private TextMeshProUGUI _legacyTextMesh;
    private Sprite _roundedSprite;
    private TMP_FontAsset _sharedFont;

    private UnityEngine.UI.Outline _inputOutline;

    private GameObject _twoOptionsContainer;
    private Button _modeBOption0Button;
    private Button _modeBOption1Button;
    private TextMeshProUGUI _modeBOption0Text;
    private TextMeshProUGUI _modeBOption1Text;

    private bool _uiBuilt;
    private bool _isWaitingForFreeText;
    private bool _isWaitingForTwoOptions;
    private bool _narrationDialogueActive;
    private bool _blockedMonologueActive;
    private float _timer;
    private int _lastAdvanceFrame = -1;
    private float _lastAdvanceTime = -1f;
    private int _lastSendFrame = -1;
    private int _lastBlockedMonologueIndex = -1;
    private string _activeContactName;

    private void Awake()
    {
        _legacyButton = GetComponent<Button>();
        _legacyImage = GetComponent<Image>();
        Transform legacyLabelTransform = transform.Find("Text (TMP)");
        if (legacyLabelTransform != null)
        {
            _legacyTextMesh = legacyLabelTransform.GetComponent<TextMeshProUGUI>();
        }

        EnsureUI();
    }

    private void OnEnable()
    {
        EnsureUI();
        RefreshActiveContactTracking();
        UpdateBarState();
    }

    private void OnDisable()
    {
        _timer = 0f;
        _isWaitingForFreeText = false;
        _isWaitingForTwoOptions = false;
        _narrationDialogueActive = false;
        _blockedMonologueActive = false;
        UnfocusInputField();
    }

    private void OnDestroy()
    {
        if (_sendButton != null)
        {
            _sendButton.onClick.RemoveListener(OnSendTriggered);
        }

        if (_inputField != null)
        {
            _inputField.onSubmit.RemoveListener(OnInputSubmit);
        }

        if (_modeBOption0Button != null)
        {
            _modeBOption0Button.onClick.RemoveAllListeners();
        }

        if (_modeBOption1Button != null)
        {
            _modeBOption1Button.onClick.RemoveAllListeners();
        }
    }

    private void Update()
    {
        RefreshActiveContactTracking();
        UpdateBarState();
        CheckNpcFastForwardInput();
    }

    private void RefreshActiveContactTracking()
    {
        string currentContactName = MessagesManager.Instance != null
            ? MessagesManager.Instance.GetCurrentContactName()
            : null;

        if (!string.Equals(_activeContactName, currentContactName, StringComparison.OrdinalIgnoreCase))
        {
            _activeContactName = currentContactName;
            _timer = 0f;
            _isWaitingForFreeText = false;
            _isWaitingForTwoOptions = false;

            if (_inputField != null)
            {
                _inputField.SetTextWithoutNotify(string.Empty);
                UnfocusInputField();
            }
        }
    }

    private void UpdateBarState()
    {
        if (MessagesManager.Instance == null)
        {
            ShowFreeTextBar(activeForModeA: false);
            return;
        }

        if (_narrationDialogueActive || _blockedMonologueActive)
        {
            _timer = 0f;
            ShowFreeTextBar(activeForModeA: false);
            return;
        }

        Contact currentContact = MessagesManager.Instance.GetCurrentContact();
        if (currentContact == null)
        {
            _isWaitingForFreeText = false;
            _isWaitingForTwoOptions = false;
            ShowFreeTextBar(activeForModeA: false);
            return;
        }

        OptionsChanger optionsChanger = MessagesManager.Instance.GetOptionsChanger();
        if (optionsChanger != null && optionsChanger.IsDecisionsPanelActive())
        {
            _isWaitingForFreeText = false;
            _isWaitingForTwoOptions = false;
            ShowFreeTextBar(activeForModeA: false);
            return;
        }

        if (!currentContact.HasNewMessages())
        {
            _isWaitingForFreeText = false;
            _isWaitingForTwoOptions = false;
            _timer = 0f;
            ShowFreeTextBar(activeForModeA: false);
            return;
        }

        NarrativeNode currentNode = MessagesManager.Instance.ResolveAndGetCurrentContactNode();
        if (currentNode == null)
        {
            currentContact.SetHasNewMessages(false);
            _isWaitingForFreeText = false;
            _isWaitingForTwoOptions = false;
            ShowFreeTextBar(activeForModeA: false);
            return;
        }

        MessagesUI messagesUI = MessagesManager.Instance.GetMessagesUI();
        if (messagesUI != null && messagesUI.IsAnimating)
        {
            _timer = 0f;
            _isWaitingForFreeText = false;
            _isWaitingForTwoOptions = false;
            ShowFreeTextBar(activeForModeA: false);
            return;
        }

        List<Message> activeDialogues = currentNode.GetActiveDialogues();
        int msgIndex = currentContact.CurrentMessageIndex();

        if (msgIndex < activeDialogues.Count)
        {
            _isWaitingForFreeText = false;
            _isWaitingForTwoOptions = false;
            ShowFreeTextBar(activeForModeA: false);

            Message nextMsg = activeDialogues[msgIndex];
            if (MessagesManager.IsDialogueOverlayLine(nextMsg))
            {
                _timer = 0f;
                ShowNarration(currentContact, nextMsg);
                return;
            }

            float requiredDelay = nextMsg.Sender() == Sender.NPC
                ? NpcMessageDelaySeconds
                : ScriptedPlayerLineDelaySeconds;

            _timer += Time.deltaTime;
            if (_timer >= requiredDelay)
            {
                _timer = 0f;
                SendNextMessage();
            }
            return;
        }

        // All dialogue lines in currentNode have played and finished animating.
        if (!currentNode.HasOptions())
        {
            _isWaitingForFreeText = false;
            _isWaitingForTwoOptions = false;
            ShowFreeTextBar(activeForModeA: false);
            CompleteCurrentNode(currentContact, currentNode);
            return;
        }

        PhoneResponseMode mode = currentNode.ResponseMode();
        if (mode == PhoneResponseMode.FreeText)
        {
            _isWaitingForFreeText = true;
            _isWaitingForTwoOptions = false;
            _timer = 0f;
            ShowFreeTextBar(activeForModeA: true);
        }
        else if (mode == PhoneResponseMode.TwoOptions)
        {
            _isWaitingForFreeText = false;
            _isWaitingForTwoOptions = true;
            _timer = 0f;
            ShowTwoOptionsBar(currentNode);
        }
        else
        {
            // DecisionOverlay: hand over to DecissionsUI via OptionsChanger
            _isWaitingForFreeText = false;
            _isWaitingForTwoOptions = false;
            _timer = 0f;
            ShowFreeTextBar(activeForModeA: false);
            CompleteCurrentNode(currentContact, currentNode);
        }
    }

    private void CheckNpcFastForwardInput()
    {
        if (MessagesManager.Instance == null || _narrationDialogueActive || _blockedMonologueActive)
        {
            return;
        }

        if (_isWaitingForFreeText || _isWaitingForTwoOptions || InputManager.IsTypingInInputField())
        {
            return;
        }

        MessagesUI messagesUI = MessagesManager.Instance.GetMessagesUI();
        if (messagesUI != null && messagesUI.IsAnimating)
        {
            return;
        }

        Contact currentContact = MessagesManager.Instance.GetCurrentContact();
        if (currentContact == null || !currentContact.HasNewMessages())
        {
            return;
        }

        if (Time.frameCount <= _lastAdvanceFrame + 1 || Time.unscaledTime - _lastAdvanceTime < PhoneAdvanceCooldownSeconds)
        {
            return;
        }

        if (InputManager.Instance != null && InputManager.Instance.IsDialogueAdvanceSuppressed())
        {
            return;
        }

        bool inputPressed = false;
        if (InputManager.Instance != null)
        {
            inputPressed = InputManager.Instance.RawInteractWasPressed() ||
                           InputManager.Instance.InteractWasPressed() ||
                           InputManager.Instance.ContinueDialogueWasPressed();
        }
        else if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            inputPressed = true;
        }

        if (!inputPressed)
        {
            // Also allow left-click outside the input field and SEND button to fast-forward NPC delay.
            bool leftClick = InputManager.Instance != null
                ? InputManager.Instance.LeftClickWasPressed()
                : (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);

            if (leftClick && !IsPointerOverInteractiveBar())
            {
                inputPressed = true;
            }
        }

        if (!inputPressed)
        {
            return;
        }

        if (InputManager.Instance != null && !InputManager.Instance.TryConsumeDialogueAdvance(0.08f, 1))
        {
            return;
        }

        _lastAdvanceFrame = Time.frameCount;
        _lastAdvanceTime = Time.unscaledTime;
        _timer = Mathf.Max(_timer, NpcMessageDelaySeconds);
    }

    private bool IsPointerOverInteractiveBar()
    {
        if (Mouse.current == null)
        {
            return false;
        }

        Vector2 pointerPos = Mouse.current.position.ReadValue();
        Canvas canvas = GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        if (transform is RectTransform selfRect &&
            RectTransformUtility.RectangleContainsScreenPoint(selfRect, pointerPos, cam))
        {
            return true;
        }

        return false;
    }

    private void OnInputSubmit(string _)
    {
        OnSendTriggered();
    }

    private void OnSendTriggered()
    {
        if (Time.frameCount == _lastSendFrame)
        {
            return;
        }
        _lastSendFrame = Time.frameCount;

        if (_narrationDialogueActive || _blockedMonologueActive)
        {
            return;
        }

        // If sending is not currently allowed (not the player's Mode A turn), trigger an inner monologue.
        if (!CanSendFreeTextNow())
        {
            TriggerBlockedSendMonologue();
            return;
        }

        string typedText = _inputField != null ? _inputField.text.Trim() : string.Empty;
        if (string.IsNullOrEmpty(typedText))
        {
            ShowInnerMonologueLine("I should type something before hitting send.");
            return;
        }

        Contact currentContact = MessagesManager.Instance.GetCurrentContact();
        NarrativeNode currentNode = MessagesManager.Instance.GetCurrentContactNarrativeNode();
        if (currentContact == null || currentNode == null)
        {
            TriggerBlockedSendMonologue();
            return;
        }

        Option[] options = currentNode.Options();
        if (options == null || options.Length == 0)
        {
            TriggerBlockedSendMonologue();
            return;
        }

        int routeIndex = PhoneTextClassifier.ClassifyResponse(typedText, currentNode);
        routeIndex = Mathf.Clamp(routeIndex, 0, options.Length - 1);
        Option chosenOption = options[routeIndex] ?? options[0];

        if (_inputField != null)
        {
            _inputField.SetTextWithoutNotify(string.Empty);
            UnfocusInputField();
        }

        _isWaitingForFreeText = false;
        _timer = 0f;
        _lastAdvanceFrame = Time.frameCount;
        _lastAdvanceTime = Time.unscaledTime;
        InputManager.Instance?.ConsumeDialogueAdvance(0.15f, 2);

        Message playerMessage = new Message(
            typedText,
            Sender.Player,
            PlayerNameService.GetActivePlayerName(),
            MessagesManager.Instance.GenerateNextMessageTimestamp());

        MessagesManager.Instance.SendMessageToCurrentContact(playerMessage);

        ExecuteOptionTransition(currentContact, currentNode, routeIndex, chosenOption, sendOptionMessages: false);
    }

    private bool CanSendFreeTextNow()
    {
        if (!_isWaitingForFreeText || MessagesManager.Instance == null)
        {
            return false;
        }

        MessagesUI messagesUI = MessagesManager.Instance.GetMessagesUI();
        if (messagesUI != null && messagesUI.IsAnimating)
        {
            return false;
        }

        OptionsChanger optionsChanger = MessagesManager.Instance.GetOptionsChanger();
        if (optionsChanger != null && optionsChanger.IsDecisionsPanelActive())
        {
            return false;
        }

        Contact currentContact = MessagesManager.Instance.GetCurrentContact();
        NarrativeNode currentNode = MessagesManager.Instance.GetCurrentContactNarrativeNode();
        return currentContact != null &&
               currentContact.HasNewMessages() &&
               currentNode != null &&
               currentNode.HasOptions() &&
               currentNode.ResponseMode() == PhoneResponseMode.FreeText;
    }

    private void TriggerBlockedSendMonologue()
    {
        UnfocusInputField();

        int chosenIndex = UnityEngine.Random.Range(0, BlockedSendMonologues.Length);
        if (BlockedSendMonologues.Length > 1 && chosenIndex == _lastBlockedMonologueIndex)
        {
            chosenIndex = (chosenIndex + 1) % BlockedSendMonologues.Length;
        }
        _lastBlockedMonologueIndex = chosenIndex;

        ShowInnerMonologueLine(BlockedSendMonologues[chosenIndex]);
    }

    private void ShowInnerMonologueLine(string line)
    {
        if (DialogueManager.Instance == null || string.IsNullOrEmpty(line))
        {
            return;
        }

        UnfocusInputField();
        _blockedMonologueActive = true;
        bool opened = DialogueManager.Instance.ShowInnerMonologue(line, () =>
        {
            _blockedMonologueActive = false;
            _timer = 0f;
            _lastAdvanceFrame = Time.frameCount;
            _lastAdvanceTime = Time.unscaledTime;
        });

        if (!opened)
        {
            _blockedMonologueActive = false;
        }
    }

    private void OnModeBOptionSelected(int optionIndex)
    {
        if (!_isWaitingForTwoOptions || MessagesManager.Instance == null)
        {
            return;
        }

        MessagesUI messagesUI = MessagesManager.Instance.GetMessagesUI();
        if (messagesUI != null && messagesUI.IsAnimating)
        {
            return;
        }

        Contact currentContact = MessagesManager.Instance.GetCurrentContact();
        NarrativeNode currentNode = MessagesManager.Instance.GetCurrentContactNarrativeNode();
        if (currentContact == null || currentNode == null || !currentNode.HasOptions())
        {
            return;
        }

        Option[] options = currentNode.Options();
        if (options == null || optionIndex < 0 || optionIndex >= options.Length)
        {
            return;
        }

        Option chosenOption = options[optionIndex];
        if (chosenOption == null || !chosenOption.AreConditionsMet())
        {
            return;
        }

        _isWaitingForTwoOptions = false;
        _timer = 0f;
        _lastAdvanceFrame = Time.frameCount;
        _lastAdvanceTime = Time.unscaledTime;
        InputManager.Instance?.ConsumeDialogueAdvance(0.15f, 2);
        ShowFreeTextBar(activeForModeA: false);

        ExecuteOptionTransition(currentContact, currentNode, optionIndex, chosenOption, sendOptionMessages: true);
    }

    private void ExecuteOptionTransition(
        Contact contact,
        NarrativeNode decisionNode,
        int selectedOptionIndex,
        Option selectedOption,
        bool sendOptionMessages)
    {
        if (selectedOption == null || contact == null || decisionNode == null)
        {
            return;
        }

        selectedOption.ApplyReputationImpact();
        PublishOptionEvent(selectedOption);

        if (sendOptionMessages)
        {
            if (selectedOption.messagesToSend != null && selectedOption.messagesToSend.Length > 0)
            {
                for (int i = 0; i < selectedOption.messagesToSend.Length; i++)
                {
                    if (selectedOption.messagesToSend[i] != null)
                    {
                        MessagesManager.Instance.SendMessageToCurrentContact(selectedOption.messagesToSend[i]);
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(selectedOption.text))
            {
                Message fallbackMsg = new Message(
                    selectedOption.text,
                    Sender.Player,
                    PlayerNameService.GetActivePlayerName(),
                    MessagesManager.Instance.GenerateNextMessageTimestamp());
                MessagesManager.Instance.SendMessageToCurrentContact(fallbackMsg);
            }
        }

        int decisionNodeId = decisionNode.ID();
        int targetNodeId = selectedOption.ResolveNextNode();
        if (DecisionDataManager.Instance != null)
        {
            DecisionDataManager.Instance.SetOrAddIntVariable($"Scenario_Node_{decisionNodeId}", selectedOptionIndex);
        }

        targetNodeId = MessagesManager.Instance.ResolveEntryRedirects(targetNodeId);
        if (DecisionDataManager.Instance != null)
        {
            DecisionDataManager.Instance.SetOrAddIntVariable($"Scenario_NodeNext_{decisionNodeId}", targetNodeId);
        }

        contact.SetNarrativeNode(targetNodeId);
        contact.SetCurrentMessageIndex(0);

        if (targetNodeId < 0)
        {
            contact.SetHasNewMessages(false);
            return;
        }

        NarrativeNode nextNode = MessagesManager.Instance.GetCurrentContactNarrativeNode();
        if (nextNode == null)
        {
            contact.SetHasNewMessages(false);
            return;
        }

        if (nextNode.GetActiveDialogues().Count > 0 ||
            (nextNode.HasOptions() && nextNode.ResponseMode() != PhoneResponseMode.DecisionOverlay))
        {
            contact.SetHasNewMessages(true);
            MessagesManager.Instance.GetPhoneStateMachine()?.ShowDialogueTab();
        }
        else if (nextNode.HasOptions() && nextNode.ResponseMode() == PhoneResponseMode.DecisionOverlay)
        {
            contact.SetHasNewMessages(false);
            OptionsChanger optionsChanger = MessagesManager.Instance.GetOptionsChanger();
            optionsChanger?.SetCurrentNode(nextNode);
            optionsChanger?.ActiveDecisionsPanel(true);
            MessagesManager.Instance.GetPhoneStateMachine()?.ShowDecisionsTab();
        }
        else
        {
            contact.SetHasNewMessages(false);
        }
    }

    private void ShowNarration(Contact currentContact, Message narration)
    {
        if (DialogueManager.Instance == null)
        {
            Debug.LogWarning("[DialogueButton] Cannot show narration because DialogueManager is unavailable.");
            return;
        }

        UnfocusInputField();
        _narrationDialogueActive = true;
        Action onClosed = () =>
        {
            _narrationDialogueActive = false;
            _timer = 0f;
            _lastAdvanceFrame = Time.frameCount;
            _lastAdvanceTime = Time.unscaledTime;
        };

        bool opened = MessagesManager.IsThought(narration)
            ? DialogueManager.Instance.ShowInnerMonologue(narration.Content(), onClosed)
            : DialogueManager.Instance.ShowNarration(narration.Content(), onClosed);

        if (!opened)
        {
            _narrationDialogueActive = false;
            return;
        }

        // Narration is consumed by the dialogue window, not the phone history.
        currentContact.SetCurrentMessageIndex(currentContact.CurrentMessageIndex() + 1);
    }

    private void SendNextMessage()
    {
        if (MessagesManager.Instance == null)
        {
            return;
        }

        Contact currentContact = MessagesManager.Instance.GetCurrentContact();
        if (currentContact != null && currentContact.HasNewMessages())
        {
            NarrativeNode currentNode = MessagesManager.Instance.GetCurrentContactNarrativeNode();
            if (currentNode != null)
            {
                List<Message> activeDialogues = currentNode.GetActiveDialogues();
                int msgIndex = currentContact.CurrentMessageIndex();
                if (msgIndex < activeDialogues.Count)
                {
                    Message msg = activeDialogues[msgIndex];
                    MessagesManager.Instance.SendMessageToCurrentContact(msg);
                    currentContact.SetCurrentMessageIndex(msgIndex + 1);
                }
            }
        }
    }

    private void CompleteCurrentNode(Contact currentContact, NarrativeNode currentNode)
    {
        currentContact.SetHasNewMessages(false);

        if (!currentNode.HasOptions())
        {
            currentNode.ApplyEndReputationImpact();
            PublishNodeEndEvent(currentNode);

            int nextNodeId = currentNode.IsLoop() ? currentNode.GetLoopTargetNodeId() : currentNode.ResolveNextNode();
            nextNodeId = MessagesManager.Instance.ResolveEntryRedirects(nextNodeId);

            currentContact.SetNarrativeNode(nextNodeId);
            currentContact.SetCurrentMessageIndex(0);

            if (nextNodeId < 0)
            {
                return;
            }

            NarrativeNode next = MessagesManager.Instance.GetCurrentContactNarrativeNode();
            if (next != null)
            {
                if (next.GetActiveDialogues().Count > 0 ||
                    (next.HasOptions() && next.ResponseMode() != PhoneResponseMode.DecisionOverlay))
                {
                    currentContact.SetHasNewMessages(true);
                    MessagesManager.Instance.GetPhoneStateMachine()?.ShowDialogueTab();
                }
                else if (next.HasOptions() && next.ResponseMode() == PhoneResponseMode.DecisionOverlay)
                {
                    MessagesManager.Instance.GetOptionsChanger()?.SetCurrentNode(next);
                    MessagesManager.Instance.GetOptionsChanger()?.ActiveDecisionsPanel(true);
                    MessagesManager.Instance.GetPhoneStateMachine()?.ShowDecisionsTab();
                }
            }
        }
        else if (currentNode.ResponseMode() == PhoneResponseMode.DecisionOverlay)
        {
            MessagesManager.Instance.GetOptionsChanger()?.SetCurrentNode(currentNode);
            MessagesManager.Instance.GetOptionsChanger()?.ActiveDecisionsPanel(true);
            MessagesManager.Instance.GetPhoneStateMachine()?.ShowDecisionsTab();
        }
    }

    private void ShowFreeTextBar(bool activeForModeA)
    {
        if (_freeTextContainer != null && !_freeTextContainer.activeSelf)
        {
            _freeTextContainer.SetActive(true);
        }

        if (_twoOptionsContainer != null && _twoOptionsContainer.activeSelf)
        {
            _twoOptionsContainer.SetActive(false);
        }

        if (_sendButton != null)
        {
            _sendButton.interactable = true;
        }

        if (_sendButtonImage != null)
        {
            _sendButtonImage.color = SendActiveColor;
        }

        if (_sendButtonIconImage != null)
        {
            _sendButtonIconImage.color = Color.white;
        }

        if (_inputOutline != null)
        {
            _inputOutline.effectColor = activeForModeA ? InputActiveBorderColor : InputBlockedBorderColor;
        }

        if (_placeholderText != null)
        {
            _placeholderText.text = "Type a message...";
        }
    }

    private void ShowTwoOptionsBar(NarrativeNode node)
    {
        if (_freeTextContainer != null && _freeTextContainer.activeSelf)
        {
            _freeTextContainer.SetActive(false);
        }

        if (_twoOptionsContainer != null && !_twoOptionsContainer.activeSelf)
        {
            _twoOptionsContainer.SetActive(true);
        }

        UnfocusInputField();

        Option[] options = node != null ? node.Options() : null;
        ConfigureModeBOptionButton(_modeBOption0Button, _modeBOption0Text, options != null && options.Length > 0 ? options[0] : null);
        ConfigureModeBOptionButton(_modeBOption1Button, _modeBOption1Text, options != null && options.Length > 1 ? options[1] : null);
    }

    private static void ConfigureModeBOptionButton(Button button, TextMeshProUGUI label, Option option)
    {
        if (button == null)
        {
            return;
        }

        bool hasOption = option != null;
        button.gameObject.SetActive(hasOption);
        if (!hasOption)
        {
            return;
        }

        bool unlocked = option.AreConditionsMet();
        button.interactable = unlocked;
        if (label != null)
        {
            string rawText = !unlocked && !string.IsNullOrEmpty(option.lockedText) ? option.lockedText : option.text;
            label.text = PlayerNameService.FormatText(rawText);
        }
    }

    private void UnfocusInputField()
    {
        if (_inputField != null && _inputField.isFocused)
        {
            _inputField.DeactivateInputField();
        }

        if (EventSystem.current != null &&
            _inputField != null &&
            EventSystem.current.currentSelectedGameObject == _inputField.gameObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void EnsureUI()
    {
        if (_uiBuilt)
        {
            return;
        }
        _uiBuilt = true;

        if (_legacyImage != null)
        {
            _roundedSprite = _legacyImage.sprite;
            _legacyImage.enabled = false;
            _legacyImage.raycastTarget = false;
        }

        if (_legacyButton != null)
        {
            _legacyButton.onClick.RemoveAllListeners();
            _legacyButton.interactable = false;
            _legacyButton.enabled = false;
        }

        if (_legacyTextMesh != null)
        {
            _sharedFont = _legacyTextMesh.font;
            _legacyTextMesh.gameObject.SetActive(false);
        }
        else if (_placeholderText != null)
        {
            _sharedFont = _placeholderText.font;
        }

        if (_sharedFont == null)
        {
            _sharedFont = TMP_Settings.defaultFontAsset;
        }

        if (_freeTextContainer != null && _inputField != null && _sendButton != null)
        {
            BindPrefabFreeTextBar();
        }
        else
        {
            if (transform is RectTransform rootRect)
            {
                rootRect.sizeDelta = new Vector2(438f, 62f);
            }

            BuildFreeTextBar();
        }

        if (_twoOptionsContainer == null)
        {
            BuildTwoOptionsBar();
        }
        ShowFreeTextBar(activeForModeA: false);
    }

    private void BindPrefabFreeTextBar()
    {
        if (_sendButtonImage == null)
        {
            _sendButtonImage = _sendButton.GetComponent<Image>();
        }

        if (_placeholderText == null && _inputField.placeholder != null)
        {
            _placeholderText = _inputField.placeholder as TextMeshProUGUI;
        }

        if (_inputField.textComponent != null)
        {
            _inputField.textComponent.richText = false;
            _inputField.textComponent.textWrappingMode = TextWrappingModes.Normal;
        }

        _inputField.lineType = TMP_InputField.LineType.MultiLineSubmit;
        _inputField.scrollSensitivity = 10f;
        _inputField.restoreOriginalTextOnEscape = false;
        _inputField.characterLimit = 400;
        _inputField.onSubmit.RemoveListener(OnInputSubmit);
        _inputField.onSubmit.AddListener(OnInputSubmit);

        Navigation nav = _sendButton.navigation;
        nav.mode = Navigation.Mode.None;
        _sendButton.navigation = nav;
        _sendButton.onClick.RemoveListener(OnSendTriggered);
        _sendButton.onClick.AddListener(OnSendTriggered);
    }

    private void BuildFreeTextBar()
    {
        _freeTextContainer = new GameObject("FreeTextContainer", typeof(RectTransform));
        _freeTextContainer.layer = gameObject.layer;
        _freeTextContainer.transform.SetParent(transform, false);

        RectTransform containerRect = _freeTextContainer.GetComponent<RectTransform>();
        containerRect.anchorMin = Vector2.zero;
        containerRect.anchorMax = Vector2.one;
        containerRect.offsetMin = Vector2.zero;
        containerRect.offsetMax = Vector2.zero;

        // Fixed-size multi-line scrollable input box (224x58) on the left
        GameObject inputGO = new GameObject(
            "MessageInputField",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(UnityEngine.UI.Outline),
            typeof(TMP_InputField));
        inputGO.layer = gameObject.layer;
        inputGO.transform.SetParent(_freeTextContainer.transform, false);

        RectTransform inputRect = inputGO.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0f, 0.5f);
        inputRect.anchorMax = new Vector2(0f, 0.5f);
        inputRect.pivot = new Vector2(0f, 0.5f);
        inputRect.anchoredPosition = Vector2.zero;
        inputRect.sizeDelta = new Vector2(224f, 58f);

        Image inputBg = inputGO.GetComponent<Image>();
        inputBg.sprite = _roundedSprite;
        inputBg.type = _roundedSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        inputBg.color = InputSurfaceColor;

        _inputOutline = inputGO.GetComponent<UnityEngine.UI.Outline>();
        _inputOutline.effectColor = InputBlockedBorderColor;
        _inputOutline.effectDistance = new Vector2(1.5f, -1.5f);

        // Text Area with RectMask2D so long messages wrap and scroll inside the fixed dimensions
        GameObject textAreaGO = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        textAreaGO.layer = gameObject.layer;
        textAreaGO.transform.SetParent(inputGO.transform, false);

        RectTransform textAreaRect = textAreaGO.GetComponent<RectTransform>();
        textAreaRect.anchorMin = Vector2.zero;
        textAreaRect.anchorMax = Vector2.one;
        textAreaRect.offsetMin = new Vector2(10f, 6f);
        textAreaRect.offsetMax = new Vector2(-16f, -6f);

        RectMask2D mask = textAreaGO.GetComponent<RectMask2D>();
        mask.padding = Vector4.zero;

        _placeholderText = CreateTmpText("Placeholder", textAreaGO.transform, "Type a message...", 15f, FontStyles.Italic, InputPlaceholderColor);
        StretchRect(_placeholderText.rectTransform);
        _placeholderText.alignment = TextAlignmentOptions.TopLeft;
        _placeholderText.textWrappingMode = TextWrappingModes.Normal;
        _placeholderText.raycastTarget = false;

        TextMeshProUGUI inputText = CreateTmpText("Text", textAreaGO.transform, string.Empty, 15.5f, FontStyles.Normal, InputTextColor);
        StretchRect(inputText.rectTransform);
        inputText.alignment = TextAlignmentOptions.TopLeft;
        inputText.richText = false;
        inputText.textWrappingMode = TextWrappingModes.Normal;
        inputText.extraPadding = true;

        Scrollbar scrollbar = CreateVerticalScrollbar(inputGO.transform);

        _inputField = inputGO.GetComponent<TMP_InputField>();
        _inputField.targetGraphic = inputBg;
        _inputField.textViewport = textAreaRect;
        _inputField.textComponent = inputText;
        _inputField.placeholder = _placeholderText;
        _inputField.verticalScrollbar = scrollbar;
        _inputField.fontAsset = _sharedFont;
        _inputField.lineType = TMP_InputField.LineType.MultiLineSubmit;
        _inputField.scrollSensitivity = 10f;
        _inputField.restoreOriginalTextOnEscape = false;
        _inputField.characterLimit = 400;
        _inputField.customCaretColor = true;
        _inputField.caretColor = InputActiveBorderColor;
        _inputField.caretWidth = 2;
        _inputField.selectionColor = new Color(0.20f, 0.72f, 0.44f, 0.25f);
        _inputField.onSubmit.AddListener(OnInputSubmit);

        // SEND button (80x58) on the right
        GameObject sendGO = new GameObject(
            "SendButton",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        sendGO.layer = gameObject.layer;
        sendGO.transform.SetParent(_freeTextContainer.transform, false);

        RectTransform sendRect = sendGO.GetComponent<RectTransform>();
        sendRect.anchorMin = new Vector2(1f, 0.5f);
        sendRect.anchorMax = new Vector2(1f, 0.5f);
        sendRect.pivot = new Vector2(1f, 0.5f);
        sendRect.anchoredPosition = Vector2.zero;
        sendRect.sizeDelta = new Vector2(80f, 58f);

        _sendButtonImage = sendGO.GetComponent<Image>();
        _sendButtonImage.sprite = _roundedSprite;
        _sendButtonImage.type = _roundedSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        _sendButtonImage.color = SendActiveColor;

        _sendButton = sendGO.GetComponent<Button>();
        _sendButton.targetGraphic = _sendButtonImage;
        Navigation nav = _sendButton.navigation;
        nav.mode = Navigation.Mode.None;
        _sendButton.navigation = nav;
        _sendButton.onClick.AddListener(OnSendTriggered);

        TextMeshProUGUI sendLabel = CreateTmpText("Text (TMP)", sendGO.transform, "SEND", 17f, FontStyles.Bold, Color.white);
        StretchRect(sendLabel.rectTransform);
        sendLabel.alignment = TextAlignmentOptions.Center;
        sendLabel.raycastTarget = false;
    }

    private void BuildTwoOptionsBar()
    {
        _twoOptionsContainer = new GameObject("TwoOptionsContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        _twoOptionsContainer.layer = gameObject.layer;
        _twoOptionsContainer.transform.SetParent(transform, false);

        float twoOptionsWidth = _freeTextContainer != null ? 318f : 438f;
        RectTransform containerRect = _twoOptionsContainer.GetComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0f, 0.5f);
        containerRect.anchorMax = new Vector2(0f, 0.5f);
        containerRect.pivot = new Vector2(0f, 0.5f);
        containerRect.anchoredPosition = Vector2.zero;
        containerRect.sizeDelta = new Vector2(twoOptionsWidth, 62f);

        HorizontalLayoutGroup layout = _twoOptionsContainer.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        (_modeBOption0Button, _modeBOption0Text) = CreateOptionButton("OptionButton0", _twoOptionsContainer.transform, 0);
        (_modeBOption1Button, _modeBOption1Text) = CreateOptionButton("OptionButton1", _twoOptionsContainer.transform, 1);

        _twoOptionsContainer.SetActive(false);
    }

    private (Button, TextMeshProUGUI) CreateOptionButton(string name, Transform parent, int optionIndex)
    {
        GameObject btnGO = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btnGO.layer = parent.gameObject.layer;
        btnGO.transform.SetParent(parent, false);

        Image img = btnGO.GetComponent<Image>();
        img.sprite = _roundedSprite;
        img.type = _roundedSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        img.color = OptionButtonColor;

        Button btn = btnGO.GetComponent<Button>();
        btn.targetGraphic = img;
        Navigation nav = btn.navigation;
        nav.mode = Navigation.Mode.None;
        btn.navigation = nav;
        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.90f, 0.96f, 1f, 1f);
        colors.pressedColor = new Color(0.75f, 0.85f, 0.95f, 1f);
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
        btn.colors = colors;
        btn.onClick.AddListener(() => OnModeBOptionSelected(optionIndex));

        TextMeshProUGUI label = CreateTmpText("Text (TMP)", btnGO.transform, string.Empty, 13.5f, FontStyles.Bold, Color.white);
        StretchRect(label.rectTransform);
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.enableAutoSizing = true;
        label.fontSizeMin = 9.5f;
        label.fontSizeMax = 13.5f;
        label.margin = new Vector4(6f, 4f, 6f, 4f);
        label.raycastTarget = false;

        return (btn, label);
    }

    private Scrollbar CreateVerticalScrollbar(Transform inputParent)
    {
        GameObject scrollbarGO = new GameObject("Scrollbar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Scrollbar));
        scrollbarGO.layer = inputParent.gameObject.layer;
        scrollbarGO.transform.SetParent(inputParent, false);

        RectTransform scrollbarRect = scrollbarGO.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 0.5f);
        scrollbarRect.anchoredPosition = new Vector2(-3f, 0f);
        scrollbarRect.sizeDelta = new Vector2(6f, -8f);

        Image trackImg = scrollbarGO.GetComponent<Image>();
        trackImg.color = new Color(0.78f, 0.82f, 0.86f, 0.35f);

        GameObject slidingArea = new GameObject("Sliding Area", typeof(RectTransform));
        slidingArea.layer = inputParent.gameObject.layer;
        slidingArea.transform.SetParent(scrollbarGO.transform, false);
        StretchRect(slidingArea.GetComponent<RectTransform>());

        GameObject handleGO = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        handleGO.layer = inputParent.gameObject.layer;
        handleGO.transform.SetParent(slidingArea.transform, false);
        RectTransform handleRect = handleGO.GetComponent<RectTransform>();
        StretchRect(handleRect);

        Image handleImg = handleGO.GetComponent<Image>();
        handleImg.sprite = _roundedSprite;
        handleImg.type = _roundedSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        handleImg.color = new Color(0.45f, 0.50f, 0.56f, 0.75f);

        Scrollbar scrollbar = scrollbarGO.GetComponent<Scrollbar>();
        scrollbar.targetGraphic = handleImg;
        scrollbar.handleRect = handleRect;
        scrollbar.direction = Scrollbar.Direction.TopToBottom;
        Navigation nav = scrollbar.navigation;
        nav.mode = Navigation.Mode.None;
        scrollbar.navigation = nav;

        return scrollbar;
    }

    private TextMeshProUGUI CreateTmpText(string name, Transform parent, string text, float fontSize, FontStyles style, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        if (_sharedFont != null)
        {
            tmp.font = _sharedFont;
        }
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        return tmp;
    }

    private static void StretchRect(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void PublishOptionEvent(Option option)
    {
        if (option == null || string.IsNullOrEmpty(option.eventToPublish) || EventBus.Instance == null)
        {
            return;
        }

        EventBus.Instance.Publish(option.eventToPublish);

        if (option.eventDataType != EventDataType.NONE && !string.IsNullOrEmpty(option.eventDataRawValue))
        {
            object val = option.EventDataValue();
            if (val == null) return;

            switch (option.eventDataType)
            {
                case EventDataType.INT:
                    EventBus.Instance.Publish<int>(option.eventToPublish, (int)val);
                    break;
                case EventDataType.FLOAT:
                    EventBus.Instance.Publish<float>(option.eventToPublish, (float)val);
                    break;
                case EventDataType.BOOL:
                    EventBus.Instance.Publish<bool>(option.eventToPublish, (bool)val);
                    break;
                case EventDataType.STRING:
                    EventBus.Instance.Publish<string>(option.eventToPublish, (string)val);
                    break;
            }
        }
    }

    private static void PublishNodeEndEvent(NarrativeNode node)
    {
        if (node == null || string.IsNullOrEmpty(node.EventOnEnd()) || EventBus.Instance == null)
        {
            return;
        }

        EventBus.Instance.Publish(node.EventOnEnd());

        if (node.EndEventDataType() != EventDataType.NONE)
        {
            object val = node.EventDataValue();
            if (val == null) return;

            switch (node.EndEventDataType())
            {
                case EventDataType.INT:
                    EventBus.Instance.Publish<int>(node.EventOnEnd(), (int)val);
                    break;
                case EventDataType.FLOAT:
                    EventBus.Instance.Publish<float>(node.EventOnEnd(), (float)val);
                    break;
                case EventDataType.BOOL:
                    EventBus.Instance.Publish<bool>(node.EventOnEnd(), (bool)val);
                    break;
                case EventDataType.STRING:
                    EventBus.Instance.Publish<string>(node.EventOnEnd(), (string)val);
                    break;
            }
        }
    }
}
