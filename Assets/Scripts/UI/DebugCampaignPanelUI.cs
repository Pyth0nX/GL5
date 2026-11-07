using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Unified Debug Campaign Panel allowing developers and testers to jump directly to Day 1..5,
/// simulate prior decision profiles (Safe / Neutral / Risky), and test live Reputation and
/// Anxiety Post-Processing while completely locking background gameplay movement and interactions.
/// </summary>
public class DebugCampaignPanelUI : MonoBehaviour
{
    public enum DebugOpenSource
    {
        NONE = 0,
        MAIN_MENU = 1,
        PAUSE_MENU = 2,
        GAMEPLAY = 3
    }

    public static DebugCampaignPanelUI Instance { get; private set; }

    [Header("Panel References")]
    [SerializeField] private GameObject _debugPanelRoot;
    [SerializeField] private Button[] _dayButtons = new Button[5];
    [SerializeField] private Button[] _routeButtons = new Button[3];
    [SerializeField] private Button _startDayButton;
    [SerializeField] private Button _repMinusButton;
    [SerializeField] private Button _repResetButton;
    [SerializeField] private Button _repPlusButton;
    [SerializeField] private Button _skipDay1TimerButton;
    [SerializeField] private Button _backButton;
    [SerializeField] private TextMeshProUGUI _summaryText;
    [SerializeField] private TextMeshProUGUI _reputationStatusText;

    [Header("Selection Palette")]
    [SerializeField] private Color _selectedOptionColor = new Color(0.17f, 0.83f, 0.78f, 1f);
    [SerializeField] private Color _unselectedOptionColor = new Color(0.14f, 0.14f, 0.14f, 1f);
    [SerializeField] private Color _selectedTextColor = new Color(0.18f, 0.18f, 0.18f, 1f);
    [SerializeField] private Color _unselectedTextColor = new Color(0.92f, 0.92f, 0.92f, 1f);

    private static readonly Color BackdropFallbackColor = new Color(0f, 0f, 0f, 0.65f);
    private static readonly Color DialogFallbackColor = new Color(0.24f, 0.24f, 0.24f, 1f);
    private static readonly Color ActionButtonFallbackColor = new Color(0.96f, 0.96f, 0.96f, 1f);
    private static readonly Color ActionTextFallbackColor = new Color(0.18f, 0.18f, 0.18f, 1f);

    private UIManager _uiManager;
    private TMP_FontAsset _sharedFont;
    private int _selectedDay = 1;
    private DayManager.TestRoute _selectedRoute = DayManager.TestRoute.Neutral;
    private DebugOpenSource _openedFrom = DebugOpenSource.NONE;
    private float _previousTimeScale = 1f;
    private bool _hasCapturedTimeScale;
    private bool _isSubscribed;
    private bool _listenersHooked;

    public bool IsOpen => _debugPanelRoot != null && _debugPanelRoot.activeSelf;

    public static DebugCampaignPanelUI EnsureAttached(UIManager uiManager)
    {
        if (uiManager == null) return null;

        DebugCampaignPanelUI panel = uiManager.GetComponent<DebugCampaignPanelUI>();
        if (panel == null)
        {
            panel = uiManager.GetComponentInChildren<DebugCampaignPanelUI>(true);
        }
        if (panel == null)
        {
            panel = uiManager.gameObject.AddComponent<DebugCampaignPanelUI>();
        }

        panel.Initialize(uiManager);
        return panel;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }

        if (_uiManager == null)
        {
            _uiManager = GetComponent<UIManager>();
        }

        if (_debugPanelRoot != null && _debugPanelRoot != gameObject)
        {
            _debugPanelRoot.SetActive(false);
        }

        TrySubscribeEventBus();
    }

    private void Start()
    {
        if (_uiManager == null)
        {
            _uiManager = UIManager.Instance != null ? UIManager.Instance : GetComponent<UIManager>();
        }

        Initialize(_uiManager);
    }

    private void OnEnable()
    {
        TrySubscribeEventBus();
    }

    private void OnDestroy()
    {
        UnsubscribeEventBus();

        if (Instance == this)
        {
            if (_hasCapturedTimeScale)
            {
                Time.timeScale = 1f;
                _hasCapturedTimeScale = false;
            }
            Instance = null;
        }
    }

    public void Initialize(UIManager uiManager)
    {
        if (uiManager != null)
        {
            _uiManager = uiManager;
        }

        ResolveSharedFont();
        ResolveSerializedReferencesIfNeeded();
        BuildModalIfNeeded();
        EnsureMenuButtonsIfNeeded();
        EnsureHoverFeedbackOnButtons();
        HookButtonListeners();
        TrySubscribeEventBus();
        RefreshSelectionVisuals();
    }

    private void Update()
    {
        if (!_isSubscribed)
        {
            TrySubscribeEventBus();
        }

        if (Keyboard.current != null)
        {
            if (Keyboard.current.f9Key.wasPressedThisFrame)
            {
                if (IsOpen)
                {
                    ClosePanel();
                }
                else
                {
                    OpenFromCurrentContext();
                }
                return;
            }

            if (IsOpen && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ClosePanel();
                return;
            }
        }

        if (IsOpen)
        {
            RefreshLiveStatusText();
        }
    }

    public void TrySubscribeEventBus()
    {
        if (_isSubscribed || EventBus.Instance == null) return;

        EventBus.Instance.Subscribe("OpenDebugMenu", OpenFromCurrentContext);
        EventBus.Instance.Subscribe("CloseDebugMenu", ClosePanel);
        EventBus.Instance.Subscribe("PauseMenu_Closed", OnPauseMenuClosed);
        _isSubscribed = true;
    }

    private void UnsubscribeEventBus()
    {
        if (!_isSubscribed || EventBus.Instance == null) return;

        EventBus.Instance.Unsubscribe("OpenDebugMenu", OpenFromCurrentContext);
        EventBus.Instance.Unsubscribe("CloseDebugMenu", ClosePanel);
        EventBus.Instance.Unsubscribe("PauseMenu_Closed", OnPauseMenuClosed);
        _isSubscribed = false;
    }

    private void OnPauseMenuClosed()
    {
        if (_openedFrom == DebugOpenSource.PAUSE_MENU && IsOpen)
        {
            if (_debugPanelRoot != null)
            {
                _debugPanelRoot.SetActive(false);
            }
            _openedFrom = DebugOpenSource.NONE;
            _hasCapturedTimeScale = false;
        }
    }

    /// <summary>
    /// Opens the debug panel while automatically detecting the active UI context
    /// (Main Menu, Pause Menu, or active Gameplay) and locking background interactions.
    /// </summary>
    public void OpenFromCurrentContext()
    {
        if (_uiManager == null)
        {
            _uiManager = UIManager.Instance;
        }

        if (_uiManager != null)
        {
            GameObject pausePanel = _uiManager.GetPauseMenuPanel();
            UIState state = _uiManager.GetCurrentState();

            if (state == UIState.MENU || (pausePanel != null && pausePanel.activeInHierarchy))
            {
                OpenPanel(DebugOpenSource.PAUSE_MENU);
                return;
            }

            GameObject mainMenu = _uiManager.GetMainMenuUI();
            if (state == UIState.MAIN_MENU || (mainMenu != null && mainMenu.activeInHierarchy))
            {
                OpenPanel(DebugOpenSource.MAIN_MENU);
                return;
            }
        }

        OpenPanel(DebugOpenSource.GAMEPLAY);
    }

    public void OpenFromMainMenu() => OpenPanel(DebugOpenSource.MAIN_MENU);
    public void OpenFromPauseMenu() => OpenPanel(DebugOpenSource.PAUSE_MENU);
    public void OpenPanel() => OpenFromCurrentContext();

    public void OpenPanel(DebugOpenSource source)
    {
        BuildModalIfNeeded();
        if (_debugPanelRoot == null) return;

        if (!IsOpen)
        {
            _openedFrom = source;
            _previousTimeScale = Time.timeScale;
            _hasCapturedTimeScale = true;
        }

        if (DayManager.Instance != null)
        {
            int activeDay = DayManager.Instance.GetCurrentDay();
            if (activeDay >= 1 && activeDay <= 5)
            {
                _selectedDay = activeDay;
            }
        }

        if (source == DebugOpenSource.PAUSE_MENU && _uiManager != null)
        {
            GameObject pausePanel = _uiManager.GetPauseMenuPanel();
            if (pausePanel != null)
            {
                pausePanel.SetActive(false);
            }
        }

        // Freeze gameplay simulation and block all player/world/phone/menu inputs while open.
        if (source != DebugOpenSource.MAIN_MENU)
        {
            Time.timeScale = 0f;
        }

        if (InputManager.Instance != null)
        {
            InputManager.Instance.DisableMovementMap();
            InputManager.Instance.DisableInteract();
            InputManager.Instance.DisablePhone();
            InputManager.Instance.DisableMenuAction();
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        _debugPanelRoot.SetActive(true);
        _debugPanelRoot.transform.SetAsLastSibling();

        RefreshSelectionVisuals();
        RefreshLiveStatusText();

        if (_uiManager != null)
        {
            _uiManager.UnlockMouse();
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    public void ClosePanel()
    {
        if (!IsOpen) return;

        if (_debugPanelRoot != null)
        {
            _debugPanelRoot.SetActive(false);
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        DebugOpenSource returnSource = _openedFrom;
        _openedFrom = DebugOpenSource.NONE;

        RestoreStateAfterClose(returnSource);
    }

    private void RestoreStateAfterClose(DebugOpenSource returnSource)
    {
        if (_uiManager == null)
        {
            _uiManager = UIManager.Instance;
        }

        switch (returnSource)
        {
            case DebugOpenSource.MAIN_MENU:
                if (_hasCapturedTimeScale)
                {
                    Time.timeScale = 1f;
                    _hasCapturedTimeScale = false;
                }

                if (_uiManager != null)
                {
                    _uiManager.ChangeState((int)UIState.MAIN_MENU);
                    GameObject mainMenu = _uiManager.GetMainMenuUI();
                    if (mainMenu != null)
                    {
                        mainMenu.SetActive(true);
                    }
                    _uiManager.UnlockMouse();
                }
                break;

            case DebugOpenSource.PAUSE_MENU:
                if (_hasCapturedTimeScale)
                {
                    Time.timeScale = _previousTimeScale;
                    _hasCapturedTimeScale = false;
                }

                if (_uiManager != null && _uiManager.GetCurrentState() == UIState.MENU)
                {
                    GameObject pausePanel = _uiManager.GetPauseMenuPanel();
                    if (pausePanel != null)
                    {
                        pausePanel.SetActive(true);
                    }
                    InputManager.Instance?.EnableMenuAction();
                    _uiManager.UnlockMouse();
                }
                else
                {
                    Time.timeScale = 1f;
                    RestoreGameplayInputsForCurrentUIState();
                }
                break;

            default:
                if (_hasCapturedTimeScale)
                {
                    Time.timeScale = _previousTimeScale > 0f ? _previousTimeScale : 1f;
                    _hasCapturedTimeScale = false;
                }
                RestoreGameplayInputsForCurrentUIState();
                break;
        }
    }

    private void RestoreGameplayInputsForCurrentUIState()
    {
        if (_uiManager == null)
        {
            InputManager.Instance?.EnableMovementMap();
            InputManager.Instance?.EnableInteract();
            InputManager.Instance?.EnablePhone();
            InputManager.Instance?.EnableMenuAction();
            return;
        }

        UIState state = _uiManager.GetCurrentState();
        switch (state)
        {
            case UIState.DEFAULT:
                if (InputManager.Instance != null)
                {
                    InputManager.Instance.EnableMovementMap();
                    InputManager.Instance.EnablePhone();
                    InputManager.Instance.EnableMenuAction();
                    InputManager.Instance.EnableInteract();
                }

                SettingsPanelUI settings = GetComponent<SettingsPanelUI>();
                if (settings != null && settings.IsPanelOpen())
                {
                    _uiManager.UnlockMouse();
                }
                else
                {
                    _uiManager.LockMouse();
                }
                break;

            case UIState.PHONE:
                if (InputManager.Instance != null)
                {
                    InputManager.Instance.DisableMovementMap();
                    InputManager.Instance.DisableInteract();
                    InputManager.Instance.DisableMenuAction();
                    InputManager.Instance.EnablePhone();
                }
                _uiManager.UnlockMouse();
                break;

            case UIState.MAP:
            case UIState.CONVERSATION:
                if (InputManager.Instance != null)
                {
                    InputManager.Instance.DisableMovementMap();
                    InputManager.Instance.DisableInteract();
                    InputManager.Instance.DisablePhone();
                    InputManager.Instance.EnableMenuAction();
                }
                _uiManager.UnlockMouse();
                break;

            case UIState.MENU:
                Time.timeScale = 0f;
                if (InputManager.Instance != null)
                {
                    InputManager.Instance.DisableMovementMap();
                    InputManager.Instance.DisableInteract();
                    InputManager.Instance.DisablePhone();
                    InputManager.Instance.EnableMenuAction();
                }
                _uiManager.UnlockMouse();
                break;

            default:
                _uiManager.UnlockMouse();
                break;
        }
    }

    public void SelectDay(int day)
    {
        _selectedDay = Mathf.Clamp(day, 1, 5);
        RefreshSelectionVisuals();
    }

    public void SelectRouteIndex(int routeIndex)
    {
        if (routeIndex <= 0)
        {
            _selectedRoute = DayManager.TestRoute.Safe;
        }
        else if (routeIndex == 1)
        {
            _selectedRoute = DayManager.TestRoute.Neutral;
        }
        else
        {
            _selectedRoute = DayManager.TestRoute.Risky;
        }

        RefreshSelectionVisuals();
    }

    public void LaunchSelectedDay()
    {
        if (DayManager.Instance == null)
        {
            Debug.LogWarning("[DebugCampaignPanelUI] DayManager instance is unavailable.");
            return;
        }

        if (_debugPanelRoot != null)
        {
            _debugPanelRoot.SetActive(false);
        }

        _openedFrom = DebugOpenSource.NONE;
        _hasCapturedTimeScale = false;
        Time.timeScale = 1f;

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        DayManager.Instance.StartDebugSession(_selectedDay, _selectedRoute);
    }

    public void AdjustLiveReputation(int delta)
    {
        ReputationManager.Instance?.AddReputation(delta);
        RefreshLiveStatusText();
    }

    public void ResetLiveReputation()
    {
        ReputationManager.Instance?.SetReputation(50);
        RefreshLiveStatusText();
    }

    public void SkipDay1ExploreTimer()
    {
        ObjectivesManager.Instance?.CompleteDay1ExploreTimerImmediately();
        RefreshLiveStatusText();
    }

    private void HookButtonListeners()
    {
        if (_listenersHooked) return;
        _listenersHooked = true;

        if (_dayButtons != null)
        {
            for (int i = 0; i < _dayButtons.Length; i++)
            {
                if (_dayButtons[i] == null) continue;
                int capturedDay = i + 1;
                _dayButtons[i].onClick.AddListener(() => SelectDay(capturedDay));
            }
        }

        if (_routeButtons != null)
        {
            for (int i = 0; i < _routeButtons.Length; i++)
            {
                if (_routeButtons[i] == null) continue;
                int capturedRoute = i;
                _routeButtons[i].onClick.AddListener(() => SelectRouteIndex(capturedRoute));
            }
        }

        _startDayButton?.onClick.AddListener(LaunchSelectedDay);
        _repMinusButton?.onClick.AddListener(() => AdjustLiveReputation(-15));
        _repResetButton?.onClick.AddListener(ResetLiveReputation);
        _repPlusButton?.onClick.AddListener(() => AdjustLiveReputation(15));
        _skipDay1TimerButton?.onClick.AddListener(SkipDay1ExploreTimer);
        _backButton?.onClick.AddListener(ClosePanel);
    }

    private void EnsureHoverFeedbackOnButtons()
    {
        if (_dayButtons != null)
        {
            for (int i = 0; i < _dayButtons.Length; i++)
            {
                EnsureHoverFeedback(_dayButtons[i]);
            }
        }

        if (_routeButtons != null)
        {
            for (int i = 0; i < _routeButtons.Length; i++)
            {
                EnsureHoverFeedback(_routeButtons[i]);
            }
        }

        EnsureHoverFeedback(_startDayButton);
        EnsureHoverFeedback(_repMinusButton);
        EnsureHoverFeedback(_repResetButton);
        EnsureHoverFeedback(_repPlusButton);
        EnsureHoverFeedback(_skipDay1TimerButton);
        EnsureHoverFeedback(_backButton);
    }

    private static UIHoverFeedback EnsureHoverFeedback(Button button)
    {
        if (button == null) return null;

        UIHoverFeedback hover = button.GetComponent<UIHoverFeedback>();
        if (hover == null)
        {
            hover = button.gameObject.AddComponent<UIHoverFeedback>();
        }
        return hover;
    }

    private void RefreshSelectionVisuals()
    {
        if (_dayButtons != null)
        {
            for (int i = 0; i < _dayButtons.Length; i++)
            {
                bool selected = (i + 1) == _selectedDay;
                ApplyToggleVisual(_dayButtons[i], selected, true);
            }
        }

        bool profileInteractable = _selectedDay > 1;
        int activeRouteIndex = _selectedRoute == DayManager.TestRoute.Safe
            ? 0
            : (_selectedRoute == DayManager.TestRoute.Neutral ? 1 : 2);

        if (_routeButtons != null)
        {
            for (int i = 0; i < _routeButtons.Length; i++)
            {
                bool selected = profileInteractable && i == activeRouteIndex;
                ApplyToggleVisual(_routeButtons[i], selected, profileInteractable);
            }
        }

        if (_summaryText != null)
        {
            if (_selectedDay == 1)
            {
                _summaryText.text = "Day 1  |  Fresh Start  |  Reputation: 50 (Neutral)";
            }
            else
            {
                int simulatedRep = DayManager.CalculateProfileReputation(_selectedDay, _selectedRoute);
                ReputationTier tier = EvaluateDefaultTier(simulatedRep);
                _summaryText.text = $"Day {_selectedDay}  |  {_selectedRoute}  |  Reputation: {simulatedRep} ({tier})";
            }
        }
    }

    private void ApplyToggleVisual(Button button, bool selected, bool interactable)
    {
        if (button == null) return;

        button.interactable = interactable;

        Color bgColor = selected ? _selectedOptionColor : _unselectedOptionColor;
        if (!interactable)
        {
            bgColor = new Color(_unselectedOptionColor.r, _unselectedOptionColor.g, _unselectedOptionColor.b, 0.55f);
        }

        if (button.targetGraphic is Image targetImage)
        {
            targetImage.color = bgColor;
        }

        UIHoverFeedback hover = button.GetComponent<UIHoverFeedback>();
        if (hover != null)
        {
            hover.SetBaseColor(bgColor);
        }

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            Color textColor = selected ? _selectedTextColor : _unselectedTextColor;
            if (!interactable)
            {
                textColor = new Color(_unselectedTextColor.r, _unselectedTextColor.g, _unselectedTextColor.b, 0.45f);
            }
            label.color = textColor;
        }
    }

    private void RefreshLiveStatusText()
    {
        if (_reputationStatusText == null) return;

        int currentRep = ReputationManager.Instance != null ? ReputationManager.Instance.CurrentReputation : 50;
        ReputationTier currentTier = ReputationManager.Instance != null
            ? ReputationManager.Instance.CurrentTier
            : EvaluateDefaultTier(currentRep);

        _reputationStatusText.text = $"Reputation: {currentRep} ({currentTier})";
    }

    private static ReputationTier EvaluateDefaultTier(int rep)
    {
        if (rep <= 20) return ReputationTier.VeryLow;
        if (rep <= 40) return ReputationTier.Low;
        if (rep >= 80) return ReputationTier.VeryHigh;
        if (rep >= 60) return ReputationTier.High;
        return ReputationTier.Neutral;
    }

    private void ResolveSharedFont()
    {
        if (_sharedFont != null) return;

        Transform searchRoot = _uiManager != null ? _uiManager.transform : transform;
        TextMeshProUGUI anyTmp = searchRoot.GetComponentInChildren<TextMeshProUGUI>(true);
        if (anyTmp != null && anyTmp.font != null)
        {
            _sharedFont = anyTmp.font;
        }
    }

    private void ResolveSerializedReferencesIfNeeded()
    {
        if (_debugPanelRoot == null)
        {
            Transform searchRoot = _uiManager != null ? _uiManager.transform : transform;
            Transform found = FindChildRecursive(searchRoot, "DebugPanel");
            if (found != null)
            {
                _debugPanelRoot = found.gameObject;
            }
        }

        if (_debugPanelRoot == null) return;

        Transform root = _debugPanelRoot.transform;

        if (_dayButtons == null || _dayButtons.Length != 5)
        {
            _dayButtons = new Button[5];
        }
        for (int i = 0; i < 5; i++)
        {
            if (_dayButtons[i] == null)
            {
                _dayButtons[i] = FindChildRecursive(root, $"Day{i + 1}Button")?.GetComponent<Button>();
            }
        }

        if (_routeButtons == null || _routeButtons.Length != 3)
        {
            _routeButtons = new Button[3];
        }
        if (_routeButtons[0] == null) _routeButtons[0] = FindChildRecursive(root, "SafeRouteButton")?.GetComponent<Button>();
        if (_routeButtons[1] == null) _routeButtons[1] = FindChildRecursive(root, "NeutralRouteButton")?.GetComponent<Button>();
        if (_routeButtons[2] == null) _routeButtons[2] = FindChildRecursive(root, "RiskyRouteButton")?.GetComponent<Button>();

        if (_startDayButton == null) _startDayButton = FindChildRecursive(root, "StartDayButton")?.GetComponent<Button>();
        if (_repMinusButton == null) _repMinusButton = FindChildRecursive(root, "RepMinusButton")?.GetComponent<Button>();
        if (_repResetButton == null) _repResetButton = FindChildRecursive(root, "RepResetButton")?.GetComponent<Button>();
        if (_repPlusButton == null) _repPlusButton = FindChildRecursive(root, "RepPlusButton")?.GetComponent<Button>();
        if (_skipDay1TimerButton == null) _skipDay1TimerButton = FindChildRecursive(root, "SkipTimerButton")?.GetComponent<Button>();
        if (_backButton == null) _backButton = FindChildRecursive(root, "BackButton")?.GetComponent<Button>();
        if (_summaryText == null) _summaryText = FindChildRecursive(root, "SummaryText")?.GetComponent<TextMeshProUGUI>();
        if (_reputationStatusText == null) _reputationStatusText = FindChildRecursive(root, "ReputationStatusText")?.GetComponent<TextMeshProUGUI>();
    }

    private void EnsureMenuButtonsIfNeeded()
    {
        if (_uiManager == null) return;

        GameObject mainMenu = _uiManager.GetMainMenuUI();
        if (mainMenu != null && FindChildRecursive(mainMenu.transform, "DebugButton") == null)
        {
            Transform buttonsContainer = FindChildRecursive(mainMenu.transform, "Buttons") ?? mainMenu.transform;
            Button template = FindTemplateButton(buttonsContainer);
            if (template != null)
            {
                Button btn = Instantiate(template, buttonsContainer);
                btn.name = "DebugButton";
                StripButtonSideEffects(btn.gameObject);
                btn.onClick = new Button.ButtonClickedEvent();
                btn.onClick.AddListener(OpenFromCurrentContext);
                EnsureHoverFeedback(btn);

                TextMeshProUGUI label = btn.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null) label.text = "DEBUG";
            }
        }

        GameObject pausePanel = _uiManager.GetPauseMenuPanel();
        if (pausePanel != null && FindChildRecursive(pausePanel.transform, "DebugButton") == null)
        {
            Button template = FindTemplateButton(pausePanel.transform);
            if (template != null)
            {
                Button btn = Instantiate(template, pausePanel.transform);
                btn.name = "DebugButton";
                StripButtonSideEffects(btn.gameObject);
                btn.onClick = new Button.ButtonClickedEvent();
                btn.onClick.AddListener(OpenFromCurrentContext);
                EnsureHoverFeedback(btn);

                TextMeshProUGUI label = btn.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null) label.text = "Debug";
            }
        }
    }

    private void BuildModalIfNeeded()
    {
        if (_debugPanelRoot != null) return;

        Transform parentTransform = _uiManager != null
            ? (FindChildRecursive(_uiManager.transform, "SafeAreaPanel") ?? _uiManager.transform)
            : transform;

        _debugPanelRoot = new GameObject(
            "DebugPanel",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(GraphicRaycaster));
        _debugPanelRoot.layer = gameObject.layer;
        _debugPanelRoot.transform.SetParent(parentTransform, false);

        RectTransform rootRect = _debugPanelRoot.GetComponent<RectTransform>();
        StretchFull(rootRect);

        Canvas modalCanvas = _debugPanelRoot.GetComponent<Canvas>();
        modalCanvas.overrideSorting = true;
        modalCanvas.sortingOrder = 500;

        Image backdrop = CreateImage("ModalBackdrop", _debugPanelRoot.transform, BackdropFallbackColor);
        StretchFull(backdrop.rectTransform);
        backdrop.raycastTarget = true;

        Image card = CreateImage("DialogPanel", _debugPanelRoot.transform, DialogFallbackColor);
        CenterRect(card.rectTransform, new Vector2(580f, 780f), Vector2.zero);

        TextMeshProUGUI title = CreateLabel("TitleText", card.transform, "Debug", 42f, FontStyles.Bold, Color.white);
        CenterRect(title.rectTransform, new Vector2(460f, 60f), new Vector2(0f, 310f));

        TextMeshProUGUI dayHeader = CreateLabel("StartingDayText", card.transform, "Starting Day", 22f, FontStyles.Normal, Color.white);
        CenterRect(dayHeader.rectTransform, new Vector2(480f, 30f), new Vector2(0f, 233f));

        _dayButtons = new Button[5];
        const float dayButtonWidth = 92f;
        const float dayGap = 10f;
        float dayStartX = -((5 * dayButtonWidth + 4 * dayGap) * 0.5f) + (dayButtonWidth * 0.5f);
        for (int i = 0; i < 5; i++)
        {
            int dayNumber = i + 1;
            float x = dayStartX + i * (dayButtonWidth + dayGap);
            _dayButtons[i] = CreateStyledButton(
                $"Day{dayNumber}Button",
                card.transform,
                $"Day {dayNumber}",
                new Vector2(dayButtonWidth, 46f),
                new Vector2(x, 189f),
                _unselectedOptionColor,
                _unselectedTextColor,
                20f);
        }

        TextMeshProUGUI routeHeader = CreateLabel("DecisionProfileText", card.transform, "Prior Decisions", 22f, FontStyles.Normal, Color.white);
        CenterRect(routeHeader.rectTransform, new Vector2(480f, 30f), new Vector2(0f, 110f));

        _routeButtons = new Button[3];
        string[] routeNames = { "Safe", "Neutral", "Risky" };
        string[] routeObjectNames = { "SafeRouteButton", "NeutralRouteButton", "RiskyRouteButton" };
        const float routeWidth = 158f;
        const float routeGap = 13f;
        float routeStartX = -(routeWidth + routeGap);
        for (int i = 0; i < 3; i++)
        {
            float x = routeStartX + i * (routeWidth + routeGap);
            _routeButtons[i] = CreateStyledButton(
                routeObjectNames[i],
                card.transform,
                routeNames[i],
                new Vector2(routeWidth, 46f),
                new Vector2(x, 66f),
                _unselectedOptionColor,
                _unselectedTextColor,
                20f);
        }

        _summaryText = CreateLabel("SummaryText", card.transform, string.Empty, 18f, FontStyles.Normal, new Color(0.80f, 0.80f, 0.80f, 1f));
        CenterRect(_summaryText.rectTransform, new Vector2(500f, 32f), new Vector2(0f, 4f));

        _startDayButton = CreateStyledButton(
            "StartDayButton",
            card.transform,
            "Start Day",
            new Vector2(260f, 52f),
            new Vector2(0f, -56f),
            ActionButtonFallbackColor,
            ActionTextFallbackColor,
            24f);

        _reputationStatusText = CreateLabel("ReputationStatusText", card.transform, "Reputation: 50 (Neutral)", 22f, FontStyles.Normal, Color.white);
        CenterRect(_reputationStatusText.rectTransform, new Vector2(480f, 30f), new Vector2(0f, -142f));

        _repMinusButton = CreateStyledButton(
            "RepMinusButton",
            card.transform,
            "-15",
            new Vector2(158f, 44f),
            new Vector2(-171f, -188f),
            ActionButtonFallbackColor,
            ActionTextFallbackColor,
            20f);

        _repResetButton = CreateStyledButton(
            "RepResetButton",
            card.transform,
            "Reset (50)",
            new Vector2(158f, 44f),
            new Vector2(0f, -188f),
            ActionButtonFallbackColor,
            ActionTextFallbackColor,
            20f);

        _repPlusButton = CreateStyledButton(
            "RepPlusButton",
            card.transform,
            "+15",
            new Vector2(158f, 44f),
            new Vector2(171f, -188f),
            ActionButtonFallbackColor,
            ActionTextFallbackColor,
            20f);

        _skipDay1TimerButton = CreateStyledButton(
            "SkipTimerButton",
            card.transform,
            "Skip Day 1 Timer",
            new Vector2(260f, 42f),
            new Vector2(0f, -252f),
            ActionButtonFallbackColor,
            ActionTextFallbackColor,
            19f);

        _backButton = CreateStyledButton(
            "BackButton",
            card.transform,
            "Back",
            new Vector2(180f, 50f),
            new Vector2(0f, -325f),
            ActionButtonFallbackColor,
            ActionTextFallbackColor,
            24f);

        _debugPanelRoot.SetActive(false);
    }

    #region UI Fallback Factory Helpers

    private Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    private TextMeshProUGUI CreateLabel(
        string objectName,
        Transform parent,
        string text,
        float fontSize,
        FontStyles style,
        Color color)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        if (_sharedFont != null)
        {
            tmp.font = _sharedFont;
        }
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        tmp.raycastTarget = false;
        return tmp;
    }

    private Button CreateStyledButton(
        string objectName,
        Transform parent,
        string labelText,
        Vector2 size,
        Vector2 anchoredPos,
        Color bgColor,
        Color textColor,
        float fontSize)
    {
        Image bg = CreateImage(objectName, parent, bgColor);
        CenterRect(bg.rectTransform, size, anchoredPos);

        Button btn = bg.gameObject.AddComponent<Button>();
        btn.targetGraphic = bg;

        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colors.selectedColor = Color.white;
        btn.colors = colors;

        Navigation nav = btn.navigation;
        nav.mode = Navigation.Mode.None;
        btn.navigation = nav;

        TextMeshProUGUI label = CreateLabel("Text (TMP)", bg.transform, labelText, fontSize, FontStyles.Bold, textColor);
        StretchFull(label.rectTransform);

        bg.gameObject.AddComponent<UIHoverFeedback>();
        return btn;
    }

    private static Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root == null) return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (string.Equals(child.name, targetName, StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }

            Transform nested = FindChildRecursive(child, targetName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private static Button FindTemplateButton(Transform root)
    {
        Button[] buttons = root.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].GetComponentInChildren<TextMeshProUGUI>(true) != null)
            {
                return buttons[i];
            }
        }
        return buttons.Length > 0 ? buttons[0] : null;
    }

    private static void StripButtonSideEffects(GameObject buttonObject)
    {
        QuitGame[] quitComponents = buttonObject.GetComponents<QuitGame>();
        for (int i = 0; i < quitComponents.Length; i++)
        {
            DestroyImmediate(quitComponents[i]);
        }

        UIEventPublisher[] publishers = buttonObject.GetComponents<UIEventPublisher>();
        for (int i = 0; i < publishers.Length; i++)
        {
            DestroyImmediate(publishers[i]);
        }
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static void CenterRect(RectTransform rect, Vector2 size, Vector2 anchoredPosition)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;
        rect.localScale = Vector3.one;
    }

    #endregion
}
