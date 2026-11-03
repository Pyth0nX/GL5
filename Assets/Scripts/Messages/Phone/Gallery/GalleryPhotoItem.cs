using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Represents an interactive individual thumbnail inside the Gallery grid.
/// Manages Sprite rendering, aspect ratio/masking, click callbacks, and the hover highlight border.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class GalleryPhotoItem : MonoBehaviour
{
    [Header("Visual References (Auto-detected if left empty)")]
    [Tooltip("Intermediate container with RectMask2D to crop the thumbnail and apply micro-scaling on hover.")]
    [SerializeField] private RectTransform _photoViewport;

    [Tooltip("Image component displaying the photograph.")]
    [SerializeField] private Image _photoImage;

    [Tooltip("Interactive button for the cell.")]
    [SerializeField] private Button _button;

    [Tooltip("Component responsible for drawing the visual border on hover/selection.")]
    [SerializeField] private UIHoverFeedback _hoverBorder;

    [Header("Thumbnail Sizing")]
    [Tooltip("If enabled, fills the entire square cell by cropping outer edges without distortion (iOS/Android style). If disabled, displays the full image with letterboxing.")]
    [SerializeField] private bool _fillAndCropCell = true;

    private GalleryPhotoData _photoData;
    private Action<GalleryPhotoItem> _onClicked;

    public GalleryPhotoData PhotoData => _photoData;

    private void Awake()
    {
        EnsureHierarchyAndComponents();
    }

    /// <summary>
    /// Initializes the thumbnail with photo data and its selection callback.
    /// </summary>
    public void Initialize(GalleryPhotoData data, Action<GalleryPhotoItem> onClickedCallback, bool fillAndCropCell = true)
    {
        _photoData = data;
        _onClicked = onClickedCallback;
        _fillAndCropCell = fillAndCropCell;

        EnsureHierarchyAndComponents();

        Sprite sprite = _photoData != null ? _photoData.ResolveSprite() : null;
        if (_photoImage != null)
        {
            _photoImage.sprite = sprite;
            _photoImage.color = sprite != null ? Color.white : new Color(0.25f, 0.25f, 0.28f, 1f);
            ConfigureImageAspect(sprite);
        }

        if (_button != null)
        {
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(HandleClick);
        }
    }
    
    /// <summary>
    /// Synchronizes the hover border style from GalleryManager.
    /// </summary>
    public void ApplyHoverStyle(
        Color borderColor,
        float borderThickness,
        GalleryBorderStyle borderStyle,
        float cornerRatio,
        bool enableHoverScale,
        float hoverScale,
        float fadeDuration)
    {
        EnsureHierarchyAndComponents();
        if (_hoverBorder != null)
        {
            _hoverBorder.SetScaleTarget(_photoViewport != null ? _photoViewport : (_photoImage != null ? _photoImage.rectTransform : null));
            _hoverBorder.ApplyStyleFromManager(
                borderColor,
                borderThickness,
                borderStyle,
                cornerRatio,
                enableHoverScale,
                hoverScale,
                fadeDuration);
        }
    }

    /// <summary>
    /// Recalculates AspectRatioFitter when GridLayoutGroup changes cell size (Zoom).
    /// </summary>
    public void RefreshLayoutForCellSize()
    {
        if (_photoImage != null && _photoImage.sprite != null)
        {
            ConfigureImageAspect(_photoImage.sprite);
        }
    }

    private void HandleClick()
    {
        _onClicked?.Invoke(this);
    }

    private void EnsureHierarchyAndComponents()
    {
        // Ensure a base graphic on the root to receive pointer raycasts reliably
        Image rootBg = GetComponent<Image>();
        if (rootBg == null)
        {
            rootBg = gameObject.AddComponent<Image>();
            rootBg.color = new Color(0.14f, 0.14f, 0.16f, 1f);
        }
        rootBg.raycastTarget = true;

        // Ensure container with RectMask2D so the photo (and hover micro-zoom) does not overflow the cell
        if (_photoViewport == null)
        {
            Transform foundMask = transform.Find("PhotoViewport");
            if (foundMask != null)
            {
                _photoViewport = foundMask as RectTransform;
            }
        }

        if (_photoViewport == null && (_photoImage == null || _photoImage.gameObject == gameObject))
        {
            GameObject viewportGO = new GameObject("PhotoViewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGO.transform.SetParent(transform, false);
            _photoViewport = viewportGO.GetComponent<RectTransform>();
            _photoViewport.anchorMin = Vector2.zero;
            _photoViewport.anchorMax = Vector2.one;
            _photoViewport.pivot = new Vector2(0.5f, 0.5f);
            _photoViewport.offsetMin = Vector2.zero;
            _photoViewport.offsetMax = Vector2.zero;
            _photoViewport.localScale = Vector3.one;

            GameObject imgGO = new GameObject("ThumbnailImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imgGO.transform.SetParent(_photoViewport, false);
            _photoImage = imgGO.GetComponent<Image>();
            RectTransform imgRect = _photoImage.rectTransform;
            imgRect.anchorMin = Vector2.zero;
            imgRect.anchorMax = Vector2.one;
            imgRect.pivot = new Vector2(0.5f, 0.5f);
            imgRect.offsetMin = Vector2.zero;
            imgRect.offsetMax = Vector2.zero;
            imgRect.localScale = Vector3.one;
        }
        else if (_photoImage == null)
        {
            // Search child Image (distinct from root and border segments)
            Image[] childImages = GetComponentsInChildren<Image>(true);
            foreach (Image candidate in childImages)
            {
                if (candidate.gameObject != gameObject && !candidate.gameObject.name.StartsWith("BorderSeg_"))
                {
                    _photoImage = candidate;
                    break;
                }
            }

            if (_photoImage != null && _photoViewport == null && _photoImage.transform.parent != transform)
            {
                _photoViewport = _photoImage.transform.parent as RectTransform;
            }
        }

        if (_photoViewport != null && _photoViewport.GetComponent<RectMask2D>() == null)
        {
            _photoViewport.gameObject.AddComponent<RectMask2D>();
        }

        if (_photoImage != null)
        {
            _photoImage.raycastTarget = false;
        }

        if (_button == null)
        {
            _button = GetComponent<Button>();
            if (_button == null)
            {
                _button = gameObject.AddComponent<Button>();
            }
            _button.targetGraphic = rootBg;
            // Disable harsh tint transitions by default when added by code, delegating highlight to the border
            Navigation nav = _button.navigation;
            nav.mode = Navigation.Mode.Automatic;
            _button.navigation = nav;
        }

        if (_hoverBorder == null)
        {
            _hoverBorder = GetComponent<UIHoverFeedback>();
            if (_hoverBorder == null)
            {
                _hoverBorder = gameObject.AddComponent<UIHoverFeedback>();
            }
        }
    }

    private void ConfigureImageAspect(Sprite sprite)
    {
        if (_photoImage == null) return;

        AspectRatioFitter fitter = _photoImage.GetComponent<AspectRatioFitter>();

        if (_fillAndCropCell && sprite != null && sprite.rect.height > 0f && _photoViewport != null)
        {
            _photoImage.type = Image.Type.Simple;
            _photoImage.preserveAspect = false;

            if (fitter == null)
            {
                fitter = _photoImage.gameObject.AddComponent<AspectRatioFitter>();
            }

            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
        }
        else
        {
            if (fitter != null)
            {
                fitter.aspectMode = AspectRatioFitter.AspectMode.None;
            }

            RectTransform rt = _photoImage.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _photoImage.type = Image.Type.Simple;
            _photoImage.preserveAspect = true;
        }
    }
}
