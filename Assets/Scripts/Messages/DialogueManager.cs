using NUnit.Framework;
using TMPro;
using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine.InputSystem;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; } // Singleton instance

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Instance.CopyReferencesFrom(this);
            Destroy(gameObject);
            return;
        }
    }

    private void CopyReferencesFrom(DialogueManager other)
    {
        this._dialogueBox = other._dialogueBox;
        this._characterNameText = other._characterNameText;
        this._dialogueText = other._dialogueText;
        this._playerInput = other._playerInput;
        this._playerController = other._playerController;

        if (this._playerInput != null)
        {
            this._continueDialogue = this._playerInput.actions["ContinueDialogue"];
        }
    }

    [SerializeField]
    private GameObject _dialogueBox; // The dialogue box UI element
    [SerializeField]
    private TextMeshProUGUI _characterNameText; // The text component for displaying the character's name
    [SerializeField]
    private TextMeshProUGUI _dialogueText; // The text component for displaying dialogue
    [SerializeField]
    private PlayerInput _playerInput; // The PlayerInput component for handling input
    [SerializeField]
    private PlayerController _playerController;


    private InputAction _continueDialogue; // The input action for continuing the dialogue

    private List<Message> _currentDialogues; // The list of dialogues for the current conversation

    private string _currentContact; // The contact that is currently being interacted with

    private int _currentDialogueIndex = 0; // The index of the current dialogue being displayed

    private bool _isEndNode = false;

    private void Start()
    {
        _continueDialogue = _playerInput.actions["ContinueDialogue"];
    }

    private void Update()
    {
        if (_dialogueBox.activeSelf && _continueDialogue.WasPressedThisFrame())
        {
            ContinueDialogue();
        }
    }

    public void StartDialogue()
    {
        NarrativeNode currentNode = MessagesManager.Instance.GetCurrentContactNarrativeNode();
        if (currentNode == null)
        {
            Debug.Log("[DialogueManager] Dialogue has ended for this contact (-1).");
            return;
        }

        _dialogueBox.SetActive(true);
        _playerController.DisableMovement();
        if (currentNode.HasOptions())
        {
            MessagesManager.Instance.GetOptionsChanger().SetCurrentNode(currentNode);
            MessagesManager.Instance.GetOptionsChanger().ActiveDecisionsPanel(true);
        }
        else
        {
            _currentDialogues = currentNode.Dialogues();
            _currentContact = MessagesManager.Instance.GetCurrentContactName();
            _isEndNode = currentNode.IsEndNode();
            ContinueDialogue();
        }
    }


    public void ContinueDialogue()
    {
        if (_currentDialogues == null || _currentDialogueIndex >= _currentDialogues.Count)
        {
            EndDialogue();
            return;
        }
        Message currentMessage = _currentDialogues[_currentDialogueIndex];
        
        string speaker = "";
        if (currentMessage.Sender() == Sender.Player) 
        {
            speaker = _playerController.GetPlayerName();
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
        if (_isEndNode) 
        {
            _dialogueBox.SetActive(false);
            _playerController.EnableMovement();
        }
        _currentDialogueIndex = 0;
        _currentDialogues = null;
        NarrativeNode currentNode = MessagesManager.Instance.GetCurrentContactNarrativeNode();
        
        if (!string.IsNullOrEmpty(currentNode.EventOnEnd()))
        {
            EventBus.Instance.Publish(currentNode.EventOnEnd());
        }

        MessagesManager.Instance.GetCurrentContact().SetNarrativeNode(currentNode.NextNode());
        currentNode = MessagesManager.Instance.GetCurrentContactNarrativeNode();
        if (currentNode != null && currentNode.HasOptions())
        {
            MessagesManager.Instance.GetOptionsChanger().SetCurrentNode(currentNode);
            MessagesManager.Instance.GetOptionsChanger().ActiveDecisionsPanel(true);
        }
    }

}
