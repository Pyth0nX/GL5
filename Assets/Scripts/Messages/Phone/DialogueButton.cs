using UnityEngine;
using UnityEngine.UI;

public class DialogueButton : MonoBehaviour
{
    private Button _button;

    void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(OnDialogueButtonPressed);
    }

    void Update()
    {
        UpdateButtonVisibility();
    }

    private void UpdateButtonVisibility()
    {
        if (MessagesManager.Instance == null)
            return;

        Contact currentContact = MessagesManager.Instance.GetCurrentContact();
        if (currentContact == null)
        {
            _button.gameObject.SetActive(false);
            return;
        }

        if (currentContact.HasNewMessages())
        {
            NarrativeNode currentNode = MessagesManager.Instance.GetCurrentContactNarrativeNode();
            if (currentNode != null && currentContact.CurrentMessageIndex() < currentNode.Dialogues().Count)
            {
                _button.gameObject.SetActive(true);
            }
            else
            {
                _button.gameObject.SetActive(false);
                currentContact.SetHasNewMessages(false);
            }
        }
        else
        {
            _button.gameObject.SetActive(false);
        }
    }

    private void OnDialogueButtonPressed()
    {
        if (MessagesManager.Instance == null)
            return;

        Contact currentContact = MessagesManager.Instance.GetCurrentContact();
        if (currentContact != null && currentContact.HasNewMessages())
        {
            NarrativeNode currentNode = MessagesManager.Instance.GetCurrentContactNarrativeNode();
            if (currentNode != null)
            {
                int msgIndex = currentContact.CurrentMessageIndex();
                if (msgIndex < currentNode.Dialogues().Count)
                {
                    Message msg = currentNode.Dialogues()[msgIndex];
                    MessagesManager.Instance.SendMessageToCurrentContact(msg);
                    
                    currentContact.SetCurrentMessageIndex(msgIndex + 1);

                    if (currentContact.CurrentMessageIndex() >= currentNode.Dialogues().Count)
                    {
                        currentContact.SetHasNewMessages(false);
                        _button.gameObject.SetActive(false);
                        
                        if (!currentNode.HasOptions())
                        {
                            if (!string.IsNullOrEmpty(currentNode.EventOnEnd()))
                            {
                                EventBus.Instance.Publish(currentNode.EventOnEnd());
                            }
                            
                            currentContact.SetNarrativeNode(currentNode.NextNode());
                            currentContact.SetCurrentMessageIndex(0);
                            
                            // Check if the new node has messages to immediately allow reading them
                            NarrativeNode next = MessagesManager.Instance.GetCurrentContactNarrativeNode();
                            if (next != null && next.Dialogues().Count > 0)
                            {
                                currentContact.SetHasNewMessages(true);
                            }
                        }
                        else
                        {
                            // Show options panel
                            MessagesManager.Instance.GetOptionsChanger().SetCurrentNode(currentNode);
                            MessagesManager.Instance.GetOptionsChanger().ActiveDecisionsPanel(true);
                        }
                    }
                }
            }
        }
    }
}
