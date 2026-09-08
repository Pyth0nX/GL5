using UnityEngine;
using UnityEngine.InputSystem;

public class PhoneUI : MonoBehaviour
{
    public GameObject phoneUI;

    void Start()
    {
        phoneUI.SetActive(false);
    }

    void Update()
    {
        if (Keyboard.current.xKey.wasPressedThisFrame)
        {
            phoneUI.SetActive(!phoneUI.activeSelf);
        }
    }
}