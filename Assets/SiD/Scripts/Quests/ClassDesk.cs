using UnityEngine;

public class ClassDesk : MonoBehaviour
{
    public SchoolQuestManager schoolQuest;

    public void Interact()
    {
        schoolQuest.StartClass();
    }
}