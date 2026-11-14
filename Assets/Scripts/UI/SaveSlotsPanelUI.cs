using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Manages the 3-slot Save Selection panel in the Main Menu UI.
/// Opens when the player clicks the "Play" button, refreshes all SaveSlotUI cards,
/// and optionally toggles the Main Menu primary buttons while the panel is visible.
/// </summary>
public class SaveSlotsPanelUI : MonoBehaviour
{
    [Header("Panel References")]
    [Tooltip("Root GameObject of the Save Slots panel. Defaults to this GameObject if left unassigned.")]
    [SerializeField] private GameObject _slotsPanelRoot;

    [Tooltip("Optional container of the primary Main Menu buttons (Play, Instructions, Exit) to hide while the slot panel is open.")]
    [SerializeField] private GameObject _mainMenuButtonsContainer;

    [Tooltip("Optional Back / Close button inside the Save Slots panel.")]
    [SerializeField] private Button _closePanelButton;

    [Header("Slot Cards")]
    [Tooltip("References to the 3 SaveSlotUI components (Slot 1, Slot 2, Slot 3). Auto-discovered from children if left empty.")]
    [SerializeField] private SaveSlotUI[] _saveSlots;

    [Header("Behavior")]
    [Tooltip("Automatically hides the Save Slots panel on startup until OpenPanel() is called.")]
    [SerializeField] private bool _hidePanelOnStart = true;

    [Header("New Game Name Prompt")]
    [Tooltip("Optional custom modal used to choose the protagonist's name before starting a New Game. Auto-created at runtime if left unassigned.")]
    [SerializeField] private NewGameNameModalUI _namePromptModal;

    private bool _isInitialized = false;
    private bool _isPanelOpen = false;

    private GameObject TargetPanelRoot => _slotsPanelRoot != null ? _slotsPanelRoot : gameObject;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void Start()
    {
        EnsureInitialized();

        if (_hidePanelOnStart && !_isPanelOpen)
        {
            ClosePanel();
        }
    }

    private void OnEnable()
    {
        if (SaveManager.HasInstance)
        {
            SaveManager.Instance.OnSlotSaved -= HandleSlotDataChanged;
            SaveManager.Instance.OnSlotSaved += HandleSlotDataChanged;
            SaveManager.Instance.OnSlotDeleted -= HandleSlotDataChanged;
            SaveManager.Instance.OnSlotDeleted += HandleSlotDataChanged;
        }

        if (TargetPanelRoot.activeInHierarchy)
        {
            RefreshAllSlots();
        }
    }

    private void OnDisable()
    {
        if (SaveManager.HasInstance)
        {
            SaveManager.Instance.OnSlotSaved -= HandleSlotDataChanged;
            SaveManager.Instance.OnSlotDeleted -= HandleSlotDataChanged;
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;

        if (_closePanelButton != null)
        {
            _closePanelButton.onClick.RemoveListener(HandleRuntimeCloseClick);
        }
    }

    /// <summary>
    /// Opens the Save Slots panel and refreshes all slot cards with the latest metadata on disk.
    /// Assign this method to the Main Menu "Play" button OnClick() event.
    /// </summary>
    public void OpenPanel()
    {
        if (TransitionManager.Instance != null && TransitionManager.Instance.IsTransitioning)
        {
            return;
        }
        EnsureInitialized();
        _isPanelOpen = true;

        if (_mainMenuButtonsContainer != null)
        {
            _mainMenuButtonsContainer.SetActive(false);
        }

        TargetPanelRoot.SetActive(true);
        RefreshAllSlots();
    }

    /// <summary>
    /// Closes the Save Slots panel and restores the Main Menu primary buttons if configured.
    /// Assign this method to the optional "Back" button OnClick() event.
    /// </summary>
    public void ClosePanel()
    {
        EnsureInitialized();
        _isPanelOpen = false;

        if (_namePromptModal != null && _namePromptModal.IsOpen)
        {
            _namePromptModal.Close();
        }

        if (_mainMenuButtonsContainer != null)
        {
            _mainMenuButtonsContainer.SetActive(true);
        }

        TargetPanelRoot.SetActive(false);
    }

    /// <summary>
    /// Opens the character-name prompt modal for an empty save slot and starts the New Game upon confirmation.
    /// </summary>
    public void PromptNewGameForSlot(int slotIndex)
    {
        if (SaveManager.Instance == null)
        {
            Debug.LogError("[SaveSlotsPanelUI] SaveManager instance is unavailable.");
            return;
        }

        EnsureNamePromptModal();
        if (_namePromptModal == null)
        {
            SaveManager.Instance.StartNewGame(slotIndex);
            return;
        }

        _namePromptModal.Open(slotIndex, HandleNewGameNameConfirmed);
    }

    private void EnsureNamePromptModal()
    {
        if (_namePromptModal != null) return;

        _namePromptModal = GetComponentInChildren<NewGameNameModalUI>(true);
        if (_namePromptModal == null)
        {
            _namePromptModal = gameObject.AddComponent<NewGameNameModalUI>();
        }
    }

    private void HandleNewGameNameConfirmed(int slotIndex, string playerName)
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.StartNewGame(slotIndex, playerName);
        }
    }

    /// <summary>
    /// Toggles the visibility of the Save Slots panel.
    /// </summary>
    public void TogglePanel()
    {
        if (TargetPanelRoot.activeSelf)
        {
            ClosePanel();
        }
        else
        {
            OpenPanel();
        }
    }

    /// <summary>
    /// Refreshes every SaveSlotUI card managed by this panel.
    /// </summary>
    public void RefreshAllSlots()
    {
        ResolveSlotCardsIfNeeded();
        if (_saveSlots == null) return;

        for (int i = 0; i < _saveSlots.Length; i++)
        {
            if (_saveSlots[i] != null)
            {
                _saveSlots[i].RefreshSlotView();
            }
        }
    }

    private void EnsureInitialized()
    {
        if (_isInitialized) return;
        _isInitialized = true;

        ResolveSlotCardsIfNeeded();

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        if (_closePanelButton != null)
        {
            _closePanelButton.onClick.AddListener(HandleRuntimeCloseClick);
        }
    }

    private void ResolveSlotCardsIfNeeded()
    {
        if (_saveSlots != null && _saveSlots.Length > 0)
        {
            bool anyAssigned = false;
            for (int i = 0; i < _saveSlots.Length; i++)
            {
                if (_saveSlots[i] != null)
                {
                    anyAssigned = true;
                    break;
                }
            }

            if (anyAssigned) return;
        }

        _saveSlots = TargetPanelRoot.GetComponentsInChildren<SaveSlotUI>(true);
        Array.Sort(_saveSlots, (a, b) => a.SlotIndex.CompareTo(b.SlotIndex));
    }

    private void HandleSlotDataChanged(int slotIndex)
    {
        if (!TargetPanelRoot.activeInHierarchy) return;
        RefreshAllSlots();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Reset panel back to closed state on scene transitions so returning to Main Menu shows the front menu first
        if (_isPanelOpen || TargetPanelRoot.activeSelf)
        {
            ClosePanel();
        }
    }

    private void HandleRuntimeCloseClick()
    {
        if (HasPersistentListener(_closePanelButton.onClick, this, nameof(ClosePanel)))
        {
            return;
        }

        ClosePanel();
    }

    private static bool HasPersistentListener(UnityEventBase unityEvent, UnityEngine.Object targetObject, string methodName)
    {
        if (unityEvent == null) return false;

        int count = unityEvent.GetPersistentEventCount();
        for (int i = 0; i < count; i++)
        {
            if (unityEvent.GetPersistentTarget(i) == targetObject &&
                string.Equals(unityEvent.GetPersistentMethodName(i), methodName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
