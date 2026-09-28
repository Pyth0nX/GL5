using UnityEngine;
using TMPro;

public class LockerConversation : MonoBehaviour
{
    [Header("Dialogue UI")]
    public GameObject dialoguePanel;
    public TMP_Text characterNameText;
    public TMP_Text dialogueText;

    [Header("Choices")]
    public GameObject choicePanel;

    [Header("Other Scripts")]
    public ConversationLock conversationLock;
    public SchoolQuestManager schoolQuestManager;

    private int dialogueStage = 0;
    private bool conversationStarted = false;
    private bool waitingForChoice = false;
    private bool questFinished = false;

    public void Interact()
    {
        // Quest has already been completed
        if (questFinished)
            return;

        // Waiting for player to click a choice
        if (waitingForChoice)
            return;

        // Start conversation
        if (!conversationStarted)
        {
            conversationStarted = true;
            dialogueStage = 0;

            dialoguePanel.SetActive(true);
            choicePanel.SetActive(false);

            if (conversationLock != null)
            {
                conversationLock.LockPlayer();
            }

            ShowDialogue();
            return;
        }

        // Move to next line
        dialogueStage++;
        ShowDialogue();
    }

    private void ShowDialogue()
    {
        switch (dialogueStage)
        {
            case 0:
                characterNameText.text = "Emma";
                dialogueText.text =
                    "Oh, look who finally decided to show up.";
                break;

            case 1:
                characterNameText.text = "Sophie";
                dialogueText.text =
                    "Did you see what she posted yesterday? What a loser.";
                break;

            case 2:
                characterNameText.text = "Emma";
                dialogueText.text =
                    "Imagine actually being that embarrassing.";
                break;

            case 3:
                ShowChoices();
                break;

            // These happen only if Ask To Move was selected

            case 4:
                characterNameText.text = "Luna";
                dialogueText.text =
                    "Could you move please?";
                break;

            case 5:
                characterNameText.text = "Sophie";
                dialogueText.text =
                    "Last time I checked the booknerds don't talk, move yourself nerd.";
                break;

            case 6:
                FinishConversation();
                break;
        }
    }

    private void ShowChoices()
    {
        waitingForChoice = true;
        choicePanel.SetActive(true);

        // Show and unlock mouse for the choice buttons
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void AskThemToMove()
    {
        if (!waitingForChoice)
            return;

        waitingForChoice = false;
        choicePanel.SetActive(false);

        dialogueStage = 4;
        ShowDialogue();
    }

    public void GoQuietly()
    {
        if (!waitingForChoice)
            return;

        waitingForChoice = false;
        choicePanel.SetActive(false);

        FinishConversation();
    }

    private void FinishConversation()
    {
        dialoguePanel.SetActive(false);
        choicePanel.SetActive(false);

        conversationStarted = false;
        questFinished = true;

        if (conversationLock != null)
        {
            conversationLock.UnlockPlayer();
        }

        if (schoolQuestManager != null)
        {
            schoolQuestManager.LockerFinished();
        }
    }
}