using System.Collections;
using TMPro;
using UnityEngine;

public class SchoolQuestManager : MonoBehaviour
{
    [Header("Quest UI")]
    public GameObject questCanvas;
    public TMP_Text questText;

    [Header("Screen Transition")]
    public GameObject blackScreen;

    [Header("Player")]
    public ConversationLock conversationLock;

    private int questStage = 0;

    void Start()
    {
        blackScreen.SetActive(false);

        // First objective when entering school
        SetQuest("Get your Books from your locker (the RED one)!");
    }

    public void LockerFinished()
    {
        if (questStage != 0)
            return;

        questStage = 1;

        SetQuest("Go to class.");
    }

    public void StartClass()
    {
        // Player hasn't finished the locker quest yet
        if (questStage != 1)
            return;

        questStage = 2;

        StartCoroutine(ClassTransition());
    }

    IEnumerator ClassTransition()
    {
        // Disable player controls
        if (conversationLock != null)
        {
            conversationLock.LockPlayer();
        }

        // Black screen
        blackScreen.SetActive(true);

        yield return new WaitForSeconds(3f);

        // Class finished
        blackScreen.SetActive(false);

        questStage = 3;

        SetQuest("Go home.");

        // Give player controls back
        if (conversationLock != null)
        {
            conversationLock.UnlockPlayer();
        }
    }

    void SetQuest(string newQuest)
    {
        questText.text = newQuest;
        questCanvas.SetActive(true);
    }
}