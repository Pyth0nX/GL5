using UnityEngine;

/// <summary>
/// Keeps the Main Menu background stretched across the full viewport while scaling
/// the 16:9 foreground content container uniformly so every menu element maintains
/// the exact same screen proportions without clipping across all resolutions and aspect ratios.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class MainMenuResponsiveLayout : MonoBehaviour
{
    [Header("Reference Layout")]
    [SerializeField] private Vector2 _referenceResolution = new Vector2(1920f, 1080f);

    [Header("Managed Targets")]
    [SerializeField] private RectTransform _backgroundRect;
    [SerializeField] private RectTransform _contentContainer;

    private RectTransform _selfRect;
    private Vector2 _lastViewportSize = Vector2.zero;

    private void Awake()
    {
        CacheReferences();
        ApplyResponsiveLayout();
    }

    private void OnEnable()
    {
        CacheReferences();
        ApplyResponsiveLayout();
    }

    private void Start()
    {
        ApplyResponsiveLayout();
    }

    private void LateUpdate()
    {
        if (_selfRect == null)
        {
            return;
        }

        Vector2 currentSize = _selfRect.rect.size;
        if (!Mathf.Approximately(currentSize.x, _lastViewportSize.x) ||
            !Mathf.Approximately(currentSize.y, _lastViewportSize.y))
        {
            ApplyResponsiveLayout();
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        ApplyResponsiveLayout();
    }

    private void OnTransformParentChanged()
    {
        ApplyResponsiveLayout();
    }

    private void CacheReferences()
    {
        if (_selfRect == null)
        {
            _selfRect = transform as RectTransform;
        }

        if (_backgroundRect == null)
        {
            Transform bg = transform.Find("Background");
            if (bg != null)
            {
                _backgroundRect = bg as RectTransform;
            }
        }

        if (_contentContainer == null)
        {
            Transform content = transform.Find("ContentContainer");
            if (content != null)
            {
                _contentContainer = content as RectTransform;
            }
        }
    }

    /// <summary>
    /// Stretches the menu root and background across the full screen and scales the foreground
    /// container uniformly to fit within the available viewport dimensions.
    /// </summary>
    public void ApplyResponsiveLayout()
    {
        CacheReferences();

        if (_selfRect != null)
        {
            _selfRect.localScale = Vector3.one;
            _selfRect.anchorMin = Vector2.zero;
            _selfRect.anchorMax = Vector2.one;
            _selfRect.offsetMin = Vector2.zero;
            _selfRect.offsetMax = Vector2.zero;
        }

        if (_backgroundRect != null)
        {
            _backgroundRect.localScale = Vector3.one;
            _backgroundRect.anchorMin = Vector2.zero;
            _backgroundRect.anchorMax = Vector2.one;
            _backgroundRect.offsetMin = Vector2.zero;
            _backgroundRect.offsetMax = Vector2.zero;
        }

        if (_selfRect == null || _contentContainer == null)
        {
            return;
        }

        Vector2 viewportSize = _selfRect.rect.size;
        if (viewportSize.x <= 1f || viewportSize.y <= 1f)
        {
            return;
        }

        _lastViewportSize = viewportSize;

        float refWidth = Mathf.Max(1f, _referenceResolution.x);
        float refHeight = Mathf.Max(1f, _referenceResolution.y);
        float uniformScale = Mathf.Min(viewportSize.x / refWidth, viewportSize.y / refHeight);

        _contentContainer.anchorMin = new Vector2(0.5f, 0.5f);
        _contentContainer.anchorMax = new Vector2(0.5f, 0.5f);
        _contentContainer.pivot = new Vector2(0.5f, 0.5f);
        _contentContainer.anchoredPosition = Vector2.zero;
        _contentContainer.sizeDelta = new Vector2(refWidth, refHeight);
        _contentContainer.localScale = new Vector3(uniformScale, uniformScale, 1f);
    }
}
