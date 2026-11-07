using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OptionsChanger : MonoBehaviour
{
    private struct DisplayOptionEntry
    {
        public int realOptionIndex;
        public Option option;
        public bool isUnlocked;
    }

    [SerializeField]
    private GameObject _decisionsPanel;

    [SerializeField]
    private NarrativeNode _currentNode;

    [Header("Option Buttons (1 to 4)")]
    [SerializeField]
    private Button[] _optionButtons;

    [Header("Rows (Option B Layout)")]
    [SerializeField]
    private GameObject _row1;
    [SerializeField]
    private GameObject _row2;

    // Backward compatibility with legacy serialized fields
    [SerializeField, HideInInspector]
    private Button _option1Button;
    [SerializeField, HideInInspector]
    private Button _option2Button;

    private void Awake()
    {
        InitializeButtons();
    }

    private GameObject FindChildRecursive(Transform parent, string name)
    {
        if (parent == null) return null;
        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child.name == name) return child.gameObject;
            var deeper = FindChildRecursive(child, name);
            if (deeper != null) return deeper;
        }
        return null;
    }

    private void InitializeButtons()
    {
        // 0. Auto-find panel if null
        if (_decisionsPanel == null)
        {
            var found = GameObject.Find("DecissionsUI");
            if (found != null)
            {
                _decisionsPanel = found;
            }
        }

        // 1. Resolve Rows if not assigned
        if (_decisionsPanel != null)
        {
            if (_row1 == null)
            {
                _row1 = FindChildRecursive(_decisionsPanel.transform, "Row1");
            }
            if (_row2 == null)
            {
                _row2 = FindChildRecursive(_decisionsPanel.transform, "Row2");
            }
        }

        // 2. Resolve Option Buttons if empty
        if (_optionButtons == null || _optionButtons.Length == 0)
        {
            List<Button> buttonsList = new List<Button>();

            if (_decisionsPanel != null)
            {
                // Find all OptionButtonHelper in the panel and sort them by optionIndex
                var helpers = _decisionsPanel.GetComponentsInChildren<OptionButtonHelper>(true);
                if (helpers != null && helpers.Length > 0)
                {
                    System.Array.Sort(helpers, (a, b) => a.OptionIndex.CompareTo(b.OptionIndex));
                    foreach (var h in helpers)
                    {
                        var btn = h.GetComponent<Button>();
                        if (btn != null && !buttonsList.Contains(btn))
                        {
                            buttonsList.Add(btn);
                        }
                    }
                }
            }

            // Fallback to legacy fields if needed
            if (buttonsList.Count == 0)
            {
                if (_option1Button != null) buttonsList.Add(_option1Button);
                if (_option2Button != null) buttonsList.Add(_option2Button);
            }

            _optionButtons = buttonsList.ToArray();
        }

        // Keep legacy fields synced
        if (_optionButtons != null && _optionButtons.Length > 0 && _option1Button == null)
            _option1Button = _optionButtons[0];
        if (_optionButtons != null && _optionButtons.Length > 1 && _option2Button == null)
            _option2Button = _optionButtons[1];
    }

    /// <summary>
    /// Updates the buttons texts with the current node info, filtering/locking options by Reputation conditions,
    /// and adapting layout for 1, 2, 3, or 4 visible options.
    /// </summary>
    private void UpdateButtonsTexts()
    {
        if (_currentNode == null || !_currentNode.HasOptions()) return;

        InitializeButtons();

        Option[] options = _currentNode.Options();
        List<DisplayOptionEntry> displayOptions = new List<DisplayOptionEntry>();

        if (options != null)
        {
            for (int i = 0; i < options.Length; i++)
            {
                Option opt = options[i];
                if (opt == null) continue;

                bool unlocked = opt.AreConditionsMet();
                if (!unlocked && opt.unavailableBehavior == UnavailableOptionBehavior.Hide)
                {
                    continue;
                }

                displayOptions.Add(new DisplayOptionEntry
                {
                    realOptionIndex = i,
                    option = opt,
                    isUnlocked = unlocked
                });
            }
        }

        int visibleCount = displayOptions.Count;

        // Populate and enable/disable individual UI button slots
        if (_optionButtons != null)
        {
            for (int slot = 0; slot < _optionButtons.Length; slot++)
            {
                Button btn = _optionButtons[slot];
                if (btn == null) continue;

                if (slot < visibleCount)
                {
                    DisplayOptionEntry entry = displayOptions[slot];
                    btn.gameObject.SetActive(true);
                    btn.interactable = entry.isUnlocked;

                    // Map this UI slot to the real Option index inside _currentNode.Options()
                    OptionButtonHelper helper = btn.GetComponent<OptionButtonHelper>();
                    if (helper != null)
                    {
                        helper.SetOptionIndex(entry.realOptionIndex);
                    }

                    var textComponent = btn.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (textComponent != null)
                    {
                        string displayText = (!entry.isUnlocked && !string.IsNullOrEmpty(entry.option.lockedText))
                            ? entry.option.lockedText
                            : entry.option.text;
                        textComponent.text = displayText;
                    }
                }
                else
                {
                    btn.gameObject.SetActive(false);
                }
            }
        }

        // Manage Row visibility (Option B layout):
        // Row 1 contains slots 0 and 1. Visible if >= 1 visible option.
        // Row 2 contains slots 2 and 3. Visible if >= 3 visible options.
        if (_row1 != null)
        {
            _row1.SetActive(visibleCount >= 1);
        }

        if (_row2 != null)
        {
            _row2.SetActive(visibleCount >= 3);
        }

        // Force layout rebuild so LayoutGroups update immediately without a 1-frame visual delay
        if (_decisionsPanel != null && _decisionsPanel.activeInHierarchy)
        {
            var rect = _decisionsPanel.GetComponent<RectTransform>();
            if (rect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            }
        }
    }

    public void ChangeOptions(int selectedOptionId)
    {
        if (_currentNode == null || !_currentNode.HasOptions() ||
            selectedOptionId < 0 || selectedOptionId >= _currentNode.Options().Length)
        {
            return;
        }

        Option selectedOption = _currentNode.Options()[selectedOptionId];
        if (selectedOption == null || !selectedOption.AreConditionsMet())
        {
            return;
        }

        Contact contact = MessagesManager.Instance != null ? MessagesManager.Instance.GetCurrentContact() : null;
        if (contact == null)
        {
            Debug.LogWarning("[OptionsChanger] No contact is selected.");
            return;
        }

        // Hide the choice overlay before starting the selected branch and restore click-to-advance
        ActiveDecisionsPanel(false);

        // 1. Apply the Reputation/Safety impact first so subsequent branches and anxiety VFX reflect the updated state
        selectedOption.ApplyReputationImpact();

        // 2. Publish optional event on the EventBus (e.g., Choice1_Safe / Choice1_Neutral / Choice1_Risky)
        PublishOptionEvent(selectedOption);

        DialogueType lastType = _currentNode.DialogueType();

        // 3. Resolve the next node by evaluating conditional reputation branches
        int targetNodeId = selectedOption.ResolveNextNode();
        targetNodeId = MessagesManager.Instance.ResolveEntryRedirects(targetNodeId);

        contact.SetNarrativeNode(targetNodeId);
        contact.SetCurrentMessageIndex(0);

        _currentNode = MessagesManager.Instance.GetCurrentContactNarrativeNode();

        if (lastType == DialogueType.PHONE)
        {
            if (selectedOption.messagesToSend != null)
            {
                foreach (var message in selectedOption.messagesToSend)
                {
                    MessagesManager.Instance.SendMessageToCurrentContact(message);
                }
            }

            if (_currentNode != null && _currentNode.DialogueType() == DialogueType.PHONE)
            {
                if (_currentNode.GetActiveDialogues().Count > 0)
                {
                    contact.SetHasNewMessages(true);
                    MessagesManager.Instance.GetPhoneStateMachine()?.ShowDialogueTab();
                }
                else if (_currentNode.HasOptions())
                {
                    ActiveDecisionsPanel(true);
                    MessagesManager.Instance.GetPhoneStateMachine()?.ShowDecisionsTab();
                }
            }
        }
        else
        {
            if (DialogueManager.Instance != null)
            {
                DialogueManager.Instance.StartDialogue();
            }
        }
    }

    private static void PublishOptionEvent(Option option)
    {
        if (option == null || string.IsNullOrEmpty(option.eventToPublish) || EventBus.Instance == null)
            return;

        // Always notify parameterless subscribers for compatibility across the project
        EventBus.Instance.Publish(option.eventToPublish);

        // If a typed payload was also defined, publish it as well
        if (option.eventDataType != EventDataType.NONE && !string.IsNullOrEmpty(option.eventDataRawValue))
        {
            object val = option.EventDataValue();
            switch (option.eventDataType)
            {
                case EventDataType.INT:
                    EventBus.Instance.Publish<int>(option.eventToPublish, (int)val);
                    break;
                case EventDataType.FLOAT:
                    EventBus.Instance.Publish<float>(option.eventToPublish, (float)val);
                    break;
                case EventDataType.BOOL:
                    EventBus.Instance.Publish<bool>(option.eventToPublish, (bool)val);
                    break;
                case EventDataType.STRING:
                    EventBus.Instance.Publish<string>(option.eventToPublish, (string)val);
                    break;
            }
        }
    }

    public void SetCurrentNode(NarrativeNode node)
    {
        _currentNode = node;
        if (_currentNode != null && _currentNode.HasOptions())
        {
            UpdateButtonsTexts();
        }
    }

    public void ActiveDecisionsPanel(bool isActive)
    {
        if (_decisionsPanel != null)
        {
            _decisionsPanel.SetActive(isActive);
            if (isActive)
            {
                _decisionsPanel.transform.SetAsLastSibling();
            }
        }

        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.SetHasActiveDecision(isActive);
            DialogueManager.Instance.SetChoiceMode(isActive);
        }

        if (isActive && _currentNode != null && _currentNode.HasOptions())
        {
            UpdateButtonsTexts();
        }
    }

    public bool IsDecisionsPanelActive()
    {
        return _decisionsPanel != null && _decisionsPanel.activeSelf;
    }
}
