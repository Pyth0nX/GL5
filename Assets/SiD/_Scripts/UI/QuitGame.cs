using UnityEngine;

public class QuitGame : MonoBehaviour
{
    public void Quit()
    {
        if (SaveManager.HasInstance)
        {
            SaveManager.Instance.TryAutoSaveBeforeQuit();
        }

        Application.Quit();
    }
}
