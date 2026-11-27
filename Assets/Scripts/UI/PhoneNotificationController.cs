using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Identifies applications that can dispatch notifications to the phone.
/// Extend this enum as new apps are created in the future.
/// </summary>
public enum PhoneApp
{
    Messages,
    Mail,
    Notes,
    Calendar,
    Bank,
    Social,
    Connect
}

[Serializable]
public class NotificationData
{
    public string AppName;
    public string Message;
    public Sprite AppIcon;
    public float Duration = 3.5f;

    public NotificationData(string appName, string message, Sprite appIcon = null, float duration = 3.5f)
    {
        AppName = appName;
        Message = message;
        AppIcon = appIcon;
        Duration = duration;
    }

    public NotificationData(PhoneApp app, string message, Sprite appIcon = null, float duration = 3.5f)
    {
        AppName = app.ToString();
        Message = message;
        AppIcon = appIcon;
        Duration = duration;
    }
}

[Serializable]
public class AppNotificationConfig
{
    public PhoneApp App;
    public string DisplayName = "Messages";
    public Sprite AppIcon;
}

/// <summary>
/// Manages the on-screen Phone Notifications banner.
/// Scalable to support notifications from any present or future app.
/// </summary>
public class PhoneNotificationController : MonoBehaviour
{
    private static PhoneNotificationController _instance;
    public static PhoneNotificationController Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<PhoneNotificationController>();
                if (_instance == null)
                {
                    // Search in loaded scene objects (including inactive)
                    GameObject notifGO = GameObject.Find("PhoneNotifications");
                    if (notifGO == null)
                    {
                        var allCanvases = Resources.FindObjectsOfTypeAll<Canvas>();
                        foreach (var c in allCanvases)
                        {
                            if (c.gameObject.scene.isLoaded && c.gameObject.name == "PhoneNotifications")
                            {
                                notifGO = c.gameObject;
                                break;
                            }
                        }
                    }

                    if (notifGO != null)
                    {
                        _instance = notifGO.GetComponent<PhoneNotificationController>();
                        if (_instance == null)
                        {
                            _instance = notifGO.AddComponent<PhoneNotificationController>();
                        }
                    }
                }
            }
            return _instance;
        }
        private set
        {
            _instance = value;
        }
    }

    [Header("UI Elements")]
    [SerializeField] private TMP_Text _appNameText;
    [SerializeField] private TMP_Text _notificationMessageText;
    [SerializeField] private Image _appLogoImage;
    [SerializeField] private Image _bgImage;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private RectTransform _panelRect;

    [Header("Animation Settings")]
    [SerializeField] private float _stayDuration = 2.0f;
    [SerializeField] private float _slideDuration = 0.45f;
    [SerializeField] private float _slideDistance = 600f;
    [SerializeField] private Ease _slideInEase = Ease.OutCubic;
    [SerializeField] private Ease _slideOutEase = Ease.InCubic;
    [SerializeField] private float _defaultDisplayDuration = 2.0f;

    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _notificationSound;

    [Header("App Configurations (Scalability)")]
    [SerializeField] private List<AppNotificationConfig> _appConfigs = new List<AppNotificationConfig>
    {
        new AppNotificationConfig { App = PhoneApp.Messages, DisplayName = "Messages" },
        new AppNotificationConfig { App = PhoneApp.Connect, DisplayName = "Connect" }
    };

    [Tooltip("Summary text displayed when multiple notifications for the same app arrive together.")]
    [SerializeField] private string _multipleMessagesText = "New unread messages";

    private readonly List<NotificationData> _notificationQueue = new List<NotificationData>();
    private NotificationData _activeNotification = null;
    private bool _isActiveNotificationVisible = false;
    private Coroutine _displayCoroutine = null;
    private Vector2 _onScreenPosition;
    private Vector2 _offScreenPosition;
    private bool _isInitialized = false;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Destroy(this);
            return;
        }

        InitializeComponents();
    }

    private void Start()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe<NotificationData>("ShowPhoneNotification", ShowNotification);
        }
    }

    private void OnEnable()
    {
        if (_displayCoroutine == null && _notificationQueue.Count > 0)
        {
            _displayCoroutine = StartCoroutine(ProcessNotificationQueue());
        }
    }

    private void OnDisable()
    {
        if (_panelRect != null)
        {
            _panelRect.DOKill();
        }
        if (_canvasGroup != null)
        {
            _canvasGroup.DOKill();
        }
        _displayCoroutine = null;
        _activeNotification = null;
        _isActiveNotificationVisible = false;
    }

    private void OnDestroy()
    {
        if (_panelRect != null)
        {
            _panelRect.DOKill();
        }
        if (_canvasGroup != null)
        {
            _canvasGroup.DOKill();
        }
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe<NotificationData>("ShowPhoneNotification", ShowNotification);
        }
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void InitializeComponents()
    {
        if (_isInitialized) return;

        if (_appNameText == null)
            _appNameText = transform.Find("AppName")?.GetComponent<TMP_Text>()
                        ?? transform.Find("NotificationPanel/AppName")?.GetComponent<TMP_Text>();

        if (_notificationMessageText == null)
            _notificationMessageText = transform.Find("NotificationMessage")?.GetComponent<TMP_Text>()
                                    ?? transform.Find("NotificationPanel/NotificationMessage")?.GetComponent<TMP_Text>();

        if (_appLogoImage == null)
            _appLogoImage = transform.Find("AppLogo")?.GetComponent<Image>()
                         ?? transform.Find("NotificationPanel/AppLogo")?.GetComponent<Image>();

        if (_bgImage == null)
            _bgImage = transform.Find("BG")?.GetComponent<Image>()
                    ?? transform.Find("NotificationPanel/BG")?.GetComponent<Image>();

        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
        }

        if (_audioSource != null)
        {
            _audioSource.playOnAwake = false;
        }

        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = 50; // Draw on top of other HUD/UI elements
        }

        if (_notificationMessageText != null)
        {
            _notificationMessageText.overflowMode = TextOverflowModes.Ellipsis;
        }

        if (_appLogoImage != null)
        {
            _appLogoImage.preserveAspect = true;
        }

        SetupPanelContainer();

        _isInitialized = true;
    }

    private void SetupPanelContainer()
    {
        if (_panelRect == null)
        {
            Transform existing = transform.Find("NotificationPanel");
            if (existing != null)
            {
                _panelRect = existing as RectTransform;
            }
            else
            {
                GameObject panelGO = new GameObject("NotificationPanel", typeof(RectTransform));
                _panelRect = panelGO.GetComponent<RectTransform>();
                _panelRect.SetParent(transform, false);
                _panelRect.anchorMin = new Vector2(0.5f, 0.5f);
                _panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                _panelRect.pivot = new Vector2(0.5f, 0.5f);
                _panelRect.anchoredPosition = Vector2.zero;
                _panelRect.sizeDelta = Vector2.zero;
                _panelRect.localScale = Vector3.one;
                _panelRect.localRotation = Quaternion.identity;

                List<Transform> children = new List<Transform>();
                for (int i = 0; i < transform.childCount; i++)
                {
                    Transform child = transform.GetChild(i);
                    if (child != _panelRect.transform)
                    {
                        children.Add(child);
                    }
                }
                foreach (var child in children)
                {
                    child.SetParent(_panelRect, true);
                }
            }
        }

        _onScreenPosition = _panelRect.anchoredPosition;
        _offScreenPosition = _onScreenPosition + new Vector2(_slideDistance, 0f);

        // Start hidden offscreen to the right
        _panelRect.anchoredPosition = _offScreenPosition;
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;
    }

    #region Public API

    public void ShowNotification(PhoneApp app, string message, Sprite appIcon = null, float duration = -1f)
    {
        string appName = GetAppDisplayName(app);
        Sprite icon = appIcon != null ? appIcon : GetDefaultAppIcon(app);
        float dur = duration > 0f ? duration : _stayDuration;
        ShowNotification(new NotificationData(appName, message, icon, dur));
    }

    public void ShowNotification(string appName, string message, Sprite appIcon = null, float duration = -1f)
    {
        float dur = duration > 0f ? duration : _stayDuration;
        ShowNotification(new NotificationData(appName, message, appIcon, dur));
    }

    public void ShowNotification(NotificationData data)
    {
        if (data == null) return;

        data.Message = PlayerNameService.FormatText(data.Message);

        InitializeComponents();

        // Coalesce with currently visible notification for the same app
        if (_activeNotification != null &&
            _isActiveNotificationVisible &&
            string.Equals(_activeNotification.AppName, data.AppName, StringComparison.OrdinalIgnoreCase))
        {
            _activeNotification.Message = _multipleMessagesText;
            if (_notificationMessageText != null)
            {
                _notificationMessageText.text = TruncateMessageToFit(_notificationMessageText, _multipleMessagesText);
            }
            return;
        }

        // Coalesce with any queued notification for the same app (e.g. multiple contacts updated in one batch)
        for (int i = 0; i < _notificationQueue.Count; i++)
        {
            NotificationData queued = _notificationQueue[i];
            if (queued != null && string.Equals(queued.AppName, data.AppName, StringComparison.OrdinalIgnoreCase))
            {
                queued.Message = _multipleMessagesText;
                if (queued.AppIcon == null && data.AppIcon != null)
                {
                    queued.AppIcon = data.AppIcon;
                }
                return;
            }
        }

        _notificationQueue.Add(data);

        if (_displayCoroutine == null && gameObject.activeInHierarchy)
        {
            _displayCoroutine = StartCoroutine(ProcessNotificationQueue());
        }
    }

    /// <summary>
    /// Clears all pending queued notifications and hides any active notification banner immediately.
    /// </summary>
    public void ClearAllNotifications()
    {
        _notificationQueue.Clear();

        if (_displayCoroutine != null)
        {
            StopCoroutine(_displayCoroutine);
            _displayCoroutine = null;
        }

        _activeNotification = null;
        _isActiveNotificationVisible = false;

        if (_panelRect != null)
        {
            _panelRect.DOKill();
            _panelRect.anchoredPosition = _offScreenPosition;
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.DOKill();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }
    }

    #endregion

    private IEnumerator ProcessNotificationQueue()
    {
        // Allow same-frame batch updates (e.g. Start_Day1 updating Dad, Mom, Maya) to coalesce before sliding in
        yield return null;

        while (_notificationQueue.Count > 0)
        {
            while (TransitionManager.Instance != null && TransitionManager.Instance.IsTransitioning)
            {
                yield return null;
            }

            if (_notificationQueue.Count == 0) break;

            NotificationData data = _notificationQueue[0];
            _notificationQueue.RemoveAt(0);

            _activeNotification = data;
            yield return StartCoroutine(DisplayNotification(data));
            _activeNotification = null;
        }
        _displayCoroutine = null;
        _activeNotification = null;
        _isActiveNotificationVisible = false;
    }

    private IEnumerator DisplayNotification(NotificationData data)
    {
        // 1. Set AppName
        if (_appNameText != null)
        {
            _appNameText.text = data.AppName;
        }

        // 2. Set AppLogo
        if (_appLogoImage != null)
        {
            Sprite icon = data.AppIcon != null ? data.AppIcon : GetDefaultAppIconByName(data.AppName);
            if (icon != null)
            {
                _appLogoImage.sprite = icon;
                _appLogoImage.enabled = true;
            }
        }

        // 3. Set Notification Message: write only the portion that fits within bounds
        if (_notificationMessageText != null)
        {
            string fittingText = TruncateMessageToFit(_notificationMessageText, data.Message);
            _notificationMessageText.text = fittingText;
        }

        // 4. Play audio (if assigned)
        PlayNotificationSound();

        // 5. Setup initial position for slide-in from right
        if (_panelRect != null)
        {
            _panelRect.DOKill();
            _panelRect.anchoredPosition = _offScreenPosition;
        }

        _canvasGroup.DOKill();
        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = true;
        _isActiveNotificationVisible = true;

        // 6. Slide In: appear moving from the right into the current position
        if (_panelRect != null)
        {
            _panelRect.DOAnchorPos(_onScreenPosition, _slideDuration).SetEase(_slideInEase);
        }

        yield return new WaitForSeconds(_slideDuration);

        // 7. Stay visible for 2 seconds (or configured duration) in its final position
        float displayTime = data.Duration > 0f ? data.Duration : _stayDuration;
        yield return new WaitForSeconds(displayTime);

        _isActiveNotificationVisible = false;

        // 8. Slide Out: hide moving back towards the right
        if (_panelRect != null)
        {
            _panelRect.DOAnchorPos(_offScreenPosition, _slideDuration).SetEase(_slideOutEase);
        }

        yield return new WaitForSeconds(_slideDuration);

        // 9. Fully hidden
        _canvasGroup.alpha = 0f;
        _canvasGroup.blocksRaycasts = false;
    }

    /// <summary>
    /// Calculates how many characters physically fit inside the notification text bounds,
    /// returning only the part that fits followed by ellipsis if truncated.
    /// </summary>
    public string TruncateMessageToFit(TMP_Text textComponent, string rawMessage)
    {
        if (string.IsNullOrEmpty(rawMessage)) return "";

        string clean = rawMessage.Replace("\r\n", " ").Replace("\n", " ").Trim();
        if (textComponent == null) return clean;

        textComponent.text = clean;
        textComponent.overflowMode = TextOverflowModes.Ellipsis;
        textComponent.ForceMeshUpdate();

        if (textComponent.textInfo != null &&
            textComponent.textInfo.characterCount > 0 &&
            textComponent.textInfo.characterInfo != null)
        {
            int charCount = textComponent.textInfo.characterCount;
            int lastVisible = -1;
            for (int i = 0; i < charCount && i < textComponent.textInfo.characterInfo.Length; i++)
            {
                if (textComponent.textInfo.characterInfo[i].isVisible)
                {
                    lastVisible = i;
                }
            }

            if (lastVisible >= 0 && lastVisible < clean.Length - 1)
            {
                return clean.Substring(0, lastVisible + 1).TrimEnd() + "...";
            }
        }

        return clean;
    }

    private void PlayNotificationSound()
    {
        if (_audioSource != null && _notificationSound != null)
        {
            _audioSource.PlayOneShot(_notificationSound);
        }
    }

    private string GetAppDisplayName(PhoneApp app)
    {
        if (_appConfigs == null) return app.ToString();
        var config = _appConfigs.Find(c => c.App == app);
        return config != null && !string.IsNullOrWhiteSpace(config.DisplayName)
            ? config.DisplayName
            : app.ToString();
    }

    private Sprite GetDefaultAppIcon(PhoneApp app)
    {
        var config = _appConfigs != null ? _appConfigs.Find(c => c.App == app) : null;
        if (config != null && config.AppIcon != null)
        {
            return config.AppIcon;
        }

        if (app == PhoneApp.Connect)
        {
            return MessagesUI.LoadSprite("_PhoneUIImages/ConenctSocialMedia.png");
        }

        return null;
    }

    private Sprite GetDefaultAppIconByName(string appName)
    {
        if (_appConfigs != null)
        {
            var config = _appConfigs.Find(c => string.Equals(c.DisplayName, appName, StringComparison.OrdinalIgnoreCase)
                                            || c.App.ToString().Equals(appName, StringComparison.OrdinalIgnoreCase));
            if (config != null && config.AppIcon != null)
            {
                return config.AppIcon;
            }
        }

        if (string.Equals(appName, "Connect", StringComparison.OrdinalIgnoreCase))
        {
            return MessagesUI.LoadSprite("_PhoneUIImages/ConenctSocialMedia.png");
        }

        return null;
    }
}
