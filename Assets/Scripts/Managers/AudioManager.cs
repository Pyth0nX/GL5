using UnityEngine;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [SerializeField]
    private Dictionary<string, AudioClip> _sfxSounds;

    [SerializeField]
    private Dictionary<string, AudioClip> _bgMusics;

    [SerializeField]
    private AudioSource _musicSource;

    [SerializeField]
    private AudioSource _sfxSource;

    [SerializeField]
    private AudioSource _voiceSource;

    private float _masterVolume = 1f;
    private float _musicVolume = 1f;
    private float _sfxVolume = 1f;
    private float _voiceVolume = 1f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Subscribe to events to listen to changes in the music or to play SFX sounds
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe<string>("PlaySFX", PlaySFXSound);
            EventBus.Instance.Subscribe<string>("ChangeMusic", ChangeMusic);
        }

        if (SettingsManager.Instance != null)
        {
            _masterVolume = SettingsManager.Instance.MasterVolume;
            _musicVolume = SettingsManager.Instance.MusicVolume;
            _sfxVolume = SettingsManager.Instance.SfxVolume;
            _voiceVolume = SettingsManager.Instance.VoiceVolume;
            UpdateSourceVolumes();
        }

        if (_musicSource != null && !_musicSource.isPlaying && _bgMusics != null && _bgMusics.ContainsKey("Menu"))
        {
            _musicSource.clip = _bgMusics["Menu"];
            _musicSource.Play();
        }
    }

    public void SetMasterVolume(float volume)
    {
        _masterVolume = Mathf.Clamp01(volume);
        UpdateSourceVolumes();
    }

    public void SetMusicVolume(float volume)
    {
        _musicVolume = Mathf.Clamp01(volume);
        UpdateSourceVolumes();
    }

    public void SetSfxVolume(float volume)
    {
        _sfxVolume = Mathf.Clamp01(volume);
        UpdateSourceVolumes();
    }

    public void SetVoiceVolume(float volume)
    {
        _voiceVolume = Mathf.Clamp01(volume);
        UpdateSourceVolumes();
    }

    private void UpdateSourceVolumes()
    {
        if (_musicSource != null)
        {
            _musicSource.volume = _musicVolume * _masterVolume;
        }

        if (_sfxSource != null)
        {
            _sfxSource.volume = _sfxVolume * _masterVolume;
        }

        if (_voiceSource != null)
        {
            _voiceSource.volume = _voiceVolume * _masterVolume;
        }
    }

    public void PlaySFXSound(string soundToPlay)
    {
        if (_sfxSounds == null || !_sfxSounds.ContainsKey(soundToPlay))
        {
            Debug.LogError($"[AudioManager][SFX]:clip named {soundToPlay} not found");
            return;
        }
        if (_sfxSource != null)
        {
            _sfxSource.PlayOneShot(_sfxSounds[soundToPlay]);
        }
    }

    public void ChangeMusic(string soundToPlay)
    {
        if (_bgMusics == null || !_bgMusics.ContainsKey(soundToPlay))
        {
            Debug.LogError($"[AudioManager][Music]:clip named {soundToPlay} not found");
            return;
        }

        if (_musicSource != null && _musicSource.clip == _bgMusics[soundToPlay] && _musicSource.isPlaying)
        {
            return;
        }

        if (_musicSource != null)
        {
            _musicSource.clip = _bgMusics[soundToPlay];
            _musicSource.Play();
        }
    }
}
