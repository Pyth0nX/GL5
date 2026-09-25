using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChangerObject : MonoBehaviour
{
    public string sceneName;

    public void Interact()
    {
        SceneManager.LoadScene(sceneName);
    }
}