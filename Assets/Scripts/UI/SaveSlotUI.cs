using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls a single save slot card in the Main Menu Save Selection UI.
/// Displays "New Game" when the slot is empty or the active objective (and optional timestamp)
/// when occupied, and routes slot selection and save deletion actions to SaveManager.
/// </summary>
public class SaveSlotUI : MonoBehaviour
{
    [Header("Slot Configuration")]
    [Tooltip("Zero-based index of this save slot (0 = Slot 1, 1 = Slot 2, 2 = Slot 3).")]
    [Range(0, 9)]
    [SerializeField] private int _slotIndex = 0;

    [Tooltip("Format string used for the optional slot header text ({0} = 1-based slot number).")]
    [SerializeField] private string _slotHeaderFormat = "Slot {0}";

    [Header("UI References")]
    [Tooltip("Main button representing the clickable save slot card.")]
    [SerializeField] private Button _slotButton;

    [Tooltip("Optional header label displaying the slot title (e.g. 'Slot 1').")]
    [SerializeField] private TMP_Text _slotTitleText;

    [Tooltip("Primary label displaying 'New Game' when empty or the saved current objective when occupied.")]
    [SerializeField] private TMP_Text _statusOrObjectiveText;

    [Tooltip("Optional label displaying the save date/time when the slot is occupied.")]
    [SerializeField] private TMP_Text _timestampText;

    [Tooltip("Delete button integrated with or positioned below this slot card.")]
    [SerializeField] private Button _deleteButton;

    [Tooltip("Optional label displaying the percentage of scenario nodes/paths discovered in this save slot.")]
    [SerializeField] private TMP_Text _discoveryPercentageText;

    [Tooltip("Format string used for the discovered percentage label ({0} = rounded integer percentage).")]
    [SerializeField] private string _discoveryFormat = "Discovered: {0}%";

    [Header("Visual Behavior")]
    [Tooltip("Text displayed on the card when the slot has no saved data.")]
    [SerializeField] private string _emptySlotText = "New Game";

    [Tooltip("Fallback text displayed when the slot is occupied and no active objective text was recorded.")]
    [SerializeField] private string _fallbackOccupiedText = "Continue Game";

    [Tooltip("If true, hides the Delete button GameObject when empty; if false, keeps it visible and disables interactability.")]
    [SerializeField] private bool _hideDeleteButtonWhenEmpty = true;

    [Tooltip("If true and no separate Timestamp Text is assigned, appends the save timestamp below the objective text.")]
    [SerializeField] private bool _appendTimestampWhenNoLabel = false;

    public int SlotIndex => _slotIndex;

    private void Awake()
    {
        if (_slotButton == null)
        {
            _slotButton = GetComponent<Button>();
        }

        if (_slotButton != null)
        {
            _slotButton.onClick.AddListener(HandleRuntimeSlotClick);
        }

        if (_deleteButton != null)
        {
            _deleteButton.onClick.AddListener(HandleRuntimeDeleteClick);
        }
    }

    private void OnEnable()
    {
        RefreshSlotView();
    }

    private void OnDestroy()
    {
        if (_slotButton != null)
        {
            _slotButton.onClick.RemoveListener(HandleRuntimeSlotClick);
        }

        if (_deleteButton != null)
        {
            _deleteButton.onClick.RemoveListener(HandleRuntimeDeleteClick);
        }
    }

    /// <summary>
    /// Assigns the zero-based slot index at runtime and refreshes the visual state.
    /// </summary>
    public void SetSlotIndex(int slotIndex)
    {
        _slotIndex = Mathf.Max(0, slotIndex);
        RefreshSlotView();
    }

    /// <summary>
    /// Queries SaveManager for this slot's metadata and updates all labels and Delete button visibility.
    /// </summary>
    public void RefreshSlotView()
    {
        SaveSlotMetadata metadata = SaveManager.Instance != null
            ? SaveManager.Instance.GetSlotMetadata(_slotIndex)
            : new SaveSlotMetadata { slotIndex = _slotIndex, isOccupied = false };

        bool isOccupied = metadata != null && metadata.isOccupied;
        string defaultSlotHeader = !string.IsNullOrEmpty(_slotHeaderFormat)
            ? string.Format(_slotHeaderFormat, _slotIndex + 1)
            : $"Slot {_slotIndex + 1}";

        if (_slotTitleText != null)
        {
            if (isOccupied && !string.IsNullOrWhiteSpace(metadata.playerName))
            {
                _slotTitleText.text = metadata.playerName;
            }
            else
            {
                _slotTitleText.text = defaultSlotHeader;
            }
        }

        if (!isOccupied)
        {
            if (_statusOrObjectiveText != null)
            {
                _statusOrObjectiveText.text = _emptySlotText;
            }

            if (_timestampText != null)
            {
                _timestampText.text = string.Empty;
                _timestampText.gameObject.SetActive(false);
            }

            if (_discoveryPercentageText != null)
            {
                _discoveryPercentageText.text = string.Empty;
                _discoveryPercentageText.gameObject.SetActive(false);
            }
        }
        else
        {
            string slotPlayerName = !string.IsNullOrWhiteSpace(metadata.playerName)
                ? metadata.playerName
                : PlayerNameService.DefaultPlayerName;

            string displayObjective = !string.IsNullOrWhiteSpace(metadata.currentObjective)
                ? PlayerNameService.FormatText(metadata.currentObjective, slotPlayerName)
                : _fallbackOccupiedText;

            if (_timestampText != null)
            {
                bool hasTimestamp = !string.IsNullOrWhiteSpace(metadata.saveTimestamp);
                _timestampText.gameObject.SetActive(hasTimestamp);
                _timestampText.text = hasTimestamp ? metadata.saveTimestamp : string.Empty;
            }
            else if (_appendTimestampWhenNoLabel && !string.IsNullOrWhiteSpace(metadata.saveTimestamp))
            {
                displayObjective = $"{displayObjective}\n<size=75%>{metadata.saveTimestamp}</size>";
            }

            if (_statusOrObjectiveText != null)
            {
                _statusOrObjectiveText.text = displayObjective;
            }

            EnsureDiscoveryLabel();
            if (_discoveryPercentageText != null)
            {
                int pct = Mathf.Clamp(Mathf.RoundToInt(metadata.scenarioDiscoveryPercent), 0, 100);
                _discoveryPercentageText.gameObject.SetActive(true);
                _discoveryPercentageText.text = string.Format(_discoveryFormat, pct);
            }
        }

        UpdateDeleteButtonState(isOccupied);
    }

    private void EnsureDiscoveryLabel()
    {
        if (_discoveryPercentageText != null)
        {
            return;
        }

        Transform existing = transform.Find("DiscoveryPercentageText");
        if (existing != null)
        {
            _discoveryPercentageText = existing.GetComponent<TMP_Text>();
            if (_discoveryPercentageText != null)
            {
                return;
            }
        }

        Transform targetParent = _timestampText != null && _timestampText.transform.parent != null
            ? _timestampText.transform.parent
            : transform;

        GameObject labelObj = new GameObject("DiscoveryPercentageText", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObj.layer = gameObject.layer;
        labelObj.transform.SetParent(targetParent, false);

        RectTransform rt = labelObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-115f, 10f);
        rt.sizeDelta = new Vector2(190f, 24f);

        TextMeshProUGUI tmp = labelObj.GetComponent<TextMeshProUGUI>();
        if (_timestampText != null && _timestampText.font != null)
        {
            tmp.font = _timestampText.font;
        }
        else if (_statusOrObjectiveText != null && _statusOrObjectiveText.font != null)
        {
            tmp.font = _statusOrObjectiveText.font;
        }

        tmp.fontSize = 14f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = new Color(0.18f, 0.45f, 0.36f, 1f);
        tmp.alignment = TextAlignmentOptions.MidlineRight;
        tmp.raycastTarget = false;

        _discoveryPercentageText = tmp;
    }

    /// <summary>
    /// Invoked when the player clicks this save slot card.
    /// Prompts for the protagonist name and starts a New Game if empty, or loads the saved game if occupied.
    /// </summary>
    public void OnSlotClicked()
    {
        if (SaveManager.Instance == null)
        {
            Debug.LogError("[SaveSlotUI] SaveManager instance is unavailable.");
            return;
        }

        if (SaveManager.Instance.IsSlotOccupied(_slotIndex))
        {
            SaveManager.Instance.LoadGame(_slotIndex);
        }
        else
        {
            SaveSlotsPanelUI panel = GetComponentInParent<SaveSlotsPanelUI>(true) ?? FindAnyObjectByType<SaveSlotsPanelUI>(FindObjectsInactive.Include);
            if (panel != null)
            {
                panel.PromptNewGameForSlot(_slotIndex);
            }
            else
            {
                SaveManager.Instance.StartNewGame(_slotIndex);
            }
        }
    }

    /// <summary>
    /// Invoked when the player clicks the Delete button for this slot.
    /// Deletes the save file on disk and immediately refreshes this card back to "New Game".
    /// </summary>
    public void OnDeleteClicked()
    {
        if (SaveManager.Instance == null)
        {
            Debug.LogError("[SaveSlotUI] SaveManager instance is unavailable.");
            return;
        }

        SaveManager.Instance.DeleteSave(_slotIndex);
        RefreshSlotView();
    }

    private void UpdateDeleteButtonState(bool isOccupied)
    {
        if (_deleteButton == null) return;

        if (_hideDeleteButtonWhenEmpty)
        {
            _deleteButton.gameObject.SetActive(isOccupied);
            _deleteButton.interactable = isOccupied;
        }
        else
        {
            _deleteButton.gameObject.SetActive(true);
            _deleteButton.interactable = isOccupied;
        }
    }

    private void HandleRuntimeSlotClick()
    {
        // Prevent duplicate execution if OnSlotClicked was also wired manually in the Inspector OnClick list
        if (HasPersistentListener(_slotButton.onClick, this, nameof(OnSlotClicked)))
        {
            return;
        }

        OnSlotClicked();
    }

    private void HandleRuntimeDeleteClick()
    {
        // Prevent duplicate execution if OnDeleteClicked was also wired manually in the Inspector OnClick list
        if (HasPersistentListener(_deleteButton.onClick, this, nameof(OnDeleteClicked)))
        {
            return;
        }

        OnDeleteClicked();
    }

    private static bool HasPersistentListener(UnityEventBase unityEvent, Object targetObject, string methodName)
    {
        if (unityEvent == null) return false;

        int count = unityEvent.GetPersistentEventCount();
        for (int i = 0; i < count; i++)
        {
            if (unityEvent.GetPersistentTarget(i) == targetObject &&
                string.Equals(unityEvent.GetPersistentMethodName(i), methodName, System.StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
