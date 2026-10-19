using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

    }

    private string _playerName;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        EventBus.Instance.Subscribe<string>("ChangeScene", ChangeScene);
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// Returns the player name
    /// </summary>
    /// <returns></returns>
    public string GetPlayerName()
    {
        return _playerName;
    }

    /// <summary>
    /// Set the player name as the given one , only in case is different from the current one
    /// </summary>
    /// <param name="name"></param>
    public void SetPlayerName(string name)
    {
        // Only reasigns the value if it's a different name
        if (_playerName != name) _playerName = name;
    }

    public void ChangeScene(string sceneName)
    {
        if(SceneManager.GetActiveScene().name != sceneName) SceneManager.LoadScene(sceneName);
        EventBus.Instance.Publish("FadeOut");
    }

    /// <summary>
    /// Method that sets what to do when a scene is loaded. The params are mandatory even if they are not used
    /// </summary>
    /// <param name="scene"></param>
    /// <param name="mode"></param>
    public void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EventBus.Instance.Publish("FadeIn");
    }
}
