using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DialogueButton : MonoBehaviour
{
    private Button _button;
    private float _timer = 0f;
    private Image _image;
    private TextMeshProUGUI _textMesh;

    void Awake()
    {
        _button = GetComponent<Button>();
        _image = GetComponent<Image>();
        _textMesh = GetComponentInChildren<TextMeshProUGUI>();
        
        _button.onClick.AddListener(OnDialogueButtonPressed);
    }

    void Update()
    {
        UpdateButtonVisibility();
    }

    private void SetVisualsActive(bool active)
    {
        if (_image != null) _image.enabled = active;
        if (_textMesh != null) _textMesh.enabled = active;
        if (_button != null) _button.interactable = active;
    }

    private void UpdateButtonVisibility()
    {
        if (MessagesManager.Instance == null)
            return;

        Contact currentContact = MessagesManager.Instance.GetCurrentContact();
        if (currentContact == null)
        {
            SetVisualsActive(false);
            _button.gameObject.SetActive(false);
            return;
        }

        if (currentContact.HasNewMessages())
        {
            NarrativeNode currentNode = MessagesManager.Instance.GetCurrentContactNarrativeNode();
            if (currentNode != null && currentContact.CurrentMessageIndex() < currentNode.Dialogues().Count)
            {
                // We keep the GameObject active so Update() continues running
                _button.gameObject.SetActive(true);
                
                Message nextMsg = currentNode.Dialogues()[currentContact.CurrentMessageIndex()];
                if (nextMsg.Sender() == Sender.NPC)
                {
                    // It's the NPC's turn. Hide the button visually and wait 1 second.
                    SetVisualsActive(false);
                    
                    _timer += Time.deltaTime;
                    if (_timer >= 2f)
                    {
                        _timer = 0f;
                        SendNextMessage();
                    }
                }
                else
                {
                    // It's the Player's turn. Show the button, wait for click.
                    SetVisualsActive(true);
                    _timer = 0f;
                }
            }
            else
            {
                SetVisualsActive(false);
                _button.gameObject.SetActive(false);
                currentContact.SetHasNewMessages(false);
            }
        }
        else
        {
            SetVisualsActive(false);
            _button.gameObject.SetActive(false);
        }
    }

    private void OnDialogueButtonPressed()
    {
        if (_button.interactable)
        {
            SendNextMessage();
        }
    }

    private void SendNextMessage()
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
                        SetVisualsActive(false);
                        
                        if (!currentNode.HasOptions())
                        {
                            if (!string.IsNullOrEmpty(currentNode.EventOnEnd()))
                            {
                                EventBus.Instance.Publish(currentNode.EventOnEnd());
                            }
                            
                            currentContact.SetNarrativeNode(currentNode.NextNode());
                            currentContact.SetCurrentMessageIndex(0);
                            
                            NarrativeNode next = MessagesManager.Instance.GetCurrentContactNarrativeNode();
                            if (next != null)
                            {
                                if (next.Dialogues().Count > 0)
                                {
                                    currentContact.SetHasNewMessages(true);
                                    gameObject.SetActive(true);
                                    MessagesManager.Instance.GetPhoneStateMachine().ShowDialogueTab();
                                }
                                else if (next.HasOptions())
                                {
                                    MessagesManager.Instance.GetOptionsChanger().SetCurrentNode(next);
                                    MessagesManager.Instance.GetOptionsChanger().ActiveDecisionsPanel(true);
                                    MessagesManager.Instance.GetPhoneStateMachine().ShowDecisionsTab();
                                }
                            }
                        }
                        else
                        {
                            // Show options panel
                            MessagesManager.Instance.GetOptionsChanger().SetCurrentNode(currentNode);
                            MessagesManager.Instance.GetOptionsChanger().ActiveDecisionsPanel(true);
                            MessagesManager.Instance.GetPhoneStateMachine().ShowDecisionsTab();
                        }
                    }
                }
            }
        }
    }
}
