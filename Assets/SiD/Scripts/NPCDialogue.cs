using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class NPCDialogue : MonoBehaviour
{
    public string characterName = "Bob";

    [TextArea(2, 5)]
    public List<string> dialogue = new List<string>();

    public GameObject dialoguePanel;
    public TMP_Text characterNameText;
    public TMP_Text dialogueText;

    private int currentDialogue = 0;
    private ConversationLock conversationLock;

    void Start()
    {
        conversationLock = FindAnyObjectByType<ConversationLock>();
    }

    public void Interact()
    {
        // Start conversation
        if (!dialoguePanel.activeSelf)
        {
            dialoguePanel.SetActive(true);

            characterNameText.text = characterName;
            currentDialogue = 0;

            if (dialogue.Count > 0)
            {
                dialogueText.text = dialogue[currentDialogue];
            }

            // Lock player and face this NPC
            if (conversationLock != null)
            {
                conversationLock.LockPlayer(transform);
            }

            return;
        }

        // Next dialogue line
        currentDialogue++;

        if (currentDialogue < dialogue.Count)
        {
            dialogueText.text = dialogue[currentDialogue];
        }
        else
        {
            // Conversation finished
            dialoguePanel.SetActive(false);
            currentDialogue = 0;

            // Give player control back
            if (conversationLock != null)
            {
                conversationLock.UnlockPlayer();
            }
        }
    }
}