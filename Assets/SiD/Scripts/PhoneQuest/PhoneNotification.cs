using System.Collections;
using UnityEngine;

public class PhoneNotification : MonoBehaviour
{
    [Header("Notification")]
    public GameObject contactButton;

    [Header("Sound")]
    public AudioSource audioSource;
    public AudioClip buzzSound;

    [Header("Timing")]
    public float notificationDelay = 5f;

    void Start()
    {
        // Contact starts hidden
        contactButton.SetActive(false);

        StartCoroutine(NotificationTimer());
    }

    IEnumerator NotificationTimer()
    {
        // Wait before receiving the message
        yield return new WaitForSeconds(notificationDelay);

        // Contact appears
        contactButton.SetActive(true);

        // Play phone buzz
        if (audioSource != null && buzzSound != null)
        {
            audioSource.PlayOneShot(buzzSound);
        }
    }
}