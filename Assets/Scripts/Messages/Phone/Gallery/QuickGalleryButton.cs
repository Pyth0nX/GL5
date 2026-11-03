using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Quick access component attached to the Attach / Gallery button inside the Messages conversation view
/// (MessagesScreen). When pressed, opens Gallery in "Quick Select Mode" automatically linked to the active contact.
/// </summary>
[RequireComponent(typeof(Button))]
public class QuickGalleryButton : MonoBehaviour
{
    [Tooltip("Optional reference to GalleryManager. If left empty, it is automatically located on the phone.")]
    [SerializeField] private GalleryManager _galleryManager;

    [Tooltip("Temporarily disables the button while message typing animation is playing.")]
    [SerializeField] private bool _disableWhileAnimatingMessages = true;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        if (_button != null)
        {
            _button.onClick.AddListener(OnQuickGalleryClicked);
        }
    }

    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(OnQuickGalleryClicked);
        }
    }

    private void Update()
    {
        if (!_disableWhileAnimatingMessages || _button == null) return;

        bool hasActiveContact = MessagesManager.Instance != null &&
                                !string.IsNullOrEmpty(MessagesManager.Instance.GetCurrentContactName());

        bool isAnimating = MessagesManager.Instance != null &&
                           MessagesManager.Instance.GetMessagesUI() != null &&
                           MessagesManager.Instance.GetMessagesUI().IsAnimating;

        bool shouldBeInteractable = hasActiveContact && !isAnimating;
        if (_button.interactable != shouldBeInteractable)
        {
            _button.interactable = shouldBeInteractable;
        }
    }

    /// <summary>
    /// Opens Gallery in quick select mode for the active contact in the current conversation.
    /// </summary>
    public void OnQuickGalleryClicked()
    {
        if (_galleryManager == null)
        {
            if (MessagesManager.Instance != null)
            {
                _galleryManager = MessagesManager.Instance.GetGalleryManager();
            }

            if (_galleryManager == null)
            {
                _galleryManager = FindAnyObjectByType<GalleryManager>(FindObjectsInactive.Include);
            }
        }

        if (_galleryManager != null)
        {
            _galleryManager.OpenQuickSelectForCurrentContact();
        }
        else
        {
            Debug.LogWarning("[QuickGalleryButton] GalleryManager not found in the scene.");
        }
    }
}
