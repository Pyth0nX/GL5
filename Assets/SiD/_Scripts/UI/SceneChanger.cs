using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneChanger : MonoBehaviour
{
    private Transform _panel;


    private void Start()
    {
        EventBus.Instance.Subscribe("Exit",SetupButtons);
        SetupButtons();
    }
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SetupButtons();
    }

    private void SetupButtons()
    {
        _panel = transform.Find("Panel");

        if (_panel != null)
        {
            // Search in all of the children, even if they are inactive
            Button[] buttons = GetComponentsInChildren<Button>(true);
            foreach (Button btn in buttons)
            {
                if (btn.gameObject.name == "Home" || btn.gameObject.name == "School")
                {
                    // Remove just in case it was already asigned
                    btn.onClick.RemoveListener(HidePanel);
                    btn.onClick.AddListener(HidePanel);
                }
            }
        }
    }

    private void HidePanel()
    {
        if (_panel != null)
        {
            _panel.gameObject.SetActive(false);
        }
    }

    public void ChangeScene(string sceneName)
    {
        if(SceneManager.GetActiveScene().name != sceneName) SceneManager.LoadScene(sceneName);
    }
}