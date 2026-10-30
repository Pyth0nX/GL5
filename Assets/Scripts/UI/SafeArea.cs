using UnityEngine;

/// <summary>
/// Dynamically adjusts a RectTransform's anchors to conform to Screen.safeArea.
/// Converts physical screen safe area pixels into normalized (0 to 1) anchor coordinates.
/// Automatically detects resolution, aspect ratio, and orientation changes.
/// Runs in Edit Mode ([ExecuteAlways]) for immediate feedback in the Unity Editor Game View.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
[DisallowMultipleComponent]
public class SafeArea : MonoBehaviour
{
    [Header("Safe Area Axes")]
    [Tooltip("Apply horizontal safe area margins (Left / Right).")]
    [SerializeField] private bool _conformX = true;

    [Tooltip("Apply vertical safe area margins (Top / Bottom).")]
    [SerializeField] private bool _conformY = true;

    [Header("Editor Simulation (Testing on PC)")]
    [Tooltip("Simulates notch / insets while in the Unity Editor to test UI robustness without mobile devices.")]
    [SerializeField] private bool _simulateInEditor = false;

    [Tooltip("Simulated inset margins in pixels (Left, Right, Top, Bottom) relative to current Game View.")]
    [SerializeField] private Vector4 _simulatedInsets = new Vector4(44f, 44f, 34f, 21f); // iPhone-style margins

    private RectTransform _rectTransform;
    private Rect _lastSafeArea = Rect.zero;
    private Vector2Int _lastScreenSize = Vector2Int.zero;
    private ScreenOrientation _lastOrientation = ScreenOrientation.AutoRotation;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        ApplySafeArea(true);
    }

    private void OnEnable()
    {
        ApplySafeArea(true);
    }

    private void Update()
    {
        RefreshIfNeeded();
    }

    private void OnValidate()
    {
        ApplySafeArea(true);
    }

    /// <summary>
    /// Checks if screen resolution, orientation, or safe area changed and applies updates if needed.
    /// </summary>
    public void RefreshIfNeeded()
    {
        Rect currentSafeArea = GetEffectiveSafeArea();
        Vector2Int currentScreenSize = new Vector2Int(Screen.width, Screen.height);
        ScreenOrientation currentOrientation = Screen.orientation;

        if (currentSafeArea != _lastSafeArea ||
            currentScreenSize != _lastScreenSize ||
            currentOrientation != _lastOrientation)
        {
            ApplySafeArea(false);
        }
    }

    /// <summary>
    /// Converts safe area to normalized anchors and applies to RectTransform.
    /// </summary>
    public void ApplySafeArea(bool force)
    {
        if (_rectTransform == null)
        {
            _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform == null) return;
        }

        int screenWidth = Screen.width;
        int screenHeight = Screen.height;

        if (screenWidth <= 0 || screenHeight <= 0) return;

        Rect safeArea = GetEffectiveSafeArea();

        if (!force && safeArea == _lastSafeArea &&
            _lastScreenSize.x == screenWidth &&
            _lastScreenSize.y == screenHeight &&
            _lastOrientation == Screen.orientation)
        {
            return;
        }

        _lastSafeArea = safeArea;
        _lastScreenSize = new Vector2Int(screenWidth, screenHeight);
        _lastOrientation = Screen.orientation;

        // Convert safe area pixel coordinates to normalized values (0.0 to 1.0)
        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;

        anchorMin.x /= screenWidth;
        anchorMin.y /= screenHeight;
        anchorMax.x /= screenWidth;
        anchorMax.y /= screenHeight;

        // Clamp to 0..1 for safety
        anchorMin.x = Mathf.Clamp01(anchorMin.x);
        anchorMin.y = Mathf.Clamp01(anchorMin.y);
        anchorMax.x = Mathf.Clamp01(anchorMax.x);
        anchorMax.y = Mathf.Clamp01(anchorMax.y);

        // Apply axis filters if disabled
        if (!_conformX)
        {
            anchorMin.x = 0f;
            anchorMax.x = 1f;
        }

        if (!_conformY)
        {
            anchorMin.y = 0f;
            anchorMax.y = 1f;
        }

        // Apply to RectTransform
        _rectTransform.anchorMin = anchorMin;
        _rectTransform.anchorMax = anchorMax;

        // Zero out pixel offsets so the rect matches the anchors perfectly
        _rectTransform.offsetMin = Vector2.zero;
        _rectTransform.offsetMax = Vector2.zero;
    }

    private Rect GetEffectiveSafeArea()
    {
        Rect safeArea = Screen.safeArea;

#if UNITY_EDITOR
        if (_simulateInEditor)
        {
            float left = _simulatedInsets.x;
            float right = _simulatedInsets.y;
            float top = _simulatedInsets.z;
            float bottom = _simulatedInsets.w;

            float x = left;
            float y = bottom;
            float width = Mathf.Max(0f, Screen.width - left - right);
            float height = Mathf.Max(0f, Screen.height - top - bottom);

            return new Rect(x, y, width, height);
        }
#endif

        return safeArea;
    }
}
