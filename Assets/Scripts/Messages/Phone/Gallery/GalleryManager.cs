using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Main controller for the mobile phone "Gallery" app.
/// Manages the responsive grid with dynamic zoom, thumbnail hover highlighting,
/// full-size photo preview, active contact picker, and two-way Quick-Select mode
/// integrated with the messaging system.
/// </summary>
public class GalleryManager : MonoBehaviour
{
    public static GalleryManager Instance { get; private set; }

    [Header("1. Grid Structure & Scrolling")]
    [Tooltip("Main ScrollRect wrapping the thumbnail container.")]
    [SerializeField] private ScrollRect _galleryScrollRect;

    [Tooltip("GridLayoutGroup where photo thumbnails are instantiated.")]
    [SerializeField] private GridLayoutGroup _gridLayoutGroup;

    [Tooltip("RectTransform of the cell container (if omitted, fetched from GridLayoutGroup).")]
    [SerializeField] private RectTransform _gridContainer;

    [Tooltip("Prefab for each photo cell/item (with GalleryPhotoItem). If null, generated procedurally at runtime.")]
    [SerializeField] private GameObject _photoItemPrefab;

    [Tooltip("Horizontal and vertical spacing in pixels between thumbnails.")]
    [SerializeField] private Vector2 _gridSpacing = new Vector2(6f, 6f);

    [Tooltip("Internal padding of the Grid (Left, Right, Top, Bottom).")]
    [SerializeField] private int _paddingLeft = 6;
    [SerializeField] private int _paddingRight = 6;
    [SerializeField] private int _paddingTop = 6;
    [SerializeField] private int _paddingBottom = 6;

    [Tooltip("Default column count when opening the Gallery (e.g. 4 columns).")]
    [Range(1, 8)]
    [SerializeField] private int _defaultColumns = 4;

    [Tooltip("Column presets available when cycling zoom levels (e.g. 4 compact -> 2 zoomed -> 1 single).")]
    [SerializeField] private int[] _zoomColumnPresets = new int[] { 4, 2, 1 };

    [Tooltip("Height-to-width aspect ratio for each cell (1.0 = square thumbnails).")]
    [Range(0.5f, 2f)]
    [SerializeField] private float _cellHeightToWidthRatio = 1.0f;

    [Tooltip("Minimum safe size for a cell in pixels.")]
    [SerializeField] private float _minCellSize = 24f;

    [Tooltip("Crops photos to fill the cell square without distorting aspect ratio.")]
    [SerializeField] private bool _fillAndCropThumbnails = true;

    [Header("2. Zoom & Scale Controls (Optional)")]
    [Tooltip("Button to cycle through column zoom levels (e.g. 4 -> 2 -> 1 -> 4).")]
    [SerializeField] private Button _zoomToggleButton;

    [Tooltip("Optional button to zoom in (fewer columns, larger photos).")]
    [SerializeField] private Button _zoomInButton;

    [Tooltip("Optional button to zoom out (more columns, more compact view).")]
    [SerializeField] private Button _zoomOutButton;

    [Tooltip("Optional text showing current zoom status (e.g. '4x' or '2 col').")]
    [SerializeField] private TMP_Text _zoomStatusText;

    [Tooltip("Enables zooming via Ctrl + Mouse Scroll Wheel when hovering over the Gallery.")]
    [SerializeField] private bool _enableCtrlScrollZoom = true;

    [Header("3. Outline & Highlight Style (Hover / Selection)")]
    [Tooltip("Outline color when hovering or selecting a thumbnail.")]
    [SerializeField] private Color _hoverBorderColor = new Color(0.25f, 0.85f, 1f, 1f);

    [Tooltip("Thickness in pixels for the highlight outline.")]
    [Range(1f, 16f)]
    [SerializeField] private float _hoverBorderThickness = 3f;

    [Tooltip("Visual outline style (SolidInside, SolidOutside, CornerBrackets).")]
    [SerializeField] private GalleryBorderStyle _hoverBorderStyle = GalleryBorderStyle.SolidInside;

    [Tooltip("Relative corner bracket segment length when style is CornerBrackets.")]
    [Range(0.15f, 0.48f)]
    [SerializeField] private float _cornerBracketRatio = 0.28f;

    [Tooltip("Enables subtle scale punch on thumbnail hover.")]
    [SerializeField] private bool _enableHoverScale = true;

    [Range(1f, 1.15f)]
    [SerializeField] private float _hoverScale = 1.04f;

    [Range(0f, 0.3f)]
    [SerializeField] private float _hoverFadeDuration = 0.08f;

    [Header("4. Full-Size Preview Panel")]
    [Tooltip("Root panel of the detailed photo preview.")]
    [SerializeField] private GameObject _previewPanel;

    [Tooltip("Image component displaying the full-size photo.")]
    [SerializeField] private Image _previewImage;

    [Tooltip("Optional text displaying photo title or caption in preview.")]
    [SerializeField] private TMP_Text _previewTitleText;

    [Tooltip("'Close' button inside preview to return to grid.")]
    [SerializeField] private Button _closePreviewButton;

    [Tooltip("'Share / Send' button inside preview.")]
    [SerializeField] private Button _sharePhotoButton;

    [Tooltip("Optional text label for share button to update dynamically.")]
    [SerializeField] private TMP_Text _shareButtonLabel;

    [SerializeField] private string _defaultShareButtonText = "Share";

    [Header("5. Contact Selector (Gallery -> Messages)")]
    [Tooltip("Popup/panel listing valid contacts to send photo to.")]
    [SerializeField] private GameObject _contactSelectorPanel;

    [Tooltip("Container where contact buttons are instantiated.")]
    [SerializeField] private Transform _contactSelectorContainer;

    [Tooltip("Contact button prefab (can reuse ContactButton.prefab or null to generate procedurally).")]
    [SerializeField] private GameObject _contactSelectorItemPrefab;

    [Tooltip("Button to close/dismiss the contact selector.")]
    [SerializeField] private Button _closeContactSelectorButton;

    [Tooltip("Optional text displayed when no contacts have received messages yet.")]
    [SerializeField] private TMP_Text _emptyContactsText;

    [Tooltip("When enabled, strictly filters contacts who already have received messages in the system.")]
    [SerializeField] private bool _onlyContactsWithReceivedMessages = true;

    [Header("6. Quick-Select Mode (Messages -> Gallery)")]
    [Tooltip("When true, clicking a thumbnail in Quick-Select mode immediately sends it to the contact and returns to chat. If false, opens preview first for confirmation.")]
    [SerializeField] private bool _instantSendOnQuickSelect = true;

    [Tooltip("Optional top banner visible only when opened in Quick-Select mode from chat.")]
    [SerializeField] private GameObject _quickSelectBanner;

    [Tooltip("Optional text inside Quick-Select banner (e.g. 'Send photo to Mom').")]
    [SerializeField] private TMP_Text _quickSelectBannerText;

    [Tooltip("Optional button in banner to cancel quick-select and return to chat.")]
    [SerializeField] private Button _cancelQuickSelectButton;

    [Header("7. Photo Storage & Preloads")]
    [Tooltip("List of photo entries configured in the Inspector.")]
    [SerializeField] private List<GalleryPhotoData> _photos = new List<GalleryPhotoData>();

    [Tooltip("Automatically scans StreamingAssets/Images (.png/.jpg/.jpeg) at startup and adds found images without duplicates.")]
    [SerializeField] private bool _autoLoadStreamingAssetsImages = true;

    [Tooltip("Automatically unlocks/adds photos received from NPCs via chat.")]
    [SerializeField] private bool _autoAddReceivedChatImages = true;

    [Tooltip("Optional empty state message when no photos are unlocked.")]
    [SerializeField] private TMP_Text _emptyGalleryText;

    private readonly List<GalleryPhotoItem> _spawnedItems = new List<GalleryPhotoItem>();

    private int _currentColumns;
    private int _currentZoomPresetIndex = 0;
    private GalleryPhotoData _selectedPhoto;

    private bool _isQuickSelectMode = false;
    private string _quickSelectTargetContact = string.Empty;
    private string _pendingQuickSelectContact = null;

    private bool _hasInitializedPhotos = false;
    private bool _uiListenersBound = false;
    private Coroutine _deferredLayoutCoroutine;
    private float _lastKnownViewportWidth = -1f;

    public bool IsQuickSelectMode => _isQuickSelectMode;
    public string QuickSelectTargetContact => _quickSelectTargetContact;
    public int CurrentColumns => _currentColumns;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        _currentColumns = Mathf.Max(1, _defaultColumns);
        SyncZoomPresetIndexFromColumns(_currentColumns);

        ResolveMissingReferences();
        EnsureGridComponents();
        BindUIButtons();
        InitializePhotoCollection();
    }

    private void Start()
    {
        SubscribeEvents();
        if (gameObject.activeInHierarchy)
        {
            RefreshGallery();
        }
    }

    private void OnEnable()
    {
        if (Instance == null)
        {
            Instance = this;
        }

        ResolveMissingReferences();
        EnsureGridComponents();
        BindUIButtons();
        InitializePhotoCollection();

        if (_autoAddReceivedChatImages)
        {
            SyncImagesFromChatHistory();
        }

        // Activate Quick-Select mode if a pending request was queued from MessagesScreen
        if (!string.IsNullOrEmpty(_pendingQuickSelectContact))
        {
            _isQuickSelectMode = true;
            _quickSelectTargetContact = _pendingQuickSelectContact;
            _pendingQuickSelectContact = null;
        }

        CloseContactSelector();
        ClosePreview();
        UpdateQuickSelectBanner();
        RefreshGallery();

        if (_deferredLayoutCoroutine != null)
        {
            StopCoroutine(_deferredLayoutCoroutine);
        }
        _deferredLayoutCoroutine = StartCoroutine(RecalculateLayoutNextFrame());
    }

    private void OnDisable()
    {
        if (_deferredLayoutCoroutine != null)
        {
            StopCoroutine(_deferredLayoutCoroutine);
            _deferredLayoutCoroutine = null;
        }

        // When leaving the Gallery screen (via Home, Back, phone close, or sending),
        // clean up modals and reset quick-select state for future normal openings.
        CloseContactSelector();
        ClosePreview();
        _isQuickSelectMode = false;
        _quickSelectTargetContact = string.Empty;
        _pendingQuickSelectContact = null;
        UpdateQuickSelectBanner();
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {
        if (!gameObject.activeInHierarchy) return;

        // Check if viewport actual width changed to keep grid responsive
        float currentWidth = GetAvailableGridWidth();
        if (currentWidth > 1f && Mathf.Abs(currentWidth - _lastKnownViewportWidth) > 0.5f)
        {
            RecalculateGridLayout();
        }

        // Optional PC zoom support via Ctrl + Mouse Scroll Wheel
        if (_enableCtrlScrollZoom && !IsPreviewOpen() && !IsContactSelectorOpen())
        {
            if (Keyboard.current != null && Mouse.current != null &&
                (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed))
            {
                float scrollY = Mouse.current.scroll.ReadValue().y;
                if (scrollY > 0.01f)
                {
                    ZoomIn();
                }
                else if (scrollY < -0.01f)
                {
                    ZoomOut();
                }
            }
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        if (isActiveAndEnabled && _gridLayoutGroup != null)
        {
            RecalculateGridLayout();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _defaultColumns = Mathf.Max(1, _defaultColumns);
        _minCellSize = Mathf.Max(10f, _minCellSize);
        _hoverBorderThickness = Mathf.Max(1f, _hoverBorderThickness);

        if (Application.isPlaying && isActiveAndEnabled)
        {
            ApplyHoverStyleToSpawnedItems();
            RecalculateGridLayout();
        }
    }
#endif

    #region Initialization & Auto-Discovery

    private void SubscribeEvents()
    {
        if (EventBus.Instance == null) return;

        EventBus.Instance.Subscribe<string>("Gallery_UnlockPhoto", UnlockPhotoByIdOrPath);
        EventBus.Instance.Subscribe<string>("Gallery_AddPhotoFromPath", AddPhotoFromPathEvent);
    }

    private void UnsubscribeEvents()
    {
        if (EventBus.Instance == null) return;

        EventBus.Instance.Unsubscribe<string>("Gallery_UnlockPhoto", UnlockPhotoByIdOrPath);
        EventBus.Instance.Unsubscribe<string>("Gallery_AddPhotoFromPath", AddPhotoFromPathEvent);
    }

    private void ResolveMissingReferences()
    {
        if (_gridLayoutGroup == null)
        {
            _gridLayoutGroup = GetComponentInChildren<GridLayoutGroup>(true);
        }

        if (_gridContainer == null && _gridLayoutGroup != null)
        {
            _gridContainer = _gridLayoutGroup.GetComponent<RectTransform>();
        }

        if (_gridLayoutGroup == null && _gridContainer != null)
        {
            _gridLayoutGroup = _gridContainer.GetComponent<GridLayoutGroup>();
        }

        if (_galleryScrollRect == null)
        {
            if (_gridContainer != null)
            {
                _galleryScrollRect = _gridContainer.GetComponentInParent<ScrollRect>();
            }

            if (_galleryScrollRect == null)
            {
                _galleryScrollRect = GetComponentInChildren<ScrollRect>(true);
            }
        }

        if (_shareButtonLabel == null && _sharePhotoButton != null)
        {
            _shareButtonLabel = _sharePhotoButton.GetComponentInChildren<TMP_Text>(true);
        }

        if (_shareButtonLabel != null)
        {
            // If serialized text has legacy Spanish default or is empty, sanitize to "Share"
            if (_defaultShareButtonText == "Enviar a..." || string.IsNullOrEmpty(_defaultShareButtonText))
            {
                _defaultShareButtonText = "Share";
            }

            // If the user manually modified the button text in the prefab/hierarchy, respect that custom text
            if (!string.IsNullOrWhiteSpace(_shareButtonLabel.text) && _shareButtonLabel.text != "Enviar a...")
            {
                _defaultShareButtonText = _shareButtonLabel.text;
            }
            else
            {
                if (!string.IsNullOrEmpty(_defaultShareButtonText) && _defaultShareButtonText != "Enviar a...")
                {
                    _shareButtonLabel.text = _defaultShareButtonText;
                }
                else if (string.IsNullOrEmpty(_shareButtonLabel.text) || _shareButtonLabel.text == "Enviar a...")
                {
                    _shareButtonLabel.text = "Share";
                }
            }
        }
    }

    private void EnsureGridComponents()
    {
        if (_gridContainer == null) return;

        if (_gridLayoutGroup == null)
        {
            _gridLayoutGroup = _gridContainer.gameObject.AddComponent<GridLayoutGroup>();
        }

        // Configure top-stretch horizontal anchors for clean vertical scrolling
        _gridContainer.anchorMin = new Vector2(0f, 1f);
        _gridContainer.anchorMax = new Vector2(1f, 1f);
        _gridContainer.pivot = new Vector2(0.5f, 1f);
        _gridContainer.sizeDelta = new Vector2(0f, _gridContainer.sizeDelta.y);

        ContentSizeFitter fitter = _gridContainer.GetComponent<ContentSizeFitter>();
        if (fitter == null)
        {
            fitter = _gridContainer.gameObject.AddComponent<ContentSizeFitter>();
        }
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        if (_galleryScrollRect != null && _galleryScrollRect.content == null)
        {
            _galleryScrollRect.content = _gridContainer;
        }
    }

    private void BindUIButtons()
    {
        if (_uiListenersBound) return;
        _uiListenersBound = true;

        if (_zoomToggleButton != null)
            _zoomToggleButton.onClick.AddListener(ToggleZoom);

        if (_zoomInButton != null)
            _zoomInButton.onClick.AddListener(ZoomIn);

        if (_zoomOutButton != null)
            _zoomOutButton.onClick.AddListener(ZoomOut);

        if (_closePreviewButton != null)
            _closePreviewButton.onClick.AddListener(ClosePreview);

        if (_sharePhotoButton != null)
            _sharePhotoButton.onClick.AddListener(OnShareButtonClicked);

        if (_closeContactSelectorButton != null)
            _closeContactSelectorButton.onClick.AddListener(CloseContactSelector);

        if (_cancelQuickSelectButton != null)
            _cancelQuickSelectButton.onClick.AddListener(CancelQuickSelectAndReturnToChat);
    }

    private void InitializePhotoCollection()
    {
        if (_hasInitializedPhotos) return;
        _hasInitializedPhotos = true;

        // Register all inspector-configured photos in MessagesUI cache
        for (int i = 0; i < _photos.Count; i++)
        {
            if (_photos[i] != null)
            {
                _photos[i].ResolveSprite();
            }
        }

        // Scan StreamingAssets/Images if enabled
        if (_autoLoadStreamingAssetsImages)
        {
            string imagesDir = Path.Combine(Application.streamingAssetsPath, "Images");
            if (Directory.Exists(imagesDir))
            {
                string[] validExtensions = { ".png", ".jpg", ".jpeg" };
                string[] files = Directory.GetFiles(imagesDir);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);

                foreach (string filePath in files)
                {
                    string ext = Path.GetExtension(filePath);
                    bool supported = false;
                    for (int e = 0; e < validExtensions.Length; e++)
                    {
                        if (string.Equals(ext, validExtensions[e], StringComparison.OrdinalIgnoreCase))
                        {
                            supported = true;
                            break;
                        }
                    }

                    if (!supported) continue;

                    string fileName = Path.GetFileName(filePath);
                    string relativePath = $"Images/{fileName}";
                    if (!ContainsPhotoWithPathOrKey(relativePath))
                    {
                        string cleanTitle = Path.GetFileNameWithoutExtension(fileName);
                        GalleryPhotoData newPhoto = new GalleryPhotoData(
                            id: cleanTitle,
                            title: cleanTitle,
                            imagePath: relativePath,
                            sprite: null,
                            unlocked: true);
                        newPhoto.ResolveSprite();
                        _photos.Add(newPhoto);
                    }
                }
            }
        }

        if (_autoAddReceivedChatImages)
        {
            SyncImagesFromChatHistory();
        }
    }

    /// <summary>
    /// Scans received messages in MessagesManager and imports attached images to the Gallery without duplicates.
    /// </summary>
    public void SyncImagesFromChatHistory()
    {
        if (MessagesManager.Instance == null) return;

        List<Contact> contacts = MessagesManager.Instance.GetContacts();
        if (contacts == null) return;

        for (int i = 0; i < contacts.Count; i++)
        {
            Contact c = contacts[i];
            if (c == null) continue;

            List<Message> messages = MessagesManager.Instance.GetContactMessages(c.Name());
            if (messages == null) continue;

            for (int m = 0; m < messages.Count; m++)
            {
                Message msg = messages[m];
                if (msg != null && msg.HasImage() && !string.IsNullOrEmpty(msg.ImagePath()))
                {
                    RegisterImagePathIfMissing(msg.ImagePath());
                }
            }
        }
    }

    /// <summary>
    /// Notification invoked by MessagesManager when a contact sends a new image during gameplay.
    /// </summary>
    public void NotifyChatImageReceived(string imagePath)
    {
        if (!_autoAddReceivedChatImages || string.IsNullOrEmpty(imagePath)) return;

        if (RegisterImagePathIfMissing(imagePath) && isActiveAndEnabled)
        {
            RefreshGallery();
        }
    }

    private bool RegisterImagePathIfMissing(string imagePath)
    {
        if (string.IsNullOrEmpty(imagePath)) return false;

        string normalized = imagePath.Replace('\\', '/');
        for (int i = 0; i < _photos.Count; i++)
        {
            GalleryPhotoData existing = _photos[i];
            if (existing == null) continue;

            if (string.Equals(existing.GetEffectiveImageKey(), normalized, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(existing.ImagePath) &&
                 string.Equals(Path.GetFileName(existing.ImagePath), Path.GetFileName(normalized), StringComparison.OrdinalIgnoreCase)))
            {
                if (!existing.IsUnlocked)
                {
                    existing.IsUnlocked = true;
                    return true;
                }
                return false;
            }
        }

        Sprite sp = MessagesUI.LoadSprite(normalized);
        if (sp == null) return false;

        string baseName = Path.GetFileNameWithoutExtension(normalized);
        GalleryPhotoData added = new GalleryPhotoData(baseName, baseName, normalized, sp, unlocked: true);
        _photos.Add(added);
        return true;
    }

    private bool ContainsPhotoWithPathOrKey(string imagePath)
    {
        string normalized = imagePath.Replace('\\', '/');
        string fileName = Path.GetFileName(normalized);

        for (int i = 0; i < _photos.Count; i++)
        {
            GalleryPhotoData p = _photos[i];
            if (p == null) continue;

            if (string.Equals(p.GetEffectiveImageKey(), normalized, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrEmpty(p.ImagePath) &&
                string.Equals(Path.GetFileName(p.ImagePath), fileName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    #endregion

    #region Grid Rendering & Zoom System

    /// <summary>
    /// Rebuilds visible Gallery grid elements and recalculates responsive cell sizes.
    /// </summary>
    public void RefreshGallery()
    {
        EnsureGridComponents();
        if (_gridContainer == null) return;

        ClearSpawnedItems();

        int unlockedCount = 0;
        for (int i = 0; i < _photos.Count; i++)
        {
            GalleryPhotoData photo = _photos[i];
            if (photo == null || !photo.IsUnlocked) continue;

            Sprite sp = photo.ResolveSprite();
            if (sp == null) continue;

            GalleryPhotoItem item = SpawnPhotoItem(photo);
            if (item != null)
            {
                _spawnedItems.Add(item);
                unlockedCount++;
            }
        }

        if (_emptyGalleryText != null)
        {
            _emptyGalleryText.gameObject.SetActive(unlockedCount == 0);
        }

        RecalculateGridLayout();
        UpdateZoomStatusLabel();
    }

    private GalleryPhotoItem SpawnPhotoItem(GalleryPhotoData photo)
    {
        GameObject itemGO;
        if (_photoItemPrefab != null)
        {
            itemGO = Instantiate(_photoItemPrefab, _gridContainer);
        }
        else
        {
            itemGO = new GameObject($"Photo_{photo.Id}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            itemGO.transform.SetParent(_gridContainer, false);
        }

        itemGO.SetActive(true);

        GalleryPhotoItem item = itemGO.GetComponent<GalleryPhotoItem>();
        if (item == null)
        {
            item = itemGO.AddComponent<GalleryPhotoItem>();
        }

        item.Initialize(photo, OnPhotoThumbnailClicked, _fillAndCropThumbnails);
        item.ApplyHoverStyle(
            _hoverBorderColor,
            _hoverBorderThickness,
            _hoverBorderStyle,
            _cornerBracketRatio,
            _enableHoverScale,
            _hoverScale,
            _hoverFadeDuration);

        return item;
    }

    private void ClearSpawnedItems()
    {
        _spawnedItems.Clear();
        if (_gridContainer == null) return;

        for (int i = _gridContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = _gridContainer.GetChild(i);
            if (_emptyGalleryText != null && child == _emptyGalleryText.transform) continue;
            Destroy(child.gameObject);
        }
    }

    private void ApplyHoverStyleToSpawnedItems()
    {
        for (int i = 0; i < _spawnedItems.Count; i++)
        {
            if (_spawnedItems[i] != null)
            {
                _spawnedItems[i].ApplyHoverStyle(
                    _hoverBorderColor,
                    _hoverBorderThickness,
                    _hoverBorderStyle,
                    _cornerBracketRatio,
                    _enableHoverScale,
                    _hoverScale,
                    _hoverFadeDuration);
            }
        }
    }

    /// <summary>
    /// Responsively calculates the exact cell size (cellSize) based on available width,
    /// padding, spacing, and current column count.
    /// </summary>
    public void RecalculateGridLayout()
    {
        if (_gridLayoutGroup == null || _gridContainer == null) return;

        Canvas.ForceUpdateCanvases();

        int cols = Mathf.Max(1, _currentColumns);
        _gridLayoutGroup.padding = new RectOffset(_paddingLeft, _paddingRight, _paddingTop, _paddingBottom);
        _gridLayoutGroup.spacing = _gridSpacing;
        _gridLayoutGroup.startCorner = GridLayoutGroup.Corner.UpperLeft;
        _gridLayoutGroup.startAxis = GridLayoutGroup.Axis.Horizontal;
        _gridLayoutGroup.childAlignment = TextAnchor.UpperLeft;
        _gridLayoutGroup.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        _gridLayoutGroup.constraintCount = cols;

        float containerWidth = GetAvailableGridWidth();
        _lastKnownViewportWidth = containerWidth;

        float totalHorizontalPadding = _paddingLeft + _paddingRight;
        float totalHorizontalSpacing = _gridSpacing.x * (cols - 1);
        float usableWidth = Mathf.Max(_minCellSize * cols, containerWidth - totalHorizontalPadding - totalHorizontalSpacing);

        float cellWidth = Mathf.Max(_minCellSize, Mathf.Floor((usableWidth / cols) * 10f) / 10f);
        float cellHeight = Mathf.Max(_minCellSize, cellWidth * _cellHeightToWidthRatio);

        _gridLayoutGroup.cellSize = new Vector2(cellWidth, cellHeight);

        LayoutRebuilder.ForceRebuildLayoutImmediate(_gridContainer);

        for (int i = 0; i < _spawnedItems.Count; i++)
        {
            if (_spawnedItems[i] != null)
            {
                _spawnedItems[i].RefreshLayoutForCellSize();
            }
        }
    }

    private float GetAvailableGridWidth()
    {
        if (_galleryScrollRect != null && _galleryScrollRect.viewport != null && _galleryScrollRect.viewport.rect.width > 10f)
        {
            return _galleryScrollRect.viewport.rect.width;
        }

        if (_gridContainer != null)
        {
            if (_gridContainer.rect.width > 10f)
            {
                return _gridContainer.rect.width;
            }

            RectTransform parentRect = _gridContainer.parent as RectTransform;
            if (parentRect != null && parentRect.rect.width > 10f)
            {
                return parentRect.rect.width;
            }
        }

        if (_galleryScrollRect != null)
        {
            RectTransform scrollRectTransform = _galleryScrollRect.transform as RectTransform;
            if (scrollRectTransform != null && scrollRectTransform.rect.width > 10f)
            {
                return scrollRectTransform.rect.width;
            }
        }

        return 260f;
    }

    private IEnumerator RecalculateLayoutNextFrame()
    {
        yield return null;
        RecalculateGridLayout();
        if (_galleryScrollRect != null)
        {
            _galleryScrollRect.verticalNormalizedPosition = 1f;
        }
        _deferredLayoutCoroutine = null;
    }

    /// <summary>
    /// Cycles sequentially through configured column zoom presets (e.g. 4 -> 2 -> 1 -> 4).
    /// </summary>
    public void ToggleZoom()
    {
        if (_zoomColumnPresets == null || _zoomColumnPresets.Length == 0)
        {
            int fallbackCols = (_currentColumns == _defaultColumns) ? Mathf.Max(1, _defaultColumns / 2) : _defaultColumns;
            SetColumns(fallbackCols);
            return;
        }

        _currentZoomPresetIndex = (_currentZoomPresetIndex + 1) % _zoomColumnPresets.Length;
        SetColumns(_zoomColumnPresets[_currentZoomPresetIndex]);
    }

    /// <summary>
    /// Zooms in by decreasing the column count (larger thumbnails).
    /// </summary>
    public void ZoomIn()
    {
        if (_zoomColumnPresets != null && _zoomColumnPresets.Length > 1)
        {
            // Find next preset with fewer columns than current
            int bestCols = _currentColumns;
            int bestDiff = int.MaxValue;
            for (int i = 0; i < _zoomColumnPresets.Length; i++)
            {
                int candidate = Mathf.Max(1, _zoomColumnPresets[i]);
                if (candidate < _currentColumns && (_currentColumns - candidate) < bestDiff)
                {
                    bestDiff = _currentColumns - candidate;
                    bestCols = candidate;
                }
            }

            if (bestCols < _currentColumns)
            {
                SetColumns(bestCols);
                return;
            }
        }

        if (_currentColumns > 1)
        {
            SetColumns(_currentColumns - 1);
        }
    }

    /// <summary>
    /// Zooms out by increasing the column count (more compact thumbnails).
    /// </summary>
    public void ZoomOut()
    {
        if (_zoomColumnPresets != null && _zoomColumnPresets.Length > 1)
        {
            int bestCols = _currentColumns;
            int bestDiff = int.MaxValue;
            for (int i = 0; i < _zoomColumnPresets.Length; i++)
            {
                int candidate = Mathf.Max(1, _zoomColumnPresets[i]);
                if (candidate > _currentColumns && (candidate - _currentColumns) < bestDiff)
                {
                    bestDiff = candidate - _currentColumns;
                    bestCols = candidate;
                }
            }

            if (bestCols > _currentColumns)
            {
                SetColumns(bestCols);
                return;
            }
        }

        if (_currentColumns < 8)
        {
            SetColumns(_currentColumns + 1);
        }
    }

    /// <summary>
    /// Explicitly sets the grid column count and responsively recalculates cell sizes.
    /// </summary>
    public void SetColumns(int columnCount)
    {
        _currentColumns = Mathf.Clamp(columnCount, 1, 8);
        SyncZoomPresetIndexFromColumns(_currentColumns);
        RecalculateGridLayout();
        UpdateZoomStatusLabel();
    }

    private void SyncZoomPresetIndexFromColumns(int cols)
    {
        if (_zoomColumnPresets == null) return;
        for (int i = 0; i < _zoomColumnPresets.Length; i++)
        {
            if (_zoomColumnPresets[i] == cols)
            {
                _currentZoomPresetIndex = i;
                return;
            }
        }
    }

    private void UpdateZoomStatusLabel()
    {
        if (_zoomStatusText != null)
        {
            _zoomStatusText.text = $"{_currentColumns} col";
        }
    }

    #endregion

    #region Thumbnail Interaction, Preview & Contact Selector

    private void OnPhotoThumbnailClicked(GalleryPhotoItem item)
    {
        if (item == null || item.PhotoData == null) return;

        _selectedPhoto = item.PhotoData;

        // If in Quick-Select mode and instant send is enabled:
        if (_isQuickSelectMode && !string.IsNullOrEmpty(_quickSelectTargetContact) && _instantSendOnQuickSelect)
        {
            SendPhotoToContact(_selectedPhoto, _quickSelectTargetContact);
            return;
        }

        OpenPreview(_selectedPhoto);
    }

    /// <summary>
    /// Opens the full-size preview panel for the specified photo.
    /// </summary>
    public void OpenPreview(GalleryPhotoData photo)
    {
        if (photo == null) return;

        _selectedPhoto = photo;
        CloseContactSelector();

        if (_previewPanel == null)
        {
            // Fallback: if no preview panel is configured, go directly to contact selector/send
            OnShareButtonClicked();
            return;
        }

        Sprite sp = photo.ResolveSprite();
        if (_previewImage != null)
        {
            _previewImage.sprite = sp;
            _previewImage.type = Image.Type.Simple;
            _previewImage.preserveAspect = true;
            _previewImage.color = Color.white;
        }

        if (_previewTitleText != null)
        {
            _previewTitleText.text = photo.Title;
        }

        if (_shareButtonLabel != null)
        {
            if (_isQuickSelectMode && !string.IsNullOrEmpty(_quickSelectTargetContact))
            {
                _shareButtonLabel.text = $"Send to {_quickSelectTargetContact}";
            }
            else
            {
                if (!string.IsNullOrEmpty(_defaultShareButtonText) && _defaultShareButtonText != "Enviar a...")
                {
                    _shareButtonLabel.text = _defaultShareButtonText;
                }
                else if (string.IsNullOrEmpty(_shareButtonLabel.text) || _shareButtonLabel.text == "Enviar a...")
                {
                    _shareButtonLabel.text = "Share";
                }
            }
        }

        _previewPanel.SetActive(true);
        _previewPanel.transform.SetAsLastSibling();
    }

    /// <summary>
    /// Closes the full-size preview and returns to the photo grid.
    /// </summary>
    public void ClosePreview()
    {
        if (_previewPanel != null)
        {
            _previewPanel.SetActive(false);
        }
    }

    public bool IsPreviewOpen()
    {
        return _previewPanel != null && _previewPanel.activeSelf;
    }

    /// <summary>
    /// Action triggered by the "Share / Send" button from preview or toolbar.
    /// </summary>
    public void OnShareButtonClicked()
    {
        if (_selectedPhoto == null) return;

        // If in Quick-Select mode (opened from a specific conversation), send directly to that contact
        if (_isQuickSelectMode && !string.IsNullOrEmpty(_quickSelectTargetContact))
        {
            SendPhotoToContact(_selectedPhoto, _quickSelectTargetContact);
            return;
        }

        OpenContactSelector();
    }

    /// <summary>
    /// Displays the list of valid contacts to write to (those with received messages).
    /// </summary>
    public void OpenContactSelector()
    {
        if (_selectedPhoto == null || _contactSelectorPanel == null) return;

        PopulateContactSelectorList();
        _contactSelectorPanel.SetActive(true);
        _contactSelectorPanel.transform.SetAsLastSibling();
    }

    /// <summary>
    /// Closes the contact selector modal.
    /// </summary>
    public void CloseContactSelector()
    {
        if (_contactSelectorPanel != null)
        {
            _contactSelectorPanel.SetActive(false);
        }
    }

    public bool IsContactSelectorOpen()
    {
        return _contactSelectorPanel != null && _contactSelectorPanel.activeSelf;
    }

    private void PopulateContactSelectorList()
    {
        if (_contactSelectorContainer == null) return;

        // Clean up previous contact buttons (preserving _emptyContactsText if parented to the same container)
        for (int i = _contactSelectorContainer.childCount - 1; i >= 0; i--)
        {
            Transform child = _contactSelectorContainer.GetChild(i);
            if (_emptyContactsText != null && child == _emptyContactsText.transform) continue;
            Destroy(child.gameObject);
        }

        List<Contact> validContacts = MessagesManager.Instance != null
            ? MessagesManager.Instance.GetWritableContacts(_onlyContactsWithReceivedMessages)
            : new List<Contact>();

        bool hasContacts = validContacts != null && validContacts.Count > 0;
        if (_emptyContactsText != null)
        {
            _emptyContactsText.gameObject.SetActive(!hasContacts);
        }

        if (!hasContacts) return;

        for (int i = 0; i < validContacts.Count; i++)
        {
            Contact contact = validContacts[i];
            if (contact == null) continue;

            string targetName = contact.Name();
            GameObject btnGO = CreateContactSelectorButton(targetName);
            Button btn = btnGO.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => SendPhotoToContact(_selectedPhoto, targetName));
            }
        }

        if (_contactSelectorContainer is RectTransform containerRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(containerRect);
        }
    }

    private GameObject CreateContactSelectorButton(string contactName)
    {
        GameObject itemGO;

        if (_contactSelectorItemPrefab != null)
        {
            itemGO = Instantiate(_contactSelectorItemPrefab, _contactSelectorContainer);

            // If ContactButton.prefab from ContactsScreen was reused, remove navigation scripts
            // to prevent interference with the Gallery photo-sharing callback.
            ContactButtonHelper legacyHelper = itemGO.GetComponent<ContactButtonHelper>();
            if (legacyHelper != null) DestroyImmediate(legacyHelper);

            PhoneTabButton legacyTabBtn = itemGO.GetComponent<PhoneTabButton>();
            if (legacyTabBtn != null) DestroyImmediate(legacyTabBtn);

            TMP_Text label = itemGO.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = contactName;
            }
        }
        else
        {
            itemGO = new GameObject($"SelectContact_{contactName}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            itemGO.transform.SetParent(_contactSelectorContainer, false);

            RectTransform rt = itemGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(200f, 30f);

            Image bg = itemGO.GetComponent<Image>();
            bg.color = new Color(0.92f, 0.92f, 0.95f, 1f);

            LayoutElement le = itemGO.GetComponent<LayoutElement>();
            le.preferredHeight = 28f;
            le.minHeight = 26f;
            le.flexibleWidth = 1f;

            GameObject textGO = new GameObject("Text (TMP)", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGO.transform.SetParent(itemGO.transform, false);
            RectTransform textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 2f);
            textRect.offsetMax = new Vector2(-8f, -2f);

            TextMeshProUGUI tmp = textGO.GetComponent<TextMeshProUGUI>();
            tmp.text = contactName;
            tmp.fontSize = 14f;
            tmp.color = new Color(0.15f, 0.15f, 0.18f, 1f);
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
        }

        itemGO.SetActive(true);
        return itemGO;
    }

    #endregion

    #region Bidirectional Sharing & Quick-Select Flow

    /// <summary>
    /// Sends the selected photo to the specified contact's conversation, appending it to chat history
    /// with native image message formatting and redirecting the player to that conversation.
    /// </summary>
    public void SendPhotoToContact(GalleryPhotoData photo, string contactName)
    {
        if (photo == null || string.IsNullOrEmpty(contactName) || MessagesManager.Instance == null)
        {
            Debug.LogWarning("[GalleryManager] Cannot send photo: invalid photo data or contact.");
            return;
        }

        Sprite sprite = photo.ResolveSprite();
        string imageKey = photo.GetEffectiveImageKey();
        string caption = photo.DefaultCaption;

        // Close modal overlays and clear quick-select before switching tabs
        CloseContactSelector();
        ClosePreview();
        _isQuickSelectMode = false;
        _quickSelectTargetContact = string.Empty;
        _pendingQuickSelectContact = null;
        UpdateQuickSelectBanner();

        // Inject photo into conversation and switch to contact chat
        MessagesManager.Instance.SendImageMessageToContactAndOpenChat(contactName, imageKey, sprite, caption);

        // Optionally dispatch narrative event to EventBus
        if (EventBus.Instance != null)
        {
            if (!string.IsNullOrEmpty(photo.EventOnShare))
            {
                EventBus.Instance.Publish(photo.EventOnShare);
            }

            EventBus.Instance.Publish("Gallery_PhotoSent", new GalleryPhotoSentPayload
            {
                PhotoId = photo.Id,
                ImageKey = imageKey,
                ContactName = contactName
            });
        }
    }

    /// <summary>
    /// Opens the Gallery in "Quick-Select Mode", targeting the active contact in MessagesManager.
    /// Ideal for hooking directly to the OnClick() event of the Attach/Camera button in MessagesScreen.
    /// </summary>
    public void OpenQuickSelectForCurrentContact()
    {
        if (MessagesManager.Instance == null) return;

        string currentContact = MessagesManager.Instance.GetCurrentContactName();
        if (string.IsNullOrEmpty(currentContact))
        {
            Debug.LogWarning("[GalleryManager] No active contact selected for Quick-Select mode.");
            return;
        }

        OpenQuickSelectForContact(currentContact);
    }

    /// <summary>
    /// Opens the Gallery in "Quick-Select Mode" bound to the specified contact.
    /// </summary>
    public void OpenQuickSelectForContact(string contactName)
    {
        if (string.IsNullOrEmpty(contactName)) return;

        _pendingQuickSelectContact = contactName;
        _isQuickSelectMode = true;
        _quickSelectTargetContact = contactName;

        PhoneStateMachine stateMachine = MessagesManager.Instance != null
            ? MessagesManager.Instance.GetPhoneStateMachine()
            : FindAnyObjectByType<PhoneStateMachine>(FindObjectsInactive.Include);

        if (stateMachine != null)
        {
            stateMachine.ChangeState((int)PhoneState.GALLERY);
        }
        else
        {
            gameObject.SetActive(true);
        }

        // Ensure active state after screen switch
        _isQuickSelectMode = true;
        _quickSelectTargetContact = contactName;
        _pendingQuickSelectContact = null;

        CloseContactSelector();
        ClosePreview();
        UpdateQuickSelectBanner();
        RefreshGallery();
    }

    /// <summary>
    /// Cancels Quick-Select Mode and returns directly to the conversation where the player came from.
    /// </summary>
    public void CancelQuickSelectAndReturnToChat()
    {
        string targetContact = _quickSelectTargetContact;
        _isQuickSelectMode = false;
        _quickSelectTargetContact = string.Empty;
        _pendingQuickSelectContact = null;
        UpdateQuickSelectBanner();

        if (!string.IsNullOrEmpty(targetContact) && MessagesManager.Instance != null)
        {
            MessagesManager.Instance.OpenContactConversation(targetContact);
        }
        else
        {
            PhoneStateMachine stateMachine = MessagesManager.Instance != null
                ? MessagesManager.Instance.GetPhoneStateMachine()
                : FindAnyObjectByType<PhoneStateMachine>(FindObjectsInactive.Include);

            stateMachine?.ChangeState((int)PhoneState.MESSAGING);
        }
    }

    private void UpdateQuickSelectBanner()
    {
        if (_quickSelectBanner != null)
        {
            _quickSelectBanner.SetActive(_isQuickSelectMode && !string.IsNullOrEmpty(_quickSelectTargetContact));
        }

        if (_quickSelectBannerText != null && _isQuickSelectMode && !string.IsNullOrEmpty(_quickSelectTargetContact))
        {
            _quickSelectBannerText.text = $"Send photo to {_quickSelectTargetContact}";
        }
    }

    /// <summary>
    /// Intercepts mobile UI/hardware "Back" action while Gallery is active.
    /// Returns true if Gallery consumed the event (dismissing a modal or returning from Quick-Select).
    /// </summary>
    public bool TryHandleBackButton()
    {
        if (IsContactSelectorOpen())
        {
            CloseContactSelector();
            return true;
        }

        if (IsPreviewOpen())
        {
            ClosePreview();
            return true;
        }

        if (_isQuickSelectMode && !string.IsNullOrEmpty(_quickSelectTargetContact))
        {
            CancelQuickSelectAndReturnToChat();
            return true;
        }

        return false;
    }

    #endregion

    #region Public Photo Management API (Events / Gameplay)

    /// <summary>
    /// Adds a new photo to the Gallery at runtime or unlocks it if already present.
    /// </summary>
    public void AddOrUnlockPhoto(string id, string title, string imagePath, Sprite sprite = null, string defaultCaption = "")
    {
        for (int i = 0; i < _photos.Count; i++)
        {
            GalleryPhotoData existing = _photos[i];
            if (existing == null) continue;

            if ((!string.IsNullOrEmpty(id) && string.Equals(existing.Id, id, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(imagePath) && string.Equals(existing.ImagePath, imagePath, StringComparison.OrdinalIgnoreCase)))
            {
                existing.IsUnlocked = true;
                if (sprite != null) existing.DirectSprite = sprite;
                existing.ResolveSprite();
                if (isActiveAndEnabled) RefreshGallery();
                return;
            }
        }

        GalleryPhotoData added = new GalleryPhotoData(id, title, imagePath, sprite, unlocked: true, defaultCaption: defaultCaption);
        added.ResolveSprite();
        _photos.Add(added);

        if (isActiveAndEnabled)
        {
            RefreshGallery();
        }
    }

    /// <summary>
    /// Unlocks an existing photo by its ID or image path.
    /// </summary>
    public void UnlockPhotoByIdOrPath(string idOrPath)
    {
        if (string.IsNullOrEmpty(idOrPath)) return;

        bool changed = false;
        for (int i = 0; i < _photos.Count; i++)
        {
            GalleryPhotoData p = _photos[i];
            if (p == null) continue;

            if (string.Equals(p.Id, idOrPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.ImagePath, idOrPath, StringComparison.OrdinalIgnoreCase))
            {
                if (!p.IsUnlocked)
                {
                    p.IsUnlocked = true;
                    changed = true;
                }
            }
        }

        if (changed && isActiveAndEnabled)
        {
            RefreshGallery();
        }
    }

    private void AddPhotoFromPathEvent(string imagePath)
    {
        if (RegisterImagePathIfMissing(imagePath) && isActiveAndEnabled)
        {
            RefreshGallery();
        }
    }

    #endregion
}
