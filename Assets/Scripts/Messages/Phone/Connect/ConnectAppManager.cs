using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Modular Instagram/Facebook-style controller for the Connect social app.
/// Manages:
/// 1) Dedicated "Create your profile" screen (CreateProfileConcept) wired to Choice 1,
///    ReputationManager, ScenarioSystem variables, MessagesManager, and D1_Profile_End.
/// 2) Main social interface with a fixed bottom tab bar (Messages & Profile tabs with
///    clear active-tab indicators and unread message badge) reusing MessagingChannel.Connect.
/// 3) Data-driven protagonist posts synchronized with story events and SaveManager persistence.
/// </summary>
public class ConnectAppManager : MonoBehaviour
{
    public static ConnectAppManager Instance { get; private set; }

    private static readonly Color ScreenBgLight = new Color(0.985f, 0.985f, 0.992f, 1f);
    private static readonly Color CardWhite = new Color(1f, 1f, 1f, 1f);
    private static readonly Color CardSurfaceSoft = new Color(0.953f, 0.957f, 0.976f, 1f);
    private static readonly Color InkDark = new Color(0.086f, 0.086f, 0.118f, 1f);
    private static readonly Color MutedSlate = new Color(0.392f, 0.408f, 0.471f, 1f);
    private static readonly Color BorderDark = new Color(0.118f, 0.118f, 0.157f, 1f);
    private static readonly Color BorderSoft = new Color(0.847f, 0.859f, 0.902f, 1f);
    private static readonly Color AccentPink = new Color(0.886f, 0.212f, 0.459f, 1f);
    private static readonly Color AccentPlum = new Color(0.455f, 0.145f, 0.467f, 1f);
    private static readonly Color NavBarBg = new Color(0.098f, 0.067f, 0.145f, 0.97f);
    private static readonly Color NavPillActive = new Color(0.886f, 0.212f, 0.459f, 0.22f);
    private static readonly Color NavInactiveTint = new Color(0.706f, 0.663f, 0.784f, 1f);
    private static readonly Color TopHeaderBg = new Color(0.165f, 0.071f, 0.196f, 0.92f);
    private static readonly Color ErrorRed = new Color(0.839f, 0.149f, 0.239f, 1f);
    private static readonly Color RealNameFill = new Color(0.914f, 0.957f, 0.996f, 1f);

    private static readonly Vector2 ConnectBottomNavPosition = new Vector2(0f, -376f);
    private static readonly Vector2 ConnectBottomNavSize = new Vector2(456f, 64f);
    private static readonly Vector2 ConnectContactsPosition = new Vector2(0f, -4f);
    private static readonly Vector2 ConnectContactsSize = new Vector2(460f, 668f);

    // Authoritative static runtime store (survives inactive SmartPhoneV3 GameObject and scene transitions)
    private static ConnectProfileData s_profileData = new ConnectProfileData();
    private static readonly List<ConnectPostData> s_posts = new List<ConnectPostData>();
    private static ConnectTab s_activeTab = ConnectTab.Messages;
    private static bool s_staticSubscriptionsBound;
    private static readonly List<KeyValuePair<string, Action>> s_eventSubscriptions = new List<KeyValuePair<string, Action>>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticRuntimeState()
    {
        ClearEventSubscriptions();
        s_profileData = new ConnectProfileData();
        s_posts.Clear();
        s_activeTab = ConnectTab.Messages;
        s_staticSubscriptionsBound = false;
        Instance = null;
    }

    [Header("Data-Driven Post Catalog (Optional Overrides / Extensions)")]
    [Tooltip("Additional or overriding protagonist posts triggered automatically by EventBus events.")]
    [SerializeField] private List<ConnectPostCatalogEntry> _customPostCatalog = new List<ConnectPostCatalogEntry>();

    [Header("Avatar Options (Optional Inspector Overrides)")]
    [SerializeField] private Sprite _anonymousAvatarSprite;
    [SerializeField] private Sprite _bookwormAvatarSprite;
    [SerializeField] private Sprite _casualSelfieSprite;
    [SerializeField] private Sprite _personalSelfieSprite;

    [Header("Create Profile UI & Texts")]
    [SerializeField] private Sprite _cameraPickerSprite;
    [SerializeField] private string _avatarHoverTooltipText = "Choose profile picture";
    [SerializeField] private string _requiredFieldsErrorMessage = "* Please fill in all required fields marked with *.";

    [Header("Profile Typography (Scalable)")]
    [SerializeField] private float _profileDisplayNameFontSize = 24f;
    [SerializeField] private float _profileHandleFontSize = 17f;
    [SerializeField] private float _profileStatValueFontSize = 22f;
    [SerializeField] private float _profileStatLabelFontSize = 15f;
    [SerializeField] private float _profileDetailsFontSize = 16f;
    [SerializeField] private float _profileBioFontSize = 18f;
    [SerializeField] private float _profilePostsHeaderFontSize = 17f;
    [SerializeField] private float _profileEmptyPostsFontSize = 18f;
    [SerializeField] private float _postAuthorFontSize = 18f;
    [SerializeField] private float _postLocationTagFontSize = 15f;
    [SerializeField] private float _postTimestampFontSize = 14.5f;
    [SerializeField] private float _postCaptionFontSize = 17.5f;
    [SerializeField] private float _postMetricFontSize = 15.5f;
    [SerializeField] private float _postCommentFontSize = 15.5f;

    private PhoneStateMachine _phoneStateMachine;
    private TMP_FontAsset _sharedFont;
    private bool _uiBuilt;
    private bool _unreadListenerBound;

    // Cached original ContactsScreen rect so classic Messages SMS is never shifted
    private bool _cachedContactsDefaultRect;
    private Vector2 _defaultContactsAnchoredPos;
    private Vector2 _defaultContactsSizeDelta;

    // Procedural UI sprites (cached per instance)
    private Sprite _roundedCardSprite;
    private Sprite _roundedPillSprite;
    private Sprite _circleFillSprite;
    private Sprite _anonIconSprite;
    private Sprite _bookIconSprite;
    private Sprite _cameraIconProceduralSprite;
    private Sprite _messagesTabSprite;
    private Sprite _profileTabSprite;
    private Sprite _heartSprite;
    private Sprite _commentSprite;

    private struct RequiredFieldBinding
    {
        public TMP_InputField input;
        public UnityEngine.UI.Outline outline;
    }

    // View 1: Create Profile (CreateProfileConcept)
    private GameObject _createProfileView;
    private RectTransform _avatarPreviewRingRect;
    private Image _cameraIconImage;
    private GameObject _avatarHoverTooltip;
    private TMP_Text _avatarHoverTooltipLabel;
    private bool _hasSelectedProfilePhoto;
    private string _selectedAvatarImagePath = string.Empty;
    private Sprite _selectedAvatarSprite;
    private bool _selectedPhotoIsPersonal;
    private int _selectedAvatarIndex;
    private TMP_InputField _nameInput;
    private UnityEngine.UI.Outline _nameInputOutline;
    private Button _useRealNameButton;
    private Image _useRealNameButtonImage;
    private TMP_InputField _firstNameInput;
    private TMP_InputField _bioInput;
    private Toggle _locationToggle;
    private Image _locationToggleTrack;
    private RectTransform _locationToggleKnob;
    private TMP_Text _validationErrorText;
    private Button _doneButton;
    private bool _usedRealName;
    private bool _suppressNameChangedCallback;
    private readonly List<RequiredFieldBinding> _requiredFields = new List<RequiredFieldBinding>();

    // View 2: Main Connect Interface (Top Header + Profile Feed + Empty Inbox + Persistent Bottom Tabs)
    private GameObject _mainConnectView;
    private GameObject _topHeaderBar;
    private Image _topHeaderLogoImage;
    private TMP_Text _topHeaderContextPillText;

    private GameObject _emptyInboxPanel;
    private GameObject _profileView;
    private ScrollRect _profileScrollRect;
    private RectTransform _profileContentRoot;

    // Persistent Bottom Tab Bar (created under PhoneStateMachine so it persists in both Connect inbox/profile & Connect DMs)
    private GameObject _bottomNavBar;
    private Button _messagesTabButton;
    private Image _messagesTabBg;
    private Image _messagesTabAccent;
    private Image _messagesTabIcon;
    private TMP_Text _messagesTabLabel;

    private Button _profileTabButton;
    private Image _profileTabBg;
    private Image _profileTabAccent;
    private Image _profileTabIcon;
    private TMP_Text _profileTabLabel;

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

        _phoneStateMachine = GetComponentInParent<PhoneStateMachine>();
        EnsureStaticStorySubscriptions();
        EnsureUnreadListener(subscribe: true);
        EnsureUI();
    }

    private void OnEnable()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        _phoneStateMachine = _phoneStateMachine != null ? _phoneStateMachine : GetComponentInParent<PhoneStateMachine>();
        EnsureStaticStorySubscriptions();
        EnsureUnreadListener(subscribe: true);
    }

    private void OnDestroy()
    {
        EnsureUnreadListener(subscribe: false);
        if (Instance == this)
        {
            Instance = null;
        }
    }

    #region Static Persistence & Story Sync API

    public static void EnsureStaticStorySubscriptions()
    {
        if (s_staticSubscriptionsBound || EventBus.Instance == null)
        {
            return;
        }

        s_staticSubscriptionsBound = true;

        void Sub(string evt, Action callback)
        {
            s_eventSubscriptions.Add(new KeyValuePair<string, Action>(evt, callback));
            EventBus.Instance.Subscribe(evt, callback);
        }

        Sub("D1_Profile_End", () =>
        {
            SyncStateWithStoryProgression();
            Instance?.RefreshActiveViewsIfVisible();
        });

        Sub("Choice4_Safe", () => OnChoice4Resolved(DayManager.TestRoute.Safe));
        Sub("Choice4_Neutral", () => OnChoice4Resolved(DayManager.TestRoute.Neutral));
        Sub("Choice4_Risky", () => OnChoice4Resolved(DayManager.TestRoute.Risky));

        Sub("Choice5_Neutral", () =>
        {
            MarkPostDeleted("d2_choice4_post");
            Instance?.RefreshActiveViewsIfVisible();
        });

        Sub("Choice5_Risky", () =>
        {
            UpdatePostEngagement(
                "d2_choice4_post",
                likesDelta: 12,
                commentsCount: 41,
                topComment: "emma: stop trying to clap back in comments lol");
            Instance?.RefreshActiveViewsIfVisible();
        });

        Sub("Start_Day3", () =>
        {
            EnsureDay3MidweekPost();
            Instance?.RefreshActiveViewsIfVisible();
        });

        Sub("Choice10_Safe", () =>
        {
            ApplyPrivacyLockdown(fullLockdown: true);
            Instance?.RefreshActiveViewsIfVisible();
        });

        Sub("Choice10_Neutral", () =>
        {
            ApplyPrivacyLockdown(fullLockdown: false);
            Instance?.RefreshActiveViewsIfVisible();
        });
    }

    private static void ClearEventSubscriptions()
    {
        if (EventBus.Instance != null)
        {
            for (int i = 0; i < s_eventSubscriptions.Count; i++)
            {
                EventBus.Instance.Unsubscribe(s_eventSubscriptions[i].Key, s_eventSubscriptions[i].Value);
            }
        }
        s_eventSubscriptions.Clear();
    }

    public static ConnectProfileData ExportProfileSaveData()
    {
        SyncStateWithStoryProgression();
        return s_profileData != null ? s_profileData.Clone() : new ConnectProfileData();
    }

    public static List<ConnectPostData> ExportPostsSaveData()
    {
        SyncStateWithStoryProgression();
        List<ConnectPostData> copy = new List<ConnectPostData>(s_posts.Count);
        for (int i = 0; i < s_posts.Count; i++)
        {
            if (s_posts[i] != null)
            {
                copy.Add(s_posts[i].Clone());
            }
        }
        return copy;
    }

    public static void RestoreFromSaveData(
        ConnectProfileData savedProfile,
        List<ConnectPostData> savedPosts,
        bool profileCreated,
        int currentDay)
    {
        s_profileData = savedProfile != null ? savedProfile.Clone() : new ConnectProfileData();
        s_posts.Clear();

        if (savedPosts != null)
        {
            for (int i = 0; i < savedPosts.Count; i++)
            {
                if (savedPosts[i] != null && !string.IsNullOrEmpty(savedPosts[i].postId))
                {
                    s_posts.Add(savedPosts[i].Clone());
                }
            }
        }

        // Legacy save fallback: synthesize coherent profile and posts from recorded choices/day
        if (profileCreated)
        {
            if (!s_profileData.IsPopulated)
            {
                PopulateDefaultProfileFromChoice1();
            }

            if (s_posts.Count == 0)
            {
                EnsureInitialDay1Post();
            }

            SyncStateWithStoryProgression(forceDayMinimum: currentDay);
        }

        s_activeTab = ConnectTab.Messages;
        Instance?.RefreshActiveViewsIfVisible();
    }

    public static void ResetPersistedState()
    {
        s_profileData = new ConnectProfileData();
        s_posts.Clear();
        s_activeTab = ConnectTab.Messages;
    }

    public void ResetUIState()
    {
        ResetPersistedState();
        _usedRealName = false;
        _selectedAvatarIndex = 0;
        _hasSelectedProfilePhoto = false;
        _selectedAvatarImagePath = string.Empty;
        _selectedAvatarSprite = null;
        _selectedPhotoIsPersonal = false;

        if (_nameInput != null)
        {
            _suppressNameChangedCallback = true;
            _nameInput.text = string.Empty;
            _suppressNameChangedCallback = false;
        }
        if (_firstNameInput != null) _firstNameInput.text = string.Empty;
        if (_bioInput != null) _bioInput.text = string.Empty;
        if (_locationToggle != null)
        {
            _locationToggle.SetIsOnWithoutNotify(false);
            UpdateLocationToggleVisual();
        }
        if (_avatarHoverTooltip != null)
        {
            _avatarHoverTooltip.SetActive(false);
        }

        ClearValidationError();
        UpdateUseRealNameButtonVisual();
        UpdateAvatarPreviewUI();
        RestoreContactsDefaultRect();
        SetBottomNavVisible(false);
    }

    /// <summary>
    /// Ensures s_profileData and s_posts reflect recorded narrative choices and story day
    /// (handles debug day-skips, legacy saves, and events fired while the phone was closed).
    /// </summary>
    public static void SyncStateWithStoryProgression(int forceDayMinimum = 0)
    {
        bool profileCreated = (UIManager.Instance != null && UIManager.Instance.IsConnectProfileCreated) || forceDayMinimum >= 2;
        int currentDay = Mathf.Max(
            forceDayMinimum,
            DayManager.Instance != null ? DayManager.Instance.GetCurrentDay() : 1);

        if (currentDay >= 2 && !profileCreated)
        {
            profileCreated = true;
        }

        if (!profileCreated)
        {
            return;
        }

        if (!s_profileData.IsPopulated)
        {
            PopulateDefaultProfileFromChoice1();
        }

        EnsureInitialDay1Post();

        if (DayManager.Instance != null)
        {
            if (DayManager.Instance.TryGetRecordedChoice(4, out DayManager.TestRoute route4))
            {
                OnChoice4Resolved(route4);
            }

            if (DayManager.Instance.TryGetRecordedChoice(5, out DayManager.TestRoute route5))
            {
                if (route5 == DayManager.TestRoute.Neutral)
                {
                    MarkPostDeleted("d2_choice4_post");
                }
                else if (route5 == DayManager.TestRoute.Risky)
                {
                    UpdatePostEngagement(
                        "d2_choice4_post",
                        likesDelta: 0,
                        commentsCount: 41,
                        topComment: "emma: stop trying to clap back in comments lol");
                }
            }

            if (currentDay >= 3)
            {
                EnsureDay3MidweekPost();
            }

            if (DayManager.Instance.TryGetRecordedChoice(10, out DayManager.TestRoute route10))
            {
                if (route10 == DayManager.TestRoute.Safe)
                {
                    ApplyPrivacyLockdown(fullLockdown: true);
                }
                else if (route10 == DayManager.TestRoute.Neutral)
                {
                    ApplyPrivacyLockdown(fullLockdown: false);
                }
            }
        }
        else if (currentDay >= 3)
        {
            EnsureDay3MidweekPost();
        }
    }

    private static void PopulateDefaultProfileFromChoice1()
    {
        DayManager.TestRoute route1 = DayManager.TestRoute.Neutral;
        if (DayManager.Instance != null)
        {
            DayManager.Instance.TryGetRecordedChoice(1, out route1);
        }

        string activeName = PlayerNameService.GetActivePlayerName();
        s_profileData = new ConnectProfileData
        {
            resolvedPrivacyRoute = (int)route1,
            isPrivateAccount = false
        };

        switch (route1)
        {
            case DayManager.TestRoute.Safe:
                s_profileData.displayName = "nightreader";
                s_profileData.handle = "@nightreader";
                s_profileData.usedRealName = false;
                s_profileData.firstName = string.Empty;
                s_profileData.schoolName = string.Empty;
                s_profileData.allowLocationAccess = false;
                s_profileData.avatarIndex = 1;
                s_profileData.avatarImagePath = string.Empty;
                s_profileData.isPersonalPhoto = false;
                s_profileData.bioText = "Lost in mystery novels.";
                break;

            case DayManager.TestRoute.Risky:
                s_profileData.displayName = $"{activeName} Brooks";
                s_profileData.handle = FormatHandle(s_profileData.displayName);
                s_profileData.usedRealName = true;
                s_profileData.firstName = activeName;
                s_profileData.schoolName = "Rainfall High School";
                s_profileData.allowLocationAccess = true;
                s_profileData.avatarIndex = 3;
                s_profileData.avatarImagePath = "SiD/Luna/LunaFlirtSelfie1.png";
                s_profileData.isPersonalPhoto = true;
                s_profileData.bioText = "Sophomore | Rainfall High School";
                break;

            default:
                s_profileData.displayName = activeName;
                s_profileData.handle = FormatHandle(activeName);
                s_profileData.usedRealName = true;
                s_profileData.firstName = activeName;
                s_profileData.schoolName = string.Empty;
                s_profileData.allowLocationAccess = false;
                s_profileData.avatarIndex = 2;
                s_profileData.avatarImagePath = "SiD/Luna/LunaNormalSelfie1.png";
                s_profileData.isPersonalPhoto = true;
                s_profileData.bioText = "Books, playlists & late-night tea.";
                break;
        }
    }

    private static string ResolveDefaultLocationTag()
    {
        if (s_profileData == null || s_profileData.isPrivateAccount)
        {
            return string.Empty;
        }

        if (s_profileData.allowLocationAccess || !string.IsNullOrWhiteSpace(s_profileData.schoolName))
        {
            return !string.IsNullOrWhiteSpace(s_profileData.schoolName)
                ? s_profileData.schoolName
                : "Rainfall High School";
        }

        return string.Empty;
    }

    private static void EnsureInitialDay1Post()
    {
        if (FindPostById("d1_first_post") != null)
        {
            return;
        }

        s_posts.Add(new ConnectPostData
        {
            postId = "d1_first_post",
            caption = "Joined Connect! Currently obsessed with Lost Cause - chapter 9 had me suspicious of everyone.",
            imageKey = "LostCause.png",
            locationTag = ResolveDefaultLocationTag(),
            timestampLabel = "Day 1 - 1:10 PM",
            likesCount = 14,
            commentsCount = 3,
            topCommentPreview = "alex_reads: finally someone who finished Lost Cause!",
            dayPosted = 1,
            isDeleted = false
        });
    }

    private static void OnChoice4Resolved(DayManager.TestRoute route)
    {
        EnsureInitialDay1Post();
        if (FindPostById("d2_choice4_post") != null)
        {
            return;
        }

        bool isPrivate = s_profileData != null && s_profileData.isPrivateAccount;

        ConnectPostData post = route switch
        {
            DayManager.TestRoute.Safe => new ConnectPostData
            {
                postId = "d2_choice4_post",
                caption = "Desk setup and re-reading my favourite chapter of Lost Cause.",
                imageKey = "LostCause.png",
                locationTag = string.Empty,
                timestampLabel = "Day 2 - 4:05 PM",
                likesCount = 19,
                commentsCount = 4,
                topCommentPreview = "jamie: gotta start somewhere! sharing this",
                dayPosted = 2
            },
            DayManager.TestRoute.Risky => new ConnectPostData
            {
                postId = "d2_choice4_post",
                caption = "Out of class at Rainfall High! Heading down Maple Ave soon.",
                imageKey = "luna_flirt_selfie",
                locationTag = isPrivate ? string.Empty : "Rainfall High School",
                timestampLabel = "Day 2 - 4:05 PM",
                likesCount = 86,
                commentsCount = 24,
                topCommentPreview = "noah_k: rainfall high?! no way, see u around",
                dayPosted = 2
            },
            _ => new ConnectPostData
            {
                postId = "d2_choice4_post",
                caption = "Quick selfie after classes today.",
                imageKey = "luna_normal_selfie",
                locationTag = ResolveDefaultLocationTag(),
                timestampLabel = "Day 2 - 4:05 PM",
                likesCount = 47,
                commentsCount = 11,
                topCommentPreview = "jamie: wait u actually look so good, reposting!",
                dayPosted = 2
            }
        };

        s_posts.Add(post);
    }

    private static void EnsureDay3MidweekPost()
    {
        if (FindPostById("d3_midweek_post") != null)
        {
            return;
        }

        s_posts.Add(new ConnectPostData
        {
            postId = "d3_midweek_post",
            caption = "Midweek candy stash before morning class.",
            imageKey = "Candy.png",
            locationTag = ResolveDefaultLocationTag(),
            timestampLabel = "Day 3 - 8:10 AM",
            likesCount = 23,
            commentsCount = 5,
            topCommentPreview = "jamie: save me one!!",
            dayPosted = 3
        });
    }

    private static void MarkPostDeleted(string postId)
    {
        ConnectPostData post = FindPostById(postId);
        if (post != null)
        {
            post.isDeleted = true;
        }
    }

    private static void UpdatePostEngagement(string postId, int likesDelta, int commentsCount, string topComment)
    {
        ConnectPostData post = FindPostById(postId);
        if (post == null || post.isDeleted)
        {
            return;
        }

        if (commentsCount > post.commentsCount)
        {
            post.likesCount = Mathf.Max(0, post.likesCount + likesDelta);
            post.commentsCount = commentsCount;
            if (!string.IsNullOrEmpty(topComment))
            {
                post.topCommentPreview = topComment;
            }
        }
    }

    private static void ApplyPrivacyLockdown(bool fullLockdown)
    {
        if (s_profileData != null)
        {
            s_profileData.allowLocationAccess = false;
            if (fullLockdown)
            {
                s_profileData.isPrivateAccount = true;
                s_profileData.schoolName = string.Empty;
            }
        }

        for (int i = 0; i < s_posts.Count; i++)
        {
            ConnectPostData post = s_posts[i];
            if (post == null) continue;

            if (!string.IsNullOrEmpty(post.locationTag))
            {
                post.locationTag = string.Empty;
            }

            if (fullLockdown && post.postId == "d2_choice4_post" && post.imageKey == "luna_flirt_selfie")
            {
                post.isDeleted = true;
            }
        }
    }

    private static ConnectPostData FindPostById(string postId)
    {
        if (string.IsNullOrEmpty(postId)) return null;
        for (int i = 0; i < s_posts.Count; i++)
        {
            if (s_posts[i] != null && string.Equals(s_posts[i].postId, postId, StringComparison.Ordinal))
            {
                return s_posts[i];
            }
        }
        return null;
    }

    #endregion

    #region Public PhoneStateMachine / UIManager API

    public void SelectInitialTabOnOpen(bool preferMessagesIfUnread = true)
    {
        if (preferMessagesIfUnread && MessagesManager.Instance != null &&
            MessagesManager.Instance.GetChannelUnreadCount(MessagingChannel.Connect) > 0)
        {
            s_activeTab = ConnectTab.Messages;
        }
    }

    public void SelectTab(ConnectTab tab)
    {
        s_activeTab = tab;

        if (_phoneStateMachine != null && _phoneStateMachine.GetCurrentState() != PhoneState.CONNECT)
        {
            _phoneStateMachine.ChangeState((int)PhoneState.CONNECT);
            return;
        }

        RenderCurrentState();
    }

    public void OnConnectOpened()
    {
        EnsureUI();
        BindCatalogTriggers();
        SyncStateWithStoryProgression();
        RenderCurrentState();
    }

    public void OnConnectExited(PhoneState nextState)
    {
        if (_createProfileView != null) _createProfileView.SetActive(false);
        if (_mainConnectView != null) _mainConnectView.SetActive(false);
        if (_profileView != null) _profileView.SetActive(false);
        if (_emptyInboxPanel != null) _emptyInboxPanel.SetActive(false);

        if (nextState != PhoneState.CONNECT && _phoneStateMachine != null)
        {
            GameObject contactsTab = _phoneStateMachine.GetContactsTab();
            if (contactsTab != null && nextState != PhoneState.CONTACTS)
            {
                contactsTab.SetActive(false);
            }

            RestoreContactsDefaultRect();
        }

        SyncChromeVisibility(nextState);
    }

    public void SyncChromeVisibility(PhoneState phoneState)
    {
        bool profileCreated = UIManager.Instance != null && UIManager.Instance.IsConnectProfileCreated;
        bool showBottomNav = profileCreated && phoneState == PhoneState.CONNECT;

        SetBottomNavVisible(showBottomNav);

        if (!showBottomNav && phoneState != PhoneState.CONNECT)
        {
            RestoreContactsDefaultRect();
        }
    }

    public bool TryHandleBackButton()
    {
        // Top-level Connect tabs return cleanly to phone Home (IDLE) via PhoneStateMachine.
        return false;
    }

    public void RefreshMessagesEmptyState()
    {
        if (_phoneStateMachine == null || _phoneStateMachine.GetCurrentState() != PhoneState.CONNECT)
        {
            return;
        }

        bool profileCreated = UIManager.Instance != null && UIManager.Instance.IsConnectProfileCreated;
        if (!profileCreated || s_activeTab != ConnectTab.Messages)
        {
            if (_emptyInboxPanel != null) _emptyInboxPanel.SetActive(false);
            return;
        }

        MessagesUI ui = MessagesManager.Instance?.GetMessagesUI();
        int visibleCount = ui != null ? ui.GetVisibleContactCount() : 0;
        if (_emptyInboxPanel != null)
        {
            _emptyInboxPanel.SetActive(visibleCount == 0);
        }
    }

    private void RefreshActiveViewsIfVisible()
    {
        UpdateBottomNavUnreadBadge();

        if (_phoneStateMachine != null && _phoneStateMachine.GetCurrentState() == PhoneState.CONNECT && gameObject.activeInHierarchy)
        {
            RenderCurrentState();
        }
    }

    #endregion

    #region Core View Switching

    private void RenderCurrentState()
    {
        EnsureUI();

        bool profileCreated = UIManager.Instance != null && UIManager.Instance.IsConnectProfileCreated;
        if (!profileCreated)
        {
            RestoreContactsDefaultRect();
            if (_createProfileView != null) _createProfileView.SetActive(true);
            if (_mainConnectView != null) _mainConnectView.SetActive(false);
            if (_emptyInboxPanel != null) _emptyInboxPanel.SetActive(false);
            if (_profileView != null) _profileView.SetActive(false);

            GameObject contactsTab = _phoneStateMachine != null ? _phoneStateMachine.GetContactsTab() : null;
            if (contactsTab != null) contactsTab.SetActive(false);

            SetBottomNavVisible(false);
            ClearValidationError();
            UpdateAvatarPreviewUI();
            UpdateUseRealNameButtonVisual();
            return;
        }

        if (_createProfileView != null) _createProfileView.SetActive(false);
        if (_mainConnectView != null) _mainConnectView.SetActive(true);

        UpdateTopHeaderChrome();
        SetBottomNavVisible(true);
        UpdateBottomNavTabVisuals();
        UpdateBottomNavUnreadBadge();

        GameObject phoneContactsTab = _phoneStateMachine != null ? _phoneStateMachine.GetContactsTab() : null;
        MessagesUI messagesUI = MessagesManager.Instance?.GetMessagesUI();

        if (s_activeTab == ConnectTab.Messages)
        {
            if (_profileView != null) _profileView.SetActive(false);

            if (phoneContactsTab != null)
            {
                ApplyConnectContactsRect(phoneContactsTab);
                phoneContactsTab.SetActive(true);
            }

            if (MessagesManager.Instance != null && MessagesManager.Instance.GetMessagingChannel() != MessagingChannel.Connect)
            {
                MessagesManager.Instance.SetMessagingChannel(MessagingChannel.Connect);
            }
            else
            {
                messagesUI?.RefreshContacts();
            }

            RefreshMessagesEmptyState();
        }
        else
        {
            if (_emptyInboxPanel != null) _emptyInboxPanel.SetActive(false);
            if (phoneContactsTab != null) phoneContactsTab.SetActive(false);
            if (_profileView != null) _profileView.SetActive(true);

            RebuildProfileAndPostsFeed();
        }
    }

    private void ApplyConnectContactsRect(GameObject contactsTab)
    {
        if (contactsTab == null || !(contactsTab.transform is RectTransform contactsRect))
        {
            return;
        }

        if (!_cachedContactsDefaultRect)
        {
            _defaultContactsAnchoredPos = contactsRect.anchoredPosition;
            _defaultContactsSizeDelta = contactsRect.sizeDelta;
            _cachedContactsDefaultRect = true;
        }

        contactsRect.anchoredPosition = ConnectContactsPosition;
        contactsRect.sizeDelta = ConnectContactsSize;
    }

    private void RestoreContactsDefaultRect()
    {
        if (!_cachedContactsDefaultRect || _phoneStateMachine == null)
        {
            return;
        }

        GameObject contactsTab = _phoneStateMachine.GetContactsTab();
        if (contactsTab != null && contactsTab.transform is RectTransform contactsRect)
        {
            contactsRect.anchoredPosition = _defaultContactsAnchoredPos;
            contactsRect.sizeDelta = _defaultContactsSizeDelta;
        }
    }

    private void SetBottomNavVisible(bool visible)
    {
        if (_bottomNavBar != null && _bottomNavBar.activeSelf != visible)
        {
            _bottomNavBar.SetActive(visible);
        }

        if (visible)
        {
            UpdateBottomNavTabVisuals();
            UpdateBottomNavUnreadBadge();
        }
    }

    #endregion

    #region Create Profile Screen (CreateProfileConcept)

    private void BuildCreateProfileView()
    {
        _createProfileView = new GameObject(
            "CreateProfileView",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(UnityEngine.UI.Outline));
        _createProfileView.layer = gameObject.layer;
        _createProfileView.transform.SetParent(transform, false);

        RectTransform viewRect = _createProfileView.GetComponent<RectTransform>();
        StretchRect(viewRect, Vector2.zero, Vector2.zero);

        Image bgImg = _createProfileView.GetComponent<Image>();
        bgImg.sprite = GetRoundedCardSprite();
        bgImg.type = Image.Type.Sliced;
        bgImg.color = ScreenBgLight;

        UnityEngine.UI.Outline cardOutline = _createProfileView.GetComponent<UnityEngine.UI.Outline>();
        cardOutline.effectColor = new Color(0.84f, 0.85f, 0.90f, 1f);
        cardOutline.effectDistance = new Vector2(1.5f, -1.5f);

        // Subtle social branding top bar strip
        GameObject accentStrip = new GameObject("TopAccent", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        accentStrip.layer = gameObject.layer;
        accentStrip.transform.SetParent(_createProfileView.transform, false);
        RectTransform stripRect = accentStrip.GetComponent<RectTransform>();
        stripRect.anchorMin = new Vector2(0f, 1f);
        stripRect.anchorMax = new Vector2(1f, 1f);
        stripRect.pivot = new Vector2(0.5f, 1f);
        stripRect.anchoredPosition = Vector2.zero;
        stripRect.sizeDelta = new Vector2(0f, 8f);
        accentStrip.GetComponent<Image>().color = AccentPink;

        // 1. Header: "Create your profile"
        TMP_Text titleText = CreateText("Title", _createProfileView.transform, "Create your profile", 27f, FontStyles.Bold, InkDark);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -20f);
        titleRect.sizeDelta = new Vector2(-32f, 40f);
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.raycastTarget = false;

        // 2. Centered Circular Camera / Profile Picture Picker + Hover Tooltip
        GameObject ringGO = new GameObject(
            "AvatarSelectorCircle",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        ringGO.layer = gameObject.layer;
        ringGO.transform.SetParent(_createProfileView.transform, false);
        RectTransform ringRect = ringGO.GetComponent<RectTransform>();
        ringRect.anchorMin = new Vector2(0.5f, 1f);
        ringRect.anchorMax = new Vector2(0.5f, 1f);
        ringRect.pivot = new Vector2(0.5f, 1f);
        ringRect.anchoredPosition = new Vector2(0f, -68f);
        ringRect.sizeDelta = new Vector2(104f, 104f);

        Image ringImg = ringGO.GetComponent<Image>();
        ringImg.sprite = GetCircleFillSprite();
        ringImg.preserveAspect = true;
        ringImg.color = BorderDark;

        Button avatarBtn = ringGO.GetComponent<Button>();
        DisableNavigation(avatarBtn);
        avatarBtn.targetGraphic = ringImg;
        avatarBtn.onClick.AddListener(OnPickProfilePhotoClicked);

        GameObject innerCircle = new GameObject("InnerBackdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        innerCircle.layer = gameObject.layer;
        innerCircle.transform.SetParent(ringGO.transform, false);
        StretchRect(innerCircle.GetComponent<RectTransform>(), new Vector2(3f, 3f), new Vector2(-3f, -3f));
        Image innerImg = innerCircle.GetComponent<Image>();
        innerImg.sprite = GetCircleFillSprite();
        innerImg.preserveAspect = true;
        innerImg.color = CardSurfaceSoft;
        innerImg.raycastTarget = false;

        GameObject camGO = new GameObject("CameraIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        camGO.layer = gameObject.layer;
        camGO.transform.SetParent(ringGO.transform, false);
        RectTransform camRect = camGO.GetComponent<RectTransform>();
        camRect.anchorMin = new Vector2(0.5f, 0.5f);
        camRect.anchorMax = new Vector2(0.5f, 0.5f);
        camRect.pivot = new Vector2(0.5f, 0.5f);
        camRect.anchoredPosition = Vector2.zero;
        camRect.sizeDelta = new Vector2(50f, 50f);
        _cameraIconImage = camGO.GetComponent<Image>();
        _cameraIconImage.sprite = _cameraPickerSprite != null ? _cameraPickerSprite : GetCameraPickerSprite();
        _cameraIconImage.preserveAspect = true;
        _cameraIconImage.color = InkDark;
        _cameraIconImage.raycastTarget = false;

        GameObject avatarInner = new GameObject("AvatarMasked", typeof(RectTransform));
        avatarInner.layer = gameObject.layer;
        avatarInner.transform.SetParent(ringGO.transform, false);
        _avatarPreviewRingRect = avatarInner.GetComponent<RectTransform>();
        StretchRect(_avatarPreviewRingRect, new Vector2(3f, 3f), new Vector2(-3f, -3f));
        avatarInner.SetActive(false);

        UIHoverFeedback avatarHover = ringGO.AddComponent<UIHoverFeedback>();
        avatarHover.EnableScale = true;
        avatarHover.HoverScaleMultiplier = 1.06f;

        _avatarHoverTooltip = new GameObject("AvatarHoverTooltip", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        _avatarHoverTooltip.layer = gameObject.layer;
        _avatarHoverTooltip.transform.SetParent(_createProfileView.transform, false);
        RectTransform tipRect = _avatarHoverTooltip.GetComponent<RectTransform>();
        tipRect.anchorMin = new Vector2(0.5f, 1f);
        tipRect.anchorMax = new Vector2(0.5f, 1f);
        tipRect.pivot = new Vector2(0.5f, 1f);
        tipRect.anchoredPosition = new Vector2(0f, -176f);
        tipRect.sizeDelta = new Vector2(224f, 28f);

        Image tipBg = _avatarHoverTooltip.GetComponent<Image>();
        tipBg.sprite = GetRoundedPillSprite();
        tipBg.type = Image.Type.Sliced;
        tipBg.color = new Color(0.086f, 0.086f, 0.118f, 0.94f);
        tipBg.raycastTarget = false;

        _avatarHoverTooltipLabel = CreateText(
            "TooltipLabel",
            _avatarHoverTooltip.transform,
            string.IsNullOrWhiteSpace(_avatarHoverTooltipText) ? "Choose profile picture" : _avatarHoverTooltipText,
            14.5f,
            FontStyles.Bold,
            Color.white);
        StretchRect(_avatarHoverTooltipLabel.rectTransform, new Vector2(10f, 0f), new Vector2(-10f, 0f));
        _avatarHoverTooltipLabel.alignment = TextAlignmentOptions.Center;
        _avatarHoverTooltipLabel.raycastTarget = false;
        _avatarHoverTooltip.SetActive(false);

        PointerHoverRelay hoverRelay = ringGO.AddComponent<PointerHoverRelay>();
        hoverRelay.Initialize(() => SetAvatarHoverTooltipVisible(true), () => SetAvatarHoverTooltipVisible(false));

        _requiredFields.Clear();

        // 3. Name:* Row + "use ur real name" button (aligned per CreateProfileConcept)
        CreateFormLabel("NameLabel", _createProfileView.transform, "Name:<color=#D6263D>*</color>", -214f);
        (_nameInput, _nameInputOutline) = CreateFormInputField("NameInput", _createProfileView.transform, "Enter display name...", -210f, 28, singleLine: true, fieldHeight: 44f);
        _nameInput.onValueChanged.AddListener(OnNameFieldChanged);
        _requiredFields.Add(new RequiredFieldBinding { input = _nameInput, outline = _nameInputOutline });

        BuildUseRealNameButton(-264f);

        // 4. First name: Optional Row
        CreateFormLabel("FirstNameLabel", _createProfileView.transform, "First name:", -314f);
        (_firstNameInput, _) = CreateFormInputField("FirstNameInput", _createProfileView.transform, "Optional", -310f, 24, singleLine: true, fieldHeight: 44f);

        // 5. Description: Optional Multiline Bio Row
        CreateFormLabel("DescriptionLabel", _createProfileView.transform, "Description:", -370f);
        (_bioInput, _) = CreateFormInputField("BioInput", _createProfileView.transform, "Add a short bio... (Optional)", -366f, 140, singleLine: false, fieldHeight: 88f);

        // 6. Location Permission Toggle Row (replaces School input)
        BuildLocationPermissionToggle(-470f);

        // 7. Validation warning & subtle privacy hint
        _validationErrorText = CreateText(
            "ValidationError",
            _createProfileView.transform,
            GetRequiredFieldsErrorText(),
            15.5f,
            FontStyles.Bold,
            ErrorRed);
        RectTransform errRect = _validationErrorText.rectTransform;
        errRect.anchorMin = new Vector2(0f, 1f);
        errRect.anchorMax = new Vector2(1f, 1f);
        errRect.pivot = new Vector2(0.5f, 1f);
        errRect.anchoredPosition = new Vector2(0f, -550f);
        errRect.sizeDelta = new Vector2(-36f, 44f);
        _validationErrorText.alignment = TextAlignmentOptions.Center;
        _validationErrorText.textWrappingMode = TextWrappingModes.Normal;
        _validationErrorText.raycastTarget = false;
        _validationErrorText.gameObject.SetActive(false);

        TMP_Text privacyNote = CreateText(
            "PrivacyHint",
            _createProfileView.transform,
            "* Required field. Only share details you want visible online.",
            13.5f,
            FontStyles.Italic,
            MutedSlate);
        RectTransform noteRect = privacyNote.rectTransform;
        noteRect.anchorMin = new Vector2(0f, 1f);
        noteRect.anchorMax = new Vector2(1f, 1f);
        noteRect.pivot = new Vector2(0.5f, 1f);
        noteRect.anchoredPosition = new Vector2(0f, -600f);
        noteRect.sizeDelta = new Vector2(-44f, 34f);
        privacyNote.alignment = TextAlignmentOptions.Center;
        privacyNote.textWrappingMode = TextWrappingModes.Normal;
        privacyNote.raycastTarget = false;

        // 8. Rounded "Done" Button at the bottom
        GameObject doneGO = new GameObject(
            "DoneButton",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(UnityEngine.UI.Outline),
            typeof(Button));
        doneGO.layer = gameObject.layer;
        doneGO.transform.SetParent(_createProfileView.transform, false);

        RectTransform doneRect = doneGO.GetComponent<RectTransform>();
        doneRect.anchorMin = new Vector2(0.5f, 0f);
        doneRect.anchorMax = new Vector2(0.5f, 0f);
        doneRect.pivot = new Vector2(0.5f, 0f);
        doneRect.anchoredPosition = new Vector2(0f, 30f);
        doneRect.sizeDelta = new Vector2(192f, 54f);

        Image doneImg = doneGO.GetComponent<Image>();
        doneImg.sprite = GetRoundedPillSprite();
        doneImg.type = Image.Type.Sliced;
        doneImg.color = CardWhite;

        UnityEngine.UI.Outline doneOutline = doneGO.GetComponent<UnityEngine.UI.Outline>();
        doneOutline.effectColor = BorderDark;
        doneOutline.effectDistance = new Vector2(2.5f, -2.5f);

        _doneButton = doneGO.GetComponent<Button>();
        DisableNavigation(_doneButton);
        _doneButton.targetGraphic = doneImg;
        _doneButton.onClick.AddListener(OnDoneCreateProfileClicked);
        doneGO.AddComponent<UIHoverFeedback>();

        TMP_Text doneText = CreateText("DoneText", doneGO.transform, "Done", 24f, FontStyles.Bold, InkDark);
        StretchRect(doneText.rectTransform, Vector2.zero, Vector2.zero);
        doneText.alignment = TextAlignmentOptions.Center;
        doneText.raycastTarget = false;

        UpdateAvatarPreviewUI();
    }

    private void CreateFormLabel(string name, Transform parent, string text, float topY)
    {
        TMP_Text label = CreateText(name, parent, text, 20f, FontStyles.Bold, InkDark);
        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f, topY);
        rect.sizeDelta = new Vector2(126f, 40f);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.raycastTarget = false;
    }

    private (TMP_InputField, UnityEngine.UI.Outline) CreateFormInputField(
        string name,
        Transform parent,
        string placeholder,
        float topY,
        int charLimit,
        bool singleLine = true,
        float fieldHeight = 46f)
    {
        GameObject inputGO = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(UnityEngine.UI.Outline),
            typeof(TMP_InputField));
        inputGO.layer = gameObject.layer;
        inputGO.transform.SetParent(parent, false);

        RectTransform inputRect = inputGO.GetComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0f, 1f);
        inputRect.anchorMax = new Vector2(1f, 1f);
        inputRect.pivot = new Vector2(0f, 1f);
        inputRect.offsetMin = new Vector2(154f, topY - fieldHeight);
        inputRect.offsetMax = new Vector2(-24f, topY);

        Image bgImg = inputGO.GetComponent<Image>();
        bgImg.sprite = GetRoundedCardSprite();
        bgImg.type = Image.Type.Sliced;
        bgImg.color = CardWhite;

        UnityEngine.UI.Outline outline = inputGO.GetComponent<UnityEngine.UI.Outline>();
        outline.effectColor = BorderDark;
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject textArea = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        textArea.layer = gameObject.layer;
        textArea.transform.SetParent(inputGO.transform, false);
        RectTransform areaRect = textArea.GetComponent<RectTransform>();
        StretchRect(areaRect, new Vector2(10f, singleLine ? 4f : 8f), new Vector2(-10f, singleLine ? -4f : -8f));

        TMP_Text placeholderTxt = CreateText("Placeholder", textArea.transform, placeholder, 16.5f, FontStyles.Italic, new Color(0.55f, 0.56f, 0.62f, 1f));
        StretchRect(placeholderTxt.rectTransform, Vector2.zero, Vector2.zero);
        placeholderTxt.alignment = singleLine ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.TopLeft;
        placeholderTxt.textWrappingMode = singleLine ? TextWrappingModes.NoWrap : TextWrappingModes.Normal;
        placeholderTxt.raycastTarget = false;

        TMP_Text textComp = CreateText("Text", textArea.transform, string.Empty, 18f, FontStyles.Normal, InkDark);
        StretchRect(textComp.rectTransform, Vector2.zero, Vector2.zero);
        textComp.alignment = singleLine ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.TopLeft;
        textComp.textWrappingMode = singleLine ? TextWrappingModes.NoWrap : TextWrappingModes.Normal;
        textComp.richText = false;

        TMP_InputField field = inputGO.GetComponent<TMP_InputField>();
        Navigation nav = field.navigation;
        nav.mode = Navigation.Mode.None;
        field.navigation = nav;
        field.targetGraphic = bgImg;
        field.textViewport = areaRect;
        field.textComponent = textComp;
        field.placeholder = placeholderTxt;
        field.pointSize = 18f;
        field.characterLimit = charLimit;
        field.lineType = singleLine ? TMP_InputField.LineType.SingleLine : TMP_InputField.LineType.MultiLineNewline;
        field.restoreOriginalTextOnEscape = false;
        field.richText = false;
        if (_sharedFont != null) field.fontAsset = _sharedFont;

        return (field, outline);
    }

    private void BuildUseRealNameButton(float topY)
    {
        GameObject btnGO = new GameObject(
            "UseRealNameButton",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(UnityEngine.UI.Outline),
            typeof(Button));
        btnGO.layer = gameObject.layer;
        btnGO.transform.SetParent(_createProfileView.transform, false);

        RectTransform btnRect = btnGO.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0f, 1f);
        btnRect.anchorMax = new Vector2(1f, 1f);
        btnRect.pivot = new Vector2(0f, 1f);
        btnRect.offsetMin = new Vector2(154f, topY - 36f);
        btnRect.offsetMax = new Vector2(-58f, topY);

        _useRealNameButtonImage = btnGO.GetComponent<Image>();
        _useRealNameButtonImage.color = CardWhite;

        UnityEngine.UI.Outline outline = btnGO.GetComponent<UnityEngine.UI.Outline>();
        outline.effectColor = BorderDark;
        outline.effectDistance = new Vector2(2f, -2f);

        _useRealNameButton = btnGO.GetComponent<Button>();
        DisableNavigation(_useRealNameButton);
        _useRealNameButton.targetGraphic = _useRealNameButtonImage;
        _useRealNameButton.onClick.AddListener(OnUseRealNameClicked);

        TMP_Text label = CreateText("Label", btnGO.transform, "use ur real name", 16f, FontStyles.Bold, InkDark);
        StretchRect(label.rectTransform, new Vector2(6f, 0f), new Vector2(-6f, 0f));
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
    }

    private void BuildLocationPermissionToggle(float topY)
    {
        GameObject rowGO = new GameObject(
            "LocationPermissionRow",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(UnityEngine.UI.Outline),
            typeof(Toggle));
        rowGO.layer = gameObject.layer;
        rowGO.transform.SetParent(_createProfileView.transform, false);

        RectTransform rowRect = rowGO.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f, 1f);
        rowRect.pivot = new Vector2(0.5f, 1f);
        rowRect.offsetMin = new Vector2(24f, topY - 68f);
        rowRect.offsetMax = new Vector2(-24f, topY);

        Image rowBg = rowGO.GetComponent<Image>();
        rowBg.sprite = GetRoundedCardSprite();
        rowBg.type = Image.Type.Sliced;
        rowBg.color = CardWhite;

        UnityEngine.UI.Outline rowOutline = rowGO.GetComponent<UnityEngine.UI.Outline>();
        rowOutline.effectColor = BorderSoft;
        rowOutline.effectDistance = new Vector2(1.5f, -1.5f);

        TMP_Text titleTxt = CreateText("Title", rowGO.transform, "Device location", 17f, FontStyles.Bold, InkDark);
        RectTransform titleRect = titleTxt.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.offsetMin = new Vector2(14f, -30f);
        titleRect.offsetMax = new Vector2(-82f, -8f);
        titleTxt.alignment = TextAlignmentOptions.MidlineLeft;
        titleTxt.raycastTarget = false;

        TMP_Text subTxt = CreateText("Subtitle", rowGO.transform, "Allow Connect to access your device location", 13.5f, FontStyles.Normal, MutedSlate);
        RectTransform subRect = subTxt.rectTransform;
        subRect.anchorMin = new Vector2(0f, 0f);
        subRect.anchorMax = new Vector2(1f, 1f);
        subRect.pivot = new Vector2(0f, 0.5f);
        subRect.offsetMin = new Vector2(14f, 6f);
        subRect.offsetMax = new Vector2(-82f, -28f);
        subTxt.alignment = TextAlignmentOptions.TopLeft;
        subTxt.textWrappingMode = TextWrappingModes.Normal;
        subTxt.raycastTarget = false;

        GameObject trackGO = new GameObject("SwitchTrack", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        trackGO.layer = gameObject.layer;
        trackGO.transform.SetParent(rowGO.transform, false);
        RectTransform trackRect = trackGO.GetComponent<RectTransform>();
        trackRect.anchorMin = new Vector2(1f, 0.5f);
        trackRect.anchorMax = new Vector2(1f, 0.5f);
        trackRect.pivot = new Vector2(1f, 0.5f);
        trackRect.anchoredPosition = new Vector2(-14f, 0f);
        trackRect.sizeDelta = new Vector2(54f, 30f);

        _locationToggleTrack = trackGO.GetComponent<Image>();
        _locationToggleTrack.sprite = GetRoundedPillSprite();
        _locationToggleTrack.type = Image.Type.Sliced;
        _locationToggleTrack.raycastTarget = false;

        GameObject knobGO = new GameObject("SwitchKnob", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        knobGO.layer = gameObject.layer;
        knobGO.transform.SetParent(trackGO.transform, false);
        _locationToggleKnob = knobGO.GetComponent<RectTransform>();
        _locationToggleKnob.anchorMin = new Vector2(0.5f, 0.5f);
        _locationToggleKnob.anchorMax = new Vector2(0.5f, 0.5f);
        _locationToggleKnob.pivot = new Vector2(0.5f, 0.5f);
        _locationToggleKnob.sizeDelta = new Vector2(24f, 24f);

        Image knobImg = knobGO.GetComponent<Image>();
        knobImg.sprite = GetCircleFillSprite();
        knobImg.preserveAspect = true;
        knobImg.color = CardWhite;
        knobImg.raycastTarget = false;

        _locationToggle = rowGO.GetComponent<Toggle>();
        Navigation toggleNav = _locationToggle.navigation;
        toggleNav.mode = Navigation.Mode.None;
        _locationToggle.navigation = toggleNav;
        _locationToggle.targetGraphic = rowBg;
        _locationToggle.graphic = null;
        _locationToggle.isOn = false;
        _locationToggle.onValueChanged.AddListener(_ => UpdateLocationToggleVisual());
        UpdateLocationToggleVisual();
    }

    private void UpdateLocationToggleVisual()
    {
        bool isOn = _locationToggle != null && _locationToggle.isOn;
        if (_locationToggleTrack != null)
        {
            _locationToggleTrack.color = isOn ? AccentPink : new Color(0.76f, 0.78f, 0.84f, 1f);
        }
        if (_locationToggleKnob != null)
        {
            _locationToggleKnob.anchoredPosition = new Vector2(isOn ? 12f : -12f, 0f);
        }
    }

    private void SetAvatarHoverTooltipVisible(bool visible)
    {
        if (_avatarHoverTooltip == null) return;
        if (visible && _avatarHoverTooltipLabel != null)
        {
            _avatarHoverTooltipLabel.text = string.IsNullOrWhiteSpace(_avatarHoverTooltipText)
                ? "Choose profile picture"
                : _avatarHoverTooltipText;
        }
        _avatarHoverTooltip.SetActive(visible);
    }

    private void OnPickProfilePhotoClicked()
    {
        SetAvatarHoverTooltipVisible(false);
        UnfocusAllInputs();

        GalleryManager gallery = GalleryManager.Instance != null
            ? GalleryManager.Instance
            : (_phoneStateMachine != null && _phoneStateMachine.GetGalleryTab() != null
                ? _phoneStateMachine.GetGalleryTab().GetComponentInChildren<GalleryManager>(true)
                : FindAnyObjectByType<GalleryManager>(FindObjectsInactive.Include));

        if (gallery == null)
        {
            Debug.LogWarning("[ConnectAppManager] GalleryManager not found for profile photo selection.");
            return;
        }

        List<GalleryPhotoData> extraBasePhotos = new List<GalleryPhotoData>
        {
            new GalleryPhotoData(
                id: "BookwormAvatar",
                title: "Bookworm Avatar",
                imagePath: string.Empty,
                sprite: _bookwormAvatarSprite != null ? _bookwormAvatarSprite : GetBookAvatarSprite(),
                unlocked: true,
                isPersonalPhoto: false)
        };

        if (_casualSelfieSprite != null)
        {
            extraBasePhotos.Add(new GalleryPhotoData(
                id: "LunaNormalSelfie",
                title: "Casual Selfie",
                imagePath: "SiD/Luna/LunaNormalSelfie1.png",
                sprite: _casualSelfieSprite,
                unlocked: true,
                isPersonalPhoto: true));
        }

        if (_personalSelfieSprite != null)
        {
            extraBasePhotos.Add(new GalleryPhotoData(
                id: "LunaFlirtSelfie",
                title: "Personal Selfie",
                imagePath: "SiD/Luna/LunaFlirtSelfie1.png",
                sprite: _personalSelfieSprite,
                unlocked: true,
                isPersonalPhoto: true));
        }

        string bannerTitle = string.IsNullOrWhiteSpace(_avatarHoverTooltipText)
            ? "Choose profile picture"
            : _avatarHoverTooltipText;

        gallery.OpenProfilePhotoSelect(
            OnProfilePhotoChosenFromGallery,
            OnProfilePhotoSelectionCancelled,
            bannerTitle,
            extraBasePhotos);
    }

    private void OnProfilePhotoChosenFromGallery(GalleryPhotoData photo)
    {
        if (photo != null)
        {
            _hasSelectedProfilePhoto = true;
            _selectedAvatarImagePath = photo.GetEffectiveImageKey();
            _selectedAvatarSprite = photo.ResolveSprite();
            _selectedPhotoIsPersonal = photo.IsPersonalPortraitPhoto();
            _selectedAvatarIndex = _selectedPhotoIsPersonal ? 2 : 0;
        }

        ReturnFromGalleryToCreateProfile();
    }

    private void OnProfilePhotoSelectionCancelled()
    {
        ReturnFromGalleryToCreateProfile();
    }

    private void ReturnFromGalleryToCreateProfile()
    {
        if (_phoneStateMachine == null)
        {
            _phoneStateMachine = GetComponentInParent<PhoneStateMachine>();
        }

        if (_phoneStateMachine != null && _phoneStateMachine.GetCurrentState() != PhoneState.CONNECT)
        {
            _phoneStateMachine.ChangeState((int)PhoneState.CONNECT);
        }
        else
        {
            RenderCurrentState();
        }
    }

    private void UpdateAvatarPreviewUI()
    {
        bool showChosenPhoto = _hasSelectedProfilePhoto && _selectedAvatarSprite != null;

        if (_cameraIconImage != null)
        {
            if (!showChosenPhoto)
            {
                _cameraIconImage.sprite = _cameraPickerSprite != null ? _cameraPickerSprite : GetCameraPickerSprite();
            }
            _cameraIconImage.gameObject.SetActive(!showChosenPhoto);
        }

        if (_avatarPreviewRingRect != null)
        {
            _avatarPreviewRingRect.gameObject.SetActive(showChosenPhoto);
            if (showChosenPhoto)
            {
                MessagesUI.ApplyCircularAvatar(_avatarPreviewRingRect, _selectedAvatarSprite, Color.white, coverCrop: true);
            }
        }
    }

    private void OnUseRealNameClicked()
    {
        string realName = PlayerNameService.GetActivePlayerName();
        if (string.IsNullOrWhiteSpace(realName))
        {
            realName = PlayerNameService.DefaultPlayerName;
        }

        _usedRealName = true;
        if (_nameInput != null)
        {
            _suppressNameChangedCallback = true;
            _nameInput.text = realName;
            _suppressNameChangedCallback = false;
        }

        ClearValidationError();
        UpdateUseRealNameButtonVisual();
    }

    private void OnNameFieldChanged(string currentText)
    {
        if (_suppressNameChangedCallback)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(currentText))
        {
            ClearValidationError();
        }

        string realName = PlayerNameService.GetActivePlayerName();
        _usedRealName = !string.IsNullOrWhiteSpace(currentText) &&
                        !string.IsNullOrWhiteSpace(realName) &&
                        currentText.IndexOf(realName, StringComparison.OrdinalIgnoreCase) >= 0;
        UpdateUseRealNameButtonVisual();
    }

    private void UpdateUseRealNameButtonVisual()
    {
        if (_useRealNameButtonImage != null)
        {
            _useRealNameButtonImage.color = _usedRealName ? RealNameFill : CardWhite;
        }
    }

    private string GetRequiredFieldsErrorText()
    {
        return string.IsNullOrWhiteSpace(_requiredFieldsErrorMessage)
            ? "* Please fill in all required fields marked with *."
            : _requiredFieldsErrorMessage;
    }

    private void ClearValidationError()
    {
        if (_validationErrorText != null)
        {
            _validationErrorText.gameObject.SetActive(false);
        }

        for (int i = 0; i < _requiredFields.Count; i++)
        {
            if (_requiredFields[i].outline != null)
            {
                _requiredFields[i].outline.effectColor = BorderDark;
            }
        }

        if (_nameInputOutline != null)
        {
            _nameInputOutline.effectColor = BorderDark;
        }
    }

    private bool ValidateRequiredFields(out TMP_InputField firstMissing)
    {
        firstMissing = null;
        bool allValid = true;

        for (int i = 0; i < _requiredFields.Count; i++)
        {
            RequiredFieldBinding binding = _requiredFields[i];
            if (binding.input == null) continue;

            bool missing = string.IsNullOrWhiteSpace(binding.input.text);
            if (binding.outline != null)
            {
                binding.outline.effectColor = missing ? ErrorRed : BorderDark;
            }

            if (missing)
            {
                allValid = false;
                if (firstMissing == null)
                {
                    firstMissing = binding.input;
                }
            }
        }

        return allValid;
    }

    private void ShowRequiredFieldsValidationError(TMP_InputField firstMissing)
    {
        if (_validationErrorText != null)
        {
            _validationErrorText.text = GetRequiredFieldsErrorText();
            _validationErrorText.gameObject.SetActive(true);
        }

        if (firstMissing != null)
        {
            firstMissing.Select();
            firstMissing.ActivateInputField();
        }
    }

    private void OnDoneCreateProfileClicked()
    {
        if (!ValidateRequiredFields(out TMP_InputField firstMissing))
        {
            ShowRequiredFieldsValidationError(firstMissing);
            return;
        }

        string enteredName = _nameInput != null ? _nameInput.text.Trim() : string.Empty;
        CompleteProfileCreation(enteredName);
    }

    private void CompleteProfileCreation(string displayName)
    {
        UnfocusAllInputs();
        SetAvatarHoverTooltipVisible(false);

        string firstName = _firstNameInput != null ? _firstNameInput.text.Trim() : string.Empty;
        string enteredBio = _bioInput != null ? _bioInput.text.Trim() : string.Empty;
        bool allowLocation = _locationToggle != null && _locationToggle.isOn;
        bool isPersonalPhoto = _hasSelectedProfilePhoto && _selectedPhotoIsPersonal;

        string realName = PlayerNameService.GetActivePlayerName();
        bool disclosesRealName = _usedRealName ||
                                 (!string.IsNullOrWhiteSpace(realName) &&
                                  displayName.IndexOf(realName, StringComparison.OrdinalIgnoreCase) >= 0);

        int routeIndex = ComputeChoice1PrivacyRoute(
            displayName,
            disclosesRealName,
            firstName,
            allowLocation,
            isPersonalPhoto);

        int branchNodeId = routeIndex switch
        {
            0 => 1,
            2 => 3,
            _ => 2
        };

        string defaultBio = routeIndex switch
        {
            0 => "Here for reading discussions and fan theories.",
            2 => allowLocation ? "Student at Rainfall High School" : "Open book, say hi!",
            _ => !string.IsNullOrWhiteSpace(firstName) ? $"Hi, I'm {firstName}." : "New to Connect."
        };

        string bioSummary = !string.IsNullOrWhiteSpace(enteredBio) ? enteredBio : defaultBio;

        s_profileData = new ConnectProfileData
        {
            displayName = displayName,
            handle = FormatHandle(displayName),
            usedRealName = disclosesRealName,
            firstName = firstName,
            schoolName = allowLocation ? "Rainfall High School" : string.Empty,
            avatarIndex = _selectedAvatarIndex,
            avatarImagePath = _hasSelectedProfilePhoto ? _selectedAvatarImagePath : string.Empty,
            allowLocationAccess = allowLocation,
            isPersonalPhoto = isPersonalPhoto,
            isPrivateAccount = false,
            resolvedPrivacyRoute = routeIndex,
            bioText = bioSummary
        };

        s_posts.Clear();
        EnsureInitialDay1Post();

        ApplyChoice1NarrativeOutcome(routeIndex, branchNodeId);

        s_activeTab = ConnectTab.Profile;
        RenderCurrentState();

        if (SaveManager.HasInstance && SaveManager.Instance.HasActiveSlot)
        {
            SaveManager.Instance.SaveCurrentSlot();
        }
    }

    private static int ComputeChoice1PrivacyRoute(
        string displayName,
        bool disclosesRealName,
        string firstName,
        bool allowLocationAccess,
        bool hasPersonalPhoto)
    {
        bool hasFirstName = !string.IsNullOrWhiteSpace(firstName);
        bool multiWordRealName = disclosesRealName && displayName.Contains(" ");

        int exposureScore = 0;
        if (disclosesRealName) exposureScore += 1;
        if (multiWordRealName) exposureScore += 1;
        if (hasFirstName) exposureScore += 1;
        if (allowLocationAccess) exposureScore += 2;
        if (hasPersonalPhoto) exposureScore += 1;

        if (exposureScore == 0)
        {
            return 0; // Safe: anonymous username + non-personal/no photo
        }

        if (exposureScore >= 3 || (allowLocationAccess && (disclosesRealName || hasFirstName || hasPersonalPhoto)))
        {
            return 2; // Risky: full/location/photo exposure
        }

        return 1; // Neutral: partial details
    }

    private static void ApplyChoice1NarrativeOutcome(int routeIndex, int branchNodeId)
    {
        MessagesManager messages = MessagesManager.Instance;
        NarrativeNode node0 = messages != null ? messages.GetNarrativeNode(0) : null;
        Option chosenOption = null;
        if (node0 != null && node0.Options() != null && routeIndex >= 0 && routeIndex < node0.Options().Length)
        {
            chosenOption = node0.Options()[routeIndex];
        }

        if (chosenOption != null)
        {
            chosenOption.ApplyReputationImpact();
        }
        else if (ReputationManager.Instance != null)
        {
            if (routeIndex == 0) ReputationManager.Instance.AddReputation(DayManager.ChoiceReputationStep);
            else if (routeIndex == 2) ReputationManager.Instance.SubtractReputation(DayManager.ChoiceReputationStep);
        }

        string choiceEvent = !string.IsNullOrEmpty(chosenOption?.eventToPublish)
            ? chosenOption.eventToPublish
            : routeIndex switch
            {
                0 => "Choice1_Safe",
                2 => "Choice1_Risky",
                _ => "Choice1_Neutral"
            };

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Publish(choiceEvent);
        }

        if (DecisionDataManager.Instance != null)
        {
            DecisionDataManager.Instance.SetOrAddIntVariable("Scenario_Node_0", routeIndex);
            DecisionDataManager.Instance.SetOrAddIntVariable("Scenario_NodeNext_0", branchNodeId);
            DecisionDataManager.Instance.SetOrAddIntVariable("Scenario_Choice_1", routeIndex);
        }

        if (messages != null)
        {
            messages.MarkContactNotificationsAsRead("Connect");
            messages.SetContactNarrativeStateSilently("Connect", branchNodeId);
        }

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Publish("D1_Profile_End");
        }
    }

    private static void UnfocusAllInputs()
    {
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
        {
            if (EventSystem.current.currentSelectedGameObject.GetComponent<TMP_InputField>() != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
    }

    #endregion

    #region Main Connect Interface (Instagram / Facebook Style)

    private void BuildMainConnectView()
    {
        _mainConnectView = new GameObject("MainConnectView", typeof(RectTransform));
        _mainConnectView.layer = gameObject.layer;
        _mainConnectView.transform.SetParent(transform, false);
        StretchRect(_mainConnectView.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

        BuildTopHeaderBar();
        BuildEmptyInboxPanel();
        BuildProfileFeedView();
        BuildPersistentBottomNavBar();
    }

    private void BuildTopHeaderBar()
    {
        _topHeaderBar = new GameObject("ConnectTopHeader", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        _topHeaderBar.layer = gameObject.layer;
        _topHeaderBar.transform.SetParent(_mainConnectView.transform, false);

        RectTransform headerRect = _topHeaderBar.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = Vector2.zero;
        headerRect.sizeDelta = new Vector2(0f, 64f);

        Image bgImg = _topHeaderBar.GetComponent<Image>();
        bgImg.sprite = GetRoundedCardSprite();
        bgImg.type = Image.Type.Sliced;
        bgImg.color = TopHeaderBg;

        GameObject logoGO = new GameObject("Logo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        logoGO.layer = gameObject.layer;
        logoGO.transform.SetParent(_topHeaderBar.transform, false);
        RectTransform logoRect = logoGO.GetComponent<RectTransform>();
        logoRect.anchorMin = new Vector2(0f, 0.5f);
        logoRect.anchorMax = new Vector2(0f, 0.5f);
        logoRect.pivot = new Vector2(0f, 0.5f);
        logoRect.anchoredPosition = new Vector2(14f, 0f);
        logoRect.sizeDelta = new Vector2(36f, 36f);
        _topHeaderLogoImage = logoGO.GetComponent<Image>();
        _topHeaderLogoImage.preserveAspect = true;
        _topHeaderLogoImage.raycastTarget = false;

        TMP_Text brandText = CreateText("Brand", _topHeaderBar.transform, "Connect", 24f, FontStyles.Bold, Color.white);
        RectTransform brandRect = brandText.rectTransform;
        brandRect.anchorMin = new Vector2(0f, 0f);
        brandRect.anchorMax = new Vector2(0.55f, 1f);
        brandRect.offsetMin = new Vector2(58f, 0f);
        brandRect.offsetMax = new Vector2(0f, 0f);
        brandText.alignment = TextAlignmentOptions.MidlineLeft;
        brandText.raycastTarget = false;

        GameObject pillGO = new GameObject("ContextPill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        pillGO.layer = gameObject.layer;
        pillGO.transform.SetParent(_topHeaderBar.transform, false);
        RectTransform pillRect = pillGO.GetComponent<RectTransform>();
        pillRect.anchorMin = new Vector2(1f, 0.5f);
        pillRect.anchorMax = new Vector2(1f, 0.5f);
        pillRect.pivot = new Vector2(1f, 0.5f);
        pillRect.anchoredPosition = new Vector2(-12f, 0f);
        pillRect.sizeDelta = new Vector2(146f, 30f);
        Image pillImg = pillGO.GetComponent<Image>();
        pillImg.sprite = GetRoundedPillSprite();
        pillImg.type = Image.Type.Sliced;
        pillImg.color = new Color(1f, 1f, 1f, 0.14f);

        _topHeaderContextPillText = CreateText("PillText", pillGO.transform, "Direct", 14f, FontStyles.Bold, Color.white);
        StretchRect(_topHeaderContextPillText.rectTransform, new Vector2(8f, 0f), new Vector2(-8f, 0f));
        _topHeaderContextPillText.alignment = TextAlignmentOptions.Center;
        _topHeaderContextPillText.overflowMode = TextOverflowModes.Ellipsis;
        _topHeaderContextPillText.textWrappingMode = TextWrappingModes.NoWrap;
        _topHeaderContextPillText.raycastTarget = false;

        GameObject divider = new GameObject("Divider", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        divider.layer = gameObject.layer;
        divider.transform.SetParent(_topHeaderBar.transform, false);
        RectTransform divRect = divider.GetComponent<RectTransform>();
        divRect.anchorMin = new Vector2(0f, 0f);
        divRect.anchorMax = new Vector2(1f, 0f);
        divRect.pivot = new Vector2(0.5f, 0f);
        divRect.sizeDelta = new Vector2(0f, 2f);
        divider.GetComponent<Image>().color = AccentPink;
    }

    private void BuildEmptyInboxPanel()
    {
        _emptyInboxPanel = new GameObject(
            "EmptyInboxCard",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image));
        _emptyInboxPanel.layer = gameObject.layer;
        _emptyInboxPanel.transform.SetParent(_mainConnectView.transform, false);

        RectTransform rect = _emptyInboxPanel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 10f);
        rect.sizeDelta = new Vector2(360f, 164f);

        Image bgImg = _emptyInboxPanel.GetComponent<Image>();
        bgImg.sprite = GetRoundedCardSprite();
        bgImg.type = Image.Type.Sliced;
        bgImg.color = new Color(1f, 1f, 1f, 0.94f);

        TMP_Text title = CreateText("Title", _emptyInboxPanel.transform, "No Direct Messages Yet", 20f, FontStyles.Bold, InkDark);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -26f);
        titleRect.sizeDelta = new Vector2(-32f, 34f);
        title.alignment = TextAlignmentOptions.Center;

        TMP_Text body = CreateText(
            "Body",
            _emptyInboxPanel.transform,
            "People you connect with will message you here. Check your Profile tab below to view your posts!",
            15.5f,
            FontStyles.Normal,
            MutedSlate);
        StretchRect(body.rectTransform, new Vector2(22f, 20f), new Vector2(-22f, -66f));
        body.alignment = TextAlignmentOptions.Center;
        body.textWrappingMode = TextWrappingModes.Normal;

        _emptyInboxPanel.SetActive(false);
    }

    private void BuildProfileFeedView()
    {
        _profileView = new GameObject("ProfileFeedView", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        _profileView.layer = gameObject.layer;
        _profileView.transform.SetParent(_mainConnectView.transform, false);

        Image bg = _profileView.GetComponent<Image>();
        bg.sprite = null;
        bg.color = ScreenBgLight;
        bg.raycastTarget = false;

        RectTransform viewRect = _profileView.GetComponent<RectTransform>();
        StretchRect(viewRect, new Vector2(0f, 44f), new Vector2(0f, -64f));

        GameObject scrollGO = new GameObject("ProfileScrollView", typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect));
        scrollGO.layer = gameObject.layer;
        scrollGO.transform.SetParent(_profileView.transform, false);
        RectTransform scrollRectTransform = scrollGO.GetComponent<RectTransform>();
        StretchRect(scrollRectTransform, Vector2.zero, Vector2.zero);

        GameObject contentGO = new GameObject(
            "Content",
            typeof(RectTransform),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        contentGO.layer = gameObject.layer;
        contentGO.transform.SetParent(scrollGO.transform, false);

        _profileContentRoot = contentGO.GetComponent<RectTransform>();
        _profileContentRoot.anchorMin = new Vector2(0f, 1f);
        _profileContentRoot.anchorMax = new Vector2(1f, 1f);
        _profileContentRoot.pivot = new Vector2(0.5f, 1f);
        _profileContentRoot.anchoredPosition = Vector2.zero;
        _profileContentRoot.sizeDelta = Vector2.zero;

        VerticalLayoutGroup vLayout = contentGO.GetComponent<VerticalLayoutGroup>();
        vLayout.spacing = 12f;
        vLayout.padding = new RectOffset(8, 8, 10, 20);
        vLayout.childControlWidth = true;
        vLayout.childControlHeight = true;
        vLayout.childForceExpandWidth = true;
        vLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentGO.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _profileScrollRect = scrollGO.GetComponent<ScrollRect>();
        _profileScrollRect.viewport = scrollRectTransform;
        _profileScrollRect.content = _profileContentRoot;
        _profileScrollRect.horizontal = false;
        _profileScrollRect.vertical = true;
        _profileScrollRect.movementType = ScrollRect.MovementType.Elastic;
        _profileScrollRect.scrollSensitivity = 25f;

        _profileView.SetActive(false);
    }

    private void BuildPersistentBottomNavBar()
    {
        Transform parentRoot = _phoneStateMachine != null ? _phoneStateMachine.transform : transform;

        Transform existing = parentRoot.Find("ConnectBottomNavBar");
        if (existing != null)
        {
            Destroy(existing.gameObject);
        }

        _bottomNavBar = new GameObject(
            "ConnectBottomNavBar",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(UnityEngine.UI.Outline));
        _bottomNavBar.layer = gameObject.layer;
        _bottomNavBar.transform.SetParent(parentRoot, false);

        RectTransform navRect = _bottomNavBar.GetComponent<RectTransform>();
        navRect.anchorMin = new Vector2(0.5f, 0.5f);
        navRect.anchorMax = new Vector2(0.5f, 0.5f);
        navRect.pivot = new Vector2(0.5f, 0.5f);
        navRect.anchoredPosition = ConnectBottomNavPosition;
        navRect.sizeDelta = ConnectBottomNavSize;

        Transform homeBtn = parentRoot.Find("HomeButton");
        if (homeBtn != null)
        {
            _bottomNavBar.transform.SetSiblingIndex(homeBtn.GetSiblingIndex());
        }

        Image navImg = _bottomNavBar.GetComponent<Image>();
        navImg.sprite = GetRoundedCardSprite();
        navImg.type = Image.Type.Sliced;
        navImg.color = NavBarBg;

        UnityEngine.UI.Outline navOutline = _bottomNavBar.GetComponent<UnityEngine.UI.Outline>();
        navOutline.effectColor = new Color(0.28f, 0.21f, 0.39f, 1f);
        navOutline.effectDistance = new Vector2(1.5f, -1.5f);

        GameObject rowGO = new GameObject("TabsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowGO.layer = gameObject.layer;
        rowGO.transform.SetParent(_bottomNavBar.transform, false);
        StretchRect(rowGO.GetComponent<RectTransform>(), new Vector2(8f, 4f), new Vector2(-8f, -4f));

        HorizontalLayoutGroup hlg = rowGO.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8f;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        (_messagesTabButton, _messagesTabBg, _messagesTabAccent, _messagesTabIcon, _messagesTabLabel) = CreateBottomTabItem(
            "MessagesTabButton",
            rowGO.transform,
            "Messages",
            GetMessagesTabSprite(),
            () => SelectTab(ConnectTab.Messages));

        (_profileTabButton, _profileTabBg, _profileTabAccent, _profileTabIcon, _profileTabLabel) = CreateBottomTabItem(
            "ProfileTabButton",
            rowGO.transform,
            "Profile",
            GetProfileTabSprite(),
            () => SelectTab(ConnectTab.Profile));

        _bottomNavBar.SetActive(false);
    }

    private (Button, Image, Image, Image, TMP_Text) CreateBottomTabItem(
        string name,
        Transform parent,
        string labelText,
        Sprite iconSprite,
        Action onClick)
    {
        GameObject tabGO = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        tabGO.layer = gameObject.layer;
        tabGO.transform.SetParent(parent, false);

        Image bgImg = tabGO.GetComponent<Image>();
        bgImg.sprite = GetRoundedPillSprite();
        bgImg.type = Image.Type.Sliced;
        bgImg.color = Color.clear;

        Button btn = tabGO.GetComponent<Button>();
        DisableNavigation(btn);
        btn.targetGraphic = bgImg;
        btn.onClick.AddListener(() => onClick?.Invoke());

        // Top active indicator bar
        GameObject accentGO = new GameObject("ActiveIndicator", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        accentGO.layer = gameObject.layer;
        accentGO.transform.SetParent(tabGO.transform, false);
        RectTransform accentRect = accentGO.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0.18f, 1f);
        accentRect.anchorMax = new Vector2(0.82f, 1f);
        accentRect.pivot = new Vector2(0.5f, 1f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(0f, 4f);
        Image accentImg = accentGO.GetComponent<Image>();
        accentImg.color = AccentPink;
        accentImg.raycastTarget = false;

        // Icon
        GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGO.layer = gameObject.layer;
        iconGO.transform.SetParent(tabGO.transform, false);
        RectTransform iconRect = iconGO.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = new Vector2(0f, -8f);
        iconRect.sizeDelta = new Vector2(22f, 22f);
        Image iconImg = iconGO.GetComponent<Image>();
        iconImg.sprite = iconSprite;
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;

        // Label
        TMP_Text label = CreateText("Label", tabGO.transform, labelText, 14f, FontStyles.Bold, NavInactiveTint);
        RectTransform lblRect = label.rectTransform;
        lblRect.anchorMin = new Vector2(0f, 0f);
        lblRect.anchorMax = new Vector2(1f, 0f);
        lblRect.pivot = new Vector2(0.5f, 0f);
        lblRect.anchoredPosition = new Vector2(0f, 3f);
        lblRect.sizeDelta = new Vector2(-8f, 22f);
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;

        return (btn, bgImg, accentImg, iconImg, label);
    }

    private void UpdateTopHeaderChrome()
    {
        if (_topHeaderLogoImage != null && _phoneStateMachine != null)
        {
            Sprite connectIcon = _phoneStateMachine.GetConnectAppIcon();
            if (connectIcon != null)
            {
                _topHeaderLogoImage.sprite = connectIcon;
            }
        }

        if (_topHeaderContextPillText != null)
        {
            if (s_activeTab == ConnectTab.Messages)
            {
                _topHeaderContextPillText.text = "Messages";
            }
            else
            {
                string handle = s_profileData != null && !string.IsNullOrEmpty(s_profileData.handle)
                    ? s_profileData.handle
                    : "@profile";
                _topHeaderContextPillText.text = handle;
            }
        }
    }

    private void UpdateBottomNavTabVisuals()
    {
        bool messagesActive = s_activeTab == ConnectTab.Messages;
        bool profileActive = s_activeTab == ConnectTab.Profile;

        if (_messagesTabBg != null) _messagesTabBg.color = messagesActive ? NavPillActive : Color.clear;
        if (_messagesTabAccent != null) _messagesTabAccent.enabled = messagesActive;
        if (_messagesTabIcon != null) _messagesTabIcon.color = messagesActive ? AccentPink : NavInactiveTint;
        if (_messagesTabLabel != null)
        {
            _messagesTabLabel.color = messagesActive ? Color.white : NavInactiveTint;
            _messagesTabLabel.fontStyle = messagesActive ? FontStyles.Bold : FontStyles.Normal;
        }

        if (_profileTabBg != null) _profileTabBg.color = profileActive ? NavPillActive : Color.clear;
        if (_profileTabAccent != null) _profileTabAccent.enabled = profileActive;
        if (_profileTabIcon != null) _profileTabIcon.color = profileActive ? AccentPink : NavInactiveTint;
        if (_profileTabLabel != null)
        {
            _profileTabLabel.color = profileActive ? Color.white : NavInactiveTint;
            _profileTabLabel.fontStyle = profileActive ? FontStyles.Bold : FontStyles.Normal;
        }
    }

    private void UpdateBottomNavUnreadBadge()
    {
        if (_messagesTabButton == null)
        {
            return;
        }

        int unreadCount = MessagesManager.Instance != null
            ? MessagesManager.Instance.GetChannelUnreadCount(MessagingChannel.Connect)
            : 0;

        MessagesUI.ApplyNotificationBadge(
            _messagesTabButton.transform,
            unreadCount,
            null,
            _sharedFont,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 0.5f),
            new Vector2(28f, -14f),
            new Vector2(22f, 22f),
            10f,
            15f);
    }

    #endregion

    #region Profile & Protagonist Posts Feed

    private void RebuildProfileAndPostsFeed()
    {
        if (_profileContentRoot == null)
        {
            return;
        }

        for (int i = _profileContentRoot.childCount - 1; i >= 0; i--)
        {
            Destroy(_profileContentRoot.GetChild(i).gameObject);
        }

        SyncStateWithStoryProgression();
        ConnectProfileData profile = s_profileData ?? new ConnectProfileData();

        List<ConnectPostData> visiblePosts = new List<ConnectPostData>();
        for (int i = s_posts.Count - 1; i >= 0; i--)
        {
            if (s_posts[i] != null && !s_posts[i].isDeleted)
            {
                visiblePosts.Add(s_posts[i]);
            }
        }

        BuildProfileHeaderCard(profile, visiblePosts.Count);
        BuildPostsSectionHeader(visiblePosts.Count);

        if (visiblePosts.Count == 0)
        {
            BuildEmptyPostsCard();
        }
        else
        {
            for (int i = 0; i < visiblePosts.Count; i++)
            {
                BuildPostCard(profile, visiblePosts[i]);
            }
        }

        Canvas.ForceUpdateCanvases();
        if (_profileScrollRect != null)
        {
            _profileScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    private void BuildProfileHeaderCard(ConnectProfileData profile, int postCount)
    {
        GameObject card = new GameObject(
            "ProfileHeaderCard",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(UnityEngine.UI.Outline),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        card.layer = gameObject.layer;
        card.transform.SetParent(_profileContentRoot, false);

        Image cardBg = card.GetComponent<Image>();
        cardBg.sprite = GetRoundedCardSprite();
        cardBg.type = Image.Type.Sliced;
        cardBg.color = CardWhite;

        UnityEngine.UI.Outline cardOutline = card.GetComponent<UnityEngine.UI.Outline>();
        cardOutline.effectColor = BorderSoft;
        cardOutline.effectDistance = new Vector2(1f, -1f);

        VerticalLayoutGroup vlg = card.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(14, 14, 14, 14);
        vlg.spacing = 10f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter csf = card.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Top Row: Flat circular Avatar directly on card + Counters (Posts / Followers / Following)
        GameObject topRow = new GameObject("TopRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        topRow.layer = gameObject.layer;
        topRow.transform.SetParent(card.transform, false);
        LayoutElement topLE = topRow.GetComponent<LayoutElement>();
        topLE.minHeight = 86f;
        topLE.preferredHeight = 86f;

        HorizontalLayoutGroup topHLG = topRow.GetComponent<HorizontalLayoutGroup>();
        topHLG.spacing = 12f;
        topHLG.childAlignment = TextAnchor.MiddleLeft;
        topHLG.childControlWidth = true;
        topHLG.childControlHeight = true;
        topHLG.childForceExpandWidth = false;
        topHLG.childForceExpandHeight = false;

        GameObject avatarGO = new GameObject("ProfileAvatar", typeof(RectTransform), typeof(LayoutElement));
        avatarGO.layer = gameObject.layer;
        avatarGO.transform.SetParent(topRow.transform, false);
        RectTransform avRect = avatarGO.GetComponent<RectTransform>();
        avRect.sizeDelta = new Vector2(80f, 80f);
        LayoutElement avLE = avatarGO.GetComponent<LayoutElement>();
        avLE.minWidth = 80f;
        avLE.preferredWidth = 80f;
        avLE.minHeight = 80f;
        avLE.preferredHeight = 80f;
        avLE.flexibleWidth = 0f;
        avLE.flexibleHeight = 0f;
        MessagesUI.ApplyCircularAvatar(avRect, ResolveProfileAvatarSprite(profile), CardSurfaceSoft, coverCrop: true);

        int followers = ComputeFollowerCount(profile, postCount);
        int following = profile.resolvedPrivacyRoute == 0 ? 18 : (profile.resolvedPrivacyRoute == 2 ? 94 : 42);
        CreateProfileStatCell(topRow.transform, postCount.ToString(), "Posts");
        CreateProfileStatCell(topRow.transform, followers.ToString(), "Followers");
        CreateProfileStatCell(topRow.transform, following.ToString(), "Following");

        // Identity Row: Display name + @handle + Privacy Pill
        string displayName = string.IsNullOrWhiteSpace(profile.displayName)
            ? PlayerNameService.GetActivePlayerName()
            : profile.displayName;
        string handle = !string.IsNullOrWhiteSpace(profile.handle)
            ? profile.handle
            : FormatHandle(displayName);

        TMP_Text nameTmp = CreateText("DisplayName", card.transform, displayName, _profileDisplayNameFontSize, FontStyles.Bold, InkDark);
        nameTmp.textWrappingMode = TextWrappingModes.Normal;

        TMP_Text handleTmp = CreateText("Handle", card.transform, handle, _profileHandleFontSize, FontStyles.Normal, MutedSlate);

        // Optional disclosed details & privacy status badge
        List<string> detailParts = new List<string>();
        detailParts.Add(GetPrivacyStatusLabel(profile));
        if (!string.IsNullOrWhiteSpace(profile.firstName))
        {
            detailParts.Add($"First name: {profile.firstName}");
        }
        if (profile.allowLocationAccess)
        {
            detailParts.Add(!string.IsNullOrWhiteSpace(profile.schoolName)
                ? $"Location: {profile.schoolName}"
                : "Location: Enabled");
        }
        else if (!string.IsNullOrWhiteSpace(profile.schoolName))
        {
            detailParts.Add($"School: {profile.schoolName}");
        }

        TMP_Text metaTmp = CreateText("Details", card.transform, string.Join("  |  ", detailParts), _profileDetailsFontSize, FontStyles.Bold, AccentPlum);
        metaTmp.textWrappingMode = TextWrappingModes.Normal;

        if (!string.IsNullOrWhiteSpace(profile.bioText))
        {
            TMP_Text bioTmp = CreateText("Bio", card.transform, profile.bioText, _profileBioFontSize, FontStyles.Normal, InkDark);
            bioTmp.textWrappingMode = TextWrappingModes.Normal;
        }
    }

    private void CreateProfileStatCell(Transform parent, string value, string label)
    {
        GameObject cell = new GameObject($"Stat_{label}", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
        cell.layer = gameObject.layer;
        cell.transform.SetParent(parent, false);
        LayoutElement le = cell.GetComponent<LayoutElement>();
        le.flexibleWidth = 1f;
        le.minWidth = 64f;
        le.minHeight = 68f;
        le.preferredHeight = 68f;

        VerticalLayoutGroup vlg = cell.GetComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.spacing = 4f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        TMP_Text valText = CreateText("Value", cell.transform, value, _profileStatValueFontSize, FontStyles.Bold, InkDark);
        valText.alignment = TextAlignmentOptions.Center;

        TMP_Text lblText = CreateText("Label", cell.transform, label, _profileStatLabelFontSize, FontStyles.Normal, MutedSlate);
        lblText.alignment = TextAlignmentOptions.Center;
    }

    private void BuildPostsSectionHeader(int count)
    {
        GameObject bar = new GameObject(
            "PostsSectionHeader",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(LayoutElement));
        bar.layer = gameObject.layer;
        bar.transform.SetParent(_profileContentRoot, false);
        LayoutElement le = bar.GetComponent<LayoutElement>();
        le.minHeight = 44f;
        le.preferredHeight = 44f;

        Image img = bar.GetComponent<Image>();
        img.sprite = GetRoundedPillSprite();
        img.type = Image.Type.Sliced;
        img.color = new Color(1f, 1f, 1f, 0.92f);

        TMP_Text txt = CreateText("Title", bar.transform, $"YOUR POSTS ({count})", _profilePostsHeaderFontSize, FontStyles.Bold, AccentPlum);
        StretchRect(txt.rectTransform, new Vector2(14f, 0f), new Vector2(-14f, 0f));
        txt.alignment = TextAlignmentOptions.MidlineLeft;
    }

    private void BuildEmptyPostsCard()
    {
        GameObject card = new GameObject("EmptyPostsCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        card.layer = gameObject.layer;
        card.transform.SetParent(_profileContentRoot, false);
        LayoutElement le = card.GetComponent<LayoutElement>();
        le.minHeight = 104f;
        le.preferredHeight = 104f;

        Image img = card.GetComponent<Image>();
        img.sprite = GetRoundedCardSprite();
        img.type = Image.Type.Sliced;
        img.color = CardWhite;

        TMP_Text txt = CreateText("Label", card.transform, "No posts on your profile yet.", _profileEmptyPostsFontSize, FontStyles.Normal, MutedSlate);
        StretchRect(txt.rectTransform, new Vector2(16f, 12f), new Vector2(-16f, -12f));
        txt.alignment = TextAlignmentOptions.Center;
    }

    private void BuildPostCard(ConnectProfileData profile, ConnectPostData post)
    {
        GameObject card = new GameObject(
            $"Post_{post.postId}",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(UnityEngine.UI.Outline),
            typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        card.layer = gameObject.layer;
        card.transform.SetParent(_profileContentRoot, false);

        Image cardImg = card.GetComponent<Image>();
        cardImg.sprite = GetRoundedCardSprite();
        cardImg.type = Image.Type.Sliced;
        cardImg.color = CardWhite;

        UnityEngine.UI.Outline cardOutline = card.GetComponent<UnityEngine.UI.Outline>();
        cardOutline.effectColor = BorderSoft;
        cardOutline.effectDistance = new Vector2(1f, -1f);

        VerticalLayoutGroup vlg = card.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(12, 12, 12, 12);
        vlg.spacing = 8f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter csf = card.GetComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Post Author Row
        GameObject headerRow = new GameObject("Header", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        headerRow.layer = gameObject.layer;
        headerRow.transform.SetParent(card.transform, false);
        LayoutElement hLE = headerRow.GetComponent<LayoutElement>();
        hLE.minHeight = 44f;
        hLE.preferredHeight = 44f;

        HorizontalLayoutGroup hlg = headerRow.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10f;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        GameObject miniAv = new GameObject("Avatar", typeof(RectTransform), typeof(LayoutElement));
        miniAv.layer = gameObject.layer;
        miniAv.transform.SetParent(headerRow.transform, false);
        miniAv.GetComponent<RectTransform>().sizeDelta = new Vector2(38f, 38f);
        LayoutElement mAvLE = miniAv.GetComponent<LayoutElement>();
        mAvLE.minWidth = 38f;
        mAvLE.preferredWidth = 38f;
        mAvLE.minHeight = 38f;
        mAvLE.preferredHeight = 38f;
        mAvLE.flexibleWidth = 0f;
        mAvLE.flexibleHeight = 0f;
        MessagesUI.ApplyCircularAvatar(miniAv.GetComponent<RectTransform>(), ResolveProfileAvatarSprite(profile), CardSurfaceSoft, coverCrop: true);

        string displayName = string.IsNullOrWhiteSpace(profile.displayName)
            ? PlayerNameService.GetActivePlayerName()
            : profile.displayName;

        string authorLine = string.IsNullOrWhiteSpace(post.locationTag)
            ? $"<b>{displayName}</b>"
            : $"<b>{displayName}</b>  <size={Mathf.RoundToInt(_postLocationTagFontSize)}><color=#646878>@ {post.locationTag}</color></size>";

        TMP_Text authorText = CreateText("Author", headerRow.transform, authorLine, _postAuthorFontSize, FontStyles.Normal, InkDark);
        authorText.textWrappingMode = TextWrappingModes.Normal;
        LayoutElement aLE = authorText.gameObject.AddComponent<LayoutElement>();
        aLE.flexibleWidth = 1f;

        if (!string.IsNullOrWhiteSpace(post.timestampLabel))
        {
            TMP_Text timeText = CreateText("Timestamp", headerRow.transform, post.timestampLabel, _postTimestampFontSize, FontStyles.Normal, MutedSlate);
            timeText.alignment = TextAlignmentOptions.MidlineRight;
            LayoutElement tLE = timeText.gameObject.AddComponent<LayoutElement>();
            tLE.minWidth = 118f;
        }

        // Post Media Image
        Sprite postSprite = ResolvePostSprite(post);
        if (postSprite != null)
        {
            GameObject mediaFrame = new GameObject(
                "MediaFrame",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(LayoutElement));
            mediaFrame.layer = gameObject.layer;
            mediaFrame.transform.SetParent(card.transform, false);

            LayoutElement mediaLE = mediaFrame.GetComponent<LayoutElement>();
            mediaLE.minHeight = 200f;
            mediaLE.preferredHeight = 200f;

            Image frameImg = mediaFrame.GetComponent<Image>();
            frameImg.sprite = GetRoundedCardSprite();
            frameImg.type = Image.Type.Sliced;
            frameImg.color = CardSurfaceSoft;

            GameObject photoGO = new GameObject("Photo", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            photoGO.layer = gameObject.layer;
            photoGO.transform.SetParent(mediaFrame.transform, false);
            StretchRect(photoGO.GetComponent<RectTransform>(), new Vector2(6f, 6f), new Vector2(-6f, -6f));

            Image photoImg = photoGO.GetComponent<Image>();
            photoImg.sprite = postSprite;
            photoImg.preserveAspect = true;
            photoImg.color = Color.white;
        }

        // Engagement Row (Likes + Comments icons + counts)
        GameObject engageRow = new GameObject("EngagementRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        engageRow.layer = gameObject.layer;
        engageRow.transform.SetParent(card.transform, false);
        LayoutElement engLE = engageRow.GetComponent<LayoutElement>();
        engLE.minHeight = 28f;
        engLE.preferredHeight = 28f;

        HorizontalLayoutGroup engHLG = engageRow.GetComponent<HorizontalLayoutGroup>();
        engHLG.spacing = 12f;
        engHLG.childAlignment = TextAnchor.MiddleLeft;
        engHLG.childControlWidth = true;
        engHLG.childControlHeight = true;
        engHLG.childForceExpandWidth = false;
        engHLG.childForceExpandHeight = false;

        CreateInlineMetric(engageRow.transform, GetHeartSprite(), AccentPink, $"{post.likesCount} likes");
        CreateInlineMetric(engageRow.transform, GetCommentSprite(), MutedSlate, $"{post.commentsCount} comments");

        // Post Caption
        string formattedCaption = PlayerNameService.FormatText(post.caption ?? string.Empty);
        TMP_Text capTmp = CreateText("Caption", card.transform, $"<b>{displayName}</b>  {formattedCaption}", _postCaptionFontSize, FontStyles.Normal, InkDark);
        capTmp.textWrappingMode = TextWrappingModes.Normal;

        // Top Comment Preview
        if (!string.IsNullOrWhiteSpace(post.topCommentPreview))
        {
            GameObject commentPill = new GameObject("TopComment", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            commentPill.layer = gameObject.layer;
            commentPill.transform.SetParent(card.transform, false);
            Image cpImg = commentPill.GetComponent<Image>();
            cpImg.sprite = GetRoundedCardSprite();
            cpImg.type = Image.Type.Sliced;
            cpImg.color = CardSurfaceSoft;
            VerticalLayoutGroup cpVlg = commentPill.GetComponent<VerticalLayoutGroup>();
            cpVlg.padding = new RectOffset(10, 10, 8, 8);
            cpVlg.childControlWidth = true;
            cpVlg.childControlHeight = true;
            commentPill.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TMP_Text cpText = CreateText("CommentText", commentPill.transform, PlayerNameService.FormatText(post.topCommentPreview), _postCommentFontSize, FontStyles.Normal, MutedSlate);
            cpText.textWrappingMode = TextWrappingModes.Normal;
        }
    }

    private void CreateInlineMetric(Transform parent, Sprite iconSprite, Color iconTint, string label)
    {
        GameObject item = new GameObject("Metric", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        item.layer = gameObject.layer;
        item.transform.SetParent(parent, false);
        HorizontalLayoutGroup hlg = item.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 6f;
        hlg.padding = new RectOffset(0, 8, 0, 0);
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        iconGO.layer = gameObject.layer;
        iconGO.transform.SetParent(item.transform, false);
        iconGO.GetComponent<RectTransform>().sizeDelta = new Vector2(18f, 18f);
        LayoutElement iconLE = iconGO.GetComponent<LayoutElement>();
        iconLE.minWidth = 18f;
        iconLE.preferredWidth = 18f;
        iconLE.minHeight = 18f;
        iconLE.preferredHeight = 18f;
        Image img = iconGO.GetComponent<Image>();
        img.sprite = iconSprite;
        img.color = iconTint;
        img.preserveAspect = true;
        img.raycastTarget = false;

        TMP_Text txt = CreateText("Count", item.transform, label, _postMetricFontSize, FontStyles.Bold, InkDark);
        txt.textWrappingMode = TextWrappingModes.NoWrap;
        txt.alignment = TextAlignmentOptions.MidlineLeft;
        txt.raycastTarget = false;
    }

    #endregion

    #region Custom Inspector Post Catalog Hooks

    private bool _catalogBound;

    private void BindCatalogTriggers()
    {
        if (_catalogBound || _customPostCatalog == null || _customPostCatalog.Count == 0 || EventBus.Instance == null)
        {
            return;
        }

        _catalogBound = true;
        for (int i = 0; i < _customPostCatalog.Count; i++)
        {
            ConnectPostCatalogEntry entry = _customPostCatalog[i];
            if (entry == null || string.IsNullOrWhiteSpace(entry.triggerEventName)) continue;

            string eventName = entry.triggerEventName.Trim();
            Action action = () => ApplyCatalogEntry(entry);
            s_eventSubscriptions.Add(new KeyValuePair<string, Action>(eventName, action));
            EventBus.Instance.Subscribe(eventName, action);
        }
    }

    private static void ApplyCatalogEntry(ConnectPostCatalogEntry entry)
    {
        if (entry == null) return;

        if (entry.deletePostOnTrigger)
        {
            MarkPostDeleted(entry.targetPostIdToDelete);
            Instance?.RefreshActiveViewsIfVisible();
            return;
        }

        if (string.IsNullOrWhiteSpace(entry.postId) || FindPostById(entry.postId) != null)
        {
            return;
        }

        if (entry.customImageSprite != null && !string.IsNullOrWhiteSpace(entry.imageKey))
        {
            MessagesUI.RegisterSpriteInCache(entry.imageKey, entry.customImageSprite);
        }

        s_posts.Add(new ConnectPostData
        {
            postId = entry.postId,
            caption = entry.caption ?? string.Empty,
            imageKey = entry.imageKey ?? string.Empty,
            locationTag = entry.locationTag ?? string.Empty,
            timestampLabel = entry.timestampLabel ?? $"Day {Mathf.Max(1, entry.dayPosted)}",
            likesCount = Mathf.Max(0, entry.initialLikes),
            commentsCount = Mathf.Max(0, entry.initialComments),
            topCommentPreview = entry.topCommentPreview ?? string.Empty,
            dayPosted = Mathf.Max(1, entry.dayPosted),
            isDeleted = false
        });

        Instance?.RefreshActiveViewsIfVisible();
    }

    #endregion

    #region Helpers, Audio/HUD & Procedural Vector Sprites

    private void EnsureUI()
    {
        if (_uiBuilt)
        {
            return;
        }
        _uiBuilt = true;

        _phoneStateMachine = _phoneStateMachine != null ? _phoneStateMachine : GetComponentInParent<PhoneStateMachine>();
        _sharedFont = _phoneStateMachine?.GetComponentInChildren<TextMeshProUGUI>(true)?.font;
        if (_sharedFont == null)
        {
            _sharedFont = TMP_Settings.defaultFontAsset;
        }

        BuildCreateProfileView();
        BuildMainConnectView();
    }

    private void EnsureUnreadListener(bool subscribe)
    {
        if (subscribe && !_unreadListenerBound)
        {
            MessagesManager.OnUnreadCountsChanged += OnUnreadCountsChangedHandler;
            _unreadListenerBound = true;
        }
        else if (!subscribe && _unreadListenerBound)
        {
            MessagesManager.OnUnreadCountsChanged -= OnUnreadCountsChangedHandler;
            _unreadListenerBound = false;
        }
    }

    private void OnUnreadCountsChangedHandler()
    {
        UpdateBottomNavUnreadBadge();
    }

    private Sprite ResolveProfileAvatarSprite(ConnectProfileData profile)
    {
        if (profile != null && !string.IsNullOrWhiteSpace(profile.avatarImagePath))
        {
            GalleryManager gallery = GalleryManager.Instance != null
                ? GalleryManager.Instance
                : (_phoneStateMachine != null && _phoneStateMachine.GetGalleryTab() != null
                    ? _phoneStateMachine.GetGalleryTab().GetComponentInChildren<GalleryManager>(true)
                    : FindAnyObjectByType<GalleryManager>(FindObjectsInactive.Include));

            if (gallery != null)
            {
                Sprite gallerySprite = gallery.FindPhotoSpriteByKey(profile.avatarImagePath);
                if (gallerySprite != null) return gallerySprite;
            }

            if (profile.avatarImagePath.IndexOf("BookwormAvatar", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return _bookwormAvatarSprite != null ? _bookwormAvatarSprite : GetBookAvatarSprite();
            }

            Sprite loaded = MessagesUI.LoadSprite(profile.avatarImagePath);
            if (loaded != null) return loaded;
        }

        return ResolveAvatarSprite(profile != null ? profile.avatarIndex : 0);
    }

    private Sprite ResolveAvatarSprite(int avatarIndex)
    {
        MessagesUI ui = MessagesManager.Instance?.GetMessagesUI();
        switch (avatarIndex)
        {
            case 1:
                if (_bookwormAvatarSprite != null) return _bookwormAvatarSprite;
                return GetBookAvatarSprite();
            case 2:
                if (_casualSelfieSprite != null) return _casualSelfieSprite;
                if (ui != null && ui.GetLunaNormalSelfieSprite() != null) return ui.GetLunaNormalSelfieSprite();
                return MessagesUI.LoadSprite("SiD/Luna/LunaNormalSelfie1.png");
            case 3:
                if (_personalSelfieSprite != null) return _personalSelfieSprite;
                if (ui != null && ui.GetLunaFlirtSelfieSprite() != null) return ui.GetLunaFlirtSelfieSprite();
                return MessagesUI.LoadSprite("SiD/Luna/LunaFlirtSelfie1.png");
            default:
                if (_anonymousAvatarSprite != null) return _anonymousAvatarSprite;
                if (ui != null && ui.GetDefaultAvatarSprite() != null) return ui.GetDefaultAvatarSprite();
                return GetAnonAvatarSprite();
        }
    }

    private Sprite ResolvePostSprite(ConnectPostData post)
    {
        if (post == null || string.IsNullOrWhiteSpace(post.imageKey))
        {
            return null;
        }

        MessagesUI ui = MessagesManager.Instance?.GetMessagesUI();
        if (string.Equals(post.imageKey, "luna_normal_selfie", StringComparison.OrdinalIgnoreCase))
        {
            return (ui != null ? ui.GetLunaNormalSelfieSprite() : null) ??
                   MessagesUI.LoadSprite("SiD/Luna/LunaNormalSelfie1.png");
        }

        if (string.Equals(post.imageKey, "luna_flirt_selfie", StringComparison.OrdinalIgnoreCase))
        {
            return (ui != null ? ui.GetLunaFlirtSelfieSprite() : null) ??
                   MessagesUI.LoadSprite("SiD/Luna/LunaFlirtSelfie1.png");
        }

        return MessagesUI.LoadSprite(post.imageKey);
    }

    private static string GetPrivacyStatusLabel(ConnectProfileData profile)
    {
        if (profile == null) return "Public";
        if (profile.isPrivateAccount) return "Private Account";
        return profile.resolvedPrivacyRoute switch
        {
            0 => "Protected Identity",
            2 => "Public - Full Info",
            _ => "Public - Partial Info"
        };
    }

    private static int ComputeFollowerCount(ConnectProfileData profile, int postCount)
    {
        int baseFollowers = profile.resolvedPrivacyRoute switch
        {
            0 => 14,
            2 => 68,
            _ => 31
        };

        if (profile.isPrivateAccount)
        {
            baseFollowers = Mathf.Min(baseFollowers, 19);
        }

        return baseFollowers + postCount * 6;
    }

    private static string FormatHandle(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return "@user";
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder(rawName.Length + 1);
        sb.Append('@');
        for (int i = 0; i < rawName.Length; i++)
        {
            char c = rawName[i];
            if (char.IsLetterOrDigit(c) || c == '_' || c == '.')
            {
                sb.Append(char.ToLowerInvariant(c));
            }
            else if (c == ' ')
            {
                sb.Append('_');
            }
        }

        return sb.Length > 1 ? sb.ToString() : "@user";
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

    private Sprite GetRoundedCardSprite()
    {
        if (_roundedCardSprite == null)
        {
            _roundedCardSprite = BuildRoundedRectSprite(32, 9);
        }
        return _roundedCardSprite;
    }

    private Sprite GetRoundedPillSprite()
    {
        if (_roundedPillSprite == null)
        {
            _roundedPillSprite = BuildRoundedRectSprite(36, 15);
        }
        return _roundedPillSprite;
    }

    private Sprite GetCircleFillSprite()
    {
        if (_circleFillSprite != null) return _circleFillSprite;

        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        float c = (size - 1) * 0.5f;
        float r = size * 0.5f - 1.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01((r - d) / 1.5f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        _circleFillSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return _circleFillSprite;
    }

    private Sprite GetAnonAvatarSprite()
    {
        if (_anonIconSprite != null) return _anonIconSprite;
        const int s = 64;
        Texture2D tex = CreateSolidTexture(s, new Color(0.84f, 0.86f, 0.94f, 1f));
        Color fg = new Color(0.32f, 0.35f, 0.48f, 1f);
        DrawDisc(tex, 32f, 40f, 11f, fg);
        DrawDisc(tex, 32f, 11f, 20f, fg);
        tex.Apply();
        _anonIconSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return _anonIconSprite;
    }

    private Sprite GetBookAvatarSprite()
    {
        if (_bookIconSprite != null) return _bookIconSprite;
        const int s = 64;
        Texture2D tex = CreateSolidTexture(s, new Color(0.83f, 0.93f, 0.92f, 1f));
        Color cover = new Color(0.14f, 0.49f, 0.48f, 1f);
        Color page = new Color(0.98f, 0.96f, 0.90f, 1f);
        DrawRect(tex, 15, 16, 49, 48, cover);
        DrawRect(tex, 20, 20, 46, 44, page);
        DrawRect(tex, 15, 16, 21, 48, new Color(0.10f, 0.36f, 0.36f, 1f));
        tex.Apply();
        _bookIconSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return _bookIconSprite;
    }

    private Sprite GetMessagesTabSprite()
    {
        if (_messagesTabSprite != null) return _messagesTabSprite;
        const int s = 32;
        Texture2D tex = CreateSolidTexture(s, Color.clear);
        DrawRect(tex, 4, 9, 28, 26, Color.white);
        DrawRect(tex, 8, 4, 14, 10, Color.white);
        DrawRect(tex, 9, 17, 12, 20, new Color(0.2f, 0.2f, 0.2f, 1f));
        DrawRect(tex, 15, 17, 18, 20, new Color(0.2f, 0.2f, 0.2f, 1f));
        DrawRect(tex, 21, 17, 24, 20, new Color(0.2f, 0.2f, 0.2f, 1f));
        tex.Apply();
        _messagesTabSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return _messagesTabSprite;
    }

    private Sprite GetProfileTabSprite()
    {
        if (_profileTabSprite != null) return _profileTabSprite;
        const int s = 32;
        Texture2D tex = CreateSolidTexture(s, Color.clear);
        DrawDisc(tex, 16f, 22f, 5.5f, Color.white);
        DrawDisc(tex, 16f, 4f, 11.5f, Color.white);
        tex.Apply();
        _profileTabSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return _profileTabSprite;
    }

    private Sprite GetHeartSprite()
    {
        if (_heartSprite != null) return _heartSprite;
        const int s = 24;
        Texture2D tex = CreateSolidTexture(s, Color.clear);
        DrawDisc(tex, 8f, 15f, 5f, Color.white);
        DrawDisc(tex, 16f, 15f, 5f, Color.white);
        for (int y = 4; y <= 15; y++)
        {
            float halfW = (y - 3f) * 0.72f;
            int minX = Mathf.Max(3, Mathf.RoundToInt(12f - halfW));
            int maxX = Mathf.Min(21, Mathf.RoundToInt(12f + halfW));
            for (int x = minX; x <= maxX; x++)
            {
                tex.SetPixel(x, y, Color.white);
            }
        }
        tex.Apply();
        _heartSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return _heartSprite;
    }

    private Sprite GetCommentSprite()
    {
        if (_commentSprite != null) return _commentSprite;
        const int s = 24;
        Texture2D tex = CreateSolidTexture(s, Color.clear);
        DrawRect(tex, 3, 7, 21, 20, Color.white);
        DrawRect(tex, 6, 3, 10, 8, Color.white);
        tex.Apply();
        _commentSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return _commentSprite;
    }

    private static Sprite BuildRoundedRectSprite(int size, int radius)
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

    private static Texture2D CreateSolidTexture(int size, Color fill)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = fill;
        tex.SetPixels(pixels);
        return tex;
    }

    private static void DrawDisc(Texture2D tex, float cx, float cy, float radius, Color col)
    {
        int w = tex.width;
        int h = tex.height;
        int minX = Mathf.Max(0, Mathf.FloorToInt(cx - radius));
        int maxX = Mathf.Min(w - 1, Mathf.CeilToInt(cx + radius));
        int minY = Mathf.Max(0, Mathf.FloorToInt(cy - radius));
        int maxY = Mathf.Min(h - 1, Mathf.CeilToInt(cy + radius));
        float rSq = radius * radius;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x - cx;
                float dy = y - cy;
                if (dx * dx + dy * dy <= rSq)
                {
                    tex.SetPixel(x, y, col);
                }
            }
        }
    }

    private Sprite GetCameraPickerSprite()
    {
        if (_cameraIconProceduralSprite != null) return _cameraIconProceduralSprite;

        const int s = 64;
        Texture2D tex = CreateSolidTexture(s, Color.clear);
        tex.filterMode = FilterMode.Bilinear;

        DrawRect(tex, 22, 42, 42, 50, Color.white);
        DrawRect(tex, 10, 14, 54, 43, Color.white);
        DrawDisc(tex, 32f, 28f, 11.5f, Color.clear);
        DrawDisc(tex, 32f, 28f, 7.5f, Color.white);
        DrawDisc(tex, 47f, 37f, 2.2f, Color.clear);

        tex.Apply();
        _cameraIconProceduralSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return _cameraIconProceduralSprite;
    }

    private static void DrawRect(Texture2D tex, int x0, int y0, int x1, int y1, Color col)
    {
        int minX = Mathf.Clamp(x0, 0, tex.width);
        int maxX = Mathf.Clamp(x1, 0, tex.width);
        int minY = Mathf.Clamp(y0, 0, tex.height);
        int maxY = Mathf.Clamp(y1, 0, tex.height);
        for (int y = minY; y < maxY; y++)
        {
            for (int x = minX; x < maxX; x++)
            {
                tex.SetPixel(x, y, col);
            }
        }
    }

    private sealed class PointerHoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Action _onEnter;
        private Action _onExit;

        public void Initialize(Action onEnter, Action onExit)
        {
            _onEnter = onEnter;
            _onExit = onExit;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _onEnter?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _onExit?.Invoke();
        }

        private void OnDisable()
        {
            _onExit?.Invoke();
        }
    }

    #endregion
}
