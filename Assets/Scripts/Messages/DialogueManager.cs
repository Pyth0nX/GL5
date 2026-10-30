using TMPro;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; } // Singleton instance

    [SerializeField]
    private GameObject _dialogueBox; // The dialogue box UI element
    [SerializeField]
    private TextMeshProUGUI _characterNameText; // The text component for displaying the character's name
    [SerializeField]
    private TextMeshProUGUI _dialogueText; // The text component for displaying dialogue

    private List<Message> _currentDialogues; // The list of dialogues for the current conversation

    private string _currentContact; // The contact that is currently being interacted with

    private int _currentDialogueIndex = 0; // The index of the current dialogue being displayed

    private bool _isEndNode = false;

    private bool _hasActiveDecision = false;

    public bool HasActiveDecision => _hasActiveDecision;

    public void SetHasActiveDecision(bool hasActiveDecision)
    {
        _hasActiveDecision = hasActiveDecision;
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe("Conversation_Started", StartDialogue);
        }
    }

    private void OnDestroy()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe("Conversation_Started", StartDialogue);
        }
    }

    private void Update()
    {
        if (_hasActiveDecision)
        {
            return;
        }

        if (_dialogueBox.activeSelf && InputManager.Instance.ContinueDialogueWasPressed())
        {
            ContinueDialogue();
        }
    }

    public void StartDialogue()
    {
        Debug.Log("dialog activated");
        NarrativeNode currentNode = MessagesManager.Instance.GetCurrentContactNarrativeNode();
        if (currentNode == null)
        {
            SetHasActiveDecision(false);
            _dialogueBox.SetActive(false);
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ChangeState(0);
            }
            return;
        }

        _dialogueBox.SetActive(true);
        InputManager.Instance.DisableMovementMap();
        
        if (currentNode.HasOptions())
        {
            SetHasActiveDecision(true);
            MessagesManager.Instance.GetOptionsChanger().SetCurrentNode(currentNode);
            MessagesManager.Instance.GetOptionsChanger().ActiveDecisionsPanel(true);
        }
        else
        {
            SetHasActiveDecision(false);
            _currentDialogues = currentNode.Dialogues();
            _currentContact = MessagesManager.Instance.GetCurrentContactName();
            _isEndNode = currentNode.IsEndNode();
            ContinueDialogue();
        }
    }

    public void ContinueDialogue()
    {
        if (_hasActiveDecision)
        {
            return;
        }

        if (_currentDialogues == null || _currentDialogueIndex >= _currentDialogues.Count)
        {
            EndDialogue();
            return;
        }
        Message currentMessage = _currentDialogues[_currentDialogueIndex];
        
        string speaker = "";
        if (currentMessage.Sender() == Sender.Player) 
        {
            speaker = GameManager.Instance.GetPlayerName();
        } 
        else 
        {
            if (!string.IsNullOrEmpty(currentMessage.SpeakerName())) 
            {
                speaker = currentMessage.SpeakerName();
            } 
            else 
            {
                speaker = _currentContact;
            }
        }
        
        _characterNameText.text = speaker;
        _dialogueText.text = currentMessage.Content();
        _currentDialogueIndex++;
    }

    private void EndDialogue()
    {
        _currentDialogueIndex = 0;
        _currentDialogues = null;
        NarrativeNode currentNode = MessagesManager.Instance.GetCurrentContactNarrativeNode();
        
        if (currentNode != null)
        {
            if (!string.IsNullOrEmpty(currentNode.EventOnEnd()))
            {
                EventBus.Instance.Publish(currentNode.EventOnEnd());
            }

            MessagesManager.Instance.GetCurrentContact().SetNarrativeNode(currentNode.NextNode());
            currentNode = MessagesManager.Instance.GetCurrentContactNarrativeNode();
        }

        if (currentNode != null && currentNode.HasOptions())
        {
            SetHasActiveDecision(true);
            MessagesManager.Instance.GetOptionsChanger().SetCurrentNode(currentNode);
            MessagesManager.Instance.GetOptionsChanger().ActiveDecisionsPanel(true);
        }
        else if (currentNode != null && !_isEndNode)
        {
            SetHasActiveDecision(false);
            StartDialogue();
        }
        else
        {
            SetHasActiveDecision(false);
            _dialogueBox.SetActive(false);
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ChangeState(0);
            }
        }
    }
}
