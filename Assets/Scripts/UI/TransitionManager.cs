using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;

public class TransitionManager : MonoBehaviour
{
    public static TransitionManager Instance { get; private set; }

    [Header("UI Reference")]
    [Tooltip("Assign a full-screen black image here.")]
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

    [Header("Scene 1 Opening")]
    [Tooltip("Comic images shown in order before the player gains control in Luna's bedroom.")]
    [SerializeField] private Sprite[] _scene1OpeningFrames;
    [Tooltip("Duration for each comic frame. Missing entries use five seconds.")]
    [SerializeField] private float[] _scene1OpeningFrameDurations;
    [Tooltip("How long each new comic frame takes to fade in over the previous frame.")]
    [SerializeField, Min(0f)] private float _scene1OpeningFrameFadeDuration = 0.75f;

    [Header("Scene 1 Opening Audio")]
    [SerializeField] private AudioClip _scene1AlarmClip;
    [Tooltip("Seconds after the opening comic begins before the alarm starts.")]
    [SerializeField, Min(0f)] private float _scene1AlarmDelay = 5f;
    [Tooltip("How long the alarm is allowed to play before it is stopped.")]
    [SerializeField, Min(0f)] private float _scene1AlarmDuration = 11f;

    private bool _isTransitioning;
    private bool _showScene1OpeningOnStart;
    private bool _scene1OpeningCompletedThisSession;

    private RectTransform _comicContainer;
    private Image _scene1OpeningImage;
    private Image _scene1OpeningIncomingImage;
    private AudioSource _scene1AlarmSource;
    private Coroutine _scene1AlarmRoutine;
    private Coroutine _activeOpeningRoutine;

    public bool IsTransitioning => _isTransitioning;

    private void Awake()
    {
        ConfigureTransitionCanvas();
        ConfigureFullScreenFade();

        if (Instance == null)
        {
            Instance = this;
            EnsureTransitionText();
            SceneManager.sceneLoaded += OnSceneLoaded;
            CheckIfShouldPrepareScene1Opening(SceneManager.GetActiveScene().name);
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
        ConfigureComicContainer();
    }

    private void OnRectTransformDimensionsChange()
    {
        ConfigureFullScreenFade();
        ConfigureComicContainer();
    }

    private void Update()
    {
        if (_isTransitioning)
        {
            // Continuously guarantee full-bleed coverage and block clicks during active transitions
            ConfigureFullScreenFade();
        }
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

    /// <summary>
    /// Ensures the black backdrop covers the entire screen plus a generous margin,
    /// preventing any subpixel seam or aspect-ratio gap from revealing the 3D room.
    /// </summary>
    private void ConfigureFullScreenFade()
    {
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

        if (_fadeImage == null) return;

        RectTransform fadeRect = _fadeImage.rectTransform;
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.pivot = new Vector2(0.5f, 0.5f);
        fadeRect.anchoredPosition = Vector2.zero;
        // Generous bleed margins so non-16:9 resolutions (16:10, 21:9, 4:3) are completely covered in pure black
        fadeRect.offsetMin = new Vector2(-1000f, -1000f);
        fadeRect.offsetMax = new Vector2(1000f, 1000f);
        fadeRect.localScale = Vector3.one;
    }

    /// <summary>
    /// Configures the comic container to match the exact screen viewport,
    /// allowing its child comic images with preserveAspect=true to center cleanly.
    /// </summary>
    private void ConfigureComicContainer()
    {
        if (_comicContainer == null) return;

        _comicContainer.anchorMin = Vector2.zero;
        _comicContainer.anchorMax = Vector2.one;
        _comicContainer.pivot = new Vector2(0.5f, 0.5f);
        _comicContainer.anchoredPosition = Vector2.zero;
        _comicContainer.offsetMin = Vector2.zero;
        _comicContainer.offsetMax = Vector2.zero;
        _comicContainer.sizeDelta = Vector2.zero;
        _comicContainer.localScale = Vector3.one;

        ConfigureChildImage(_scene1OpeningImage);
        ConfigureChildImage(_scene1OpeningIncomingImage);
    }

    private static void ConfigureChildImage(Image img)
    {
        if (img == null) return;

        RectTransform r = img.rectTransform;
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = Vector2.zero;
        r.offsetMin = Vector2.zero;
        r.offsetMax = Vector2.zero;
        r.sizeDelta = Vector2.zero;
        r.localScale = Vector3.one;

        img.type = Image.Type.Simple;
        img.preserveAspect = true;
        img.raycastTarget = false;
    }

    private void EnsureTransitionText()
    {
        if (_transitionText == null)
        {
            _transitionText = GetComponentInChildren<TMP_Text>(true);
        }

        if (_transitionText == null)
        {
            GameObject messageObject = new GameObject(
                "TransitionMessage",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            messageObject.layer = gameObject.layer;
            messageObject.transform.SetParent(transform, false);
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
            EventBus.Instance.Subscribe("PlayDay1Cutscene", HandlePlayDay1CutsceneEvent);
        }

        if (_showScene1OpeningOnStart && _activeOpeningRoutine == null)
        {
            PlayDayOneOpening();
        }
    }

    private void HandlePlayDay1CutsceneEvent()
    {
        PlayDayOneOpening(force: true);
    }

    private void OnDisable()
    {
        StopScene1Alarm();

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe("SleepTransition", OnSleepTransition);
            EventBus.Instance.Unsubscribe("FadeOut", StartFadeOut);
            EventBus.Instance.Unsubscribe("FadeIn", StartFadeIn);
            EventBus.Instance.Unsubscribe("ClassesTransition", OnClassesTransition);
            EventBus.Instance.Unsubscribe("PlayDay1Cutscene", HandlePlayDay1CutsceneEvent);
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        StopScene1Alarm();

        if (_transitionText != null)
        {
            _transitionText.DOKill();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (string.Equals(scene.name, "Home", StringComparison.OrdinalIgnoreCase))
        {
            if (_activeOpeningRoutine == null && ShouldPlayDayOneOpening())
            {
                PlayDayOneOpening();
            }
        }
    }

    private void CheckIfShouldPrepareScene1Opening(string sceneName)
    {
        if (!string.Equals(sceneName, "Home", StringComparison.OrdinalIgnoreCase)) return;
        if (!ShouldPlayDayOneOpening()) return;

        _showScene1OpeningOnStart = true;
        _isTransitioning = true;
        EnsureScene1OpeningImages();
        SetTransitionMessage(null);

        if (_fadeImage != null)
        {
            ConfigureFullScreenFade();
            _fadeImage.gameObject.SetActive(true);
            _fadeImage.color = Color.black;
            _fadeImage.raycastTarget = true;
        }

        SetScene1OpeningImageAlpha(0f);
    }

    public bool ShouldPlayDayOneOpening()
    {
        if (_scene1OpeningCompletedThisSession) return false;
        if (DayManager.ShouldSkipSceneOneOpening) return false;
        if (DayManager.Instance != null && DayManager.Instance.GetCurrentDay() > 0) return false;
        if (SaveManager.Instance != null && SaveManager.Instance.IsLoadingSavedGame) return false;
        if (ObjectivesManager.Instance != null &&
            ObjectivesManager.Instance.GetCurrentObjective() != null &&
            ObjectivesManager.Instance.GetCurrentObjective().Id() > 0)
        {
            return false;
        }

        return true;
    }

    public void ResetCutsceneState()
    {
        _scene1OpeningCompletedThisSession = false;
        _showScene1OpeningOnStart = false;
        if (_activeOpeningRoutine != null)
        {
            StopCoroutine(_activeOpeningRoutine);
            _activeOpeningRoutine = null;
        }
        StopScene1Alarm();
        HideScene1OpeningImages();
    }

    public void PlayDayOneOpening(bool force = false)
    {
        if (_activeOpeningRoutine != null) return;
        if (!force && !ShouldPlayDayOneOpening()) return;

        _showScene1OpeningOnStart = false;
        _activeOpeningRoutine = StartCoroutine(Scene1OpeningRoutine());
    }

    private IEnumerator Scene1OpeningRoutine()
    {
        _isTransitioning = true;
        SetTransitionMessage(null);
        EnsureScene1OpeningImages();
        ConfigureFullScreenFade();

        if (_fadeImage != null)
        {
            _fadeImage.gameObject.SetActive(true);
            _fadeImage.color = Color.black;
            _fadeImage.raycastTarget = true;
        }

        if (_comicContainer != null)
        {
            _comicContainer.gameObject.SetActive(true);
        }

        StartScene1Alarm();

        // Lock player movement and interactions completely
        if (InputManager.Instance != null)
        {
            InputManager.Instance.DisableMovementMap();
            InputManager.Instance.DisableInteractionsMap();
        }

        ReturnPlayerToIdle();

        // Play all 6 comic frames sequentially without skip (mandatory viewing)
        if (_scene1OpeningFrames != null && _scene1OpeningFrames.Length > 0)
        {
            for (int i = 0; i < _scene1OpeningFrames.Length; i++)
            {
                Sprite frame = _scene1OpeningFrames[i];
                if (frame == null) continue;

                float duration = GetScene1OpeningFrameDuration(i);
                yield return ShowOpeningFrame(frame, duration);
            }
        }

        StopScene1Alarm();

        // Fade both the solid black background and the final comic frame together into Luna's bedroom
        yield return FadeOpeningToRoom(_fadeDuration);

        HideScene1OpeningImages();
        SetTransitionMessage(null);

        if (_fadeImage != null)
        {
            _fadeImage.raycastTarget = false;
            _fadeImage.gameObject.SetActive(false);
        }

        // Restore player control
        if (InputManager.Instance != null)
        {
            InputManager.Instance.EnableInteractionsMap();
            InputManager.Instance.EnableMovementMap();
        }

        ReturnPlayerToIdle();
        _scene1OpeningCompletedThisSession = true;
        _activeOpeningRoutine = null;
        _isTransitioning = false;

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Publish("Day1CutsceneCompleted");
        }
    }

    /// <summary>
    /// Displays a comic frame with smooth cross-fade, holding for the full frame duration.
    /// Unskippable: player must watch the full frame sequence.
    /// </summary>
    private IEnumerator ShowOpeningFrame(Sprite frame, float frameDuration)
    {
        float fadeDuration = Mathf.Min(Mathf.Max(0f, _scene1OpeningFrameFadeDuration), Mathf.Max(0f, frameDuration));

        Image targetImage;
        Image currentImage;

        if (_scene1OpeningImage.sprite == null)
        {
            targetImage = _scene1OpeningImage;
            currentImage = null;
        }
        else
        {
            targetImage = _scene1OpeningIncomingImage;
            currentImage = _scene1OpeningImage;
        }

        targetImage.sprite = frame;
        targetImage.gameObject.SetActive(true);
        targetImage.transform.SetAsLastSibling();
        SetImageAlpha(targetImage, 0f);

        // Crossfade into new frame
        float fadeElapsed = 0f;
        while (fadeElapsed < fadeDuration)
        {
            fadeElapsed += Time.unscaledDeltaTime;
            float t = fadeDuration > 0f ? Mathf.Clamp01(fadeElapsed / fadeDuration) : 1f;
            SetImageAlpha(targetImage, t);
            yield return null;
        }

        SetImageAlpha(targetImage, 1f);

        if (currentImage != null)
        {
            currentImage.sprite = null;
            currentImage.gameObject.SetActive(false);
            _scene1OpeningImage = targetImage;
            _scene1OpeningIncomingImage = currentImage;
        }

        // Hold frame for remaining duration without any skipping allowed
        float remainingDuration = Mathf.Max(0f, frameDuration - fadeDuration);
        if (remainingDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(remainingDuration);
        }
    }

    /// <summary>
    /// Fades out the black screen and the active comic frame synchronously to reveal the 3D room.
    /// </summary>
    private IEnumerator FadeOpeningToRoom(float duration)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Clamp01(1f - (elapsed / safeDuration));

            if (_fadeImage != null)
            {
                Color c = Color.black;
                c.a = alpha;
                _fadeImage.color = c;
            }

            SetScene1OpeningImageAlpha(alpha);
            yield return null;
        }

        if (_fadeImage != null)
        {
            Color c = Color.black;
            c.a = 0f;
            _fadeImage.color = c;
        }

        SetScene1OpeningImageAlpha(0f);
    }

    private void StartScene1Alarm()
    {
        if (_scene1AlarmClip == null) return;
        StopScene1Alarm();
        _scene1AlarmRoutine = StartCoroutine(Scene1AlarmRoutine());
    }

    private void StopScene1Alarm()
    {
        if (_scene1AlarmRoutine != null)
        {
            StopCoroutine(_scene1AlarmRoutine);
            _scene1AlarmRoutine = null;
        }

        if (_scene1AlarmSource != null)
        {
            _scene1AlarmSource.Stop();
            _scene1AlarmSource.loop = false;
        }
    }

    private IEnumerator Scene1AlarmRoutine()
    {
        if (_scene1AlarmDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(_scene1AlarmDelay);
        }

        EnsureScene1AlarmSource();
        if (_scene1AlarmSource != null && _scene1AlarmClip != null)
        {
            _scene1AlarmSource.clip = _scene1AlarmClip;
            _scene1AlarmSource.volume = GetConfiguredAlarmVolume();
            _scene1AlarmSource.loop = true;
            _scene1AlarmSource.Play();
        }

        if (_scene1AlarmDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(_scene1AlarmDuration);
        }

        if (_scene1AlarmSource != null)
        {
            _scene1AlarmSource.Stop();
            _scene1AlarmSource.loop = false;
        }

        _scene1AlarmRoutine = null;
    }

    private float GetConfiguredAlarmVolume()
    {
        if (SettingsManager.Instance != null)
        {
            return SettingsManager.Instance.SfxVolume;
        }

        return PlayerPrefs.GetFloat(SettingsManager.PrefKeySfxVolume, 1.0f);
    }

    private void EnsureScene1AlarmSource()
    {
        if (_scene1AlarmSource == null)
        {
            _scene1AlarmSource = GetComponent<AudioSource>();
        }

        if (_scene1AlarmSource == null)
        {
            _scene1AlarmSource = gameObject.AddComponent<AudioSource>();
        }

        _scene1AlarmSource.playOnAwake = false;
        _scene1AlarmSource.loop = false;
        _scene1AlarmSource.spatialBlend = 0f;
        _scene1AlarmSource.ignoreListenerPause = true;
    }

    /// <summary>
    /// Creates or retrieves the dedicated comic container and the two cross-fade comic images.
    /// Sibling order: _fadeImage (index 0), comicContainer (index 1), transitionText (last).
    /// </summary>
    private void EnsureScene1OpeningImages()
    {
        if (_fadeImage == null) return;

        // Position _fadeImage as the bottom backdrop
        _fadeImage.transform.SetAsFirstSibling();

        if (_comicContainer == null)
        {
            Transform existingContainer = transform.Find("OpeningComicContainer");
            if (existingContainer != null)
            {
                _comicContainer = existingContainer as RectTransform;
            }
            else
            {
                GameObject containerObj = new GameObject("OpeningComicContainer", typeof(RectTransform));
                containerObj.layer = gameObject.layer;
                containerObj.transform.SetParent(transform, false);
                _comicContainer = containerObj.GetComponent<RectTransform>();
            }
        }

        _comicContainer.transform.SetSiblingIndex(1);
        ConfigureComicContainer();

        if (_scene1OpeningImage == null)
        {
            _scene1OpeningImage = GetOrCreateComicImage("Scene1OpeningComic");
        }

        if (_scene1OpeningIncomingImage == null)
        {
            _scene1OpeningIncomingImage = GetOrCreateComicImage("Scene1OpeningComicIncoming");
        }

        _scene1OpeningImage.transform.SetAsFirstSibling();
        _scene1OpeningIncomingImage.transform.SetSiblingIndex(1);
        _scene1OpeningIncomingImage.sprite = null;
        _scene1OpeningIncomingImage.gameObject.SetActive(false);

        if (_transitionText != null)
        {
            _transitionText.transform.SetAsLastSibling();
        }
    }

    private Image GetOrCreateComicImage(string objectName)
    {
        Transform existingImage = _comicContainer.Find(objectName);
        Image openingImage = existingImage != null ? existingImage.GetComponent<Image>() : null;

        if (openingImage == null)
        {
            GameObject imageObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            imageObject.layer = gameObject.layer;
            imageObject.transform.SetParent(_comicContainer, false);
            openingImage = imageObject.GetComponent<Image>();
        }

        ConfigureChildImage(openingImage);
        openingImage.color = Color.white;
        return openingImage;
    }

    private float GetScene1OpeningFrameDuration(int frameIndex)
    {
        if (_scene1OpeningFrameDurations != null &&
            frameIndex >= 0 &&
            frameIndex < _scene1OpeningFrameDurations.Length)
        {
            return Mathf.Max(0f, _scene1OpeningFrameDurations[frameIndex]);
        }

        return 5f;
    }

    private void SetScene1OpeningImageAlpha(float alpha)
    {
        if (_scene1OpeningImage != null && _scene1OpeningImage.gameObject.activeSelf)
        {
            SetImageAlpha(_scene1OpeningImage, alpha);
        }

        if (_scene1OpeningIncomingImage != null && _scene1OpeningIncomingImage.gameObject.activeSelf)
        {
            SetImageAlpha(_scene1OpeningIncomingImage, alpha);
        }
    }

    private void HideScene1OpeningImages()
    {
        if (_scene1OpeningImage != null)
        {
            _scene1OpeningImage.sprite = null;
            _scene1OpeningImage.gameObject.SetActive(false);
        }

        if (_scene1OpeningIncomingImage != null)
        {
            _scene1OpeningIncomingImage.sprite = null;
            _scene1OpeningIncomingImage.gameObject.SetActive(false);
        }

        if (_comicContainer != null)
        {
            _comicContainer.gameObject.SetActive(false);
        }
    }

    private static void SetImageAlpha(Image image, float alpha)
    {
        if (image == null) return;
        Color c = image.color;
        c.a = alpha;
        image.color = c;
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

        ConfigureFullScreenFade();
        _fadeImage.gameObject.SetActive(true);
        _fadeImage.raycastTarget = endAlpha > 0f;

        float elapsed = 0f;
        Color c = Color.black;

        while (elapsed < _fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            c.a = Mathf.Lerp(startAlpha, endAlpha, elapsed / _fadeDuration);
            _fadeImage.color = c;
            if (syncMessageAlpha)
            {
                SetMessageAlpha(c.a);
            }
            SetScene1OpeningImageAlpha(c.a);
            yield return null;
        }

        c.a = endAlpha;
        _fadeImage.color = c;
        if (syncMessageAlpha)
        {
            SetMessageAlpha(c.a);
        }
        SetScene1OpeningImageAlpha(c.a);

        if (endAlpha <= 0f)
        {
            _fadeImage.raycastTarget = false;
            _fadeImage.gameObject.SetActive(false);
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
