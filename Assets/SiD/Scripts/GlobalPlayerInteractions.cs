using UnityEngine;
using UnityEngine.InputSystem;

public class GlobalPlayerInteraction : MonoBehaviour
{
    private SceneChangerObject nearbyDoor;
    private NPCDialogue nearbyNPC;

    void Update()
    {
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (nearbyNPC != null)
            {
                nearbyNPC.Interact();
            }
            else if (nearbyDoor != null)
            {
                nearbyDoor.Interact();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        SceneChangerObject door = other.GetComponent<SceneChangerObject>();

        if (door != null)
        {
            nearbyDoor = door;
        }

        NPCDialogue npc = other.GetComponent<NPCDialogue>();

        if (npc != null)
        {
            nearbyNPC = npc;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        SceneChangerObject door = other.GetComponent<SceneChangerObject>();

        if (door == nearbyDoor)
        {
            nearbyDoor = null;
        }

        NPCDialogue npc = other.GetComponent<NPCDialogue>();

        if (npc == nearbyNPC)
        {
            nearbyNPC = null;
        }
    }
}