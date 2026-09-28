using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PhoneDialogueSequence : MonoBehaviour
{
    [System.Serializable]
    public class DialogueSequence
    {
        public string dialogueName;

        [Tooltip("Message boxes in the order they should appear.")]
        public List<GameObject> messageBoxes = new List<GameObject>();
    }

    [Header("Dialogues")]
    public List<DialogueSequence> dialogues = new List<DialogueSequence>();

    [Header("Timing")]
    [SerializeField] private float timeBetweenMessages = 3f;

    [Header("Sound")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip typingSound;

    // Remembers which dialogue should play next,
    // even if the scene is unloaded and loaded again.
    private static int currentDialogue = 0;

    private void Start()
    {
        // Hide every message from every dialogue
        HideAllMessages();

        // Start the current dialogue
        if (dialogues.Count > 0)
        {
            StartCoroutine(PlayDialogue());
        }
    }

    private IEnumerator PlayDialogue()
    {
        // Prevent going outside the list
        int dialogueIndex = Mathf.Clamp(
            currentDialogue,
            0,
            dialogues.Count - 1
        );

        DialogueSequence sequence = dialogues[dialogueIndex];

        foreach (GameObject messageBox in sequence.messageBoxes)
        {
            yield return new WaitForSeconds(timeBetweenMessages);

            if (messageBox != null)
            {
                messageBox.SetActive(true);

                // Play typing/message sound
                if (audioSource != null && typingSound != null)
                {
                    audioSource.PlayOneShot(typingSound);
                }
            }
        }

        // Next time Home loads, use the next dialogue
        if (currentDialogue < dialogues.Count - 1)
        {
            currentDialogue++;
        }
    }

    private void HideAllMessages()
    {
        foreach (DialogueSequence dialogue in dialogues)
        {
            foreach (GameObject messageBox in dialogue.messageBoxes)
            {
                if (messageBox != null)
                {
                    messageBox.SetActive(false);
                }
            }
        }
    }
}