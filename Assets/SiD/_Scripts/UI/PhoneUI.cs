using UnityEngine;
using UnityEngine.InputSystem;

public class PhoneUI : MonoBehaviour
{
    public GameObject phoneUI;
    [SerializeField] private PlayerController playerController;

    void Start()
    {
        phoneUI.SetActive(false);
    }

    void Update()
    {
        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            playerController.UnlockMouse();
            phoneUI.SetActive(!phoneUI.activeSelf);
        }
    }
}