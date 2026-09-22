using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class SceneChangerObject : MonoBehaviour
{
    [SerializeField] private PlayerController playerController;
    public string sceneName;
    
    void Update()
    {
        if (Keyboard.current.fKey.wasPressedThisFrame)
        {
            playerController.UnlockMouse();
            SceneManager.LoadScene(sceneName);
        }
    }
}