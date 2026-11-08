using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum UIState
{
    DEFAULT,
    PHONE,
    MAP,
    MENU,
    CONVERSATION,
    MAIN_MENU,
    NONE
}

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    // High-visibility warning red used by every unavailable interaction
    public static readonly Color UnavailableInteractionColor = new Color(1f, 0.22f, 0.3f, 1f);

    [Header("UI Elements")]
    [SerializeField] private GameObject _pauseMenuPanel;
    [SerializeField] private GameObject _phone;
    [SerializeField] private CanvasGroup _interactionCanvasGroup;
    [SerializeField] private TMP_Text _interactionText;
    [SerializeField] private GameObject _map;
    [SerializeField] private GameObject _mainMenuUI;
    [SerializeField] private Sprite _appStoreIcon;
    [SerializeField] private Sprite _connectAppIcon;

    [Header("Messages Animations")]
    [SerializeField] private float _appearTime = 0.3f;
    [SerializeField] private float _disappearTime = 0.5f;

    [Header("Blur Options")]
    [SerializeField] private Volume _blurVolume;
    [SerializeField] private float _fadeDuration;

    private InputAction _menuAction;
    private Tween _fadeTween;
    private Tween _blurTween;
    private Tween _delayTween;

    private GameObject _simpleChoicePanel;
    private TMP_Text _simpleChoicePrompt;
    private Button[] _simpleChoiceButtons;
    private Action<int> _simpleChoiceCallback;
    private bool _simpleChoicesReturnToPhone;

    private CanvasGroup _phoneCanvasGroup;
    private Button _settingsAppButton;
    private Button _appStoreButton;
    private Button _connectAppButton;
    private bool _connectDownloadAvailable;
    private bool _connectInstalled;
    private bool _connectProfileCreated;

    private Transform _hudRoot;
    private GameObject _dayTrackerRoot;
    private TextMeshProUGUI _dayTrackerText;
    private int _displayedDay = -1;

    private UIState _currentState = UIState.NONE;

    public bool IsConnectDownloadAvailable => _connectDownloadAvailable;
    public bool IsConnectInstalled => _connectInstalled;
    public bool IsConnectProfileCreated => _connectProfileCreated;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            ResolveMainMenuReference();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        ResolveMainMenuReference();
        ConfigurePhoneLayout();
        ConfigureSimpleChoices();
        ConfigureDayTracker();
        ConfigureMapButtons();
        DebugCampaignPanelUI.EnsureAttached(this);

        if (_pauseMenuPanel != null)
        {
            _pauseMenuPanel.SetActive(false);
        }

        if (_interactionCanvasGroup != null)
        {
            _interactionCanvasGroup.alpha = 0f;
        }

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe("OpenMap", ShowMap);
            EventBus.Instance.Subscribe("CloseMap", HideMap);
            EventBus.Instance.Subscribe("D1_StudentConnectConversationComplete", EnableConnectDownload);
            EventBus.Instance.Subscribe("D1_Profile_End", CompleteConnectProfile);
        }

        SyncInitialSceneState();
        SyncHudVisibility();
        RefreshDayTracker();
    }

    private void OnDisable()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe("OpenMap", ShowMap);
            EventBus.Instance.Unsubscribe("CloseMap", HideMap);
            EventBus.Instance.Unsubscribe("D1_StudentConnectConversationComplete", EnableConnectDownload);
            EventBus.Instance.Unsubscribe("D1_Profile_End", CompleteConnectProfile);
        }
    }

    private void Update()
    {
        RefreshDayTracker();

        if ((DebugCampaignPanelUI.Instance != null && DebugCampaignPanelUI.Instance.IsOpen) ||
            (TransitionManager.Instance != null && TransitionManager.Instance.IsTransitioning))
        {
            return;
        }

        if (InputManager.Instance != null && InputManager.Instance.MenuWasTapped())
        {
            TogglePause();
        }
    }

    private void ResolveMainMenuReference()
    {
        if (_mainMenuUI == null)
        {
            Transform foundMainMenu = FindChildByName(transform, "MainMenuUI");
            if (foundMainMenu != null)
            {
                _mainMenuUI = foundMainMenu.gameObject;
            }
        }

        if (_hudRoot == null)
        {
            _hudRoot = FindChildByName(transform, "HUD");
        }
    }

    private void SyncInitialSceneState()
    {
        if (_currentState != UIState.NONE) return;

        string activeScene = SceneManager.GetActiveScene().name;
        bool isMainMenu = string.Equals(activeScene, "MainMenu", StringComparison.OrdinalIgnoreCase);

        if (_mainMenuUI != null)
        {
            _mainMenuUI.SetActive(isMainMenu);
        }

        ChangeState(isMainMenu ? (int)UIState.MAIN_MENU : (int)UIState.DEFAULT);
    }

    private void SyncHudVisibility()
    {
        if (_hudRoot == null)
        {
            _hudRoot = FindChildByName(transform, "HUD");
        }

        if (_hudRoot != null)
        {
            bool showHud = _currentState != UIState.MAIN_MENU;
            if (_hudRoot.gameObject.activeSelf != showHud)
            {
                _hudRoot.gameObject.SetActive(showHud);
            }
        }
    }

    public UIState GetCurrentState() => _currentState;
    public GameObject GetPauseMenuPanel() => _pauseMenuPanel;
    public GameObject GetMainMenuUI() => _mainMenuUI;

    public void TogglePause()
    {
        if (TransitionManager.Instance != null && TransitionManager.Instance.IsTransitioning)
        {
            return;
        }

        if (_currentState == UIState.DEFAULT) ChangeState(3);
        else if (_currentState == UIState.MENU) ChangeState(0);
    }

    public void ShowMessage(string message, Color colorText)
    {
        if (_interactionCanvasGroup == null || _interactionText == null) return;

        _fadeTween?.Kill();
        _delayTween?.Kill();
        _interactionCanvasGroup.DOKill();

        _interactionText.text = message;
        _interactionText.color = colorText;

        _fadeTween = _interactionCanvasGroup.DOFade(1f, _appearTime);
    }

    public void HideMessage()
    {
        if (_interactionCanvasGroup == null) return;

        _fadeTween?.Kill();
        _delayTween?.Kill();
        _interactionCanvasGroup.DOKill();

        _fadeTween = _interactionCanvasGroup.DOFade(0f, _disappearTime);
    }

    public void ShowTemporaryMessage(string message, Color textColor, float duration)
    {
        if (_interactionCanvasGroup == null || _interactionText == null) return;

        ShowMessage(message, textColor);

        _delayTween = DOVirtual.DelayedCall(duration, () =>
        {
            HideMessage();
        });
    }

    public void LockMouse()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void UnlockMouse()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ShowMap()
    {
        ChangeState(2);
    }

    public void HideMap()
    {
        ChangeState(0);
        ReturnPlayerToIdle();
    }

    private void ConfigureMapButtons()
    {
        if (_map == null) return;

        foreach (Button button in _map.GetComponentsInChildren<Button>(true))
        {
            if (button.gameObject.name != "Home" && button.gameObject.name != "School")
            {
                continue;
            }

            string destination = button.gameObject.name;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => HandleMapDestination(destination));
        }
    }

    private void HandleMapDestination(string destination)
    {
        string currentScene = SceneManager.GetActiveScene().name;

        if (string.Equals(currentScene, destination, StringComparison.OrdinalIgnoreCase))
        {
            string monologue = destination == "Home"
                ? "I need to go to school or I will be late."
                : "School is finished. I ain't going back.";

            if (DialogueManager.Instance == null ||
                !DialogueManager.Instance.ShowInnerMonologue(monologue))
            {
                ShowTemporaryMessage(monologue, Color.white, 5f);
            }

            return;
        }

        HideMap();

        string travelEvent = destination == "School"
            ? "Day1_GoToSchool"
            : "Day1_ComebackHome";

        EventBus.Instance?.Publish(travelEvent);
        GameManager.Instance.ChangeScene(destination);
    }

    private static void ReturnPlayerToIdle()
    {
        PlayerStateMachine playerState = FindAnyObjectByType<PlayerStateMachine>();
        if (playerState != null)
        {
            playerState.ChangeState(PlayerState.Idle);
        }
    }

    /// <summary>
    /// Displays a lightweight two-option modal choice using a private clone of DecissionsUI.
    /// </summary>
    public bool ShowSimpleChoices(
        string prompt,
        string firstChoice,
        string secondChoice,
        Action<int> onChoiceSelected)
    {
        if (_simpleChoicePanel == null)
        {
            ConfigureSimpleChoices();
        }

        if (_simpleChoicePanel == null || _simpleChoiceButtons == null || _simpleChoiceButtons.Length < 2)
        {
            Debug.LogWarning("[UIManager] The simple-choice UI could not be created.");
            return false;
        }

        _simpleChoicesReturnToPhone = false;
        _simpleChoiceCallback = onChoiceSelected;

        if (_simpleChoicePrompt != null)
        {
            _simpleChoicePrompt.gameObject.SetActive(true);
            _simpleChoicePrompt.enabled = true;
            _simpleChoicePrompt.text = prompt;
        }

        SetSimpleChoiceButton(0, firstChoice);
        SetSimpleChoiceButton(1, secondChoice);

        _simpleChoicePanel.SetActive(true);
        _simpleChoicePanel.transform.SetAsLastSibling();
        ChangeState((int)UIState.NONE);

        if (InputManager.Instance != null)
        {
            InputManager.Instance.DisablePhone();
            InputManager.Instance.DisableMenuAction();
        }
        UnlockMouse();
        return true;
    }

    public bool ShowPhoneChoices(
        string prompt,
        string firstChoice,
        string secondChoice,
        Action<int> onChoiceSelected)
    {
        if (_currentState != UIState.PHONE) return false;

        if (_simpleChoicePanel == null)
        {
            ConfigureSimpleChoices();
        }

        if (_simpleChoicePanel == null || _simpleChoiceButtons == null || _simpleChoiceButtons.Length < 2)
        {
            Debug.LogWarning("[UIManager] The phone choice UI could not be created.");
            return false;
        }

        _simpleChoicesReturnToPhone = true;
        _simpleChoiceCallback = onChoiceSelected;

        if (_simpleChoicePrompt != null)
        {
            _simpleChoicePrompt.gameObject.SetActive(true);
            _simpleChoicePrompt.enabled = true;
            _simpleChoicePrompt.text = prompt;
        }

        SetSimpleChoiceButton(0, firstChoice);
        SetSimpleChoiceButton(1, secondChoice);
        _simpleChoicePanel.SetActive(true);
        _simpleChoicePanel.transform.SetAsLastSibling();
        SetPhoneAppInteraction(false);
        return true;
    }

    public void OpenAppStoreMonologue()
    {
        if (_currentState != UIState.PHONE || DialogueManager.Instance == null) return;

        if (_connectDownloadAvailable && !_connectInstalled)
        {
            bool choicesOpened = ShowPhoneChoices(
                "<b>App Store</b>\nConnect is available to download.",
                "Close App Store",
                "Download Connect",
                HandleAppStoreChoice);

            if (choicesOpened) return;
        }

        SetPhoneAppInteraction(false);
        bool opened = DialogueManager.Instance.ShowInnerMonologue(
            "I don't want to install anything.",
            CloseAppStoreMonologue);

        if (!opened)
        {
            SetPhoneAppInteraction(true);
        }
    }

    private void CloseAppStoreMonologue()
    {
        SetPhoneAppInteraction(true);
    }

    private void EnableConnectDownload()
    {
        _connectDownloadAvailable = true;
        Debug.Log("[UIManager] Connect is now available in the App Store.");
    }

    private void CompleteConnectProfile()
    {
        _connectProfileCreated = true;
        Debug.Log("[UIManager] Luna's Connect profile setup is complete.");
    }

    public void ResetConnectState()
    {
        RestoreConnectState(false, false, false);
    }

    public void RestoreConnectState(bool downloadAvailable, bool installed, bool profileCreated)
    {
        _connectDownloadAvailable = downloadAvailable;
        _connectInstalled = installed;
        _connectProfileCreated = profileCreated;

        if (_connectAppButton != null)
        {
            _connectAppButton.gameObject.SetActive(_connectInstalled);
        }

        UpdatePhoneAppLayout();
    }

    public void ApplyCompletedConnectSetupForTestSkip()
    {
        RestoreConnectState(true, true, true);
        Debug.Log("[UIManager] Restored completed Connect setup for debug day start.");
    }

    private void HandleAppStoreChoice(int choiceIndex)
    {
        const int DownloadConnectChoice = 1;
        if (choiceIndex != DownloadConnectChoice || _connectInstalled) return;

        _connectInstalled = true;
        if (_connectAppButton != null)
        {
            _connectAppButton.gameObject.SetActive(true);
        }

        UpdatePhoneAppLayout();
        EventBus.Instance?.Publish("D1_ConnectAvailable");
        EventBus.Instance?.Publish("D1_ConnectDownloaded");
        ShowTemporaryMessage("Connect installed.", Color.white, 2.5f);
        Debug.Log("[UIManager] Connect was downloaded and added to the phone.");
    }

    public void ChangeState(int newState)
    {
        ResolveMainMenuReference();

        UIState targetState = (UIState)newState;
        if (targetState == _currentState)
        {
            if (_mainMenuUI != null)
            {
                _mainMenuUI.SetActive(targetState == UIState.MAIN_MENU);
            }
            SyncHudVisibility();
            return;
        }

        Debug.Log("[UIManager] Previous:" + _currentState);
        ExitCurrentState();
        _currentState = targetState;
        Debug.Log("[UIManager] Current:" + _currentState);
        EnterNewState();
        SyncHudVisibility();
        RefreshDayTracker();
        HideMessage();
    }

    private void ExitCurrentState()
    {
        switch (_currentState)
        {
            case UIState.DEFAULT:
                {
                    if (InputManager.Instance != null)
                    {
                        InputManager.Instance.DisableMovementMap();
                        InputManager.Instance.DisableInteract();
                    }
                    UnlockMouse();
                }
                break;
            case UIState.PHONE:
                {
                    if (_phone != null) _phone.SetActive(false);
                    EnableBlur(false);
                    if (EventBus.Instance != null) EventBus.Instance.Publish("Phone_Closed", 0);
                }
                break;
            case UIState.MAP:
                {
                    if (_map != null) _map.SetActive(false);
                }
                break;
            case UIState.CONVERSATION:
                {
                    if (EventBus.Instance != null) EventBus.Instance.Publish("Conversation_Ended");
                }
                break;
            case UIState.MENU:
                {
                    Time.timeScale = 1f;
                    if (_pauseMenuPanel != null) _pauseMenuPanel.SetActive(false);
                    if (EventBus.Instance != null) EventBus.Instance.Publish("PauseMenu_Closed");
                }
                break;
            case UIState.MAIN_MENU:
                {
                    if (_mainMenuUI != null) _mainMenuUI.SetActive(false);
                }
                break;
        }
    }

    private void EnterNewState()
    {
        if (_currentState != UIState.MAIN_MENU && _mainMenuUI != null && _mainMenuUI.activeSelf)
        {
            _mainMenuUI.SetActive(false);
        }

        switch (_currentState)
        {
            case UIState.DEFAULT:
                {
                    if (InputManager.Instance != null)
                    {
                        InputManager.Instance.EnableMovementMap();
                        InputManager.Instance.EnablePhone();
                        InputManager.Instance.EnableMenuAction();
                        InputManager.Instance.EnableInteract();
                    }
                    LockMouse();
                }
                break;
            case UIState.PHONE:
                {
                    if (EventBus.Instance != null) EventBus.Instance.Publish("Phone_Opened");
                    if (_phone != null) _phone.SetActive(true);
                    SetPhoneAppInteraction(true);
                    EnableBlur(true);
                    if (InputManager.Instance != null)
                    {
                        InputManager.Instance.DisableInteract();
                        InputManager.Instance.DisableMenuAction();
                        InputManager.Instance.EnablePhone();
                    }
                    UnlockMouse();
                }
                break;
            case UIState.MAP:
                {
                    if (_map != null) _map.SetActive(true);
                    if (InputManager.Instance != null)
                    {
                        InputManager.Instance.DisableInteract();
                        InputManager.Instance.DisablePhone();
                    }
                    UnlockMouse();
                }
                break;
            case UIState.CONVERSATION:
                {
                    Debug.Log("[UIManager] Conversation");
                    if (EventBus.Instance != null) EventBus.Instance.Publish("Conversation_Started");
                    if (InputManager.Instance != null)
                    {
                        InputManager.Instance.DisableInteract();
                        InputManager.Instance.DisablePhone();
                    }
                    UnlockMouse();
                }
                break;
            case UIState.MENU:
                {
                    if (_pauseMenuPanel != null) _pauseMenuPanel.SetActive(true);
                    if (InputManager.Instance != null)
                    {
                        InputManager.Instance.DisableInteract();
                        InputManager.Instance.DisablePhone();
                    }
                    UnlockMouse();
                }
                break;
            case UIState.MAIN_MENU:
                {
                    if (_mainMenuUI != null) _mainMenuUI.SetActive(true);
                    if (InputManager.Instance != null)
                    {
                        InputManager.Instance.DisableMovementMap();
                        InputManager.Instance.DisableInteract();
                        InputManager.Instance.DisablePhone();
                        InputManager.Instance.DisableMenuAction();
                    }
                    UnlockMouse();
                }
                break;
        }
    }

    private void EnableBlur(bool enable)
    {
        if (_blurVolume == null)
        {
            Debug.LogWarning("UIManager: _blurVolume not assigned");
            return;
        }

        _blurTween?.Kill();
        float targetWeight = enable ? 1f : 0f;

        _blurTween = DOTween.To(() => _blurVolume.weight, x => _blurVolume.weight = x, targetWeight, _fadeDuration).SetUpdate(true);
    }

    private void ConfigurePhoneLayout()
    {
        if (_phone == null) return;

        _phoneCanvasGroup = _phone.GetComponent<CanvasGroup>();
        if (_phoneCanvasGroup == null)
        {
            _phoneCanvasGroup = _phone.AddComponent<CanvasGroup>();
        }

        Transform buttonPanel = _phone.transform.Find("StartScreen/ButtonPanel");
        if (buttonPanel == null)
        {
            buttonPanel = FindChildByName(_phone.transform, "ButtonPanel");
        }
        if (buttonPanel == null) return;

        _settingsAppButton = buttonPanel.Find("Settings")?.GetComponent<Button>();

        Button messagesButton = buttonPanel.Find("Messages")?.GetComponent<Button>();
        if (messagesButton != null)
        {
            messagesButton.onClick = new Button.ButtonClickedEvent();
            messagesButton.onClick.AddListener(OpenPhoneMessages);
        }

        ConfigureConnectAppButton(buttonPanel);
        ConfigureAppStoreButton(buttonPanel);
        UpdatePhoneAppLayout();
    }

    private void ConfigureAppStoreButton(Transform buttonPanel)
    {
        Button appStoreButton = buttonPanel.Find("AppStore")?.GetComponent<Button>();
        if (appStoreButton == null)
        {
            Button template = _connectAppButton != null ? _connectAppButton : _settingsAppButton;
            if (template == null)
            {
                Debug.LogWarning("[UIManager] Phone app button template was not found; App Store button cannot be created.");
                return;
            }

            appStoreButton = Instantiate(template, buttonPanel);
            appStoreButton.name = "AppStore";
        }

        // Ensure cloned button never inherits Settings' UIEventPublisher
        UIEventPublisher[] eventPublishers = appStoreButton.GetComponents<UIEventPublisher>();
        for (int i = 0; i < eventPublishers.Length; i++)
        {
            DestroyImmediate(eventPublishers[i]);
        }

        appStoreButton.gameObject.SetActive(true);
        appStoreButton.onClick = new Button.ButtonClickedEvent();
        appStoreButton.onClick.AddListener(OpenAppStoreMonologue);
        _appStoreButton = appStoreButton;

        Sprite icon = _appStoreIcon != null ? _appStoreIcon : MessagesUI.LoadSprite("_PhoneUIImages/AppStore.png");
        Image appStoreImage = appStoreButton.GetComponent<Image>();
        if (appStoreImage != null && icon != null)
        {
            appStoreImage.sprite = icon;
            appStoreImage.preserveAspect = true;
            appStoreButton.targetGraphic = appStoreImage;
        }
    }

    private void ConfigureConnectAppButton(Transform buttonPanel)
    {
        _connectAppButton = buttonPanel.Find("Connect")?.GetComponent<Button>();
        if (_connectAppButton == null)
        {
            if (_settingsAppButton == null) return;
            _connectAppButton = Instantiate(_settingsAppButton, buttonPanel);
            _connectAppButton.name = "Connect";
        }

        UIEventPublisher[] eventPublishers = _connectAppButton.GetComponents<UIEventPublisher>();
        for (int i = 0; i < eventPublishers.Length; i++)
        {
            DestroyImmediate(eventPublishers[i]);
        }

        _connectAppButton.onClick = new Button.ButtonClickedEvent();
        _connectAppButton.onClick.AddListener(OpenConnectApp);

        Image connectImage = _connectAppButton.GetComponent<Image>();
        if (connectImage != null)
        {
            Sprite icon = _connectAppIcon != null
                ? _connectAppIcon
                : (connectImage.sprite != null ? connectImage.sprite : MessagesUI.LoadSprite("_PhoneUIImages/ConenctSocialMedia.png"));

            if (icon != null)
            {
                connectImage.sprite = icon;
                connectImage.preserveAspect = true;
                _connectAppButton.targetGraphic = connectImage;
            }
        }

        _connectAppButton.gameObject.SetActive(_connectInstalled);
    }

    private void UpdatePhoneAppLayout()
    {
        // Preserve Settings (-150, 340) and Gallery (-50, 340) untouched on the top row.
        // Place AppStore at (50, 340) and Connect at (150, 340) with matching 100x100 dimensions.
        if (_appStoreButton != null && _appStoreButton.transform is RectTransform appStoreRect)
        {
            appStoreRect.anchorMin = new Vector2(0.5f, 0.5f);
            appStoreRect.anchorMax = new Vector2(0.5f, 0.5f);
            appStoreRect.pivot = new Vector2(0.5f, 0.5f);
            appStoreRect.sizeDelta = new Vector2(100f, 100f);
            appStoreRect.anchoredPosition = new Vector2(50f, 340f);
            appStoreRect.localScale = Vector3.one;
        }

        if (_connectAppButton != null && _connectAppButton.transform is RectTransform connectRect)
        {
            connectRect.anchorMin = new Vector2(0.5f, 0.5f);
            connectRect.anchorMax = new Vector2(0.5f, 0.5f);
            connectRect.pivot = new Vector2(0.5f, 0.5f);
            connectRect.sizeDelta = new Vector2(100f, 100f);
            connectRect.anchoredPosition = new Vector2(150f, 340f);
            connectRect.localScale = Vector3.one;
        }
    }

    private void OpenPhoneMessages()
    {
        if (MessagesManager.Instance == null) return;

        MessagesManager.Instance.SetMessagingChannel(MessagingChannel.Phone);
        PhoneStateMachine phoneState = MessagesManager.Instance.GetPhoneStateMachine();
        phoneState?.ChangeState((int)PhoneState.CONTACTS);
        MessagesManager.Instance.GetMessagesUI()?.RefreshContacts();
    }

    private void OpenConnectApp()
    {
        if (!_connectInstalled || MessagesManager.Instance == null) return;

        MessagesManager messages = MessagesManager.Instance;
        messages.SetMessagingChannel(MessagingChannel.Connect);

        PhoneStateMachine phoneState = messages.GetPhoneStateMachine();
        MessagesUI messagesUI = messages.GetMessagesUI();
        if (phoneState == null || messagesUI == null) return;

        if (_connectProfileCreated)
        {
            phoneState.ChangeState((int)PhoneState.CONTACTS);
            messagesUI.RefreshContacts();
            return;
        }

        Contact connectContact = messages.GetContactByName("Connect");
        if (connectContact == null || connectContact.NarrativeNode() < 0 || connectContact.NarrativeNode() > 3)
        {
            messages.AdvanceContactNarrativeNodeByEvent("Connect", 0);
        }

        messages.SetCurrentContact("Connect");
        phoneState.ChangeState((int)PhoneState.MESSAGING);
        messagesUI.SetContactName("Connect");
        messagesUI.CreateMessages();

        NarrativeNode profileNode = messages.GetCurrentContactNarrativeNode();
        if (profileNode != null && profileNode.HasOptions())
        {
            messages.GetOptionsChanger()?.SetCurrentNode(profileNode);
            messages.GetOptionsChanger()?.ActiveDecisionsPanel(true);
            phoneState.ShowDecisionsTab();
        }
        else if (connectContact != null && connectContact.HasNewMessages())
        {
            phoneState.ShowDialogueTab();
        }
    }

    private void SetPhoneAppInteraction(bool interactable)
    {
        if (_phoneCanvasGroup != null)
        {
            _phoneCanvasGroup.interactable = interactable;
            _phoneCanvasGroup.blocksRaycasts = true;
        }

        if (InputManager.Instance == null) return;

        if (interactable && _currentState == UIState.PHONE)
        {
            InputManager.Instance.EnablePhone();
        }
        else
        {
            InputManager.Instance.DisablePhone();
        }
    }

    private void ConfigureSimpleChoices()
    {
        if (_simpleChoicePanel != null) return;

        Transform decisions = FindChildByName(transform, "DecissionsUI");
        if (decisions == null)
        {
            Debug.LogWarning("[UIManager] DecissionsUI was not found; simple choices are unavailable.");
            return;
        }

        Transform parentTransform = decisions.parent != null ? decisions.parent : transform;
        _simpleChoicePanel = Instantiate(decisions.gameObject, parentTransform);
        _simpleChoicePanel.name = "SimpleChoicesUI";
        _simpleChoicePanel.SetActive(false);
        _simpleChoicePanel.transform.SetAsLastSibling();

        Canvas simpleCanvas = _simpleChoicePanel.GetComponent<Canvas>();
        if (simpleCanvas == null)
        {
            simpleCanvas = _simpleChoicePanel.AddComponent<Canvas>();
        }
        simpleCanvas.overrideSorting = true;
        simpleCanvas.sortingOrder = 150;

        if (_simpleChoicePanel.GetComponent<GraphicRaycaster>() == null)
        {
            _simpleChoicePanel.AddComponent<GraphicRaycaster>();
        }

        _simpleChoicePrompt = FindChildByName(_simpleChoicePanel.transform, "MainText")?.GetComponent<TMP_Text>();

        Transform buttonsRoot = FindChildByName(_simpleChoicePanel.transform, "Buttons");
        if (buttonsRoot == null)
        {
            Debug.LogWarning("[UIManager] DecissionsUI has no Buttons container.");
            Destroy(_simpleChoicePanel);
            _simpleChoicePanel = null;
            return;
        }

        Transform row1 = FindChildByName(buttonsRoot, "Row1");
        Transform row2 = FindChildByName(buttonsRoot, "Row2");
        if (row1 != null) row1.gameObject.SetActive(true);
        if (row2 != null) row2.gameObject.SetActive(false);

        List<Button> orderedButtons = new List<Button>();
        OptionButtonHelper[] narrativeHelpers = buttonsRoot.GetComponentsInChildren<OptionButtonHelper>(true);
        if (narrativeHelpers != null && narrativeHelpers.Length > 0)
        {
            Array.Sort(narrativeHelpers, (left, right) => left.OptionIndex.CompareTo(right.OptionIndex));
            foreach (OptionButtonHelper helper in narrativeHelpers)
            {
                helper.enabled = false;
                Button btn = helper.GetComponent<Button>();
                if (btn != null && !orderedButtons.Contains(btn))
                {
                    orderedButtons.Add(btn);
                }
            }
        }
        else
        {
            Button[] rawButtons = buttonsRoot.GetComponentsInChildren<Button>(true);
            orderedButtons.AddRange(rawButtons);
        }

        _simpleChoiceButtons = orderedButtons.ToArray();
        for (int i = 0; i < _simpleChoiceButtons.Length; i++)
        {
            int choiceIndex = i;
            Button button = _simpleChoiceButtons[i];
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => CompleteSimpleChoice(choiceIndex));
            button.interactable = true;
            button.gameObject.SetActive(i < 2);
        }
    }

    private void SetSimpleChoiceButton(int index, string label)
    {
        if (_simpleChoiceButtons == null || index < 0 || index >= _simpleChoiceButtons.Length) return;

        Button button = _simpleChoiceButtons[index];
        button.gameObject.SetActive(true);
        button.interactable = true;

        TMP_Text buttonLabel = button.GetComponentInChildren<TMP_Text>(true);
        if (buttonLabel != null)
        {
            buttonLabel.text = label;
        }
    }

    private void CompleteSimpleChoice(int choiceIndex)
    {
        if (_simpleChoicePanel != null)
        {
            _simpleChoicePanel.SetActive(false);
        }

        Action<int> callback = _simpleChoiceCallback;
        _simpleChoiceCallback = null;

        bool returnToPhone = _simpleChoicesReturnToPhone;
        _simpleChoicesReturnToPhone = false;
        if (returnToPhone)
        {
            SetPhoneAppInteraction(true);
        }
        else
        {
            ChangeState((int)UIState.DEFAULT);
        }

        callback?.Invoke(choiceIndex);
    }

    private void ConfigureDayTracker()
    {
        if (_hudRoot == null)
        {
            _hudRoot = FindChildByName(transform, "HUD");
        }
        if (_hudRoot == null) return;

        Transform existing = _hudRoot.Find("DayTracker");
        if (existing != null)
        {
            _dayTrackerRoot = existing.gameObject;
            Transform dayTextChild = FindChildByName(existing, "DayText");
            _dayTrackerText = dayTextChild != null
                ? dayTextChild.GetComponent<TextMeshProUGUI>()
                : existing.GetComponentInChildren<TextMeshProUGUI>(true);
            RefreshDayTracker();
            return;
        }

        GameObject trackerObject = new GameObject(
            "DayTracker",
            typeof(RectTransform));
        trackerObject.layer = _hudRoot.gameObject.layer;
        trackerObject.transform.SetParent(_hudRoot, false);
        _dayTrackerRoot = trackerObject;

        RectTransform trackerRect = trackerObject.GetComponent<RectTransform>();
        trackerRect.anchorMin = new Vector2(0.5f, 1f);
        trackerRect.anchorMax = new Vector2(0.5f, 1f);
        trackerRect.pivot = new Vector2(0.5f, 1f);
        trackerRect.anchoredPosition = new Vector2(0f, -40f);
        trackerRect.sizeDelta = new Vector2(220f, 96f);
        trackerRect.localScale = Vector3.one;

        GameObject backgroundObject = new GameObject(
            "BackGround",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        backgroundObject.layer = _hudRoot.gameObject.layer;
        backgroundObject.transform.SetParent(trackerObject.transform, false);

        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.pivot = new Vector2(0.5f, 0.5f);
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        Image background = backgroundObject.GetComponent<Image>();
        background.color = new Color(0.6150943f, 0.6150943f, 0.6150943f, 0.5058824f);
        background.raycastTarget = false;

        Transform objectiveTextTransform = FindChildByName(_hudRoot, "Objective");
        TextMeshProUGUI objectiveTemplate = objectiveTextTransform != null
            ? objectiveTextTransform.GetComponent<TextMeshProUGUI>()
            : null;

        GameObject titleObject = new GameObject(
            "Title",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        titleObject.layer = _hudRoot.gameObject.layer;
        titleObject.transform.SetParent(trackerObject.transform, false);

        RectTransform titleRect = titleObject.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 0.5f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 0.5f);
        titleRect.offsetMin = new Vector2(12f, 2f);
        titleRect.offsetMax = new Vector2(-12f, -6f);

        TextMeshProUGUI titleText = titleObject.GetComponent<TextMeshProUGUI>();
        titleText.text = "Current Day";
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = new Color(0.2702812f, 0.7056604f, 0.68429273f, 1f);
        titleText.fontSize = 24f;
        titleText.fontStyle = FontStyles.Bold | FontStyles.Underline;
        titleText.raycastTarget = false;
        if (objectiveTemplate != null && objectiveTemplate.font != null)
        {
            titleText.font = objectiveTemplate.font;
        }

        GameObject textObject = new GameObject(
            "DayText",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        textObject.layer = _hudRoot.gameObject.layer;
        textObject.transform.SetParent(trackerObject.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 0.54f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.offsetMin = new Vector2(12f, 6f);
        textRect.offsetMax = new Vector2(-12f, -2f);

        _dayTrackerText = textObject.GetComponent<TextMeshProUGUI>();
        _dayTrackerText.alignment = TextAlignmentOptions.Center;
        _dayTrackerText.color = Color.white;
        _dayTrackerText.fontSize = 32f;
        _dayTrackerText.fontStyle = FontStyles.Bold;
        _dayTrackerText.enableAutoSizing = true;
        _dayTrackerText.fontSizeMin = 20f;
        _dayTrackerText.fontSizeMax = 32f;
        _dayTrackerText.raycastTarget = false;
        if (objectiveTemplate != null && objectiveTemplate.font != null)
        {
            _dayTrackerText.font = objectiveTemplate.font;
        }

        trackerObject.transform.SetAsLastSibling();
        RefreshDayTracker();
    }

    private void RefreshDayTracker()
    {
        if (_dayTrackerText == null || DayManager.Instance == null) return;

        int currentDay = DayManager.Instance.GetCurrentDay();
        bool visible = _currentState != UIState.MAIN_MENU && currentDay > 0;

        if (_dayTrackerRoot != null && _dayTrackerRoot.activeSelf != visible)
        {
            _dayTrackerRoot.SetActive(visible);
        }

        if (currentDay == _displayedDay) return;

        _displayedDay = currentDay;
        _dayTrackerText.text = currentDay > 0 ? $"Day {currentDay}" : string.Empty;
    }

    private static Transform FindChildByName(Transform root, string targetName)
    {
        if (root == null) return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (string.Equals(child.name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }

            Transform nested = FindChildByName(child, targetName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
