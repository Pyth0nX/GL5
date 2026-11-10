using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent manager responsible for player character spawn placement across scenes,
/// day cycles, game starts, and school returns.
/// </summary>
public class PlayerSpawnManager : MonoBehaviour
{
    public const string SpawnerWakingUp = "WakingUp";
    public const string SpawnerFromSchool = "FromSchool";
    public const string HomeSceneName = "Home";
    public const string SchoolSceneName = "School";

    private static PlayerSpawnManager _instance;
    private static bool _applicationIsQuitting = false;

    public static bool HasInstance => _instance != null;

    public static PlayerSpawnManager Instance
    {
        get
        {
            if (_instance == null && !_applicationIsQuitting)
            {
                _instance = FindAnyObjectByType<PlayerSpawnManager>(FindObjectsInactive.Include);
                if (_instance == null && Application.isPlaying)
                {
                    GameObject go = new GameObject("PlayerSpawnManager");
                    _instance = go.AddComponent<PlayerSpawnManager>();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("Spawn Configuration")]
    [Tooltip("Default fallback position when waking up if no WakingUp spawner transform is found in the scene.")]
    [SerializeField] private Vector3 _fallbackWakingUpPosition = new Vector3(0f, 4.5f, 0f);

    [Tooltip("Default fallback position when returning from school if no FromSchool spawner transform is found.")]
    [SerializeField] private Vector3 _fallbackFromSchoolPosition = new Vector3(0f, 4.5f, 17f);

    [Tooltip("Default fallback rotation Euler angles for spawners.")]
    [SerializeField] private Vector3 _fallbackRotationEuler = new Vector3(0f, 180f, 0f);

    private string _lastActiveSceneName;
    private string _queuedSpawnerName;
    private bool _isReturningFromSchool;
    private Coroutine _confirmSpawnCoroutine;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _lastActiveSceneName = SceneManager.GetActiveScene().name;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (EventBus.Instance != null)
        {
            SubscribeEvents();
        }
    }

    private void Start()
    {
        // Re-ensure subscription to EventBus if EventBus initialized after this Awake
        SubscribeEvents();

        // If starting directly in Home scene (e.g. in Unity Editor), handle initial spawn
        string currentScene = SceneManager.GetActiveScene().name;
        if (string.Equals(currentScene, HomeSceneName, StringComparison.OrdinalIgnoreCase))
        {
            HandleHomeSceneSpawn();
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnsubscribeEvents();
    }

    private void OnApplicationQuit()
    {
        _applicationIsQuitting = true;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void SubscribeEvents()
    {
        if (EventBus.Instance == null) return;

        EventBus.Instance.Unsubscribe("NextDay", OnNextDayEvent);
        EventBus.Instance.Subscribe("NextDay", OnNextDayEvent);

        EventBus.Instance.Unsubscribe("Day1_ComebackHome", OnComebackHomeEvent);
        EventBus.Instance.Subscribe("Day1_ComebackHome", OnComebackHomeEvent);

        EventBus.Instance.Unsubscribe("ComebackHome", OnComebackHomeEvent);
        EventBus.Instance.Subscribe("ComebackHome", OnComebackHomeEvent);

        for (int day = 1; day <= 5; day++)
        {
            string startDayEvent = $"Start_Day{day}";
            EventBus.Instance.Unsubscribe(startDayEvent, OnStartDayEvent);
            EventBus.Instance.Subscribe(startDayEvent, OnStartDayEvent);
        }
    }

    private void UnsubscribeEvents()
    {
        if (EventBus.Instance == null) return;

        EventBus.Instance.Unsubscribe("NextDay", OnNextDayEvent);
        EventBus.Instance.Unsubscribe("Day1_ComebackHome", OnComebackHomeEvent);
        EventBus.Instance.Unsubscribe("ComebackHome", OnComebackHomeEvent);

        for (int day = 1; day <= 5; day++)
        {
            EventBus.Instance.Unsubscribe($"Start_Day{day}", OnStartDayEvent);
        }
    }

    /// <summary>
    /// Explicitly queues a designated spawner to be used upon entering the Home scene.
    /// </summary>
    public void QueueSpawn(string spawnerName)
    {
        _queuedSpawnerName = spawnerName;
    }

    /// <summary>
    /// Notifies the manager immediately before a scene change occurs so origin and destination can be tracked.
    /// </summary>
    public void NotifySceneChanging(string currentSceneName, string targetSceneName)
    {
        _lastActiveSceneName = currentSceneName;

        if (string.Equals(currentSceneName, SchoolSceneName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(targetSceneName, HomeSceneName, StringComparison.OrdinalIgnoreCase))
        {
            _isReturningFromSchool = true;
            _queuedSpawnerName = SpawnerFromSchool;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (string.Equals(scene.name, HomeSceneName, StringComparison.OrdinalIgnoreCase))
        {
            HandleHomeSceneSpawn();
        }

        _lastActiveSceneName = scene.name;
    }

    private void HandleHomeSceneSpawn()
    {
        // If SaveManager is actively restoring a saved game slot, preserve the player's saved position
        if (SaveManager.HasInstance && SaveManager.Instance.IsLoadingSavedGame)
        {
            _queuedSpawnerName = null;
            _isReturningFromSchool = false;
            return;
        }

        string targetSpawner;
        if (!string.IsNullOrEmpty(_queuedSpawnerName))
        {
            targetSpawner = _queuedSpawnerName;
        }
        else if (_isReturningFromSchool || string.Equals(_lastActiveSceneName, SchoolSceneName, StringComparison.OrdinalIgnoreCase))
        {
            targetSpawner = SpawnerFromSchool;
        }
        else
        {
            targetSpawner = SpawnerWakingUp;
        }

        _queuedSpawnerName = null;
        _isReturningFromSchool = false;

        // Immediate placement before first rendered frame
        SpawnPlayerAt(targetSpawner);

        // Follow-up confirmation next frame after all scene Start() methods have executed
        if (_confirmSpawnCoroutine != null)
        {
            StopCoroutine(_confirmSpawnCoroutine);
        }
        _confirmSpawnCoroutine = StartCoroutine(ConfirmSpawnNextFrame(targetSpawner));
    }

    private IEnumerator ConfirmSpawnNextFrame(string spawnerName)
    {
        yield return null;
        SpawnPlayerAt(spawnerName);
        _confirmSpawnCoroutine = null;
    }

    private void OnNextDayEvent()
    {
        HandleDayAdvanced();
    }

    private void OnStartDayEvent()
    {
        // Ensure that when a specific day starts, the player is at WakingUp if in Home scene
        if (string.Equals(SceneManager.GetActiveScene().name, HomeSceneName, StringComparison.OrdinalIgnoreCase))
        {
            // Only reposition to WakingUp if not returning from school
            if (!_isReturningFromSchool && !string.Equals(_lastActiveSceneName, SchoolSceneName, StringComparison.OrdinalIgnoreCase))
            {
                SpawnPlayerAt(SpawnerWakingUp);
            }
        }
    }

    private void HandleDayAdvanced()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        if (string.Equals(currentScene, HomeSceneName, StringComparison.OrdinalIgnoreCase))
        {
            SpawnPlayerAt(SpawnerWakingUp);
            if (_confirmSpawnCoroutine != null)
            {
                StopCoroutine(_confirmSpawnCoroutine);
            }
            _confirmSpawnCoroutine = StartCoroutine(ConfirmSpawnNextFrame(SpawnerWakingUp));
        }
        else
        {
            _queuedSpawnerName = SpawnerWakingUp;
        }
    }

    private void OnComebackHomeEvent()
    {
        _isReturningFromSchool = true;
        _queuedSpawnerName = SpawnerFromSchool;
    }

    /// <summary>
    /// Searches for a spawner Transform in the current scene matching the specified name (case-insensitive).
    /// Searches first under a root GameObject named "Spawners", then across all scene GameObjects.
    /// </summary>
    public Transform FindSpawner(string spawnerName)
    {
        if (string.IsNullOrEmpty(spawnerName)) return null;

        // 1. Search under a GameObject named "Spawners"
        GameObject spawnersRoot = GameObject.Find("Spawners");
        if (spawnersRoot != null)
        {
            foreach (Transform child in spawnersRoot.transform)
            {
                if (string.Equals(child.name, spawnerName, StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
            }
        }

        // 2. Direct search in scene by name
        GameObject directObj = GameObject.Find(spawnerName);
        if (directObj != null && directObj.scene == SceneManager.GetActiveScene())
        {
            return directObj.transform;
        }

        // 3. Fallback: Search all transforms in active scene
        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform t in allTransforms)
        {
            if (t.gameObject.scene == SceneManager.GetActiveScene() &&
                string.Equals(t.name, spawnerName, StringComparison.OrdinalIgnoreCase))
            {
                return t;
            }
        }

        return null;
    }

    /// <summary>
    /// Teleports the player to the requested spawner in the current scene.
    /// Uses predefined scene coordinates as a fallback if the spawner GameObject is missing.
    /// </summary>
    public bool SpawnPlayerAt(string spawnerName)
    {
        PlayerController player = PlayerController.Instance != null
            ? PlayerController.Instance
            : FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);

        if (player == null)
        {
            return false;
        }

        Transform spawnerTransform = FindSpawner(spawnerName);
        if (spawnerTransform != null)
        {
            player.Teleport(spawnerTransform.position, spawnerTransform.rotation);
            return true;
        }

        // Fallback coordinates if spawner GameObject is not found
        Vector3 fallbackPos = string.Equals(spawnerName, SpawnerFromSchool, StringComparison.OrdinalIgnoreCase)
            ? _fallbackFromSchoolPosition
            : _fallbackWakingUpPosition;
        Quaternion fallbackRot = Quaternion.Euler(_fallbackRotationEuler);

        player.Teleport(fallbackPos, fallbackRot);
        return true;
    }
}
