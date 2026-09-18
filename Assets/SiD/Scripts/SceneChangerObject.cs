using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class SceneChangerObject : MonoBehaviour
{
    public string sceneName;

    void Update()
    {
        if (Keyboard.current.fKey.wasPressedThisFrame)
        {
            SceneManager.LoadScene(sceneName);
        }
    }
}