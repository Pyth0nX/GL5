using UnityEngine;
using UnityEngine.InputSystem;

public class EscapeMenu : MonoBehaviour
{
    public GameObject escapeMenu;
    [SerializeField] private PlayerController playerController;

    void Start()
    {
        escapeMenu.SetActive(false);
    }

    void Update()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            playerController.UnlockMouse();
            escapeMenu.SetActive(!escapeMenu.activeSelf);
        }
    }
}