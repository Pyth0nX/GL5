using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OptionsChanger : MonoBehaviour
{
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
    /// Update the buttons texts with the current node info and adapt layout for 1, 2, 3, or 4 options.
    /// </summary>
    private void UpdateButtonsTexts()
    {
        if (_currentNode == null || !_currentNode.HasOptions()) return;

        InitializeButtons();

        Option[] options = _currentNode.Options();
        int totalOptions = options != null ? options.Length : 0;

        // Populate and enable/disable individual buttons
        if (_optionButtons != null)
        {
            for (int i = 0; i < _optionButtons.Length; i++)
            {
                if (_optionButtons[i] == null) continue;

                if (i < totalOptions)
                {
                    _optionButtons[i].gameObject.SetActive(true);
                    var textComponent = _optionButtons[i].GetComponentInChildren<TextMeshProUGUI>();
                    if (textComponent != null)
                    {
                        textComponent.text = options[i].text;
                    }
                }
                else
                {
                    _optionButtons[i].gameObject.SetActive(false);
                }
            }
        }

        // Manage Row visibility (Option B layout):
        // Row 1 contains options 0 and 1. Visible if >= 1 option.
        // Row 2 contains options 2 and 3. Visible if >= 3 options.
        if (_row1 != null)
        {
            _row1.SetActive(totalOptions >= 1);
        }

        if (_row2 != null)
        {
            _row2.SetActive(totalOptions >= 3);
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

    // Update is called once per frame
    void Update()
    {

    }

    public void ChangeOptions(int selectedOptionId)
    {
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.SetHasActiveDecision(false);
        }

        if (_currentNode == null || !_currentNode.HasOptions() || selectedOptionId >= _currentNode.Options().Length)
        {
            return;
        }

        Option selectedOption = _currentNode.Options()[selectedOptionId];
        
        if (!string.IsNullOrEmpty(selectedOption.eventToPublish))
        {
            EventBus.Instance.Publish(selectedOption.eventToPublish);
        }

        DialogueType lastType = _currentNode.DialogueType();
        Contact contact = MessagesManager.Instance.GetCurrentContact();
        contact.SetNarrativeNode(selectedOption.nextNode);
        contact.SetCurrentMessageIndex(0);

        _currentNode = MessagesManager.Instance.GetCurrentContactNarrativeNode();

        // If the new node doesn't have options, close the decisions panel
        if (_currentNode == null || !_currentNode.HasOptions())
        {
            _decisionsPanel.SetActive(false);
        }

        if (lastType == DialogueType.PHONE)
        {
            foreach (var message in selectedOption.messagesToSend)
            {
                MessagesManager.Instance.SendMessageToCurrentContact(message);
            }

            if (_currentNode != null && _currentNode.DialogueType() == DialogueType.PHONE)
            {
                if (_currentNode.Dialogues().Count > 0)
                {
                    contact.SetHasNewMessages(true);
                    MessagesManager.Instance.GetPhoneStateMachine().ShowDialogueTab();
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

        if (_currentNode != null && _currentNode.HasOptions())
        {
            UpdateButtonsTexts();
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
        _decisionsPanel.SetActive(isActive);
        if (DialogueManager.Instance != null)
        {
            DialogueManager.Instance.SetHasActiveDecision(isActive);
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
