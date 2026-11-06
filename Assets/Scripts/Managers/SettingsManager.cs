using System;
using UnityEngine;

/// <summary>
/// Central manager for game settings (Audio volumes and Camera sensitivity).
/// Automatically persists preferences via PlayerPrefs and applies changes
/// across systems (AudioManager, PlayerController, AudioListener) using weak coupling and events.
/// </summary>
public class SettingsManager : MonoBehaviour
{
    public const string PrefKeyCameraSensitivity = "Setting_CameraSensitivity";
    public const string PrefKeyMasterVolume = "Setting_MasterVolume";
    public const string PrefKeyMusicVolume = "Setting_MusicVolume";
    public const string PrefKeySfxVolume = "Setting_SfxVolume";
    public const string PrefKeyVoiceVolume = "Setting_VoiceVolume";

    public const float DefaultCameraSensitivity = 1.75f;
    public const float DefaultMasterVolume = 1.0f;
    public const float DefaultMusicVolume = 0.35f;
    public const float DefaultSfxVolume = 1.0f;
    public const float DefaultVoiceVolume = 1.0f;

    public const string EventCameraSensitivityChanged = "Settings_CameraSensitivityChanged";
    public const string EventMasterVolumeChanged = "Settings_MasterVolumeChanged";
    public const string EventMusicVolumeChanged = "Settings_MusicVolumeChanged";
    public const string EventSfxVolumeChanged = "Settings_SfxVolumeChanged";
    public const string EventVoiceVolumeChanged = "Settings_VoiceVolumeChanged";

    public static SettingsManager Instance { get; private set; }

    [Header("Current Runtime Values")]
    [SerializeField] private float _cameraSensitivity = DefaultCameraSensitivity;
    [SerializeField, Range(0f, 1f)] private float _masterVolume = DefaultMasterVolume;
    [SerializeField, Range(0f, 1f)] private float _musicVolume = DefaultMusicVolume;
    [SerializeField, Range(0f, 1f)] private float _sfxVolume = DefaultSfxVolume;
    [SerializeField, Range(0f, 1f)] private float _voiceVolume = DefaultVoiceVolume;

    public float CameraSensitivity => _cameraSensitivity;
    public float MasterVolume => _masterVolume;
    public float MusicVolume => _musicVolume;
    public float SfxVolume => _sfxVolume;
    public float VoiceVolume => _voiceVolume;

    public event Action<float> OnCameraSensitivityChanged;
    public event Action<float> OnMasterVolumeChanged;
    public event Action<float> OnMusicVolumeChanged;
    public event Action<float> OnSfxVolumeChanged;
    public event Action<float> OnVoiceVolumeChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
            LoadSettings();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        ApplyAllSettings();
    }

    public void LoadSettings()
    {
        _cameraSensitivity = PlayerPrefs.GetFloat(PrefKeyCameraSensitivity, DefaultCameraSensitivity);
        _masterVolume = PlayerPrefs.GetFloat(PrefKeyMasterVolume, DefaultMasterVolume);
        _musicVolume = PlayerPrefs.GetFloat(PrefKeyMusicVolume, DefaultMusicVolume);
        _sfxVolume = PlayerPrefs.GetFloat(PrefKeySfxVolume, DefaultSfxVolume);
        _voiceVolume = PlayerPrefs.GetFloat(PrefKeyVoiceVolume, DefaultVoiceVolume);
    }

    public void SaveSettings()
    {
        PlayerPrefs.SetFloat(PrefKeyCameraSensitivity, _cameraSensitivity);
        PlayerPrefs.SetFloat(PrefKeyMasterVolume, _masterVolume);
        PlayerPrefs.SetFloat(PrefKeyMusicVolume, _musicVolume);
        PlayerPrefs.SetFloat(PrefKeySfxVolume, _sfxVolume);
        PlayerPrefs.SetFloat(PrefKeyVoiceVolume, _voiceVolume);
        PlayerPrefs.Save();
    }

    public void SetCameraSensitivity(float value)
    {
        _cameraSensitivity = Mathf.Max(0.05f, value);
        PlayerPrefs.SetFloat(PrefKeyCameraSensitivity, _cameraSensitivity);
        PlayerPrefs.Save();

        ApplyCameraSensitivity();
        OnCameraSensitivityChanged?.Invoke(_cameraSensitivity);
        EventBus.Instance?.Publish(EventCameraSensitivityChanged, _cameraSensitivity);
    }

    public void SetMasterVolume(float value)
    {
        _masterVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(PrefKeyMasterVolume, _masterVolume);
        PlayerPrefs.Save();

        ApplyMasterVolume();
        OnMasterVolumeChanged?.Invoke(_masterVolume);
        EventBus.Instance?.Publish(EventMasterVolumeChanged, _masterVolume);
    }

    public void SetMusicVolume(float value)
    {
        _musicVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(PrefKeyMusicVolume, _musicVolume);
        PlayerPrefs.Save();

        ApplyMusicVolume();
        OnMusicVolumeChanged?.Invoke(_musicVolume);
        EventBus.Instance?.Publish(EventMusicVolumeChanged, _musicVolume);
    }

    public void SetSfxVolume(float value)
    {
        _sfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(PrefKeySfxVolume, _sfxVolume);
        PlayerPrefs.Save();

        ApplySfxVolume();
        OnSfxVolumeChanged?.Invoke(_sfxVolume);
        EventBus.Instance?.Publish(EventSfxVolumeChanged, _sfxVolume);
    }

    public void SetVoiceVolume(float value)
    {
        _voiceVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(PrefKeyVoiceVolume, _voiceVolume);
        PlayerPrefs.Save();

        ApplyVoiceVolume();
        OnVoiceVolumeChanged?.Invoke(_voiceVolume);
        EventBus.Instance?.Publish(EventVoiceVolumeChanged, _voiceVolume);
    }

    public void ApplyAllSettings()
    {
        ApplyCameraSensitivity();
        ApplyMasterVolume();
        ApplyMusicVolume();
        ApplySfxVolume();
        ApplyVoiceVolume();
    }

    private void ApplyCameraSensitivity()
    {
        PlayerController[] players = FindObjectsByType<PlayerController>(FindObjectsInactive.Include);
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null)
            {
                players[i].SetMouseSensitivity(_cameraSensitivity);
            }
        }
    }

    private void ApplyMasterVolume()
    {
        AudioListener.volume = _masterVolume;
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMasterVolume(_masterVolume);
        }
    }

    private void ApplyMusicVolume()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMusicVolume(_musicVolume);
        }
    }

    private void ApplySfxVolume()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetSfxVolume(_sfxVolume);
        }
    }

    private void ApplyVoiceVolume()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetVoiceVolume(_voiceVolume);
        }
    }
}
