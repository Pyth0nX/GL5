using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum SettingsSource
{
    NONE,
    MAIN_MENU,
    PAUSE_MENU,
    PHONE
}

/// <summary>
/// Controls the unified Settings panel. Can be opened from MainMenu, PauseMenu, or SmartPhone
/// via direct invocation (OpenFromCurrentContext) or by publishing the "OpenSettings" event.
/// Automatically closes if the calling Pause Menu or Phone is closed, and restores the caller upon pressing Back.
/// </summary>
public class SettingsPanelUI : MonoBehaviour
{
    public static SettingsPanelUI Instance { get; private set; }

    [Header("Root Panel")]
    [Tooltip("The root settings GameObject/Panel to activate/deactivate.")]
    [SerializeField] private GameObject _settingsPanelRoot;

    [Header("Sliders")]
    [SerializeField] private Slider _cameraSensitivitySlider;
    [SerializeField] private Slider _masterVolumeSlider;
    [SerializeField] private Slider _musicVolumeSlider;
    [SerializeField] private Slider _sfxVolumeSlider;
    [SerializeField] private Slider _voiceVolumeSlider;

    [Header("Value Labels (TMP)")]
    [SerializeField] private TMP_Text _cameraSensitivityText;
    [SerializeField] private TMP_Text _masterVolumeText;
    [SerializeField] private TMP_Text _musicVolumeText;
    [SerializeField] private TMP_Text _sfxVolumeText;
    [SerializeField] private TMP_Text _voiceVolumeText;

    [Header("Navigation")]
    [SerializeField] private Button _backButton;

    private SettingsSource _openedFrom = SettingsSource.NONE;
    private bool _isUpdatingUI = false;
    private bool _isSubscribed = false;
    private PhoneStateMachine _cachedPhoneStateMachine;

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

        if (_settingsPanelRoot == null)
        {
            _settingsPanelRoot = gameObject;
        }

        TrySubscribeEventBus();
    }

    private void Start()
    {
        HookSliderListeners();

        if (_backButton != null)
        {
            _backButton.onClick.AddListener(ClosePanel);
        }

        TrySubscribeEventBus();

        // Ensure the visual panel is initially hidden
        if (_settingsPanelRoot != null)
        {
            _settingsPanelRoot.SetActive(false);
        }
    }

    private void Update()
    {
        if (!_isSubscribed)
        {
            TrySubscribeEventBus();
        }

        // Automatic synchronization: If the caller (Pause Menu or Phone) was closed, close Settings as well
        if (IsPanelOpen())
        {
            if (_openedFrom == SettingsSource.PAUSE_MENU)
            {
                if (UIManager.Instance != null && UIManager.Instance.GetCurrentState() != UIState.MENU)
                {
                    ClosePanelSilently();
                }
            }
            else if (_openedFrom == SettingsSource.PHONE)
            {
                if (UIManager.Instance != null && UIManager.Instance.GetCurrentState() != UIState.PHONE)
                {
                    ClosePanelSilently();
                }
                else
                {
                    if (_cachedPhoneStateMachine == null)
                    {
                        _cachedPhoneStateMachine = UnityEngine.Object.FindAnyObjectByType<PhoneStateMachine>();
                    }

                    if (_cachedPhoneStateMachine != null && (!_cachedPhoneStateMachine.gameObject.activeInHierarchy || _cachedPhoneStateMachine.GetCurrentState() != PhoneState.SETTINGS))
                    {
                        ClosePanelSilently();
                    }
                }
            }
        }
    }

    private void OnDestroy()
    {
        UnhookSliderListeners();

        if (_backButton != null)
        {
            _backButton.onClick.RemoveListener(ClosePanel);
        }

        if (_isSubscribed && EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe("OpenSettings", OpenFromCurrentContext);
            EventBus.Instance.Unsubscribe("CloseSettings", ClosePanelSilently);
            EventBus.Instance.Unsubscribe("PauseMenu_Closed", OnPauseMenuClosed);
            EventBus.Instance.Unsubscribe("Phone_Closed", OnPhoneClosedParameterless);
            EventBus.Instance.Unsubscribe<int>("Phone_Closed", OnPhoneClosedWithParam);
            _isSubscribed = false;
        }
    }

    public void TrySubscribeEventBus()
    {
        if (_isSubscribed) return;

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe("OpenSettings", OpenFromCurrentContext);
            EventBus.Instance.Subscribe("CloseSettings", ClosePanelSilently);
            EventBus.Instance.Subscribe("PauseMenu_Closed", OnPauseMenuClosed);
            EventBus.Instance.Subscribe("Phone_Closed", OnPhoneClosedParameterless);
            EventBus.Instance.Subscribe<int>("Phone_Closed", OnPhoneClosedWithParam);
            _isSubscribed = true;
        }
    }

    private void OnPauseMenuClosed()
    {
        if (_openedFrom == SettingsSource.PAUSE_MENU && IsPanelOpen())
        {
            ClosePanelSilently();
        }
    }

    private void OnPhoneClosedWithParam(int _)
    {
        OnPhoneClosedParameterless();
    }

    private void OnPhoneClosedParameterless()
    {
        if (_openedFrom == SettingsSource.PHONE && IsPanelOpen())
        {
            ClosePanelSilently();
        }
    }

    public bool IsPanelOpen()
    {
        return _settingsPanelRoot != null && _settingsPanelRoot.activeSelf;
    }

    private void HookSliderListeners()
    {
        if (_cameraSensitivitySlider != null)
        {
            _cameraSensitivitySlider.onValueChanged.AddListener(OnCameraSensitivitySliderChanged);
        }

        if (_masterVolumeSlider != null)
        {
            _masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeSliderChanged);
        }

        if (_musicVolumeSlider != null)
        {
            _musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeSliderChanged);
        }

        if (_sfxVolumeSlider != null)
        {
            _sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeSliderChanged);
        }

        if (_voiceVolumeSlider != null)
        {
            _voiceVolumeSlider.onValueChanged.AddListener(OnVoiceVolumeSliderChanged);
        }
    }

    private void UnhookSliderListeners()
    {
        if (_cameraSensitivitySlider != null)
        {
            _cameraSensitivitySlider.onValueChanged.RemoveListener(OnCameraSensitivitySliderChanged);
        }

        if (_masterVolumeSlider != null)
        {
            _masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeSliderChanged);
        }

        if (_musicVolumeSlider != null)
        {
            _musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeSliderChanged);
        }

        if (_sfxVolumeSlider != null)
        {
            _sfxVolumeSlider.onValueChanged.RemoveListener(OnSfxVolumeSliderChanged);
        }

        if (_voiceVolumeSlider != null)
        {
            _voiceVolumeSlider.onValueChanged.RemoveListener(OnVoiceVolumeSliderChanged);
        }
    }

    /// <summary>
    /// Opens the settings panel dynamically detecting current game context (Main Menu, Pause Menu, or Phone).
    /// Can be linked directly in button OnClick or invoked via "OpenSettings" on EventBus.
    /// </summary>
    public void OpenFromCurrentContext()
    {
        Debug.Log("[SettingsPanelUI] OpenFromCurrentContext invoked.");

        if (UIManager.Instance != null)
        {
            GameObject pausePanel = UIManager.Instance.GetPauseMenuPanel();
            if (pausePanel != null && pausePanel.activeInHierarchy)
            {
                OpenPanel(SettingsSource.PAUSE_MENU);
                return;
            }

            UIState currentState = UIManager.Instance.GetCurrentState();
            if (currentState == UIState.MENU)
            {
                OpenPanel(SettingsSource.PAUSE_MENU);
                return;
            }

            if (currentState == UIState.PHONE)
            {
                OpenPanel(SettingsSource.PHONE);
                return;
            }

            PhoneStateMachine phoneSM = UnityEngine.Object.FindAnyObjectByType<PhoneStateMachine>();
            if (phoneSM != null && phoneSM.gameObject.activeInHierarchy)
            {
                OpenPanel(SettingsSource.PHONE);
                return;
            }

            GameObject mainMenuUI = UIManager.Instance.GetMainMenuUI();
            if (currentState == UIState.MAIN_MENU || (mainMenuUI != null && mainMenuUI.activeInHierarchy))
            {
                OpenPanel(SettingsSource.MAIN_MENU);
                return;
            }
        }

        PhoneStateMachine activePhone = UnityEngine.Object.FindAnyObjectByType<PhoneStateMachine>();
        if (activePhone != null && activePhone.gameObject.activeInHierarchy)
        {
            OpenPanel(SettingsSource.PHONE);
            return;
        }

        OpenPanel(SettingsSource.NONE);
    }

    public void OpenFromMainMenu() => OpenPanel(SettingsSource.MAIN_MENU);
    public void OpenFromPauseMenu() => OpenPanel(SettingsSource.PAUSE_MENU);
    public void OpenFromPhone() => OpenPanel(SettingsSource.PHONE);

    public void OpenPanel(SettingsSource source)
    {
        _openedFrom = source;
        RefreshUIFromSettings();

        if (source == SettingsSource.PAUSE_MENU)
        {
            // Temporarily hide the pause menu panel so settings takes focus
            if (UIManager.Instance != null)
            {
                GameObject pausePanel = UIManager.Instance.GetPauseMenuPanel();
                if (pausePanel != null) pausePanel.SetActive(false);
            }
        }

        if (_settingsPanelRoot != null)
        {
            _settingsPanelRoot.SetActive(true);
            _settingsPanelRoot.transform.SetAsLastSibling();
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UnlockMouse();
        }
    }

    /// <summary>
    /// Normal Back button action: Closes settings and restores the previous menu.
    /// </summary>
    public void ClosePanel()
    {
        if (_settingsPanelRoot != null)
        {
            _settingsPanelRoot.SetActive(false);
        }

        SettingsSource returnSource = _openedFrom;
        _openedFrom = SettingsSource.NONE;

        switch (returnSource)
        {
            case SettingsSource.MAIN_MENU:
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.ChangeState((int)UIState.MAIN_MENU);
                    GameObject mainMenuUI = UIManager.Instance.GetMainMenuUI();
                    if (mainMenuUI != null) mainMenuUI.SetActive(true);
                }
                break;

            case SettingsSource.PAUSE_MENU:
                if (UIManager.Instance != null && UIManager.Instance.GetCurrentState() == UIState.MENU)
                {
                    GameObject pausePanel = UIManager.Instance.GetPauseMenuPanel();
                    if (pausePanel != null) pausePanel.SetActive(true);
                    UIManager.Instance.UnlockMouse();
                }
                break;

            case SettingsSource.PHONE:
                if (UIManager.Instance != null && UIManager.Instance.GetCurrentState() == UIState.PHONE)
                {
                    if (_cachedPhoneStateMachine == null)
                    {
                        _cachedPhoneStateMachine = UnityEngine.Object.FindAnyObjectByType<PhoneStateMachine>();
                    }
                    if (_cachedPhoneStateMachine != null)
                    {
                        _cachedPhoneStateMachine.ChangeState((int)PhoneState.IDLE);
                    }
                }
                break;

            default:
                if (UIManager.Instance != null)
                {
                    UIState state = UIManager.Instance.GetCurrentState();
                    if (state == UIState.DEFAULT)
                    {
                        UIManager.Instance.LockMouse();
                    }
                    else
                    {
                        UIManager.Instance.UnlockMouse();
                    }
                }
                break;
        }
    }

    /// <summary>
    /// Closes settings silently without reopening the previous menu.
    /// Used when the pause menu itself or the mobile phone is closed by the player.
    /// </summary>
    public void ClosePanelSilently()
    {
        if (_settingsPanelRoot != null)
        {
            _settingsPanelRoot.SetActive(false);
        }

        _openedFrom = SettingsSource.NONE;

        if (UIManager.Instance != null)
        {
            UIState state = UIManager.Instance.GetCurrentState();
            if (state == UIState.DEFAULT)
            {
                UIManager.Instance.LockMouse();
            }
        }
    }

    public void RefreshUIFromSettings()
    {
        _isUpdatingUI = true;

        float sens = SettingsManager.Instance != null ? SettingsManager.Instance.CameraSensitivity : 1.75f;
        float master = SettingsManager.Instance != null ? SettingsManager.Instance.MasterVolume : 1.0f;
        float music = SettingsManager.Instance != null ? SettingsManager.Instance.MusicVolume : 0.35f;
        float sfx = SettingsManager.Instance != null ? SettingsManager.Instance.SfxVolume : 1.0f;
        float voice = SettingsManager.Instance != null ? SettingsManager.Instance.VoiceVolume : 1.0f;

        if (_cameraSensitivitySlider != null) _cameraSensitivitySlider.value = sens;
        if (_masterVolumeSlider != null) _masterVolumeSlider.value = master;
        if (_musicVolumeSlider != null) _musicVolumeSlider.value = music;
        if (_sfxVolumeSlider != null) _sfxVolumeSlider.value = sfx;
        if (_voiceVolumeSlider != null) _voiceVolumeSlider.value = voice;

        UpdateCameraSensitivityLabel(sens);
        UpdateMasterVolumeLabel(master);
        UpdateMusicVolumeLabel(music);
        UpdateSfxVolumeLabel(sfx);
        UpdateVoiceVolumeLabel(voice);

        _isUpdatingUI = false;
    }

    private void OnCameraSensitivitySliderChanged(float value)
    {
        UpdateCameraSensitivityLabel(value);
        if (_isUpdatingUI) return;
        SettingsManager.Instance?.SetCameraSensitivity(value);
    }

    private void OnMasterVolumeSliderChanged(float value)
    {
        UpdateMasterVolumeLabel(value);
        if (_isUpdatingUI) return;
        SettingsManager.Instance?.SetMasterVolume(value);
    }

    private void OnMusicVolumeSliderChanged(float value)
    {
        UpdateMusicVolumeLabel(value);
        if (_isUpdatingUI) return;
        SettingsManager.Instance?.SetMusicVolume(value);
    }

    private void OnSfxVolumeSliderChanged(float value)
    {
        UpdateSfxVolumeLabel(value);
        if (_isUpdatingUI) return;
        SettingsManager.Instance?.SetSfxVolume(value);
    }

    private void OnVoiceVolumeSliderChanged(float value)
    {
        UpdateVoiceVolumeLabel(value);
        if (_isUpdatingUI) return;
        SettingsManager.Instance?.SetVoiceVolume(value);
    }

    private void UpdateCameraSensitivityLabel(float value)
    {
        if (_cameraSensitivityText != null)
        {
            _cameraSensitivityText.text = $"Camera Sensitivity: {value:0.00}";
        }
    }

    private void UpdateMasterVolumeLabel(float value)
    {
        if (_masterVolumeText != null)
        {
            int percent = Mathf.RoundToInt(value * 100f);
            _masterVolumeText.text = $"Master Volume: {percent}%";
        }
    }

    private void UpdateMusicVolumeLabel(float value)
    {
        if (_musicVolumeText != null)
        {
            int percent = Mathf.RoundToInt(value * 100f);
            _musicVolumeText.text = $"Music Volume: {percent}%";
        }
    }

    private void UpdateSfxVolumeLabel(float value)
    {
        if (_sfxVolumeText != null)
        {
            int percent = Mathf.RoundToInt(value * 100f);
            _sfxVolumeText.text = $"SFX Volume: {percent}%";
        }
    }

    private void UpdateVoiceVolumeLabel(float value)
    {
        if (_voiceVolumeText != null)
        {
            int percent = Mathf.RoundToInt(value * 100f);
            _voiceVolumeText.text = $"Voice Volume: {percent}%";
        }
    }
}
