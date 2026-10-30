using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OptionsChanger : MonoBehaviour
{
    [SerializeField]
    private GameObject _decisionsPanel;

    [SerializeField]
    private NarrativeNode _currentNode;

    [SerializeField]
    private Button _option1Button;
    [SerializeField]
    private Button _option2Button;


    /// <summary>
    /// Update the buttons texts with the current node info
    /// </summary>
    private void UpdateButtonsTexts()
    {
        //set the initial narrative text and button texts based on the current node
        _option1Button.GetComponentInChildren<TextMeshProUGUI>().text = _currentNode.Options()[0].text;
        _option2Button.GetComponentInChildren<TextMeshProUGUI>().text = _currentNode.Options()[1].text;
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
    }

    public bool IsDecisionsPanelActive()
    {
        return _decisionsPanel != null && _decisionsPanel.activeSelf;
    }
}
