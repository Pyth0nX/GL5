using UnityEngine;

public class NPCData : MonoBehaviour
{
    [SerializeField]
    private string _characterName; // Name of the character (this need to be exactly the same as stated in the JSON)

    public void Interact()
    {
        Debug.Log($"Interacting with NPC: {_characterName}");
        MessagesManager.Instance.SetCurrentContact(_characterName);
        DialogueManager.Instance.StartDialogue();
    }
}
