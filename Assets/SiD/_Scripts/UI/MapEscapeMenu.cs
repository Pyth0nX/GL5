using UnityEngine;
using UnityEngine.InputSystem;

public class MapEscapeMenu : MonoBehaviour
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