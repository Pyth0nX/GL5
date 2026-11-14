using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private string _playerName = PlayerNameService.DefaultPlayerName;
    private bool _hasEnteredHomeFirstTime = false;

    private ScenarioSystemUI _scenarioSystemInstance;

    public bool HasEnteredHomeFirstTime => _hasEnteredHomeFirstTime;

    public void SetHasEnteredHomeFirstTime(bool hasEntered)
    {
        _hasEnteredHomeFirstTime = hasEntered;
    }

    /// <summary>
    /// Activates the ScenarioSystem Canvas when reaching the end of the game.
    /// Delegates to UIManager if available, or looks up an existing ScenarioSystemUI in the scene.
    /// </summary>
    public void ShowScenarioSystem()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowScenarioSystem();
            return;
        }

        if (_scenarioSystemInstance == null)
        {
            _scenarioSystemInstance = ScenarioSystemUI.Instance != null
                ? ScenarioSystemUI.Instance
                : FindAnyObjectByType<ScenarioSystemUI>(FindObjectsInactive.Include);
        }

        if (_scenarioSystemInstance == null)
        {
            Debug.LogWarning("[GameManager] ScenarioSystemUI was not found in UI or the active scene.");
            return;
        }

        _scenarioSystemInstance.ShowScenarioTree();
    }

    public void HideScenarioSystem()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.HideScenarioSystem();
        }
        _scenarioSystemInstance?.HideScenarioTree();
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _ = PlayerSpawnManager.Instance;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe<string>("ChangeScene", ChangeScene);
        }
    }

    private void OnDestroy()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe<string>("ChangeScene", ChangeScene);
        }
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
    /// Returns the player name.
    /// </summary>
    public string GetPlayerName()
    {
        return string.IsNullOrWhiteSpace(_playerName) ? PlayerNameService.DefaultPlayerName : _playerName;
    }

    /// <summary>
    /// Sets the player name if different from the current value.
    /// </summary>
    public void SetPlayerName(string name)
    {
        string sanitized = PlayerNameService.SanitizeName(name);
        if (_playerName != sanitized)
        {
            _playerName = sanitized;
        }
    }

    public void ChangeScene(string sceneName)
    {
        string currentSceneName = SceneManager.GetActiveScene().name;
        if (currentSceneName != sceneName)
        {
            if (PlayerSpawnManager.HasInstance)
            {
                PlayerSpawnManager.Instance.NotifySceneChanging(currentSceneName, sceneName);
            }

            if (SaveManager.HasInstance)
            {
                SaveManager.Instance.NotifyBeforeSceneTransition(currentSceneName, sceneName);
            }

            SceneManager.LoadScene(sceneName);
        }
    }

    /// <summary>
    /// Coordinates music, UI state, and Day 1 / Debug start initialization when a scene finishes loading.
    /// </summary>
    public void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name.Equals("MainMenu", System.StringComparison.OrdinalIgnoreCase))
        {
            _hasEnteredHomeFirstTime = false;
            _playerName = PlayerNameService.DefaultPlayerName;
            DayManager.ClearQueuedTestStart();

            if (DayManager.Instance != null)
            {
                DayManager.Instance.ResetDay();
            }

            if (DecisionDataManager.Instance != null)
            {
                DecisionDataManager.Instance.ResetVariables();
            }

            var reputationManager = FindAnyObjectByType<ReputationManager>(FindObjectsInactive.Include);
            if (reputationManager != null)
            {
                reputationManager.ResetReputation();
            }

            if (MessagesManager.Instance != null)
            {
                MessagesManager.Instance.ResetToInitialState();
            }

            if (ObjectivesManager.Instance != null)
            {
                ObjectivesManager.Instance.ResetObjectives();
            }

            if (UIManager.Instance != null)
            {
                UIManager.Instance.HideScenarioSystem();
                UIManager.Instance.ResetConnectState();
                UIManager.Instance.ChangeState((int)UIState.MAIN_MENU);
            }

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.ChangeMusic("Menu");
            }
        }
        else if (scene.name.Equals("Home", System.StringComparison.OrdinalIgnoreCase) ||
                 scene.name.Equals("School", System.StringComparison.OrdinalIgnoreCase))
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.ChangeMusic("Game");
            }

            if (UIManager.Instance != null)
            {
                UIManager.Instance.HideScenarioSystem();
                UIManager.Instance.ChangeState((int)UIState.DEFAULT);
            }

            bool isLoadingSave = SaveManager.HasInstance && SaveManager.Instance.IsLoadingSavedGame;
            if (scene.name.Equals("Home", System.StringComparison.OrdinalIgnoreCase) && !isLoadingSave)
            {
                if (DayManager.HasQueuedDebugStart)
                {
                    _hasEnteredHomeFirstTime = true;
                    StartCoroutine(ApplyDebugStartNextFrame());
                }
                else if (!_hasEnteredHomeFirstTime)
                {
                    _hasEnteredHomeFirstTime = true;
                    StartCoroutine(AdvanceDayNextFrame());
                }
            }
        }
    }

    private IEnumerator ApplyDebugStartNextFrame()
    {
        // Wait one frame so scene objects (Bed, HomeDoor, EventNodeUpdater) complete Start() subscriptions
        yield return null;
        if (DayManager.Instance != null)
        {
            DayManager.Instance.ApplyQueuedDebugStart();
        }
    }

    private IEnumerator AdvanceDayNextFrame()
    {
        yield return null;
        if (DayManager.Instance != null)
        {
            EventBus.Instance.Publish("NextDay");
        }
        else
        {
            Debug.LogWarning("[GameManager] DayManager.Instance is null when advancing day.");
        }
    }
}
