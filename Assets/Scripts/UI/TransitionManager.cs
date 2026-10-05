using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TransitionManager : MonoBehaviour
{
    public static TransitionManager Instance { get; private set; }

    [Header("UI Reference")]
    [Tooltip("Assign a full-screen black image here. Ensure Raycast Target is OFF.")]
    [SerializeField] private Image _fadeImage;

    [Header("Settings")]
    [SerializeField] private float _fadeDuration = 1.0f;
    [SerializeField] private float _blackScreenDuration = 1.5f;

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

    private void Start()
    {
        EventBus.Instance.Subscribe("SleepTransition", OnSleepTransition);
        EventBus.Instance.Subscribe("FadeOut", StartFadeOut);
        EventBus.Instance.Subscribe("FadeIn", StartFadeIn);
        
        // Start game with a fade in from black
        if (_fadeImage != null)
        {
            Color c = _fadeImage.color;
            c.a = 1f;
            _fadeImage.color = c;
            _fadeImage.gameObject.SetActive(true);
            StartCoroutine(FadeRoutine(1f, 0f));
        }
    }

    private void OnDestroy()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe("SleepTransition", OnSleepTransition);
            EventBus.Instance.Unsubscribe("FadeOut", StartFadeOut);
            EventBus.Instance.Unsubscribe("FadeIn", StartFadeIn);
        }
    }

    private void StartFadeOut()
    {
        StartCoroutine(FadeRoutine(0f, 1f));
    }

    private void StartFadeIn()
    {
        StartCoroutine(FadeRoutine(1f, 0f));
    }

    private void OnSleepTransition()
    {
        StartCoroutine(SleepTransitionRoutine());
    }

    private IEnumerator SleepTransitionRoutine()
    {
        Debug.Log("[TransitionManager] Starting Sleep Transition...");
        
        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player != null) player.DisableInput();

        // 1. Fade Out
        yield return FadeRoutine(0f, 1f);

        // 2. Wait in black and trigger next day
        EventBus.Instance.Publish("NextDay");
        yield return new WaitForSeconds(_blackScreenDuration);

        // 3. Fade In
        yield return FadeRoutine(1f, 0f);

        if (player != null) player.EnableInput();
        
        Debug.Log("[TransitionManager] Sleep Transition Complete.");
    }

    private IEnumerator FadeRoutine(float startAlpha, float endAlpha)
    {
        if (_fadeImage == null) yield break;

        _fadeImage.gameObject.SetActive(true);
        float elapsed = 0f;
        Color c = _fadeImage.color;

        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(startAlpha, endAlpha, elapsed / _fadeDuration);
            _fadeImage.color = c;
            yield return null;
        }

        c.a = endAlpha;
        _fadeImage.color = c;

        if (endAlpha == 0f)
        {
            _fadeImage.gameObject.SetActive(false);
        }
    }
}
