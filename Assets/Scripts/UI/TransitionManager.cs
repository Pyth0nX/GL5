using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class TransitionManager : MonoBehaviour
{
    public static TransitionManager Instance { get; private set; }

    [Header("UI Reference")]
    [Tooltip("Assign a full-screen black image here. Ensure Raycast Target is OFF.")]
    [SerializeField] private Image _fadeImage;

    [Header("Transition Text")]
    [Tooltip("Text displayed during black screen transitions (e.g. '6 hours later...').")]
    [SerializeField] private TMP_Text _transitionText;

    [Header("Settings")]
    [SerializeField] private float _fadeDuration = 1.0f;
    [SerializeField] private float _blackScreenDuration = 1.5f;

    [Header("Text Settings")]
    [SerializeField] private float _textFadeInDuration = 0.5f;
    [SerializeField] private float _textDisplayDuration = 2.0f;
    [SerializeField] private float _textFadeOutDuration = 0.5f;

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
            return;
        }

        if (_transitionText == null)
        {
            _transitionText = GetComponentInChildren<TMP_Text>(true);
        }
    }

    private void Start()
    {
        if (_transitionText != null)
        {
            Color c = _transitionText.color;
            c.a = 0f;
            _transitionText.color = c;
            _transitionText.gameObject.SetActive(false);
        }

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe("SleepTransition", OnSleepTransition);
            EventBus.Instance.Subscribe("FadeOut", StartFadeOut);
            EventBus.Instance.Subscribe("FadeIn", StartFadeIn);
            EventBus.Instance.Subscribe("ClassesTransition", OnClassesTransition);
        }
    }

    private void OnDisable()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe("SleepTransition", OnSleepTransition);
            EventBus.Instance.Unsubscribe("FadeOut", StartFadeOut);
            EventBus.Instance.Unsubscribe("FadeIn", StartFadeIn);
            EventBus.Instance.Unsubscribe("ClassesTransition", OnClassesTransition);
        }
    }

    private void OnDestroy()
    {
        if (_transitionText != null)
        {
            _transitionText.DOKill();
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

    private void OnClassesTransition()
    {
        PlayTextTransition("6 hours later...");
    }

    public void PlayTextTransition(string message = "6 hours later...", Action onMiddle = null, Action onComplete = null)
    {
        StartCoroutine(TextTransitionRoutine(message, onMiddle, onComplete));
    }

    private IEnumerator TextTransitionRoutine(string message, Action onMiddle, Action onComplete)
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.DisableMovementMap();
            InputManager.Instance.DisableInteractionsMap();
        }

        // Fade Out to black
        yield return FadeRoutine(0f, 1f);

        onMiddle?.Invoke();

        // Show text progressively over _textFadeInDuration with DOTween
        if (_transitionText != null)
        {
            if (!string.IsNullOrEmpty(message))
            {
                _transitionText.text = message;
            }
            Color c = _transitionText.color;
            c.a = 0f;
            _transitionText.color = c;
            _transitionText.gameObject.SetActive(true);
            _transitionText.DOKill();
            yield return _transitionText.DOFade(1f, _textFadeInDuration).WaitForCompletion();
        }

        // Hold text visible
        yield return new WaitForSeconds(_textDisplayDuration);

        // Hide text progressively over _textFadeOutDuration with DOTween
        if (_transitionText != null)
        {
            _transitionText.DOKill();
            yield return _transitionText.DOFade(0f, _textFadeOutDuration).WaitForCompletion();
            _transitionText.gameObject.SetActive(false);
        }

        // Fade In back from black
        yield return FadeRoutine(1f, 0f);

        onComplete?.Invoke();

        if (InputManager.Instance != null)
        {
            InputManager.Instance.EnableMovementMap();
            InputManager.Instance.EnableInteractionsMap();
        }
    }

    private IEnumerator SleepTransitionRoutine()
    {
        if (InputManager.Instance != null)
        {
            InputManager.Instance.DisableMovementMap();
            InputManager.Instance.DisableInteractionsMap();
        }

        // Fade Out
        yield return FadeRoutine(0f, 1f);

  
        yield return new WaitForSeconds(_blackScreenDuration);

        // Fade In
        yield return FadeRoutine(1f, 0f);

        if (InputManager.Instance != null)
        {
            InputManager.Instance.EnableInteractionsMap();
            InputManager.Instance.EnableMovementMap();
        }
        // Wait in black and trigger next day
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Publish("NextDay");
        }
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
    }
}
