using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Persistent multi-slot Save and Load manager.
/// Serializes and deserializes GameSaveData as JSON inside Application.persistentDataPath,
/// coordinates state restoration across narrative, reputation, objective, and day managers,
/// and safely spawns the player character with a vertical gravity offset on scene load.
/// </summary>
public class SaveManager : MonoBehaviour
{
    public const int DefaultSlotCount = 3;

    private static SaveManager _instance;
    private static bool _applicationIsQuitting = false;

    public static bool HasInstance => _instance != null;

    public static SaveManager Instance
    {
        get
        {
            if (_instance == null && !_applicationIsQuitting)
            {
                _instance = FindAnyObjectByType<SaveManager>(FindObjectsInactive.Include);
                if (_instance == null && Application.isPlaying)
                {
                    GameObject go = new GameObject("SaveManager");
                    _instance = go.AddComponent<SaveManager>();
                }
            }
            return _instance;
        }
        private set => _instance = value;
    }

    [Header("Slot Configuration")]
    [Tooltip("Total number of available save slots (indices 0 to SlotCount - 1).")]
    [Range(1, 10)]
    [SerializeField] private int _slotCount = DefaultSlotCount;

    [Tooltip("Filename prefix for JSON save files stored in Application.persistentDataPath.")]
    [SerializeField] private string _saveFilePrefix = "save_slot_";

    [Tooltip("Timestamp format recorded in slot metadata.")]
    [SerializeField] private string _timestampFormat = "yyyy-MM-dd HH:mm";

    [Header("Scene & Spawn Settings")]
    [Tooltip("Gameplay scene loaded when starting a New Game from an empty slot.")]
    [SerializeField] private string _newGameStartingScene = "Home";

    [Tooltip("Name of the Main Menu scene where gameplay saving is disabled.")]
    [SerializeField] private string _mainMenuSceneName = "MainMenu";

    [Tooltip("Vertical upward offset (in meters) added to saved Y coordinates on load so gravity settles the player cleanly onto the floor.")]
    [Min(0f)]
    [SerializeField] private float _spawnHeightOffset = 0.5f;

    [Header("Auto-Save Behavior")]
    [Tooltip("Creates the initial save file automatically once Day 1 starts in a New Game slot.")]
    [SerializeField] private bool _autoSaveOnNewGameStart = true;

    [Tooltip("Automatically saves the active slot when the current objective progresses or changes.")]
    [SerializeField] private bool _autoSaveOnObjectiveChange = true;

    [Tooltip("Automatically saves the active slot before returning to Main Menu and after entering a new gameplay scene.")]
    [SerializeField] private bool _autoSaveOnSceneTransitions = true;

    [Header("Runtime State (Read Only)")]
    [SerializeField] private int _activeSlotIndex = -1;
    [SerializeField] private bool _isLoadingSavedGame = false;

    private GameSaveData _pendingLoadData;
    private bool _pendingInitialNewGameSave = false;
    private Coroutine _deferredAutoSaveCoroutine;

    /// <summary>
    /// Invoked whenever a slot is saved to disk. Parameter: slotIndex.
    /// </summary>
    public event Action<int> OnSlotSaved;

    /// <summary>
    /// Invoked whenever a slot file is deleted from disk. Parameter: slotIndex.
    /// </summary>
    public event Action<int> OnSlotDeleted;

    /// <summary>
    /// Invoked after a saved game finishes loading and restoring scene state. Parameter: slotIndex.
    /// </summary>
    public event Action<int> OnGameLoaded;

    public int SlotCount => _slotCount;
    public int ActiveSlotIndex => _activeSlotIndex;
    public bool HasActiveSlot => IsValidSlotIndex(_activeSlotIndex);
    public bool IsLoadingSavedGame => _isLoadingSavedGame;
    public float SpawnHeightOffset => _spawnHeightOffset;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnEnable()
    {
        if (_instance != null && _instance != this) return;

        SceneManager.sceneLoaded += OnSceneLoaded;

        if (ObjectivesManager.Instance != null)
        {
            ObjectivesManager.Instance.OnObjectiveUpdated -= HandleObjectiveUpdated;
            ObjectivesManager.Instance.OnObjectiveUpdated += HandleObjectiveUpdated;
        }
    }

    private void Start()
    {
        if (ObjectivesManager.Instance != null)
        {
            ObjectivesManager.Instance.OnObjectiveUpdated -= HandleObjectiveUpdated;
            ObjectivesManager.Instance.OnObjectiveUpdated += HandleObjectiveUpdated;
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        var objectivesManager = FindAnyObjectByType<ObjectivesManager>(FindObjectsInactive.Include);
        if (objectivesManager != null)
        {
            objectivesManager.OnObjectiveUpdated -= HandleObjectiveUpdated;
        }
    }

    private void OnApplicationQuit()
    {
        _applicationIsQuitting = true;
    }

    #region Public Save & Load API

    /// <summary>
    /// Detaches any active save slot so debug/testing jumps do not overwrite user save files.
    /// </summary>
    public void DetachActiveSlotForDebugSession()
    {
        if (_deferredAutoSaveCoroutine != null)
        {
            StopCoroutine(_deferredAutoSaveCoroutine);
            _deferredAutoSaveCoroutine = null;
        }

        _activeSlotIndex = -1;
        _isLoadingSavedGame = false;
        _pendingLoadData = null;
        _pendingInitialNewGameSave = false;
    }

    /// <summary>
    /// Saves the complete runtime game state into the specified slot index as JSON on disk.
    /// </summary>
    /// <param name="slotIndex">Zero-based slot index (e.g. 0, 1, 2).</param>
    public void SaveGame(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex))
        {
            Debug.LogError($"[SaveManager] Cannot save: invalid slot index {slotIndex}. Valid range is 0..{_slotCount - 1}.");
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (!IsGameplayScene(activeScene.name))
        {
            Debug.LogWarning($"[SaveManager] Save skipped: current scene '{activeScene.name}' is not a gameplay scene.");
            return;
        }

        GameSaveData saveData = BuildCurrentGameSaveData(slotIndex, activeScene);

        try
        {
            string filePath = GetSaveFilePath(slotIndex);
            string directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonUtility.ToJson(saveData, true);
            File.WriteAllText(filePath, json, Encoding.UTF8);

            _activeSlotIndex = slotIndex;
            Debug.Log($"[SaveManager] Saved slot {slotIndex} to '{filePath}' (Scene: {saveData.sceneName}, Objective: '{saveData.currentObjective}').");
            OnSlotSaved?.Invoke(slotIndex);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveManager] Failed to write save file for slot {slotIndex}: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves gameplay progress into the currently active slot (useful for Pause Menu buttons or checkpoints).
    /// </summary>
    public void SaveCurrentSlot()
    {
        if (!HasActiveSlot)
        {
            Debug.LogWarning("[SaveManager] No active save slot is currently assigned.");
            return;
        }

        SaveGame(_activeSlotIndex);
    }

    /// <summary>
    /// Loads the saved game from the specified slot index, transitions to the saved scene,
    /// restores all narrative/reputation/objective states, and spawns the player at the saved position.
    /// </summary>
    /// <param name="slotIndex">Zero-based slot index (e.g. 0, 1, 2).</param>
    public void LoadGame(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex))
        {
            Debug.LogError($"[SaveManager] Cannot load: invalid slot index {slotIndex}. Valid range is 0..{_slotCount - 1}.");
            return;
        }

        GameSaveData saveData = ReadSaveFileFromDisk(slotIndex);
        if (saveData == null || !saveData.isOccupied)
        {
            Debug.LogWarning($"[SaveManager] Cannot load slot {slotIndex}: slot is empty or unreadable.");
            return;
        }

        if (_deferredAutoSaveCoroutine != null)
        {
            StopCoroutine(_deferredAutoSaveCoroutine);
            _deferredAutoSaveCoroutine = null;
        }

        Time.timeScale = 1f;
        _activeSlotIndex = slotIndex;
        _isLoadingSavedGame = true;
        _pendingInitialNewGameSave = false;
        _pendingLoadData = saveData;

        RestorePersistentManagersState(saveData);

        string targetScene = !string.IsNullOrWhiteSpace(saveData.sceneName)
            ? saveData.sceneName
            : _newGameStartingScene;

        Debug.Log($"[SaveManager] Loading slot {slotIndex} -> Scene '{targetScene}' at position {saveData.playerPosition.ToVector3()} (+{_spawnHeightOffset}m Y offset).");
        SceneManager.LoadScene(targetScene);
    }

    /// <summary>
    /// Starts a fresh game session assigned to the specified slot index using the default protagonist name.
    /// </summary>
    /// <param name="slotIndex">Zero-based slot index (e.g. 0, 1, 2).</param>
    public void StartNewGame(int slotIndex)
    {
        StartNewGame(slotIndex, PlayerNameService.DefaultPlayerName);
    }

    /// <summary>
    /// Starts a fresh game session assigned to the specified slot index and protagonist name,
    /// and loads the initial gameplay scene.
    /// </summary>
    /// <param name="slotIndex">Zero-based slot index (e.g. 0, 1, 2).</param>
    /// <param name="playerName">Protagonist name chosen by the player.</param>
    public void StartNewGame(int slotIndex, string playerName)
    {
        if (!IsValidSlotIndex(slotIndex))
        {
            Debug.LogError($"[SaveManager] Cannot start new game: invalid slot index {slotIndex}.");
            return;
        }

        if (_deferredAutoSaveCoroutine != null)
        {
            StopCoroutine(_deferredAutoSaveCoroutine);
            _deferredAutoSaveCoroutine = null;
        }

        string resolvedPlayerName = PlayerNameService.SanitizeName(playerName);

        Time.timeScale = 1f;
        _activeSlotIndex = slotIndex;
        _isLoadingSavedGame = false;
        _pendingLoadData = null;
        _pendingInitialNewGameSave = _autoSaveOnNewGameStart;

        ResetAllRuntimeSystemsForNewGame();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetPlayerName(resolvedPlayerName);
        }

        Debug.Log($"[SaveManager] Starting New Game in slot {slotIndex} as '{resolvedPlayerName}' -> Scene '{_newGameStartingScene}'.");
        SceneManager.LoadScene(_newGameStartingScene);
    }

    /// <summary>
    /// Deletes the JSON save file associated with the specified slot index from disk.
    /// </summary>
    /// <param name="slotIndex">Zero-based slot index (e.g. 0, 1, 2).</param>
    public void DeleteSave(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex))
        {
            Debug.LogError($"[SaveManager] Cannot delete save: invalid slot index {slotIndex}.");
            return;
        }

        string filePath = GetSaveFilePath(slotIndex);
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                Debug.Log($"[SaveManager] Deleted save file for slot {slotIndex} ('{filePath}').");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[SaveManager] Error deleting save file for slot {slotIndex}: {ex.Message}");
        }

        if (_activeSlotIndex == slotIndex && !IsGameplayScene(SceneManager.GetActiveScene().name))
        {
            _activeSlotIndex = -1;
        }

        OnSlotDeleted?.Invoke(slotIndex);
    }

    /// <summary>
    /// Returns true if a valid occupied save file exists on disk for the specified slot index.
    /// </summary>
    public bool IsSlotOccupied(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex))
        {
            return false;
        }

        GameSaveData data = ReadSaveFileFromDisk(slotIndex);
        return data != null && data.isOccupied;
    }

    /// <summary>
    /// Reads and returns the lightweight metadata for the specified save slot.
    /// Returns an unoccupied SaveSlotMetadata object if the slot is empty or does not exist.
    /// </summary>
    public SaveSlotMetadata GetSlotMetadata(int slotIndex)
    {
        GameSaveData data = IsValidSlotIndex(slotIndex) ? ReadSaveFileFromDisk(slotIndex) : null;
        if (data != null && data.isOccupied)
        {
            return data.ToMetadata();
        }

        return new SaveSlotMetadata
        {
            slotIndex = slotIndex,
            isOccupied = false,
            saveTimestamp = string.Empty,
            currentObjective = string.Empty,
            sceneName = string.Empty,
            currentDay = 0,
            reputationValue = 0
        };
    }

    /// <summary>
    /// Returns the absolute file path in Application.persistentDataPath for a given slot index.
    /// </summary>
    public string GetSaveFilePath(int slotIndex)
    {
        return Path.Combine(Application.persistentDataPath, $"{_saveFilePrefix}{slotIndex}.json");
    }

    /// <summary>
    /// Checks whether the given slot index lies within [0, SlotCount - 1].
    /// </summary>
    public bool IsValidSlotIndex(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < _slotCount;
    }

    #endregion

    #region Scene Transition & Auto-Save Coordination

    /// <summary>
    /// Notified by GameManager immediately before SceneManager.LoadScene is called.
    /// Saves the active slot before unloading the current gameplay scene when returning to Main Menu.
    /// </summary>
    public void NotifyBeforeSceneTransition(string currentSceneName, string targetSceneName)
    {
        if (_isLoadingSavedGame || !HasActiveSlot || !_autoSaveOnSceneTransitions)
        {
            return;
        }

        if (IsGameplayScene(currentSceneName) && !IsGameplayScene(targetSceneName))
        {
            SaveGame(_activeSlotIndex);
        }
    }

    /// <summary>
    /// Saves the active slot if currently inside a gameplay scene (used before quitting the application).
    /// </summary>
    public void TryAutoSaveBeforeQuit()
    {
        if (_isLoadingSavedGame || !HasActiveSlot || !_autoSaveOnSceneTransitions)
        {
            return;
        }

        if (IsGameplayScene(SceneManager.GetActiveScene().name))
        {
            SaveGame(_activeSlotIndex);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Re-ensure subscription to ObjectivesManager if it spawned with this scene
        if (ObjectivesManager.Instance != null)
        {
            ObjectivesManager.Instance.OnObjectiveUpdated -= HandleObjectiveUpdated;
            ObjectivesManager.Instance.OnObjectiveUpdated += HandleObjectiveUpdated;
        }

        if (!IsGameplayScene(scene.name))
        {
            _isLoadingSavedGame = false;
            _pendingLoadData = null;
            _pendingInitialNewGameSave = false;
            _activeSlotIndex = -1;
            return;
        }

        if (_isLoadingSavedGame && _pendingLoadData != null)
        {
            // Immediately place the player before the first rendered frame, then finalize after Start()
            bool playerAlreadyPositioned = ApplyPlayerSpawnTransform(_pendingLoadData);
            StartCoroutine(FinalizeLoadedSceneNextFrame(_pendingLoadData, playerAlreadyPositioned));
            return;
        }

        if (_pendingInitialNewGameSave && HasActiveSlot)
        {
            _pendingInitialNewGameSave = false;
            StartCoroutine(DelayedAutoSaveFrames(2));
            return;
        }

        if (_autoSaveOnSceneTransitions && HasActiveSlot && !_isLoadingSavedGame)
        {
            StartCoroutine(DelayedAutoSaveFrames(2));
        }
    }

    private void HandleObjectiveUpdated(Objective updatedObjective, int progress)
    {
        if (!_autoSaveOnObjectiveChange || _isLoadingSavedGame || _pendingInitialNewGameSave || !HasActiveSlot)
        {
            return;
        }

        if (!IsGameplayScene(SceneManager.GetActiveScene().name))
        {
            return;
        }

        if (_deferredAutoSaveCoroutine != null)
        {
            StopCoroutine(_deferredAutoSaveCoroutine);
        }
        _deferredAutoSaveCoroutine = StartCoroutine(AutoSaveAtEndOfFrame());
    }

    private IEnumerator AutoSaveAtEndOfFrame()
    {
        yield return new WaitForEndOfFrame();
        _deferredAutoSaveCoroutine = null;

        if (!_isLoadingSavedGame && HasActiveSlot && IsGameplayScene(SceneManager.GetActiveScene().name))
        {
            SaveGame(_activeSlotIndex);
        }
    }

    private IEnumerator DelayedAutoSaveFrames(int frameCount)
    {
        for (int i = 0; i < frameCount; i++)
        {
            yield return null;
        }

        if (!_isLoadingSavedGame && HasActiveSlot && IsGameplayScene(SceneManager.GetActiveScene().name))
        {
            SaveGame(_activeSlotIndex);
        }
    }

    #endregion

    #region Data Gathering & State Restoration

    private GameSaveData BuildCurrentGameSaveData(int slotIndex, Scene activeScene)
    {
        PlayerController player = FindAnyObjectByType<PlayerController>();
        Vector3 playerPos = player != null ? player.transform.position : Vector3.zero;
        float playerRotY = player != null ? player.transform.eulerAngles.y : 0f;

        string objectiveText = string.Empty;
        int objectiveId = -1;
        int objectiveProgress = 0;
        bool isObjectiveActive = true;

        string activePlayerName = PlayerNameService.GetActivePlayerName();

        if (ObjectivesManager.Instance != null)
        {
            objectiveText = PlayerNameService.FormatText(ObjectivesManager.Instance.GetCurrentObjectiveDisplayText(), activePlayerName);
            objectiveId = ObjectivesManager.Instance.GetCurrentObjectiveId();
            objectiveProgress = ObjectivesManager.Instance.GetCurrentProgress();
            isObjectiveActive = ObjectivesManager.Instance.IsCurrentObjectiveActive();
        }

        int currentRep = ReputationManager.Instance != null
            ? ReputationManager.Instance.CurrentReputation
            : 50;

        int currentDay = DayManager.Instance != null
            ? Mathf.Max(1, DayManager.Instance.GetCurrentDay())
            : 1;

        int narrativeRisk = DayManager.Instance != null
            ? DayManager.Instance.NarrativeRiskScore
            : 0;

        bool connectDownload = UIManager.Instance != null && UIManager.Instance.IsConnectDownloadAvailable;
        bool connectInstalled = UIManager.Instance != null && UIManager.Instance.IsConnectInstalled;
        bool connectProfile = UIManager.Instance != null && UIManager.Instance.IsConnectProfileCreated;

        List<ContactNarrativeSaveData> contactsProgress = MessagesManager.Instance != null
            ? MessagesManager.Instance.ExportContactsSaveData()
            : new List<ContactNarrativeSaveData>();

        List<DecisionVariableSaveData> decisionVars = DecisionDataManager.Instance != null
            ? DecisionDataManager.Instance.ExportVariables()
            : new List<DecisionVariableSaveData>();

        GalleryManager galleryManager = GalleryManager.Instance != null
            ? GalleryManager.Instance
            : FindAnyObjectByType<GalleryManager>(FindObjectsInactive.Include);

        List<string> unlockedPhotos = galleryManager != null
            ? galleryManager.ExportUnlockedPhotoPaths()
            : new List<string>();

        List<SceneInteractableSaveData> interactableStates = CaptureSceneInteractables(activeScene);

        return new GameSaveData
        {
            slotIndex = slotIndex,
            saveTimestamp = DateTime.Now.ToString(_timestampFormat),
            isOccupied = true,
            playerName = activePlayerName,
            currentObjective = objectiveText,
            sceneName = activeScene.name,
            sceneBuildIndex = activeScene.buildIndex,
            playerPosition = new SerializableVector3(playerPos),
            playerRotationY = playerRotY,
            reputationValue = currentRep,
            narrativeRiskScore = narrativeRisk,
            currentDay = currentDay,
            currentObjectiveId = objectiveId,
            currentObjectiveProgress = objectiveProgress,
            isCurrentObjectiveActive = isObjectiveActive,
            connectDownloadAvailable = connectDownload,
            connectInstalled = connectInstalled,
            connectProfileCreated = connectProfile,
            contactNarrativeProgress = contactsProgress,
            decisionVariables = decisionVars,
            unlockedGalleryPhotos = unlockedPhotos,
            sceneInteractables = interactableStates
        };
    }

    private void RestorePersistentManagersState(GameSaveData saveData)
    {
        DayManager.ClearQueuedTestStart();

        if (GameManager.Instance != null)
        {
            // Prevent GameManager from firing initial Day 1 advancement when loading into Home
            GameManager.Instance.SetHasEnteredHomeFirstTime(true);
            GameManager.Instance.SetPlayerName(PlayerNameService.SanitizeName(saveData.playerName));
        }

        if (DayManager.Instance != null)
        {
            DayManager.Instance.SetCurrentDay(Mathf.Max(1, saveData.currentDay));
            DayManager.Instance.SetNarrativeRiskScore(saveData.narrativeRiskScore);
        }

        if (DecisionDataManager.Instance != null)
        {
            DecisionDataManager.Instance.RestoreVariables(saveData.decisionVariables);
        }

        var reputationManager = FindAnyObjectByType<ReputationManager>(FindObjectsInactive.Include);
        if (reputationManager != null)
        {
            reputationManager.RestoreReputation(saveData.reputationValue);
        }

        ApplySavedConnectState(saveData);

        if (MessagesManager.Instance != null)
        {
            MessagesManager.Instance.RestoreContactsSaveData(saveData.contactNarrativeProgress);
        }

        GalleryManager galleryManager = GalleryManager.Instance != null
            ? GalleryManager.Instance
            : FindAnyObjectByType<GalleryManager>(FindObjectsInactive.Include);
        if (galleryManager != null)
        {
            galleryManager.RestoreUnlockedPhotos(saveData.unlockedGalleryPhotos);
        }

        if (ObjectivesManager.Instance != null)
        {
            ObjectivesManager.Instance.RestoreObjectiveState(
                saveData.currentObjectiveId,
                saveData.currentObjectiveProgress,
                saveData.isCurrentObjectiveActive);
        }
    }

    private static void ApplySavedConnectState(GameSaveData saveData)
    {
        if (UIManager.Instance == null || saveData == null) return;

        if (saveData.currentDay >= 2 && !saveData.connectInstalled)
        {
            UIManager.Instance.RestoreConnectState(true, true, true);
        }
        else
        {
            UIManager.Instance.RestoreConnectState(
                saveData.connectDownloadAvailable,
                saveData.connectInstalled,
                saveData.connectProfileCreated);
        }
    }

    private void ResetAllRuntimeSystemsForNewGame()
    {
        DayManager.ClearQueuedTestStart();
        TransitionManager.Instance?.ResetCutsceneState();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.SetHasEnteredHomeFirstTime(false);
        }

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

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ResetConnectState();
        }

        if (MessagesManager.Instance != null)
        {
            MessagesManager.Instance.ResetToInitialState();
        }

        if (ObjectivesManager.Instance != null)
        {
            ObjectivesManager.Instance.ResetObjectives();
        }
    }

    private IEnumerator FinalizeLoadedSceneNextFrame(GameSaveData saveData, bool playerAlreadyPositioned)
    {
        // Wait one frame so all scene MonoBehaviours finish Awake() and Start()
        yield return null;

        if (!playerAlreadyPositioned)
        {
            ApplyPlayerSpawnTransform(saveData);
        }

        ApplySavedConnectState(saveData);
        RestoreSceneInteractables(SceneManager.GetActiveScene(), saveData.sceneInteractables);

        // Re-broadcast objective and reputation states so newly initialized HUD elements update
        if (ObjectivesManager.Instance != null)
        {
            ObjectivesManager.Instance.RestoreObjectiveState(
                saveData.currentObjectiveId,
                saveData.currentObjectiveProgress,
                saveData.isCurrentObjectiveActive);
        }

        var reputationManager = FindAnyObjectByType<ReputationManager>(FindObjectsInactive.Include);
        if (reputationManager != null)
        {
            reputationManager.RestoreReputation(saveData.reputationValue);
        }

        int loadedSlot = _activeSlotIndex;
        _isLoadingSavedGame = false;
        _pendingLoadData = null;

        OnGameLoaded?.Invoke(loadedSlot);
    }

    /// <summary>
    /// Repositions the player at the saved 3D coordinates plus _spawnHeightOffset on the Y axis,
    /// resetting physics velocity so gravity settles the character onto the floor without clipping.
    /// Returns true if the PlayerController was found and positioned.
    /// </summary>
    private bool ApplyPlayerSpawnTransform(GameSaveData saveData)
    {
        if (saveData == null) return false;

        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player == null)
        {
            Debug.LogWarning("[SaveManager] PlayerController not found in loaded scene; could not apply saved position.");
            return false;
        }

        Vector3 spawnPosition = saveData.playerPosition.ToVector3() + (Vector3.up * _spawnHeightOffset);
        Quaternion spawnRotation = Quaternion.Euler(0f, saveData.playerRotationY, 0f);

        CharacterController characterController = player.GetComponent<CharacterController>();
        if (characterController != null)
        {
            bool wasEnabled = characterController.enabled;
            characterController.enabled = false;
            player.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            characterController.enabled = wasEnabled;
        }
        else
        {
            player.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        }

        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
            rb.position = spawnPosition;
            rb.rotation = spawnRotation;
        }

        PlayerStateMachine stateMachine = player.GetComponent<PlayerStateMachine>();
        if (stateMachine != null)
        {
            stateMachine.SetPhoneOpen(false);
            stateMachine.ChangeState(PlayerState.Idle);
        }

        Physics.SyncTransforms();
        return true;
    }

    private static List<SceneInteractableSaveData> CaptureSceneInteractables(Scene scene)
    {
        List<SceneInteractableSaveData> list = new List<SceneInteractableSaveData>();
        if (!scene.IsValid() || !scene.isLoaded) return list;

        IInteractable[] interactables = FindObjectsByType<IInteractable>(FindObjectsInactive.Include);
        for (int i = 0; i < interactables.Length; i++)
        {
            IInteractable item = interactables[i];
            if (item == null || item.gameObject.scene != scene) continue;

            list.Add(new SceneInteractableSaveData
            {
                sceneName = scene.name,
                hierarchyPath = GetHierarchyPath(item.transform),
                canInteract = item.CanInteract()
            });
        }

        return list;
    }

    private static void RestoreSceneInteractables(Scene scene, List<SceneInteractableSaveData> savedInteractables)
    {
        if (!scene.IsValid() || !scene.isLoaded || savedInteractables == null || savedInteractables.Count == 0)
        {
            return;
        }

        Dictionary<string, bool> lookup = new Dictionary<string, bool>(StringComparer.Ordinal);
        for (int i = 0; i < savedInteractables.Count; i++)
        {
            SceneInteractableSaveData entry = savedInteractables[i];
            if (string.Equals(entry.sceneName, scene.name, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(entry.hierarchyPath))
            {
                lookup[entry.hierarchyPath] = entry.canInteract;
            }
        }

        if (lookup.Count == 0) return;

        IInteractable[] interactables = FindObjectsByType<IInteractable>(FindObjectsInactive.Include);
        for (int i = 0; i < interactables.Length; i++)
        {
            IInteractable item = interactables[i];
            if (item == null || item.gameObject.scene != scene) continue;

            string path = GetHierarchyPath(item.transform);
            if (lookup.TryGetValue(path, out bool canInteract))
            {
                item.SetCanInteract(canInteract);
            }
        }
    }

    private static string GetHierarchyPath(Transform target)
    {
        if (target == null) return string.Empty;

        StringBuilder sb = new StringBuilder(target.name);
        Transform current = target.parent;
        while (current != null)
        {
            sb.Insert(0, current.name + "/");
            current = current.parent;
        }
        return sb.ToString();
    }

    private GameSaveData ReadSaveFileFromDisk(int slotIndex)
    {
        string filePath = GetSaveFilePath(slotIndex);
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(filePath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return JsonUtility.FromJson<GameSaveData>(json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[SaveManager] Failed to parse save file '{filePath}': {ex.Message}");
            return null;
        }
    }

    private bool IsGameplayScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return false;
        return !string.Equals(sceneName, _mainMenuSceneName, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Inspector Debug Helpers

    [ContextMenu("Debug: Save To Slot 0")]
    private void DebugSaveSlot0() => SaveGame(0);

    [ContextMenu("Debug: Load From Slot 0")]
    private void DebugLoadSlot0() => LoadGame(0);

    [ContextMenu("Debug: Delete Slot 0")]
    private void DebugDeleteSlot0() => DeleteSave(0);

    [ContextMenu("Debug: Log Persistent Data Path")]
    private void DebugLogSaveFolder() => Debug.Log($"[SaveManager] Save folder: {Application.persistentDataPath}");

    #endregion
}
