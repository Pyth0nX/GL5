using UnityEngine;

public class ConversationLock : MonoBehaviour
{
    [Header("Player Controls")]
    public MonoBehaviour movementScript;

    [Header("Player Physics")]
    public Rigidbody playerRigidbody;

    public void LockPlayer()
    {
        // Disable movement / mouse controls
        if (movementScript != null)
        {
            movementScript.enabled = false;
        }

        // Stop physics from moving the player
        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector3.zero;
            playerRigidbody.angularVelocity = Vector3.zero;
            playerRigidbody.isKinematic = true;
        }
    }

    public void UnlockPlayer()
    {
        // Turn physics back on
        if (playerRigidbody != null)
        {
            playerRigidbody.isKinematic = false;
        }

        // Give controls back
        if (movementScript != null)
        {
            movementScript.enabled = true;
        }
    }
}