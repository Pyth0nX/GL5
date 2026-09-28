using UnityEngine;
using UnityEngine.InputSystem;

public class GlobalPlayerInteraction : MonoBehaviour
{
    private SceneChangerObject nearbyDoor;
    private NPCDialogue nearbyNPC;
    private LockerConversation nearbyLockerConversation;
    private ClassDesk nearbyClassDesk;

    void Update()
    {
        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            // Locker quest conversation
            if (nearbyLockerConversation != null)
            {
                nearbyLockerConversation.Interact();
            }

            // Class desk
            else if (nearbyClassDesk != null)
            {
                nearbyClassDesk.Interact();
            }

            // Normal NPC
            else if (nearbyNPC != null)
            {
                nearbyNPC.Interact();
            }

            // Door / scene changer
            else if (nearbyDoor != null)
            {
                nearbyDoor.Interact();
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Door
        SceneChangerObject door = other.GetComponent<SceneChangerObject>();

        if (door != null)
        {
            nearbyDoor = door;
        }

        // Normal NPC
        NPCDialogue npc = other.GetComponent<NPCDialogue>();

        if (npc != null)
        {
            nearbyNPC = npc;
        }

        // Locker quest conversation
        LockerConversation lockerConversation =
            other.GetComponent<LockerConversation>();

        if (lockerConversation != null)
        {
            nearbyLockerConversation = lockerConversation;
        }

        // Class desk
        ClassDesk classDesk = other.GetComponent<ClassDesk>();

        if (classDesk != null)
        {
            nearbyClassDesk = classDesk;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        // Door
        SceneChangerObject door = other.GetComponent<SceneChangerObject>();

        if (door == nearbyDoor)
        {
            nearbyDoor = null;
        }

        // Normal NPC
        NPCDialogue npc = other.GetComponent<NPCDialogue>();

        if (npc == nearbyNPC)
        {
            nearbyNPC = null;
        }

        // Locker quest conversation
        LockerConversation lockerConversation =
            other.GetComponent<LockerConversation>();

        if (lockerConversation == nearbyLockerConversation)
        {
            nearbyLockerConversation = null;
        }

        // Class desk
        ClassDesk classDesk = other.GetComponent<ClassDesk>();

        if (classDesk == nearbyClassDesk)
        {
            nearbyClassDesk = null;
        }
    }
}