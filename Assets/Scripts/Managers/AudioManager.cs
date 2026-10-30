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
        //Suscribe to events to listen to changes in the music or to play SFX sounds
        EventBus.Instance.Subscribe<string>("PlaySFX",PlaySFXSound);
        EventBus.Instance.Subscribe<string>("ChangeMusic", ChangeMusic);

        if (_musicSource != null && !_musicSource.isPlaying && _bgMusics != null && _bgMusics.ContainsKey("Menu"))
        {
            _musicSource.clip = _bgMusics["Menu"];
            _musicSource.Play();
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    public void PlaySFXSound(string soundToPlay)
    {
        if (_sfxSounds == null || !_sfxSounds.ContainsKey(soundToPlay))
        {
            Debug.LogError($"[AudioManager][SFX]:clip named {soundToPlay} not found");
            return;
        }
        _sfxSource.PlayOneShot(_sfxSounds[soundToPlay]);
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
