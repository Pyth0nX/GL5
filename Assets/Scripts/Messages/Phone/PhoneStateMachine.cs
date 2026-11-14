using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[Serializable]
public enum PhoneState
{
    IDLE,       //0
    MESSAGING,  //1
    CONTACTS,   //2
    GALLERY,    //3
    SETTINGS,   //4
    APP_STORE,   //5
    CONNECT,   //6
    CALLS,   //7
    QUIT   //8
}

/// <summary>
/// State machine for the phone interface, managing different states and transitions.
/// </summary>
public class PhoneStateMachine : MonoBehaviour
{
    private PhoneState _currentState = PhoneState.IDLE;

    [Header("Buttons")]
    [SerializeField]
    private Button _backButton;

    [Header("Tabs")]
    [SerializeField]
    private GameObject _startTab;
    [SerializeField]
    private GameObject _contactsTab;

    [SerializeField]
    private GameObject _messagingTab;

    [SerializeField]
    private GameObject _galleryTab;

    [SerializeField]
    private GameObject _quickGalleryButton;

    [SerializeField]
    private GameObject _dialogueTab; 

    [SerializeField]
    private GameObject _quitTab;

    [Header("App Managers (Optional - Auto-Detected)")]
    [SerializeField]
    private GalleryManager _galleryManager;

    [Header("Phone Background")]
    [Tooltip("Background Image component of the phone screen (PhoneBG).")]
    [SerializeField] private Image _phoneBgImage;
    [Tooltip("Default wallpaper sprite when on the phone home screen (Phone-Screen).")]
    [SerializeField] private Sprite _defaultBgSprite;
    [Tooltip("Background sprite used while inside the Messages app (Messages-UI).")]
    [SerializeField] private Sprite _messagesBgSprite;
    [Tooltip("Background sprite used while inside the Connect app (Connect-UI).")]
    [SerializeField] private Sprite _connectBgSprite;

    [Header("App Notification Badges")]
    [Tooltip("Notification icon sprite displayed in the top-right corner of app icons (Notifications).")]
    [SerializeField] private Sprite _notificationBadgeSprite;
    [SerializeField] private Button _messagesAppButton;
    [SerializeField] private Button _connectAppButton;

    private TMP_FontAsset _cachedBadgeFont;
    private int _backButtonState = 0; // State to change to when the back button is pressed

    private void Awake()
    {
        EnsureBackgroundReferences();
        EnsureAppButtons();
        MessagesManager.OnUnreadCountsChanged += UpdateNotificationBadges;
    }

    private void OnEnable()
    {
        UpdatePhoneBackground();
        UpdateNotificationBadges();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        EnsureBackgroundReferences();
        EnsureAppButtons();

        if (_backButton != null)
        {
            _backButton.onClick.AddListener(OnBackButtonPressed);
        }

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe<int>("Phone_Closed", ChangeState);
        }

        UpdatePhoneBackground();
        UpdateNotificationBadges();
    }

    private void OnDestroy()
    {
        MessagesManager.OnUnreadCountsChanged -= UpdateNotificationBadges;

        if (_backButton != null)
        {
            _backButton.onClick.RemoveListener(OnBackButtonPressed);
        }

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe<int>("Phone_Closed", ChangeState);
        }
    }

    private void OnBackButtonPressed()
    {
        // On the Idle home screen, Back does nothing and must never jump back into a closed app
        if (_currentState == PhoneState.IDLE)
        {
            return;
        }

        if (_currentState == PhoneState.GALLERY)
        {
            EnsureGalleryManager();
            if (_galleryManager != null && _galleryManager.TryHandleBackButton())
            {
                return;
            }
        }

        ChangeState(_backButtonState);
    }

    private void EnsureGalleryManager()
    {
        if (_galleryManager == null && _galleryTab != null)
        {
            _galleryManager = _galleryTab.GetComponentInChildren<GalleryManager>(true);
        }

        if (_galleryManager == null)
        {
            _galleryManager = GalleryManager.Instance;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    /// <summary>
    /// Changes the current state to the specified new state and updates the button callbacks accordingly.
    /// </summary>
    /// <param name="newState"></param>
    public void ChangeState(int newState)
    {
        PhoneState targetState = (PhoneState)newState;
        if (_currentState == targetState) return;

        _backButtonState = (int)_currentState;
        ExitCurrentState(targetState);
        _currentState = targetState;
        EnterNewState();
    }

    public void SetBackButtonState(int state)
    {
        _backButtonState = state;
    }

    public PhoneState GetCurrentState()
    {
        return _currentState;
    }

    /// <summary>
    /// Disable the tab associated to the current state
    /// </summary>
    private void ExitCurrentState(PhoneState nextState = PhoneState.IDLE)
    {
        switch (_currentState)
        {
            case PhoneState.IDLE:
                _startTab?.SetActive(false);
                break;
            case PhoneState.CONTACTS:
                _contactsTab?.SetActive(false);
                break;
            case PhoneState.MESSAGING:
                _messagingTab?.SetActive(false);
                _quickGalleryButton?.SetActive(false);
                _dialogueTab?.SetActive(false);

                // Hide decisions panel if it was open during mobile messaging when switching tabs
                OptionsChanger optionsChanger = MessagesManager.Instance?.GetOptionsChanger();
                if (optionsChanger != null && optionsChanger.IsDecisionsPanelActive())
                {
                    optionsChanger.ActiveDecisionsPanel(false);
                }

                // Keep MESSAGING as return state if navigating to GALLERY (Quick Select); otherwise return to IDLE
                if (nextState != PhoneState.GALLERY)
                {
                    _backButtonState = (int)PhoneState.IDLE;
                }
                break;
            case PhoneState.GALLERY:
                _galleryTab?.SetActive(false);
                break;
            case PhoneState.SETTINGS:
                if (EventBus.Instance != null)
                {
                    EventBus.Instance.Publish("CloseSettings");
                }
                break;
            case PhoneState.QUIT:
                _quitTab?.SetActive(false);
                break;
        }

    }

    /// <summary>
    /// Enable the tab associated with the new state and calls the required methods if needed
    /// </summary>
    private void EnterNewState()
    {
        switch (_currentState)
        {
            case PhoneState.IDLE:
                _backButtonState = (int)PhoneState.IDLE;
                _startTab?.SetActive(true);
                UpdateNotificationBadges();
                break;
            case PhoneState.CONTACTS:
                _contactsTab?.SetActive(true);
                MessagesManager.Instance?.GetMessagesUI()?.RefreshContacts();
                break;
            case PhoneState.MESSAGING:
                _messagingTab?.SetActive(true);
                _quickGalleryButton?.SetActive(true);
                break;
            case PhoneState.GALLERY:
                _galleryTab?.SetActive(true);
                break;
            case PhoneState.SETTINGS:
                if (EventBus.Instance != null)
                {
                    EventBus.Instance.Publish("OpenSettings");
                }
                break;
            case PhoneState.QUIT:
                _quitTab?.SetActive(true);
                break;
        }

        UpdatePhoneBackground();
    }

    #region Getters
    public GameObject GetStartTab()
    {
        return _startTab;
    }

    public GameObject GetContactsTab()
    {
        return _contactsTab;
    }

    public GameObject GetMessagingTab()
    {
        return _messagingTab;
    }

    public GameObject GetGalleryTab()
    {
        return _galleryTab;
    }

    
    #endregion

    /// <summary>
    /// Show the decision tab and include it in the list of tabs to close when coming back to other tabs
    /// </summary>
    public void ShowDecisionsTab()
    {
        //_homeButton.GetTabsToClose().Add(_decisionsTab);
        //_backButton.GetTabsToClose().Add(_decisionsTab);
        //_decisionsTab.SetActive(true);
    }

    public void ShowDialogueTab()
    {
        _dialogueTab.SetActive(true);
    }

    public void UpdatePhoneBackground()
    {
        EnsureBackgroundReferences();
        if (_phoneBgImage == null) return;

        Sprite targetSprite = null;
        if (_currentState == PhoneState.CONTACTS || _currentState == PhoneState.MESSAGING)
        {
            MessagingChannel channel = MessagesManager.Instance != null
                ? MessagesManager.Instance.GetMessagingChannel()
                : MessagingChannel.Phone;

            targetSprite = channel == MessagingChannel.Connect
                ? (_connectBgSprite != null ? _connectBgSprite : MessagesUI.LoadSprite("Hilmir/UI/Connect-UI.png"))
                : (_messagesBgSprite != null ? _messagesBgSprite : MessagesUI.LoadSprite("Hilmir/UI/Messages-UI.png"));
        }
        else
        {
            targetSprite = _defaultBgSprite != null
                ? _defaultBgSprite
                : MessagesUI.LoadSprite("Hilmir/UI/Phone-Screen.png");
        }

        if (targetSprite != null)
        {
            _phoneBgImage.sprite = targetSprite;
        }
    }

    public void UpdateNotificationBadges()
    {
        EnsureAppButtons();

        Sprite badgeSprite = _notificationBadgeSprite != null
            ? _notificationBadgeSprite
            : MessagesUI.LoadSprite("Hilmir/UI/Notifications.png");

        MessagesManager manager = MessagesManager.Instance;
        int phoneUnread = manager != null ? manager.GetChannelUnreadCount(MessagingChannel.Phone) : 0;
        int connectUnread = manager != null ? manager.GetChannelUnreadCount(MessagingChannel.Connect) : 0;

        if (_messagesAppButton != null)
        {
            MessagesUI.ApplyNotificationBadge(
                _messagesAppButton.transform,
                phoneUnread,
                badgeSprite,
                _cachedBadgeFont,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-10f, -10f),
                new Vector2(36f, 36f),
                12f,
                22f);
        }

        if (_connectAppButton != null)
        {
            MessagesUI.ApplyNotificationBadge(
                _connectAppButton.transform,
                connectUnread,
                badgeSprite,
                _cachedBadgeFont,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(0.5f, 0.5f),
                new Vector2(-10f, -10f),
                new Vector2(36f, 36f),
                12f,
                22f);
        }
    }

    private void EnsureBackgroundReferences()
    {
        if (_phoneBgImage == null)
        {
            Transform bgTransform = transform.Find("PhoneBG") ?? FindDescendantByName(transform, "PhoneBG");
            if (bgTransform != null)
            {
                _phoneBgImage = bgTransform.GetComponent<Image>();
            }
        }

        if (_defaultBgSprite == null && _phoneBgImage != null)
        {
            _defaultBgSprite = _phoneBgImage.sprite;
        }
    }

    private void EnsureAppButtons()
    {
        Transform searchRoot = _startTab != null ? _startTab.transform : transform;
        Transform buttonPanel = searchRoot.Find("ButtonPanel") ?? FindDescendantByName(searchRoot, "ButtonPanel") ?? searchRoot;

        if (_messagesAppButton == null && buttonPanel != null)
        {
            _messagesAppButton = (buttonPanel.Find("Messages") ?? FindDescendantByName(buttonPanel, "Messages"))?.GetComponent<Button>();
        }

        if (_connectAppButton == null && buttonPanel != null)
        {
            _connectAppButton = (buttonPanel.Find("Connect") ?? FindDescendantByName(buttonPanel, "Connect"))?.GetComponent<Button>();
        }

        if (_cachedBadgeFont == null)
        {
            _cachedBadgeFont = GetComponentInChildren<TextMeshProUGUI>(true)?.font;
        }
    }

    private static Transform FindDescendantByName(Transform root, string targetName)
    {
        if (root == null) return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == targetName) return child;

            Transform match = FindDescendantByName(child, targetName);
            if (match != null) return match;
        }

        return null;
    }

}
