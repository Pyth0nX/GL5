using UnityEngine;

public class DisableGameMode : MonoBehaviour
{
    void Start()
    {
        UnityEditor.SceneView.FocusWindowIfItsOpen(typeof(UnityEditor.SceneView));
    }
}
