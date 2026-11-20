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
        if (_button == null) return;

        // Keep clickable so pressing the camera icon when photo sending is disallowed
        // triggers the protagonist's blocked inner thought instead of failing silently.
        if (!_button.interactable)
        {
            _button.interactable = true;
        }
    }

    /// <summary>
    /// Opens Gallery in quick select mode for the active contact in the current conversation,
    /// or triggers an inner thought if the current narrative node does not require a photo.
    /// </summary>
    public void OnQuickGalleryClicked()
    {
        if (MessagesManager.Instance == null || !MessagesManager.Instance.CanSendPhotoToCurrentContact())
        {
            MessagesManager.Instance?.TriggerBlockedPhotoSendMonologue();
            return;
        }

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
