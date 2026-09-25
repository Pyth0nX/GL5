using UnityEngine;
using UnityEngine.InputSystem;

public class PhoneUI : MonoBehaviour
{
    public GameObject phoneUI;

    void Start()
    {
        // Phone starts closed
        phoneUI.SetActive(false);

        // Mouse starts hidden and locked
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            // Toggle phone
            phoneUI.SetActive(!phoneUI.activeSelf);

            if (phoneUI.activeSelf)
            {
                // Phone is open
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                // Phone is closed
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}