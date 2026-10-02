using NUnit.Framework;
using TMPro;
using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;

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

    private List<Message> _currentDialogues; // The list of dialogues for the current conversation

    private string _currentContact; // The contact that is currently being interacted with

    private int _currentDialogueIndex = 0; // The index of the current dialogue being displayed

    public void StartDialogue()
    {
        _dialogueBox.SetActive(true);
        _currentDialogues = MessagesManager.Instance.GetCurrentContactNarrativeNode().Dialogues();
        _currentContact = MessagesManager.Instance.GetCurrentContactName();
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
        _dialogueBox.SetActive(false);
        _currentDialogueIndex = 0;
        _currentDialogues = null;
    }

}
