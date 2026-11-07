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
    [Tooltip("Text displayed during black screen transitions (e.g. '5 Hours Later').")]
    [SerializeField] private TMP_Text _transitionText;

    [Header("Settings")]
    [SerializeField] private float _fadeDuration = 1.0f;
    [SerializeField] private float _blackScreenDuration = 1.5f;

    [Header("Text Settings")]
    [SerializeField] private float _textFadeInDuration = 0.5f;
    [SerializeField] private float _textDisplayDuration = 2.0f;
    [SerializeField] private float _textFadeOutDuration = 0.5f;

    private bool _isTransitioning;

    public bool IsTransitioning => _isTransitioning;

    private void Awake()
    {
        ConfigureTransitionCanvas();
        ConfigureFullScreenFade();

        if (Instance == null)
        {
            Instance = this;
            EnsureTransitionText();
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnValidate()
    {
        ConfigureFullScreenFade();
    }

    private void ConfigureTransitionCanvas()
    {
        Canvas transitionCanvas = GetComponent<Canvas>();
        if (transitionCanvas != null)
        {
            transitionCanvas.overrideSorting = true;
            transitionCanvas.sortingOrder = 32760;
        }
    }

    private void ConfigureFullScreenFade()
    {
        if (_fadeImage == null) return;

        RectTransform managerRect = transform as RectTransform;
        if (managerRect != null && managerRect.parent is RectTransform)
        {
            managerRect.anchorMin = Vector2.zero;
            managerRect.anchorMax = Vector2.one;
            managerRect.pivot = new Vector2(0.5f, 0.5f);
            managerRect.anchoredPosition = Vector2.zero;
            managerRect.sizeDelta = Vector2.zero;
            managerRect.localScale = Vector3.one;
        }

        RectTransform fadeRect = _fadeImage.rectTransform;
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.pivot = new Vector2(0.5f, 0.5f);
        fadeRect.anchoredPosition = Vector2.zero;
        fadeRect.sizeDelta = Vector2.zero;
        fadeRect.localScale = Vector3.one;
    }

    private void EnsureTransitionText()
    {
        if (_transitionText == null)
        {
            _transitionText = GetComponentInChildren<TMP_Text>(true);
        }

        if (_transitionText == null && _fadeImage != null)
        {
            GameObject messageObject = new GameObject(
                "TransitionMessage",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            messageObject.layer = _fadeImage.gameObject.layer;
            messageObject.transform.SetParent(_fadeImage.transform.parent != null ? _fadeImage.transform.parent : _fadeImage.transform, false);
            _transitionText = messageObject.GetComponent<TextMeshProUGUI>();
        }

        if (_transitionText == null) return;

        RectTransform textRect = _transitionText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = Vector2.zero;
        textRect.localScale = Vector3.one;

        _transitionText.alignment = TextAlignmentOptions.Center;
        _transitionText.fontSize = 72f;
        _transitionText.enableAutoSizing = true;
        _transitionText.fontSizeMin = 26f;
        _transitionText.fontSizeMax = 72f;
        _transitionText.fontStyle = FontStyles.Normal;
        _transitionText.richText = true;
        _transitionText.margin = new Vector4(120f, 80f, 120f, 80f);
        _transitionText.color = new Color(1f, 1f, 1f, 0f);
        _transitionText.raycastTarget = false;
        _transitionText.text = string.Empty;
        _transitionText.gameObject.SetActive(false);
        _transitionText.transform.SetAsLastSibling();
    }

    private void SetTransitionMessage(string message)
    {
        EnsureTransitionText();
        if (_transitionText == null) return;

        _transitionText.DOKill();
        bool showMessage = !string.IsNullOrWhiteSpace(message);
        _transitionText.text = showMessage ? message : string.Empty;
        _transitionText.gameObject.SetActive(showMessage);
        _transitionText.transform.SetAsLastSibling();

        Color messageColor = _transitionText.color;
        messageColor.a = 0f;
        _transitionText.color = messageColor;
    }

    private void SetMessageAlpha(float alpha)
    {
        if (_transitionText == null || !_transitionText.gameObject.activeSelf) return;

        Color messageColor = _transitionText.color;
        messageColor.a = alpha;
        _transitionText.color = messageColor;
    }

    private void Start()
    {
        EnsureTransitionText();

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
        SetTransitionMessage(null);
        StartCoroutine(FadeRoutine(0f, 1f));
    }

    private void StartFadeIn()
    {
        StartCoroutine(FadeRoutine(1f, 0f));
    }

    private void OnSleepTransition()
    {
        if (_isTransitioning) return;
        StartCoroutine(SleepTransitionRoutine());
    }

    private void OnClassesTransition()
    {
        PlayTextTransition("6 hours later...");
    }

    public void PlaySchoolClass(string completionEvent)
    {
        if (_isTransitioning) return;
        StartCoroutine(SchoolClassRoutine(completionEvent));
    }

    public void PlayMessageCard(string message, float holdDuration)
    {
        if (_isTransitioning)
        {
            ReturnPlayerToIdle();
            return;
        }

        StartCoroutine(MessageCardRoutine(message, Mathf.Max(0f, holdDuration)));
    }

    public void PlayTextTransition(string message = "6 hours later...", Action onMiddle = null, Action onComplete = null)
    {
        if (_isTransitioning) return;
        StartCoroutine(TextTransitionRoutine(message, onMiddle, onComplete));
    }

    private IEnumerator TextTransitionRoutine(string message, Action onMiddle, Action onComplete)
    {
        _isTransitioning = true;

        if (InputManager.Instance != null)
        {
            InputManager.Instance.DisableMovementMap();
            InputManager.Instance.DisableInteractionsMap();
        }

        SetTransitionMessage(null);
        yield return FadeRoutine(0f, 1f);

        onMiddle?.Invoke();

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
            _transitionText.transform.SetAsLastSibling();
            _transitionText.DOKill();
            yield return _transitionText.DOFade(1f, _textFadeInDuration).WaitForCompletion();
        }

        yield return new WaitForSeconds(_textDisplayDuration);

        if (_transitionText != null)
        {
            _transitionText.DOKill();
            yield return _transitionText.DOFade(0f, _textFadeOutDuration).WaitForCompletion();
            _transitionText.gameObject.SetActive(false);
        }

        yield return FadeRoutine(1f, 0f);

        onComplete?.Invoke();

        if (InputManager.Instance != null)
        {
            InputManager.Instance.EnableMovementMap();
            InputManager.Instance.EnableInteractionsMap();
        }

        ReturnPlayerToIdle();
        _isTransitioning = false;
    }

    private IEnumerator SchoolClassRoutine(string completionEvent)
    {
        _isTransitioning = true;

        if (InputManager.Instance != null)
        {
            InputManager.Instance.DisableMovementMap();
            InputManager.Instance.DisableInteractionsMap();
        }

        SetTransitionMessage("<b>5 Hours Later</b>");
        yield return FadeRoutine(0f, 1f, syncMessageAlpha: true);
        yield return new WaitForSeconds(_blackScreenDuration);
        yield return FadeRoutine(1f, 0f, syncMessageAlpha: true);
        SetTransitionMessage(null);

        if (!string.IsNullOrEmpty(completionEvent) && EventBus.Instance != null)
        {
            EventBus.Instance.Publish(completionEvent);
        }

        if (InputManager.Instance != null)
        {
            InputManager.Instance.EnableInteractionsMap();
            InputManager.Instance.EnableMovementMap();
        }

        ReturnPlayerToIdle();
        _isTransitioning = false;
    }

    private IEnumerator MessageCardRoutine(string message, float holdDuration)
    {
        _isTransitioning = true;

        if (InputManager.Instance != null)
        {
            InputManager.Instance.DisableMovementMap();
            InputManager.Instance.DisableInteractionsMap();
        }

        SetTransitionMessage(message);
        yield return FadeRoutine(0f, 1f, syncMessageAlpha: true);
        yield return new WaitForSecondsRealtime(holdDuration);
        yield return FadeRoutine(1f, 0f, syncMessageAlpha: true);
        SetTransitionMessage(null);

        if (InputManager.Instance != null)
        {
            InputManager.Instance.EnableInteractionsMap();
            InputManager.Instance.EnableMovementMap();
        }

        ReturnPlayerToIdle();
        _isTransitioning = false;
    }

    private IEnumerator SleepTransitionRoutine()
    {
        _isTransitioning = true;

        if (InputManager.Instance != null)
        {
            InputManager.Instance.DisableMovementMap();
            InputManager.Instance.DisableInteractionsMap();
        }

        SetTransitionMessage("<b>ZzzZzzzz</b>");
        yield return FadeRoutine(0f, 1f, syncMessageAlpha: true);

        // Advance day while the screen is black so morning messages and door locks initialize cleanly
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Publish("NextDay");
        }

        yield return new WaitForSeconds(_blackScreenDuration);

        yield return FadeRoutine(1f, 0f, syncMessageAlpha: true);
        SetTransitionMessage(null);

        if (InputManager.Instance != null)
        {
            InputManager.Instance.EnableInteractionsMap();
            InputManager.Instance.EnableMovementMap();
        }

        ReturnPlayerToIdle();
        _isTransitioning = false;
    }

    private IEnumerator FadeRoutine(float startAlpha, float endAlpha, bool syncMessageAlpha = false)
    {
        if (_fadeImage == null) yield break;

        _fadeImage.gameObject.SetActive(true);
        float elapsed = 0f;
        Color c = _fadeImage.color;

        while (elapsed < _fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            c.a = Mathf.Lerp(startAlpha, endAlpha, elapsed / _fadeDuration);
            _fadeImage.color = c;
            if (syncMessageAlpha)
            {
                SetMessageAlpha(c.a);
            }
            yield return null;
        }

        c.a = endAlpha;
        _fadeImage.color = c;
        if (syncMessageAlpha)
        {
            SetMessageAlpha(c.a);
        }
    }

    private static void ReturnPlayerToIdle()
    {
        PlayerStateMachine playerState = FindAnyObjectByType<PlayerStateMachine>();
        if (playerState != null)
        {
            playerState.ChangeState(PlayerState.Idle);
        }
    }
}
