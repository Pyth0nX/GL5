using UnityEngine;
using UnityEngine.InputSystem;

public class CPCOpen : MonoBehaviour
{
    [SerializeField] private GameObject canvas;

    private bool playerInside = false;
    private bool canvasOpen = false;

    private void Start()
    {
        canvas.SetActive(false);
    }

    private void Update()
    {
        if (playerInside && Keyboard.current.eKey.wasPressedThisFrame)
        {
            canvasOpen = !canvasOpen;
            canvas.SetActive(canvasOpen);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = false;

            // Optional: automatically close when walking away
            if (canvasOpen)
            {
                canvasOpen = false;
                canvas.SetActive(false);
            }
        }
    }
}