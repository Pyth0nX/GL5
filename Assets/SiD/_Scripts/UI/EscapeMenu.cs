using UnityEngine;
using UnityEngine.InputSystem;

public class EscapeMenu : MonoBehaviour
{
    public GameObject escapeMenu;

    void Start()
    {
        escapeMenu.SetActive(false);
    }

    void Update()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            escapeMenu.SetActive(!escapeMenu.activeSelf);
        }
    }
}