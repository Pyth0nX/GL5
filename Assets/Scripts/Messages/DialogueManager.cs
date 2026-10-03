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
            Destroy(gameObject);
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
        _dialogueBox.SetActive(true);
        NarrativeNode currentNode = MessagesManager.Instance.GetCurrentContactNarrativeNode();
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
        _characterNameText.text = (currentMessage.Sender() == Sender.NPC) ? _currentContact : _currentContact; // !!!THE SECOND PART MUST BE PLAYER NAME
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
        MessagesManager.Instance.GetCurrentContact().SetNarrativeNode(currentNode.NextNode());
        currentNode = MessagesManager.Instance.GetCurrentContactNarrativeNode();
        if (currentNode != null && currentNode.HasOptions())
        {
            MessagesManager.Instance.GetOptionsChanger().SetCurrentNode(currentNode);
            MessagesManager.Instance.GetOptionsChanger().ActiveDecisionsPanel(true);
        }
    }

}
