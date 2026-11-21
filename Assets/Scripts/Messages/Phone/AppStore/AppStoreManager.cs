using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class AppStoreAppData
{
    public string appId;
    public string displayName;
    public string developerName;
    public string category;
    public string ratingLabel = "4.6 / 5";
    public string sizeLabel = "32 MB";
    public string downloadsLabel = "1M+";

    [TextArea(2, 4)]
    public string shortTagline;

    [TextArea(3, 8)]
    public string fullDescription;

    public Sprite iconSprite;
    public string iconResourcePath;
    public Color accentColor = new Color(0.004f, 0.529f, 0.373f, 1f);

    public List<Sprite> screenshots = new List<Sprite>();
    public string[] previewCaptions;

    public bool isConnectApp = false;
    public bool requireConnectStoryUnlock = false;

    [Min(0.5f)]
    public float downloadDurationSeconds = 2.4f;

    [Tooltip("Optional EventBus event published when installation completes.")]
    public string onInstalledEvent;

    [Tooltip("Optional EventBus event published when the player presses Open.")]
    public string onOpenedEvent;

    [Tooltip("Optional protagonist inner thought shown when opening non-interactive store apps.")]
    public string openFallbackMonologue;
}

/// <summary>
/// Android Play Store-style phone application.
/// Supports a scalable catalog of apps, Storefront card list, App Detail sub-tab with screenshots and description,
/// animated "Downloading... XX%" progress bar, Install -> Open transitions, and story-gated Connect unlock.
/// </summary>
public class AppStoreManager : MonoBehaviour
{
    public static AppStoreManager Instance { get; private set; }

    private static readonly Color PlayGreen = new Color(0.004f, 0.529f, 0.373f, 1f);
    private static readonly Color PlayGreenSoft = new Color(0.902f, 0.957f, 0.937f, 1f);
    private static readonly Color PlayCardWhite = new Color(1f, 1f, 1f, 1f);
    private static readonly Color PlaySurfaceAlt = new Color(0.945f, 0.957f, 0.969f, 1f);
    private static readonly Color PlayBorderGray = new Color(0.835f, 0.863f, 0.886f, 1f);
    private static readonly Color PlayProgressTrack = new Color(0.867f, 0.898f, 0.922f, 1f);
    private static readonly Color PlayTextDark = new Color(0.110f, 0.129f, 0.153f, 1f);
    private static readonly Color PlayTextMuted = new Color(0.361f, 0.400f, 0.439f, 1f);

    private sealed class AppRuntimeState
    {
        public bool isDownloading;
        public bool isInstalled;
        public float progress;
        public Coroutine downloadCoroutine;
    }

    private sealed class CardViewRefs
    {
        public AppStoreAppData app;
        public GameObject rootGO;
        public Button actionButton;
        public Image actionButtonImage;
        public UnityEngine.UI.Outline actionButtonOutline;
        public TMP_Text actionButtonLabel;
        public GameObject progressRow;
        public TMP_Text progressText;
        public RectTransform progressFill;
    }

    [Header("App Catalog (Scalable)")]
    [Tooltip("Includes the built-in Play Store apps (Connect + realistic store apps).")]
    [SerializeField] private bool _includeBuiltInApps = true;

    [Tooltip("Add new apps here (or match an existing appId to override a built-in app) without editing code.")]
    [SerializeField] private List<AppStoreAppData> _apps = new List<AppStoreAppData>();

    [Header("Default Assets (Optional Overrides)")]
    [SerializeField] private Sprite _appStoreLogoSprite;
    [SerializeField] private Sprite _connectIconOverride;
    [SerializeField] private Sprite _connectPreviewSprite;
    [SerializeField] private Sprite _roundedCardSprite;

    private readonly Dictionary<string, AppRuntimeState> _runtimeStates =
        new Dictionary<string, AppRuntimeState>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, CardViewRefs> _cardViews =
        new Dictionary<string, CardViewRefs>(StringComparer.OrdinalIgnoreCase);

    private TMP_FontAsset _sharedFont;
    private Sprite _generatedRoundedSprite;
    private Sprite _generatedPlaySprite;
    private bool _uiBuilt;
    private bool _sortByTopCharts;
    private string _selectedDetailAppId;

    private GameObject _storefrontView;
    private RectTransform _storefrontContent;
    private GameObject _emptyStateGO;
    private Button _forYouChipButton;
    private Button _topChartsChipButton;
    private Image _forYouChipImage;
    private Image _topChartsChipImage;
    private TMP_Text _forYouChipText;
    private TMP_Text _topChartsChipText;

    private GameObject _detailView;
    private ScrollRect _detailScrollRect;
    private Image _detailIconImage;
    private TMP_Text _detailIconMonogram;
    private TMP_Text _detailTitleText;
    private TMP_Text _detailDeveloperText;
    private TMP_Text _detailCategoryText;
    private TMP_Text _detailRatingValueText;
    private TMP_Text _detailSizeValueText;
    private TMP_Text _detailDownloadsValueText;
    private Button _detailActionButton;
    private Image _detailActionImage;
    private UnityEngine.UI.Outline _detailActionOutline;
    private TMP_Text _detailActionLabel;
    private GameObject _detailProgressContainer;
    private TMP_Text _detailProgressLabel;
    private RectTransform _detailProgressFill;
    private RectTransform _detailScreenshotsContent;
    private TMP_Text _detailDescriptionText;

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

        EnsureUI();
    }

    private void OnDestroy()
    {
        StopAllDownloads(resetProgress: false);
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnDisable()
    {
        StopAllDownloads(resetProgress: true);
    }

    /// <summary>
    /// Called by PhoneStateMachine whenever the App Store tab opens.
    /// Rebuilds the visible catalog cleanly and resets to the Storefront view.
    /// </summary>
    public void OnStoreOpened()
    {
        EnsureUI();
        SyncConnectRuntimeState();
        ShowStorefrontView();
        RebuildStorefrontList();
    }

    /// <summary>
    /// Refreshes visible apps and active UI elements when story progression unlocks Connect or loads a save.
    /// </summary>
    public void RefreshStoreState()
    {
        SyncConnectRuntimeState();

        if (!_uiBuilt || !gameObject.activeInHierarchy)
        {
            return;
        }

        if (_detailView != null && _detailView.activeSelf && !string.IsNullOrEmpty(_selectedDetailAppId))
        {
            AppStoreAppData selected = FindAppById(_selectedDetailAppId);
            if (selected != null && IsAppVisibleInStore(selected))
            {
                RefreshDetailActionUI(selected);
            }
            else
            {
                ShowStorefrontView();
            }
        }

        RebuildStorefrontList();
    }

    /// <summary>
    /// Resets all download/install states (used when starting a New Game).
    /// </summary>
    public void ResetStoreState()
    {
        StopAllDownloads(resetProgress: true);
        _runtimeStates.Clear();
        _selectedDetailAppId = null;
        _sortByTopCharts = false;

        if (_uiBuilt && gameObject.activeInHierarchy)
        {
            UpdateFilterChipsUI();
            ShowStorefrontView();
            RebuildStorefrontList();
        }
    }

    /// <summary>
    /// Handles Back button navigation while inside the App Store.
    /// Returns true if returning from the Detail view to Storefront; false to exit the App Store.
    /// </summary>
    public bool TryHandleBackButton()
    {
        if (_detailView != null && _detailView.activeSelf)
        {
            ShowStorefrontView();
            RefreshVisibleCardsUI();
            return true;
        }

        return false;
    }

    #region Catalog & Story State

    private List<AppStoreAppData> BuildEffectiveCatalog()
    {
        List<AppStoreAppData> result = new List<AppStoreAppData>();
        Dictionary<string, int> indexById = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (_includeBuiltInApps)
        {
            List<AppStoreAppData> builtIns = CreateBuiltInApps();
            for (int i = 0; i < builtIns.Count; i++)
            {
                AppStoreAppData app = builtIns[i];
                if (app == null || string.IsNullOrWhiteSpace(app.appId)) continue;
                indexById[app.appId.Trim()] = result.Count;
                result.Add(app);
            }
        }

        if (_apps != null)
        {
            for (int i = 0; i < _apps.Count; i++)
            {
                AppStoreAppData custom = _apps[i];
                if (custom == null || string.IsNullOrWhiteSpace(custom.appId)) continue;

                string key = custom.appId.Trim();
                if (indexById.TryGetValue(key, out int existingIndex))
                {
                    result[existingIndex] = custom;
                }
                else
                {
                    indexById[key] = result.Count;
                    result.Add(custom);
                }
            }
        }

        return result;
    }

    private List<AppStoreAppData> CreateBuiltInApps()
    {
        return new List<AppStoreAppData>
        {
            new AppStoreAppData
            {
                appId = "connect",
                displayName = "Connect",
                developerName = "Connect Social Ltd.",
                category = "Social",
                ratingLabel = "4.6 / 5",
                sizeLabel = "38 MB",
                downloadsLabel = "5M+",
                shortTagline = "Meet students, post photos, and message friends.",
                fullDescription =
                    "Connect brings students and creators together in one place. " +
                    "Customize your profile, share moments with friends, follow trending posts around school, " +
                    "and chat directly in real time.",
                accentColor = new Color(0.11f, 0.45f, 0.90f, 1f),
                previewCaptions = new[]
                {
                    "Create your custom profile",
                    "Chat with new friends",
                    "Discover school posts"
                },
                isConnectApp = true,
                requireConnectStoryUnlock = true,
                downloadDurationSeconds = 2.5f
            },
            new AppStoreAppData
            {
                appId = "pulse_beats",
                displayName = "PulseBeats",
                developerName = "SoundWave Labs",
                category = "Music & Audio",
                ratingLabel = "4.8 / 5",
                sizeLabel = "29 MB",
                downloadsLabel = "10M+",
                shortTagline = "Lo-fi playlists and indie tracks for studying.",
                fullDescription =
                    "Stream ad-free lo-fi study mixes, indie albums, and offline playlists built " +
                    "for late-night homework sessions and quiet bus rides.",
                accentColor = new Color(0.36f, 0.31f, 0.81f, 1f),
                previewCaptions = new[]
                {
                    "Chill Lo-Fi Mixes",
                    "Offline Listening",
                    "Custom Playlists"
                },
                downloadDurationSeconds = 2.0f,
                openFallbackMonologue = "I don't need to listen to music right now."
            },
            new AppStoreAppData
            {
                appId = "page_turner",
                displayName = "PageTurner",
                developerName = "ChapterOne Studio",
                category = "Books & Reference",
                ratingLabel = "4.7 / 5",
                sizeLabel = "18 MB",
                downloadsLabel = "500K+",
                shortTagline = "Track your reading list and mystery theories.",
                fullDescription =
                    "Keep up with your favorite novels, bookmark key clues, save quotes, " +
                    "and organize chapter notes without risking spoilers.",
                accentColor = new Color(0.06f, 0.54f, 0.48f, 1f),
                previewCaptions = new[]
                {
                    "Reading Tracker",
                    "Chapter Notes",
                    "Spoiler-Free Lists"
                },
                downloadDurationSeconds = 1.8f,
                openFallbackMonologue = "Maybe later—I already remember where I left off in Lost Cause."
            },
            new AppStoreAppData
            {
                appId = "study_spark",
                displayName = "StudySpark",
                developerName = "BrightDesk Apps",
                category = "Education",
                ratingLabel = "4.5 / 5",
                sizeLabel = "24 MB",
                downloadsLabel = "1M+",
                shortTagline = "Smart flashcards, focus timers, and homework notes.",
                fullDescription =
                    "Stay ahead of assignments with customizable Pomodoro focus sessions, " +
                    "flashcard decks for class, and weekly deadline reminders.",
                accentColor = new Color(0.88f, 0.43f, 0.12f, 1f),
                previewCaptions = new[]
                {
                    "Focus Timer",
                    "Smart Flashcards",
                    "Weekly Planner"
                },
                downloadDurationSeconds = 2.0f,
                openFallbackMonologue = "I'll organize my study notes later."
            }
        };
    }

    private bool IsAppVisibleInStore(AppStoreAppData app)
    {
        if (app == null) return false;
        if (!app.requireConnectStoryUnlock) return true;

        return UIManager.Instance != null &&
               (UIManager.Instance.IsConnectDownloadAvailable || UIManager.Instance.IsConnectInstalled);
    }

    private AppStoreAppData FindAppById(string appId)
    {
        if (string.IsNullOrWhiteSpace(appId)) return null;

        List<AppStoreAppData> catalog = BuildEffectiveCatalog();
        for (int i = 0; i < catalog.Count; i++)
        {
            if (string.Equals(catalog[i].appId, appId, StringComparison.OrdinalIgnoreCase))
            {
                return catalog[i];
            }
        }
        return null;
    }

    private AppRuntimeState GetOrCreateState(AppStoreAppData app)
    {
        string key = app.appId.Trim();
        if (!_runtimeStates.TryGetValue(key, out AppRuntimeState state))
        {
            state = new AppRuntimeState();
            _runtimeStates[key] = state;
        }

        if (app.isConnectApp && UIManager.Instance != null)
        {
            state.isInstalled = UIManager.Instance.IsConnectInstalled;
            if (state.isInstalled)
            {
                state.isDownloading = false;
                state.progress = 1f;
            }
        }

        return state;
    }

    private void SyncConnectRuntimeState()
    {
        if (UIManager.Instance == null) return;

        bool connectInstalled = UIManager.Instance.IsConnectInstalled;
        if (_runtimeStates.TryGetValue("connect", out AppRuntimeState connectState))
        {
            connectState.isInstalled = connectInstalled;
            if (connectInstalled)
            {
                connectState.isDownloading = false;
                connectState.progress = 1f;
            }
        }
    }

    private Sprite ResolveAppIcon(AppStoreAppData app)
    {
        if (app == null) return null;
        if (app.iconSprite != null) return app.iconSprite;

        if (!string.IsNullOrWhiteSpace(app.iconResourcePath))
        {
            Sprite loaded = MessagesUI.LoadSprite(app.iconResourcePath);
            if (loaded != null) return loaded;
        }

        if (app.isConnectApp)
        {
            if (_connectIconOverride != null) return _connectIconOverride;

            PhoneStateMachine phoneState = GetComponentInParent<PhoneStateMachine>();
            Sprite tierIcon = phoneState != null ? phoneState.GetConnectAppIcon() : null;
            if (tierIcon != null) return tierIcon;

            Sprite neutral = MessagesUI.LoadSprite("Hilmir/UI/Neutral.png");
            if (neutral != null) return neutral;

            return MessagesUI.LoadSprite("_PhoneUIImages/ConenctSocialMedia.png");
        }

        return null;
    }

    #endregion

    #region Actions & Download Coroutine

    private void OnActionClicked(AppStoreAppData app)
    {
        if (app == null) return;

        AppRuntimeState state = GetOrCreateState(app);
        if (state.isDownloading)
        {
            return;
        }

        if (state.isInstalled)
        {
            OpenInstalledApp(app);
            return;
        }

        state.downloadCoroutine = StartCoroutine(DownloadAppRoutine(app, state));
    }

    private IEnumerator DownloadAppRoutine(AppStoreAppData app, AppRuntimeState state)
    {
        state.isDownloading = true;
        state.progress = 0f;
        UpdateAppProgressUI(app, state);

        float duration = Mathf.Max(0.5f, app.downloadDurationSeconds);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float linear = Mathf.Clamp01(elapsed / duration);

            // Organic Play Store progress curve: quick initial burst, brief verification pause ~82%, crisp finish
            float curved = linear < 0.75f
                ? (linear / 0.75f) * 0.82f
                : 0.82f + Mathf.SmoothStep(0f, 1f, (linear - 0.75f) / 0.25f) * 0.18f;

            state.progress = Mathf.Clamp01(curved);
            UpdateAppProgressUI(app, state);
            yield return null;
        }

        state.progress = 1f;
        state.isDownloading = false;
        state.isInstalled = true;
        state.downloadCoroutine = null;

        if (app.isConnectApp)
        {
            UIManager.Instance?.CompleteConnectDownload();
        }

        if (!string.IsNullOrWhiteSpace(app.onInstalledEvent) && EventBus.Instance != null)
        {
            EventBus.Instance.Publish(app.onInstalledEvent);
        }

        UpdateAppProgressUI(app, state);
    }

    private void OpenInstalledApp(AppStoreAppData app)
    {
        if (app == null) return;

        if (!string.IsNullOrWhiteSpace(app.onOpenedEvent) && EventBus.Instance != null)
        {
            EventBus.Instance.Publish(app.onOpenedEvent);
        }

        if (app.isConnectApp)
        {
            UIManager.Instance?.OpenConnectApp();
            return;
        }

        if (!string.IsNullOrWhiteSpace(app.openFallbackMonologue) && DialogueManager.Instance != null)
        {
            DialogueManager.Instance.ShowInnerMonologue(app.openFallbackMonologue);
        }
    }

    private void StopAllDownloads(bool resetProgress)
    {
        foreach (AppRuntimeState state in _runtimeStates.Values)
        {
            if (state.downloadCoroutine != null)
            {
                StopCoroutine(state.downloadCoroutine);
                state.downloadCoroutine = null;
            }

            if (resetProgress && state.isDownloading)
            {
                state.isDownloading = false;
                state.progress = 0f;
            }
        }
    }

    private void UpdateAppProgressUI(AppStoreAppData app, AppRuntimeState state)
    {
        if (app == null) return;

        if (_cardViews.TryGetValue(app.appId.Trim(), out CardViewRefs card))
        {
            ApplyStateToCardUI(card, state);
        }

        if (_detailView != null &&
            _detailView.activeSelf &&
            string.Equals(_selectedDetailAppId, app.appId, StringComparison.OrdinalIgnoreCase))
        {
            RefreshDetailActionUI(app);
        }
    }

    #endregion

    #region UI Construction (Android Play Store Theme)

    private void EnsureUI()
    {
        if (_uiBuilt) return;
        _uiBuilt = true;

        _sharedFont = GetComponentInParent<PhoneStateMachine>()?.GetComponentInChildren<TextMeshProUGUI>(true)?.font;
        if (_sharedFont == null)
        {
            _sharedFont = TMP_Settings.defaultFontAsset;
        }

        if (_roundedCardSprite == null)
        {
            _roundedCardSprite = CreateRoundedRectSprite(32, 8);
        }

        BuildHeader();
        BuildStorefrontView();
        BuildDetailView();

        ShowStorefrontView();
    }

    private void BuildHeader()
    {
        GameObject headerGO = new GameObject("PlayStoreHeader", typeof(RectTransform));
        headerGO.layer = gameObject.layer;
        headerGO.transform.SetParent(transform, false);

        RectTransform headerRect = headerGO.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = new Vector2(0f, -8f);
        headerRect.sizeDelta = new Vector2(-20f, 72f);

        // App Store logo badge on the left
        GameObject badgeGO = new GameObject("PlayBadge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        badgeGO.layer = gameObject.layer;
        badgeGO.transform.SetParent(headerGO.transform, false);

        RectTransform badgeRect = badgeGO.GetComponent<RectTransform>();
        badgeRect.anchorMin = new Vector2(0f, 0.5f);
        badgeRect.anchorMax = new Vector2(0f, 0.5f);
        badgeRect.pivot = new Vector2(0f, 0.5f);
        badgeRect.anchoredPosition = new Vector2(8f, 2f);
        badgeRect.sizeDelta = new Vector2(42f, 42f);

        Sprite appStoreLogo = _appStoreLogoSprite != null
            ? _appStoreLogoSprite
            : MessagesUI.LoadSprite("Hilmir/UI/App-Store.png");

        Image badgeImg = badgeGO.GetComponent<Image>();
        badgeImg.raycastTarget = false;
        if (appStoreLogo != null)
        {
            badgeImg.sprite = appStoreLogo;
            badgeImg.type = Image.Type.Simple;
            badgeImg.preserveAspect = true;
            badgeImg.color = Color.white;
        }
        else
        {
            badgeImg.sprite = GetRoundedSprite();
            badgeImg.type = Image.Type.Sliced;
            badgeImg.color = PlayGreen;

            GameObject glyphGO = new GameObject("PlayGlyph", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            glyphGO.layer = gameObject.layer;
            glyphGO.transform.SetParent(badgeGO.transform, false);
            RectTransform glyphRect = glyphGO.GetComponent<RectTransform>();
            glyphRect.anchorMin = new Vector2(0.5f, 0.5f);
            glyphRect.anchorMax = new Vector2(0.5f, 0.5f);
            glyphRect.pivot = new Vector2(0.5f, 0.5f);
            glyphRect.anchoredPosition = new Vector2(1.5f, 0f);
            glyphRect.sizeDelta = new Vector2(18f, 18f);
            Image glyphImg = glyphGO.GetComponent<Image>();
            glyphImg.sprite = GetPlayTriangleSprite();
            glyphImg.preserveAspect = true;
            glyphImg.color = Color.white;
            glyphImg.raycastTarget = false;
        }

        TMP_Text titleText = CreateText("Title", headerGO.transform, "App Store", 27f, FontStyles.Bold, PlayTextDark);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 0.45f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0f, 0.5f);
        titleRect.offsetMin = new Vector2(58f, 0f);
        titleRect.offsetMax = new Vector2(-8f, -2f);
        titleText.alignment = TextAlignmentOptions.BottomLeft;
        titleText.raycastTarget = false;

        TMP_Text subtitleText = CreateText("Subtitle", headerGO.transform, "Apps & Games", 15f, FontStyles.Normal, PlayTextMuted);
        RectTransform subRect = subtitleText.rectTransform;
        subRect.anchorMin = new Vector2(0f, 0f);
        subRect.anchorMax = new Vector2(1f, 0.48f);
        subRect.pivot = new Vector2(0f, 0.5f);
        subRect.offsetMin = new Vector2(58f, 4f);
        subRect.offsetMax = new Vector2(-8f, 0f);
        subtitleText.alignment = TextAlignmentOptions.TopLeft;
        subtitleText.raycastTarget = false;

        // Bottom accent divider
        GameObject divider = new GameObject("HeaderDivider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        divider.layer = gameObject.layer;
        divider.transform.SetParent(headerGO.transform, false);
        RectTransform divRect = divider.GetComponent<RectTransform>();
        divRect.anchorMin = new Vector2(0f, 0f);
        divRect.anchorMax = new Vector2(1f, 0f);
        divRect.pivot = new Vector2(0.5f, 0f);
        divRect.anchoredPosition = Vector2.zero;
        divRect.sizeDelta = new Vector2(0f, 2f);
        divider.GetComponent<Image>().color = PlayGreen;
    }

    private void BuildStorefrontView()
    {
        _storefrontView = new GameObject("StorefrontView", typeof(RectTransform));
        _storefrontView.layer = gameObject.layer;
        _storefrontView.transform.SetParent(transform, false);

        RectTransform viewRect = _storefrontView.GetComponent<RectTransform>();
        viewRect.anchorMin = Vector2.zero;
        viewRect.anchorMax = Vector2.one;
        viewRect.offsetMin = new Vector2(10f, 6f);
        viewRect.offsetMax = new Vector2(-10f, -86f);

        // Filter chip bar ("For you" / "Top charts")
        GameObject chipBar = new GameObject("FilterBar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        chipBar.layer = gameObject.layer;
        chipBar.transform.SetParent(_storefrontView.transform, false);

        RectTransform chipRect = chipBar.GetComponent<RectTransform>();
        chipRect.anchorMin = new Vector2(0f, 1f);
        chipRect.anchorMax = new Vector2(1f, 1f);
        chipRect.pivot = new Vector2(0.5f, 1f);
        chipRect.anchoredPosition = new Vector2(0f, -4f);
        chipRect.sizeDelta = new Vector2(0f, 38f);

        HorizontalLayoutGroup chipLayout = chipBar.GetComponent<HorizontalLayoutGroup>();
        chipLayout.spacing = 8f;
        chipLayout.padding = new RectOffset(2, 2, 0, 0);
        chipLayout.childAlignment = TextAlignmentOptions.Left.ToTextAnchor();
        chipLayout.childControlWidth = false;
        chipLayout.childControlHeight = true;
        chipLayout.childForceExpandWidth = false;
        chipLayout.childForceExpandHeight = true;

        (_forYouChipButton, _forYouChipImage, _forYouChipText) = CreateChipButton("ForYouChip", chipBar.transform, "For you", 104f, () =>
        {
            _sortByTopCharts = false;
            UpdateFilterChipsUI();
            RebuildStorefrontList();
        });

        (_topChartsChipButton, _topChartsChipImage, _topChartsChipText) = CreateChipButton("TopChartsChip", chipBar.transform, "Top charts", 122f, () =>
        {
            _sortByTopCharts = true;
            UpdateFilterChipsUI();
            RebuildStorefrontList();
        });

        UpdateFilterChipsUI();

        // Scrollable card feed
        GameObject scrollGO = new GameObject("AppsScrollView", typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect));
        scrollGO.layer = gameObject.layer;
        scrollGO.transform.SetParent(_storefrontView.transform, false);

        RectTransform scrollRectTransform = scrollGO.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = Vector2.zero;
        scrollRectTransform.anchorMax = Vector2.one;
        scrollRectTransform.offsetMin = Vector2.zero;
        scrollRectTransform.offsetMax = new Vector2(0f, -48f);

        GameObject contentGO = new GameObject(
            "Content",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        contentGO.layer = gameObject.layer;
        contentGO.transform.SetParent(scrollGO.transform, false);

        _storefrontContent = contentGO.GetComponent<RectTransform>();
        _storefrontContent.anchorMin = new Vector2(0f, 1f);
        _storefrontContent.anchorMax = new Vector2(1f, 1f);
        _storefrontContent.pivot = new Vector2(0.5f, 1f);
        _storefrontContent.anchoredPosition = Vector2.zero;
        _storefrontContent.sizeDelta = Vector2.zero;

        VerticalLayoutGroup vLayout = contentGO.GetComponent<VerticalLayoutGroup>();
        vLayout.spacing = 10f;
        vLayout.padding = new RectOffset(2, 2, 2, 12);
        vLayout.childControlWidth = true;
        vLayout.childControlHeight = true;
        vLayout.childForceExpandWidth = true;
        vLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentGO.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scrollRect = scrollGO.GetComponent<ScrollRect>();
        scrollRect.viewport = scrollRectTransform;
        scrollRect.content = _storefrontContent;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.scrollSensitivity = 25f;

        // Empty fallback state
        _emptyStateGO = new GameObject("EmptyState", typeof(RectTransform));
        _emptyStateGO.layer = gameObject.layer;
        _emptyStateGO.transform.SetParent(_storefrontView.transform, false);
        RectTransform emptyRect = _emptyStateGO.GetComponent<RectTransform>();
        StretchRect(emptyRect, new Vector2(20f, 40f), new Vector2(-20f, -60f));
        TMP_Text emptyText = CreateText("EmptyText", _emptyStateGO.transform, "No apps available right now.", 16f, FontStyles.Normal, PlayTextMuted);
        StretchRect(emptyText.rectTransform, Vector2.zero, Vector2.zero);
        emptyText.alignment = TextAlignmentOptions.Center;
        _emptyStateGO.SetActive(false);
    }

    private void BuildDetailView()
    {
        _detailView = new GameObject("AppDetailView", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Outline));
        _detailView.layer = gameObject.layer;
        _detailView.transform.SetParent(transform, false);

        RectTransform detailRect = _detailView.GetComponent<RectTransform>();
        detailRect.anchorMin = Vector2.zero;
        detailRect.anchorMax = Vector2.one;
        detailRect.offsetMin = new Vector2(10f, 8f);
        detailRect.offsetMax = new Vector2(-10f, -86f);

        Image bg = _detailView.GetComponent<Image>();
        bg.sprite = GetRoundedSprite();
        bg.type = Image.Type.Sliced;
        bg.color = PlayCardWhite;

        UnityEngine.UI.Outline detailOutline = _detailView.GetComponent<UnityEngine.UI.Outline>();
        detailOutline.effectColor = PlayBorderGray;
        detailOutline.effectDistance = new Vector2(1f, -1f);

        // Top back-to-store bar
        GameObject topBar = new GameObject("DetailTopBar", typeof(RectTransform));
        topBar.layer = gameObject.layer;
        topBar.transform.SetParent(_detailView.transform, false);
        RectTransform topRect = topBar.GetComponent<RectTransform>();
        topRect.anchorMin = new Vector2(0f, 1f);
        topRect.anchorMax = new Vector2(1f, 1f);
        topRect.pivot = new Vector2(0.5f, 1f);
        topRect.anchoredPosition = new Vector2(0f, -6f);
        topRect.sizeDelta = new Vector2(-20f, 36f);

        GameObject backBtnGO = new GameObject("BackToStoreButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        backBtnGO.layer = gameObject.layer;
        backBtnGO.transform.SetParent(topBar.transform, false);
        RectTransform backBtnRect = backBtnGO.GetComponent<RectTransform>();
        backBtnRect.anchorMin = new Vector2(0f, 0.5f);
        backBtnRect.anchorMax = new Vector2(0f, 0.5f);
        backBtnRect.pivot = new Vector2(0f, 0.5f);
        backBtnRect.anchoredPosition = Vector2.zero;
        backBtnRect.sizeDelta = new Vector2(172f, 34f);

        Image backBtnBg = backBtnGO.GetComponent<Image>();
        backBtnBg.sprite = GetRoundedSprite();
        backBtnBg.type = Image.Type.Sliced;
        backBtnBg.color = PlaySurfaceAlt;

        Button backBtn = backBtnGO.GetComponent<Button>();
        backBtn.targetGraphic = backBtnBg;
        DisableNavigation(backBtn);
        backBtn.onClick.AddListener(() =>
        {
            ShowStorefrontView();
            RefreshVisibleCardsUI();
        });

        TMP_Text backLabel = CreateText("Label", backBtnGO.transform, "<  Back to Store", 15f, FontStyles.Bold, PlayTextDark);
        StretchRect(backLabel.rectTransform, new Vector2(8f, 2f), new Vector2(-8f, -2f));
        backLabel.alignment = TextAlignmentOptions.Center;
        backLabel.raycastTarget = false;

        _detailCategoryText = CreateText("CategoryTag", topBar.transform, "Social", 14.5f, FontStyles.Bold, PlayGreen);
        RectTransform catRect = _detailCategoryText.rectTransform;
        catRect.anchorMin = new Vector2(0.50f, 0f);
        catRect.anchorMax = new Vector2(1f, 1f);
        catRect.offsetMin = new Vector2(6f, 0f);
        catRect.offsetMax = new Vector2(-4f, 0f);
        _detailCategoryText.alignment = TextAlignmentOptions.Right;
        _detailCategoryText.raycastTarget = false;

        // Scrollable detail content
        GameObject scrollGO = new GameObject("DetailScroll", typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect));
        scrollGO.layer = gameObject.layer;
        scrollGO.transform.SetParent(_detailView.transform, false);
        RectTransform scrollRectTransform = scrollGO.GetComponent<RectTransform>();
        scrollRectTransform.anchorMin = Vector2.zero;
        scrollRectTransform.anchorMax = Vector2.one;
        scrollRectTransform.offsetMin = new Vector2(12f, 10f);
        scrollRectTransform.offsetMax = new Vector2(-12f, -48f);

        GameObject contentGO = new GameObject(
            "DetailContent",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        contentGO.layer = gameObject.layer;
        contentGO.transform.SetParent(scrollGO.transform, false);
        RectTransform contentRect = contentGO.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;

        VerticalLayoutGroup detailLayout = contentGO.GetComponent<VerticalLayoutGroup>();
        detailLayout.spacing = 14f;
        detailLayout.padding = new RectOffset(4, 4, 4, 16);
        detailLayout.childControlWidth = true;
        detailLayout.childControlHeight = true;
        detailLayout.childForceExpandWidth = true;
        detailLayout.childForceExpandHeight = false;

        ContentSizeFitter contentFitter = contentGO.GetComponent<ContentSizeFitter>();
        contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _detailScrollRect = scrollGO.GetComponent<ScrollRect>();
        _detailScrollRect.viewport = scrollRectTransform;
        _detailScrollRect.content = contentRect;
        _detailScrollRect.horizontal = false;
        _detailScrollRect.vertical = true;
        _detailScrollRect.movementType = ScrollRect.MovementType.Elastic;
        _detailScrollRect.scrollSensitivity = 25f;

        // 1. Hero section (Icon + Title + Developer)
        GameObject heroGO = new GameObject("Hero", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        heroGO.layer = gameObject.layer;
        heroGO.transform.SetParent(contentRect, false);
        heroGO.GetComponent<LayoutElement>().minHeight = 86f;

        HorizontalLayoutGroup heroLayout = heroGO.GetComponent<HorizontalLayoutGroup>();
        heroLayout.spacing = 14f;
        heroLayout.childAlignment = TextAnchor.MiddleLeft;
        heroLayout.childControlWidth = true;
        heroLayout.childControlHeight = true;
        heroLayout.childForceExpandWidth = false;
        heroLayout.childForceExpandHeight = false;

        GameObject iconFrameGO = CreateIconWidget("DetailIcon", heroGO.transform, 80f, out _detailIconImage, out _detailIconMonogram);
        LayoutElement iconFrameLE = iconFrameGO.AddComponent<LayoutElement>();
        iconFrameLE.minWidth = 80f;
        iconFrameLE.preferredWidth = 80f;
        iconFrameLE.minHeight = 80f;
        iconFrameLE.preferredHeight = 80f;
        iconFrameLE.flexibleWidth = 0f;

        GameObject heroInfoGO = new GameObject("HeroText", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        heroInfoGO.layer = gameObject.layer;
        heroInfoGO.transform.SetParent(heroGO.transform, false);
        LayoutElement heroInfoLE = heroInfoGO.GetComponent<LayoutElement>();
        heroInfoLE.minWidth = 180f;
        heroInfoLE.flexibleWidth = 1f;

        VerticalLayoutGroup heroTextLayout = heroInfoGO.GetComponent<VerticalLayoutGroup>();
        heroTextLayout.spacing = 4f;
        heroTextLayout.childAlignment = TextAnchor.MiddleLeft;
        heroTextLayout.childControlWidth = true;
        heroTextLayout.childControlHeight = true;
        heroTextLayout.childForceExpandWidth = true;
        heroTextLayout.childForceExpandHeight = false;

        _detailTitleText = CreateText("AppTitle", heroInfoGO.transform, "Connect", 25f, FontStyles.Bold, PlayTextDark);
        _detailTitleText.textWrappingMode = TextWrappingModes.Normal;

        _detailDeveloperText = CreateText("Developer", heroInfoGO.transform, "Connect Social Ltd.", 16.5f, FontStyles.Bold, PlayGreen);
        _detailDeveloperText.textWrappingMode = TextWrappingModes.Normal;

        TMP_Text verifiedText = CreateText("Verified", heroInfoGO.transform, "Verified app • Safe download", 14f, FontStyles.Normal, PlayTextMuted);
        verifiedText.textWrappingMode = TextWrappingModes.Normal;

        // 2. Stats row (Rating | Size | Downloads)
        GameObject statsGO = new GameObject("StatsBar", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        statsGO.layer = gameObject.layer;
        statsGO.transform.SetParent(contentRect, false);
        statsGO.GetComponent<LayoutElement>().preferredHeight = 64f;
        Image statsBg = statsGO.GetComponent<Image>();
        statsBg.sprite = GetRoundedSprite();
        statsBg.type = Image.Type.Sliced;
        statsBg.color = PlaySurfaceAlt;

        HorizontalLayoutGroup statsLayout = statsGO.GetComponent<HorizontalLayoutGroup>();
        statsLayout.padding = new RectOffset(8, 8, 6, 6);
        statsLayout.spacing = 6f;
        statsLayout.childControlWidth = true;
        statsLayout.childControlHeight = true;
        statsLayout.childForceExpandWidth = false;
        statsLayout.childForceExpandHeight = true;

        _detailRatingValueText = CreateStatColumn(statsGO.transform, "Rating");
        CreateVerticalDivider(statsGO.transform);
        _detailSizeValueText = CreateStatColumn(statsGO.transform, "Size");
        CreateVerticalDivider(statsGO.transform);
        _detailDownloadsValueText = CreateStatColumn(statsGO.transform, "Downloads");

        // 3. Action button + Download progress bar
        GameObject actionWrap = new GameObject("ActionWrap", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        actionWrap.layer = gameObject.layer;
        actionWrap.transform.SetParent(contentRect, false);
        actionWrap.GetComponent<LayoutElement>().preferredHeight = 54f;
        VerticalLayoutGroup actionWrapLayout = actionWrap.GetComponent<VerticalLayoutGroup>();
        actionWrapLayout.childControlWidth = true;
        actionWrapLayout.childControlHeight = false;
        actionWrapLayout.childForceExpandWidth = true;
        actionWrapLayout.childForceExpandHeight = false;

        GameObject btnGO = new GameObject("PrimaryActionButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Outline), typeof(Button), typeof(LayoutElement));
        btnGO.layer = gameObject.layer;
        btnGO.transform.SetParent(actionWrap.transform, false);
        btnGO.GetComponent<LayoutElement>().preferredHeight = 48f;
        btnGO.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 48f);

        _detailActionImage = btnGO.GetComponent<Image>();
        _detailActionImage.sprite = GetRoundedSprite();
        _detailActionImage.type = Image.Type.Sliced;
        _detailActionImage.color = PlayGreen;

        _detailActionOutline = btnGO.GetComponent<UnityEngine.UI.Outline>();
        _detailActionOutline.effectColor = PlayGreen;
        _detailActionOutline.effectDistance = new Vector2(1.5f, -1.5f);
        _detailActionOutline.enabled = false;

        _detailActionButton = btnGO.GetComponent<Button>();
        _detailActionButton.targetGraphic = _detailActionImage;
        DisableNavigation(_detailActionButton);
        _detailActionButton.onClick.AddListener(() =>
        {
            AppStoreAppData selected = FindAppById(_selectedDetailAppId);
            if (selected != null)
            {
                OnActionClicked(selected);
            }
        });

        _detailActionLabel = CreateText("Label", btnGO.transform, "Install", 18.5f, FontStyles.Bold, Color.white);
        StretchRect(_detailActionLabel.rectTransform, Vector2.zero, Vector2.zero);
        _detailActionLabel.alignment = TextAlignmentOptions.Center;
        _detailActionLabel.raycastTarget = false;

        _detailProgressContainer = CreateProgressBlock(
            actionWrap.transform,
            52f,
            16.5f,
            12f,
            out _detailProgressLabel,
            out _detailProgressFill);
        _detailProgressContainer.SetActive(false);

        // 4. Screenshots strip
        TMP_Text screenshotsHeader = CreateText("ScreenshotsHeader", contentRect, "Screenshots & Preview", 18.5f, FontStyles.Bold, PlayTextDark);
        screenshotsHeader.gameObject.AddComponent<LayoutElement>().preferredHeight = 28f;

        GameObject shotsScrollGO = new GameObject("ScreenshotsScroll", typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect), typeof(LayoutElement));
        shotsScrollGO.layer = gameObject.layer;
        shotsScrollGO.transform.SetParent(contentRect, false);
        shotsScrollGO.GetComponent<LayoutElement>().preferredHeight = 176f;

        GameObject shotsContentGO = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(ContentSizeFitter));
        shotsContentGO.layer = gameObject.layer;
        shotsContentGO.transform.SetParent(shotsScrollGO.transform, false);
        _detailScreenshotsContent = shotsContentGO.GetComponent<RectTransform>();
        _detailScreenshotsContent.anchorMin = new Vector2(0f, 0f);
        _detailScreenshotsContent.anchorMax = new Vector2(0f, 1f);
        _detailScreenshotsContent.pivot = new Vector2(0f, 0.5f);
        _detailScreenshotsContent.anchoredPosition = Vector2.zero;
        _detailScreenshotsContent.sizeDelta = Vector2.zero;

        HorizontalLayoutGroup shotsLayout = shotsContentGO.GetComponent<HorizontalLayoutGroup>();
        shotsLayout.spacing = 10f;
        shotsLayout.padding = new RectOffset(2, 2, 2, 2);
        shotsLayout.childAlignment = TextAnchor.MiddleLeft;
        shotsLayout.childControlWidth = false;
        shotsLayout.childControlHeight = false;
        shotsLayout.childForceExpandWidth = false;
        shotsLayout.childForceExpandHeight = false;

        ContentSizeFitter shotsFitter = shotsContentGO.GetComponent<ContentSizeFitter>();
        shotsFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        shotsFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        ScrollRect shotsScroll = shotsScrollGO.GetComponent<ScrollRect>();
        shotsScroll.viewport = shotsScrollGO.GetComponent<RectTransform>();
        shotsScroll.content = _detailScreenshotsContent;
        shotsScroll.horizontal = true;
        shotsScroll.vertical = false;
        shotsScroll.movementType = ScrollRect.MovementType.Elastic;
        shotsScroll.scrollSensitivity = 25f;

        // 5. About this app section
        TMP_Text aboutHeader = CreateText("AboutHeader", contentRect, "About this app", 18.5f, FontStyles.Bold, PlayTextDark);
        aboutHeader.gameObject.AddComponent<LayoutElement>().preferredHeight = 28f;

        _detailDescriptionText = CreateText("Description", contentRect, string.Empty, 16f, FontStyles.Normal, PlayTextMuted);
        _detailDescriptionText.textWrappingMode = TextWrappingModes.Normal;
        _detailDescriptionText.overflowMode = TextOverflowModes.Overflow;
        ContentSizeFitter descFitter = _detailDescriptionText.gameObject.AddComponent<ContentSizeFitter>();
        descFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        descFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _detailView.SetActive(false);
    }

    private void RebuildStorefrontList()
    {
        if (_storefrontContent == null) return;

        for (int i = _storefrontContent.childCount - 1; i >= 0; i--)
        {
            Destroy(_storefrontContent.GetChild(i).gameObject);
        }
        _cardViews.Clear();

        List<AppStoreAppData> catalog = BuildEffectiveCatalog();
        List<AppStoreAppData> visibleApps = new List<AppStoreAppData>();
        for (int i = 0; i < catalog.Count; i++)
        {
            if (IsAppVisibleInStore(catalog[i]))
            {
                visibleApps.Add(catalog[i]);
            }
        }

        if (_sortByTopCharts)
        {
            visibleApps.Sort((a, b) => string.Compare(b.ratingLabel, a.ratingLabel, StringComparison.Ordinal));
        }

        if (_emptyStateGO != null)
        {
            _emptyStateGO.SetActive(visibleApps.Count == 0);
        }

        for (int i = 0; i < visibleApps.Count; i++)
        {
            AppStoreAppData app = visibleApps[i];
            CardViewRefs refs = CreateAppCard(app, i + 1);
            _cardViews[app.appId.Trim()] = refs;
            ApplyStateToCardUI(refs, GetOrCreateState(app));
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(_storefrontContent);
    }

    private CardViewRefs CreateAppCard(AppStoreAppData app, int rankNumber)
    {
        GameObject cardGO = new GameObject(
            $"AppCard_{app.appId}",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(UnityEngine.UI.Outline),
            typeof(Button),
            typeof(VerticalLayoutGroup));
        cardGO.layer = gameObject.layer;
        cardGO.transform.SetParent(_storefrontContent, false);

        Image cardBg = cardGO.GetComponent<Image>();
        cardBg.sprite = GetRoundedSprite();
        cardBg.type = Image.Type.Sliced;
        cardBg.color = PlayCardWhite;

        UnityEngine.UI.Outline cardOutline = cardGO.GetComponent<UnityEngine.UI.Outline>();
        cardOutline.effectColor = PlayBorderGray;
        cardOutline.effectDistance = new Vector2(1f, -1f);

        VerticalLayoutGroup cardLayout = cardGO.GetComponent<VerticalLayoutGroup>();
        cardLayout.padding = new RectOffset(12, 12, 10, 10);
        cardLayout.spacing = 8f;
        cardLayout.childControlWidth = true;
        cardLayout.childControlHeight = true;
        cardLayout.childForceExpandWidth = true;
        cardLayout.childForceExpandHeight = false;

        Button cardButton = cardGO.GetComponent<Button>();
        cardButton.targetGraphic = cardBg;
        DisableNavigation(cardButton);
        AppStoreAppData capturedApp = app;
        cardButton.onClick.AddListener(() => OpenAppDetail(capturedApp));

        // Top row: Rank + Icon + Title/Meta/Tagline + Install/Open button
        GameObject topRow = new GameObject("TopRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        topRow.layer = gameObject.layer;
        topRow.transform.SetParent(cardGO.transform, false);
        topRow.GetComponent<LayoutElement>().minHeight = 82f;

        HorizontalLayoutGroup rowLayout = topRow.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 10f;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;

        if (_sortByTopCharts)
        {
            TMP_Text rankTxt = CreateText("Rank", topRow.transform, rankNumber.ToString(), 17f, FontStyles.Bold, PlayTextMuted);
            rankTxt.alignment = TextAlignmentOptions.Center;
            rankTxt.raycastTarget = false;
            LayoutElement rankLE = rankTxt.gameObject.AddComponent<LayoutElement>();
            rankLE.minWidth = 22f;
            rankLE.preferredWidth = 22f;
            rankLE.minHeight = 40f;
            rankLE.preferredHeight = 40f;
            rankLE.flexibleWidth = 0f;
        }

        GameObject iconWidget = CreateIconWidget("Icon", topRow.transform, 64f, out Image iconImg, out TMP_Text monogramTxt);
        ApplyIconToWidget(app, iconImg, monogramTxt);
        LayoutElement iconLE = iconWidget.AddComponent<LayoutElement>();
        iconLE.minWidth = 64f;
        iconLE.preferredWidth = 64f;
        iconLE.minHeight = 64f;
        iconLE.preferredHeight = 64f;
        iconLE.flexibleWidth = 0f;

        GameObject infoCol = new GameObject("Info", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        infoCol.layer = gameObject.layer;
        infoCol.transform.SetParent(topRow.transform, false);
        LayoutElement infoColLE = infoCol.GetComponent<LayoutElement>();
        infoColLE.minWidth = 110f;
        infoColLE.flexibleWidth = 1f;

        VerticalLayoutGroup infoLayout = infoCol.GetComponent<VerticalLayoutGroup>();
        infoLayout.spacing = 3f;
        infoLayout.childAlignment = TextAnchor.MiddleLeft;
        infoLayout.childControlWidth = true;
        infoLayout.childControlHeight = true;
        infoLayout.childForceExpandWidth = true;
        infoLayout.childForceExpandHeight = false;

        TMP_Text titleTxt = CreateText("Title", infoCol.transform, app.displayName, 20f, FontStyles.Bold, PlayTextDark);
        titleTxt.overflowMode = TextOverflowModes.Ellipsis;
        titleTxt.textWrappingMode = TextWrappingModes.NoWrap;
        titleTxt.raycastTarget = false;

        TMP_Text metaTxt = CreateText("Meta", infoCol.transform, $"{app.category} • {app.ratingLabel} • {app.sizeLabel}", 13.5f, FontStyles.Normal, PlayTextMuted);
        metaTxt.overflowMode = TextOverflowModes.Ellipsis;
        metaTxt.textWrappingMode = TextWrappingModes.NoWrap;
        metaTxt.raycastTarget = false;

        TMP_Text taglineTxt = CreateText("Tagline", infoCol.transform, app.shortTagline, 13.5f, FontStyles.Normal, PlayTextMuted);
        taglineTxt.textWrappingMode = TextWrappingModes.Normal;
        taglineTxt.overflowMode = TextOverflowModes.Ellipsis;
        taglineTxt.raycastTarget = false;
        LayoutElement tagLE = taglineTxt.gameObject.AddComponent<LayoutElement>();
        tagLE.preferredHeight = 36f;

        // Action button (Install / XX% / Open)
        GameObject btnGO = new GameObject("ActionButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Outline), typeof(Button), typeof(LayoutElement));
        btnGO.layer = gameObject.layer;
        btnGO.transform.SetParent(topRow.transform, false);
        btnGO.GetComponent<RectTransform>().sizeDelta = new Vector2(92f, 40f);
        LayoutElement btnLE = btnGO.GetComponent<LayoutElement>();
        btnLE.minWidth = 92f;
        btnLE.preferredWidth = 92f;
        btnLE.minHeight = 40f;
        btnLE.preferredHeight = 40f;
        btnLE.flexibleWidth = 0f;

        Image btnImg = btnGO.GetComponent<Image>();
        btnImg.sprite = GetRoundedSprite();
        btnImg.type = Image.Type.Sliced;
        btnImg.color = PlayGreen;

        UnityEngine.UI.Outline btnOutline = btnGO.GetComponent<UnityEngine.UI.Outline>();
        btnOutline.effectColor = PlayGreen;
        btnOutline.effectDistance = new Vector2(1.25f, -1.25f);
        btnOutline.enabled = false;

        Button actionBtn = btnGO.GetComponent<Button>();
        actionBtn.targetGraphic = btnImg;
        DisableNavigation(actionBtn);
        actionBtn.onClick.AddListener(() => OnActionClicked(capturedApp));

        TMP_Text btnLabel = CreateText("Label", btnGO.transform, "Install", 16f, FontStyles.Bold, Color.white);
        StretchRect(btnLabel.rectTransform, Vector2.zero, Vector2.zero);
        btnLabel.alignment = TextAlignmentOptions.Center;
        btnLabel.raycastTarget = false;

        GameObject progressRow = CreateProgressBlock(
            cardGO.transform,
            30f,
            14f,
            7f,
            out TMP_Text progressText,
            out RectTransform progressFill);
        progressRow.SetActive(false);

        return new CardViewRefs
        {
            app = app,
            rootGO = cardGO,
            actionButton = actionBtn,
            actionButtonImage = btnImg,
            actionButtonOutline = btnOutline,
            actionButtonLabel = btnLabel,
            progressRow = progressRow,
            progressText = progressText,
            progressFill = progressFill
        };
    }

    private void ApplyStateToCardUI(CardViewRefs card, AppRuntimeState state)
    {
        if (card == null || state == null) return;

        if (state.isDownloading)
        {
            int pct = Mathf.Clamp(Mathf.RoundToInt(state.progress * 100f), 0, 100);
            card.actionButton.interactable = false;
            card.actionButtonImage.color = PlayGreenSoft;
            card.actionButtonOutline.enabled = true;
            card.actionButtonLabel.color = PlayGreen;
            card.actionButtonLabel.text = $"{pct}%";

            if (card.progressRow != null && !card.progressRow.activeSelf)
            {
                card.progressRow.SetActive(true);
            }
            if (card.progressText != null)
            {
                card.progressText.text = $"Downloading... {pct}%";
            }
            if (card.progressFill != null)
            {
                card.progressFill.anchorMax = new Vector2(Mathf.Clamp01(state.progress), 1f);
            }
        }
        else if (state.isInstalled)
        {
            card.actionButton.interactable = true;
            card.actionButtonImage.color = PlayGreenSoft;
            card.actionButtonOutline.enabled = true;
            card.actionButtonLabel.color = PlayGreen;
            card.actionButtonLabel.text = "Open";

            if (card.progressRow != null && card.progressRow.activeSelf)
            {
                card.progressRow.SetActive(false);
            }
        }
        else
        {
            card.actionButton.interactable = true;
            card.actionButtonImage.color = PlayGreen;
            card.actionButtonOutline.enabled = false;
            card.actionButtonLabel.color = Color.white;
            card.actionButtonLabel.text = "Install";

            if (card.progressRow != null && card.progressRow.activeSelf)
            {
                card.progressRow.SetActive(false);
            }
        }

        if (_storefrontContent != null && card.rootGO != null)
        {
            LayoutRebuilder.MarkLayoutForRebuild(card.rootGO.GetComponent<RectTransform>());
        }
    }

    private void OpenAppDetail(AppStoreAppData app)
    {
        if (app == null) return;

        _selectedDetailAppId = app.appId;

        if (_storefrontView != null) _storefrontView.SetActive(false);
        if (_detailView != null) _detailView.SetActive(true);

        ApplyIconToWidget(app, _detailIconImage, _detailIconMonogram);

        if (_detailTitleText != null) _detailTitleText.text = app.displayName;
        if (_detailDeveloperText != null) _detailDeveloperText.text = app.developerName;
        if (_detailCategoryText != null) _detailCategoryText.text = app.category;
        if (_detailRatingValueText != null) _detailRatingValueText.text = app.ratingLabel;
        if (_detailSizeValueText != null) _detailSizeValueText.text = app.sizeLabel;
        if (_detailDownloadsValueText != null) _detailDownloadsValueText.text = app.downloadsLabel;
        if (_detailDescriptionText != null) _detailDescriptionText.text = app.fullDescription;

        RebuildDetailScreenshots(app);
        RefreshDetailActionUI(app);

        if (_detailScrollRect != null)
        {
            if (_detailScrollRect.content != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_detailScrollRect.content);
            }
            _detailScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private void RefreshDetailActionUI(AppStoreAppData app)
    {
        if (app == null) return;

        AppRuntimeState state = GetOrCreateState(app);
        if (state.isDownloading)
        {
            int pct = Mathf.Clamp(Mathf.RoundToInt(state.progress * 100f), 0, 100);
            if (_detailActionButton != null) _detailActionButton.gameObject.SetActive(false);
            if (_detailProgressContainer != null) _detailProgressContainer.SetActive(true);
            if (_detailProgressLabel != null) _detailProgressLabel.text = $"Downloading... {pct}%";
            if (_detailProgressFill != null) _detailProgressFill.anchorMax = new Vector2(Mathf.Clamp01(state.progress), 1f);
        }
        else if (state.isInstalled)
        {
            if (_detailProgressContainer != null) _detailProgressContainer.SetActive(false);
            if (_detailActionButton != null) _detailActionButton.gameObject.SetActive(true);
            if (_detailActionImage != null) _detailActionImage.color = PlayGreenSoft;
            if (_detailActionOutline != null) _detailActionOutline.enabled = true;
            if (_detailActionLabel != null)
            {
                _detailActionLabel.color = PlayGreen;
                _detailActionLabel.text = "Open";
            }
        }
        else
        {
            if (_detailProgressContainer != null) _detailProgressContainer.SetActive(false);
            if (_detailActionButton != null) _detailActionButton.gameObject.SetActive(true);
            if (_detailActionImage != null) _detailActionImage.color = PlayGreen;
            if (_detailActionOutline != null) _detailActionOutline.enabled = false;
            if (_detailActionLabel != null)
            {
                _detailActionLabel.color = Color.white;
                _detailActionLabel.text = "Install";
            }
        }
    }

    private void RebuildDetailScreenshots(AppStoreAppData app)
    {
        if (_detailScreenshotsContent == null) return;

        for (int i = _detailScreenshotsContent.childCount - 1; i >= 0; i--)
        {
            Destroy(_detailScreenshotsContent.GetChild(i).gameObject);
        }

        // 1. Add real screenshots assigned in Inspector (or Connect-UI preview for Connect)
        if (app.screenshots != null)
        {
            for (int i = 0; i < app.screenshots.Count; i++)
            {
                if (app.screenshots[i] != null)
                {
                    CreateImageScreenshotCard(app.screenshots[i], app.accentColor);
                }
            }
        }

        if (app.isConnectApp && (app.screenshots == null || app.screenshots.Count == 0))
        {
            Sprite connectShot = _connectPreviewSprite != null
                ? _connectPreviewSprite
                : MessagesUI.LoadSprite("Hilmir/UI/Connect-UI.png");
            if (connectShot != null)
            {
                CreateImageScreenshotCard(connectShot, app.accentColor);
            }
        }

        // 2. Add stylized feature preview cards for previewCaptions
        if (app.previewCaptions != null)
        {
            for (int i = 0; i < app.previewCaptions.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(app.previewCaptions[i]))
                {
                    CreateStylizedPreviewCard(app, app.previewCaptions[i], i);
                }
            }
        }
    }

    private void CreateImageScreenshotCard(Sprite sprite, Color accentColor)
    {
        GameObject cardGO = new GameObject("ScreenshotSpriteCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Outline));
        cardGO.layer = gameObject.layer;
        cardGO.transform.SetParent(_detailScreenshotsContent, false);
        cardGO.GetComponent<RectTransform>().sizeDelta = new Vector2(110f, 166f);

        Image bg = cardGO.GetComponent<Image>();
        bg.sprite = sprite;
        bg.preserveAspect = true;
        bg.color = Color.white;

        UnityEngine.UI.Outline outline = cardGO.GetComponent<UnityEngine.UI.Outline>();
        outline.effectColor = Color.Lerp(PlayBorderGray, accentColor, 0.35f);
        outline.effectDistance = new Vector2(1f, -1f);
    }

    private void CreateStylizedPreviewCard(AppStoreAppData app, string caption, int index)
    {
        GameObject cardGO = new GameObject($"PreviewCard_{index}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Outline));
        cardGO.layer = gameObject.layer;
        cardGO.transform.SetParent(_detailScreenshotsContent, false);
        cardGO.GetComponent<RectTransform>().sizeDelta = new Vector2(132f, 166f);

        Image bg = cardGO.GetComponent<Image>();
        bg.sprite = GetRoundedSprite();
        bg.type = Image.Type.Sliced;
        bg.color = Color.Lerp(PlaySurfaceAlt, app.accentColor, 0.14f);

        UnityEngine.UI.Outline outline = cardGO.GetComponent<UnityEngine.UI.Outline>();
        outline.effectColor = PlayBorderGray;
        outline.effectDistance = new Vector2(1f, -1f);

        // Mini phone top header
        GameObject banner = new GameObject("MiniBanner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        banner.layer = gameObject.layer;
        banner.transform.SetParent(cardGO.transform, false);
        RectTransform bannerRect = banner.GetComponent<RectTransform>();
        bannerRect.anchorMin = new Vector2(0f, 1f);
        bannerRect.anchorMax = new Vector2(1f, 1f);
        bannerRect.pivot = new Vector2(0.5f, 1f);
        bannerRect.anchoredPosition = new Vector2(0f, -8f);
        bannerRect.sizeDelta = new Vector2(-16f, 26f);
        Image bannerImg = banner.GetComponent<Image>();
        bannerImg.sprite = GetRoundedSprite();
        bannerImg.type = Image.Type.Sliced;
        bannerImg.color = app.accentColor;

        TMP_Text bannerText = CreateText("Name", banner.transform, app.displayName, 13.5f, FontStyles.Bold, Color.white);
        StretchRect(bannerText.rectTransform, new Vector2(6f, 0f), new Vector2(-6f, 0f));
        bannerText.alignment = TextAlignmentOptions.Center;
        bannerText.overflowMode = TextOverflowModes.Ellipsis;
        bannerText.textWrappingMode = TextWrappingModes.NoWrap;

        // Decorative UI skeleton lines inside screenshot mockup
        for (int lineIdx = 0; lineIdx < 3; lineIdx++)
        {
            GameObject bar = new GameObject($"SkeletonBar_{lineIdx}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bar.layer = gameObject.layer;
            bar.transform.SetParent(cardGO.transform, false);
            RectTransform barRect = bar.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(0f, 1f);
            float barWidthAdjust = lineIdx == 1 ? -34f : (lineIdx == 2 ? -48f : -20f);
            barRect.anchoredPosition = new Vector2(10f, -44f - lineIdx * 18f);
            barRect.sizeDelta = new Vector2(barWidthAdjust, 10f);
            Image barImg = bar.GetComponent<Image>();
            barImg.sprite = GetRoundedSprite();
            barImg.type = Image.Type.Sliced;
            barImg.color = Color.Lerp(Color.white, app.accentColor, 0.25f);
        }

        // Caption pill at bottom
        TMP_Text captionTxt = CreateText("Caption", cardGO.transform, caption, 13.5f, FontStyles.Bold, PlayTextDark);
        RectTransform capRect = captionTxt.rectTransform;
        capRect.anchorMin = new Vector2(0f, 0f);
        capRect.anchorMax = new Vector2(1f, 0f);
        capRect.pivot = new Vector2(0.5f, 0f);
        capRect.anchoredPosition = new Vector2(0f, 8f);
        capRect.sizeDelta = new Vector2(-14f, 48f);
        captionTxt.alignment = TextAlignmentOptions.Center;
        captionTxt.textWrappingMode = TextWrappingModes.Normal;
    }

    private void ShowStorefrontView()
    {
        if (_storefrontView != null) _storefrontView.SetActive(true);
        if (_detailView != null) _detailView.SetActive(false);
        _selectedDetailAppId = null;
    }

    private void RefreshVisibleCardsUI()
    {
        foreach (CardViewRefs card in _cardViews.Values)
        {
            if (card != null && card.app != null)
            {
                ApplyStateToCardUI(card, GetOrCreateState(card.app));
            }
        }
    }

    private void UpdateFilterChipsUI()
    {
        if (_forYouChipImage != null && _forYouChipText != null)
        {
            _forYouChipImage.color = !_sortByTopCharts ? PlayGreenSoft : PlaySurfaceAlt;
            _forYouChipText.color = !_sortByTopCharts ? PlayGreen : PlayTextMuted;
        }

        if (_topChartsChipImage != null && _topChartsChipText != null)
        {
            _topChartsChipImage.color = _sortByTopCharts ? PlayGreenSoft : PlaySurfaceAlt;
            _topChartsChipText.color = _sortByTopCharts ? PlayGreen : PlayTextMuted;
        }
    }

    private void ApplyIconToWidget(AppStoreAppData app, Image targetImage, TMP_Text monogramText)
    {
        if (targetImage == null || app == null) return;

        Sprite icon = ResolveAppIcon(app);
        if (icon != null)
        {
            targetImage.sprite = icon;
            targetImage.type = Image.Type.Simple;
            targetImage.preserveAspect = true;
            targetImage.color = Color.white;
            if (monogramText != null) monogramText.gameObject.SetActive(false);
        }
        else
        {
            targetImage.sprite = GetRoundedSprite();
            targetImage.type = Image.Type.Sliced;
            targetImage.color = app.accentColor;
            if (monogramText != null)
            {
                monogramText.gameObject.SetActive(true);
                monogramText.text = BuildMonogram(app.displayName);
            }
        }
    }

    private static string BuildMonogram(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return "?";

        string trimmed = displayName.Trim();
        char first = char.ToUpperInvariant(trimmed[0]);
        for (int i = 1; i < trimmed.Length; i++)
        {
            if (char.IsUpper(trimmed[i]) || trimmed[i - 1] == ' ')
            {
                if (char.IsLetterOrDigit(trimmed[i]))
                {
                    return $"{first}{char.ToUpperInvariant(trimmed[i])}";
                }
            }
        }
        return first.ToString();
    }

    private GameObject CreateIconWidget(string name, Transform parent, float size, out Image iconImg, out TMP_Text monogramTxt)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(size, size);

        iconImg = go.GetComponent<Image>();
        iconImg.sprite = GetRoundedSprite();
        iconImg.type = Image.Type.Sliced;
        iconImg.raycastTarget = false;

        monogramTxt = CreateText("Monogram", go.transform, "", size * 0.36f, FontStyles.Bold, Color.white);
        StretchRect(monogramTxt.rectTransform, Vector2.zero, Vector2.zero);
        monogramTxt.alignment = TextAlignmentOptions.Center;
        monogramTxt.raycastTarget = false;

        return go;
    }

    private GameObject CreateProgressBlock(
        Transform parent,
        float height,
        float fontSize,
        float barHeight,
        out TMP_Text labelText,
        out RectTransform fillRect)
    {
        GameObject container = new GameObject("DownloadProgress", typeof(RectTransform), typeof(LayoutElement));
        container.layer = gameObject.layer;
        container.transform.SetParent(parent, false);
        container.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, height);
        LayoutElement progressLE = container.GetComponent<LayoutElement>();
        progressLE.minHeight = height;
        progressLE.preferredHeight = height;

        labelText = CreateText("ProgressText", container.transform, "Downloading... 0%", fontSize, FontStyles.Bold, PlayGreen);
        RectTransform lblRect = labelText.rectTransform;
        lblRect.anchorMin = new Vector2(0f, 1f);
        lblRect.anchorMax = new Vector2(1f, 1f);
        lblRect.pivot = new Vector2(0.5f, 1f);
        lblRect.anchoredPosition = Vector2.zero;
        lblRect.sizeDelta = new Vector2(0f, height - barHeight - 2f);
        labelText.alignment = TextAlignmentOptions.Left;
        labelText.raycastTarget = false;

        GameObject trackGO = new GameObject("Track", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        trackGO.layer = gameObject.layer;
        trackGO.transform.SetParent(container.transform, false);
        RectTransform trackRect = trackGO.GetComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(0f, 0f);
        trackRect.anchorMax = new Vector2(1f, 0f);
        trackRect.pivot = new Vector2(0.5f, 0f);
        trackRect.anchoredPosition = Vector2.zero;
        trackRect.sizeDelta = new Vector2(0f, barHeight);
        Image trackImg = trackGO.GetComponent<Image>();
        trackImg.sprite = GetRoundedSprite();
        trackImg.type = Image.Type.Sliced;
        trackImg.color = PlayProgressTrack;

        GameObject fillGO = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillGO.layer = gameObject.layer;
        fillGO.transform.SetParent(trackGO.transform, false);
        fillRect = fillGO.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fillImg = fillGO.GetComponent<Image>();
        fillImg.sprite = GetRoundedSprite();
        fillImg.type = Image.Type.Sliced;
        fillImg.color = PlayGreen;

        return container;
    }

    private (Button, Image, TMP_Text) CreateChipButton(string name, Transform parent, string text, float width, Action onClick)
    {
        GameObject chipGO = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Outline), typeof(Button));
        chipGO.layer = gameObject.layer;
        chipGO.transform.SetParent(parent, false);
        chipGO.GetComponent<RectTransform>().sizeDelta = new Vector2(width, 36f);

        Image img = chipGO.GetComponent<Image>();
        img.sprite = GetRoundedSprite();
        img.type = Image.Type.Sliced;
        img.color = PlaySurfaceAlt;

        UnityEngine.UI.Outline outline = chipGO.GetComponent<UnityEngine.UI.Outline>();
        outline.effectColor = PlayBorderGray;
        outline.effectDistance = new Vector2(1f, -1f);

        Button btn = chipGO.GetComponent<Button>();
        btn.targetGraphic = img;
        DisableNavigation(btn);
        btn.onClick.AddListener(() => onClick?.Invoke());

        TMP_Text label = CreateText("Text", chipGO.transform, text, 15.5f, FontStyles.Bold, PlayTextDark);
        StretchRect(label.rectTransform, Vector2.zero, Vector2.zero);
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;

        return (btn, img, label);
    }

    private TMP_Text CreateStatColumn(Transform parent, string subtitle)
    {
        GameObject col = new GameObject($"Stat_{subtitle}", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        col.layer = gameObject.layer;
        col.transform.SetParent(parent, false);
        LayoutElement colLE = col.GetComponent<LayoutElement>();
        colLE.flexibleWidth = 1f;
        colLE.minWidth = 60f;

        VerticalLayoutGroup layout = col.GetComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        TMP_Text valueTxt = CreateText("Value", col.transform, "-", 17.5f, FontStyles.Bold, PlayTextDark);
        valueTxt.alignment = TextAlignmentOptions.Center;

        TMP_Text subTxt = CreateText("Label", col.transform, subtitle, 13.5f, FontStyles.Normal, PlayTextMuted);
        subTxt.alignment = TextAlignmentOptions.Center;

        return valueTxt;
    }

    private void CreateVerticalDivider(Transform parent)
    {
        GameObject div = new GameObject("Divider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        div.layer = gameObject.layer;
        div.transform.SetParent(parent, false);
        LayoutElement le = div.GetComponent<LayoutElement>();
        le.minWidth = 1f;
        le.preferredWidth = 1f;
        le.flexibleWidth = 0f;
        div.GetComponent<Image>().color = PlayBorderGray;
    }

    private TMP_Text CreateText(string name, Transform parent, string content, float fontSize, FontStyles style, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        if (_sharedFont != null) tmp.font = _sharedFont;
        return tmp;
    }

    private static void StretchRect(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void DisableNavigation(Button button)
    {
        Navigation nav = button.navigation;
        nav.mode = Navigation.Mode.None;
        button.navigation = nav;
    }

    private Sprite GetPlayTriangleSprite()
    {
        if (_generatedPlaySprite != null) return _generatedPlaySprite;

        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            float ny = (y + 0.5f) / size; // 0..1
            float halfHeightAtEnd = Mathf.Abs(ny - 0.5f) * 2f; // 1 at bottom/top, 0 at center
            float maxX = (1f - halfHeightAtEnd * 0.92f) * 0.85f + 0.08f;

            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size;
                bool inside = nx >= 0.14f && nx <= maxX;
                pixels[y * size + x] = inside ? Color.white : Color.clear;
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        _generatedPlaySprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return _generatedPlaySprite;
    }

    private Sprite GetRoundedSprite()
    {
        if (_roundedCardSprite != null) return _roundedCardSprite;
        if (_generatedRoundedSprite == null)
        {
            _generatedRoundedSprite = CreateRoundedRectSprite(32, 8);
        }
        return _generatedRoundedSprite;
    }

    private static Sprite CreateRoundedRectSprite(int size, int radius)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float alpha = 1f;
                int dx = x < radius ? radius - x - 1 : (x >= size - radius ? x - (size - radius) : 0);
                int dy = y < radius ? radius - y - 1 : (y >= size - radius ? y - (size - radius) : 0);
                if (dx > 0 && dy > 0)
                {
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    alpha = Mathf.Clamp01(radius - dist);
                }
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius));
    }

    #endregion
}

internal static class TextAlignmentAnchorExtensions
{
    public static TextAnchor ToTextAnchor(this TextAlignmentOptions _) => TextAnchor.MiddleLeft;
}
