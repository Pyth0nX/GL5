using TMPro;
using Unity.VisualScripting;
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

    [SerializeField]
    private PlayerController _playerController;


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
        Option selectedOption =  _currentNode.Options()[selectedOptionId];
        
        if (!string.IsNullOrEmpty(selectedOption.eventToPublish))
        {
            EventBus.Instance.Publish(selectedOption.eventToPublish);
        }

        MessagesManager.Instance.GetCurrentContact().SetNarrativeNode(selectedOption.nextNode);
        if(_currentNode.DialogueType() == DialogueType.PHONE)
        {
            foreach (var message in selectedOption.messagesToSend)
            {
                MessagesManager.Instance.SendMessageToCurrentContact(message);
            }
        }
        else
        {
            DialogueManager.Instance.StartDialogue();
        }

        _currentNode = MessagesManager.Instance.GetCurrentContactNarrativeNode();
        if (_currentNode == null || !_currentNode.HasOptions())
        {
            ActiveDecisionsPanel(false);
            return;
        }

        if(_currentNode != null && _currentNode.HasOptions())
        {
            UpdateButtonsTexts();
        }
    }

    public void SetCurrentNode(NarrativeNode node)
    {
        _currentNode = node;
        if(_currentNode != null && _currentNode.HasOptions())
        {
            UpdateButtonsTexts();
        }
    }

    public void ActiveDecisionsPanel(bool isActive)
    {
        _decisionsPanel.SetActive(isActive);
        if (isActive)
        {
            _playerController.UnlockMouse();
        }
        else
        {
            _playerController.LockMouse();
        }
    }
}
