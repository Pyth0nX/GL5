using UnityEngine;

/// <summary>
/// Attach this script to any GameObject (like your UI Canvas or Managers) 
/// to prevent it from being destroyed when loading a new scene.
/// NOTE: The object must be at the root of the hierarchy (it cannot have a parent).
/// </summary>
public class PersistBetweenScenes : MonoBehaviour
{
    private void Awake()
    {
        // Check if the object is at the root of the hierarchy
        if (transform.parent != null)
        {
            Debug.LogWarning($"[PersistBetweenScenes] {gameObject.name} has a parent! DontDestroyOnLoad only works on root objects. Unparenting it now.");
            transform.SetParent(null);
        }

        DontDestroyOnLoad(gameObject);
    }
}
