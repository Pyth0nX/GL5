using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

public class ObjectivesManager : MonoBehaviour
{
    public static ObjectivesManager Instance { get; private set; }

    [SerializeField] private string _objectivesFileName = "objectives.json";

    [Header("Day 1 - Scene 1")]
    [Tooltip("How long Luna can explore her bedroom before the objective changes to Go to school.")]
    [SerializeField, Min(0f)] private float _day1BedroomExploreDuration = 30f;

    private const int Day1BedroomExploreObjectiveId = 0;
    private const string Day1BedroomExploreCompleteEvent = "D1_ExploreTimerComplete";

    [SerializeField]
    private Dictionary<int, Objective> _objectives = new Dictionary<int, Objective>();
    private Objective _currentObjective;
    private int _currentProgress = 0;
    private Coroutine _day1BedroomExploreTimer;

    /// <summary>
    /// Triggered when the current objective changes or its progress updates.
    /// Passes the current objective and the current progress count.
    /// If all objectives are completed, the objective will be null.
    /// </summary>
    public event Action<Objective, int> OnObjectiveUpdated;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            CreateObjectives();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void CreateObjectives()
    {
        if (_objectives == null)
        {
            _objectives = new Dictionary<int, Objective>();
        }
        else
        {
            _objectives.Clear();
        }

        // Load objectives early so UI can access them in Start
        List<Objective> allObjectives = DataLoader.LoadObjectives(_objectivesFileName);
        if (allObjectives != null)
        {
            foreach (Objective objective in allObjectives)
            {
                if (objective != null)
                {
                    _objectives[objective.Id()] = objective;
                }
            }
        }

        _currentObjective = _objectives.ContainsKey(0)
            ? _objectives[0]
            : _objectives.Values.FirstOrDefault();

        if (_currentObjective == null) Debug.LogWarning("[ObjectivesManager] No objectives loaded.");
    }

    public Objective GetCurrentObjective()
    {
        return _currentObjective;
    }

    public Objective GetObjectiveById(int objectiveId)
    {
        if (_objectives != null && _objectives.TryGetValue(objectiveId, out Objective objective))
        {
            return objective;
        }
        return null;
    }
    
    public int GetCurrentProgress()
    {
        return _currentProgress;
    }

    /// <summary>
    /// Returns the active objective ID, or -1 if no objective is currently assigned.
    /// </summary>
    public int GetCurrentObjectiveId()
    {
        return _currentObjective != null ? _currentObjective.Id() : -1;
    }

    /// <summary>
    /// Returns whether the current objective is marked active for HUD display.
    /// </summary>
    public bool IsCurrentObjectiveActive()
    {
        return _currentObjective != null && _currentObjective.IsActive();
    }

    /// <summary>
    /// Formats the active objective text (including progress counters such as "(1/3)") for UI and save metadata.
    /// </summary>
    public string GetCurrentObjectiveDisplayText()
    {
        if (_currentObjective == null)
        {
            return string.Empty;
        }

        string desc = _currentObjective.Description() ?? string.Empty;
        if (_currentObjective.TargetCount() > 1)
        {
            return $"{desc} ({_currentProgress}/{_currentObjective.TargetCount()})";
        }

        return desc;
    }

    private void StopTimedObjective()
    {
        if (_day1BedroomExploreTimer != null)
        {
            StopCoroutine(_day1BedroomExploreTimer);
            _day1BedroomExploreTimer = null;
        }
    }

    private void UnsubscribeFromCurrentObjective()
    {
        StopTimedObjective();

        if (_currentObjective == null || EventBus.Instance == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(_currentObjective.EventToListen()))
        {
            EventBus.Instance.Unsubscribe(_currentObjective.EventToListen(), OnObjectiveProgressed);
        }

        if (!string.IsNullOrEmpty(_currentObjective.EventToActivate()))
        {
            EventBus.Instance.Unsubscribe(_currentObjective.EventToActivate(), OnObjectiveActivated);
        }
    }

    /// <summary>
    /// Resets all objectives back to their initial definitions from JSON (used when starting a New Game).
    /// </summary>
    public void ResetObjectives()
    {
        UnsubscribeFromCurrentObjective();
        _currentProgress = 0;
        CreateObjectives();

        if (_currentObjective != null && EventBus.Instance != null)
        {
            SubscribeToCurrentObjective();
        }
        else
        {
            OnObjectiveUpdated?.Invoke(_currentObjective, _currentProgress);
        }
    }

    /// <summary>
    /// Restores the exact objective ID, progress count, and activation flag from a saved game.
    /// </summary>
    public void RestoreObjectiveState(int objectiveId, int progress, bool isActive)
    {
        UnsubscribeFromCurrentObjective();
        CreateObjectives();

        if (objectiveId >= 0 && _objectives.TryGetValue(objectiveId, out Objective targetObjective) && targetObjective != null)
        {
            _currentObjective = targetObjective;
        }
        else if (_objectives.ContainsKey(0))
        {
            _currentObjective = _objectives[0];
        }
        else
        {
            _currentObjective = null;
        }

        if (_currentObjective != null)
        {
            _currentObjective.SetActiveState(isActive);
            _currentProgress = Mathf.Clamp(progress, 0, Mathf.Max(0, _currentObjective.TargetCount()));

            if (EventBus.Instance != null)
            {
                if (!string.IsNullOrEmpty(_currentObjective.EventToListen()))
                {
                    EventBus.Instance.Subscribe(_currentObjective.EventToListen(), OnObjectiveProgressed);
                }

                if (!string.IsNullOrEmpty(_currentObjective.EventToActivate()))
                {
                    EventBus.Instance.Subscribe(_currentObjective.EventToActivate(), OnObjectiveActivated);
                }
            }
        }
        else
        {
            _currentProgress = 0;
        }

        OnObjectiveUpdated?.Invoke(_currentObjective, _currentProgress);
        UpdateTimedObjective();
    }

    private void SubscribeToCurrentObjective()
    {
        _currentProgress = 0;
            
        // Subscribe to the event required to progress this objective
        if (!string.IsNullOrEmpty(_currentObjective.EventToListen()))
        {
            EventBus.Instance.Subscribe(_currentObjective.EventToListen(), OnObjectiveProgressed);
        }

        // Subscribe to the event required to activate this objective
        if (!string.IsNullOrEmpty(_currentObjective.EventToActivate()))
        {
            EventBus.Instance.Subscribe(_currentObjective.EventToActivate(), OnObjectiveActivated);
        }
            
        OnObjectiveUpdated?.Invoke(_currentObjective, _currentProgress);
        UpdateTimedObjective();
    }

    private void UpdateTimedObjective()
    {
        StopTimedObjective();

        if (_currentObjective != null && _currentObjective.Id() == Day1BedroomExploreObjectiveId)
        {
            _day1BedroomExploreTimer = StartCoroutine(Day1BedroomExploreRoutine());
        }
    }

    private IEnumerator Day1BedroomExploreRoutine()
    {
        // Give scene Start methods one frame before checking transition and scene state
        yield return null;

        float elapsed = 0f;
        while (elapsed < _day1BedroomExploreDuration)
        {
            bool inHomeScene = string.Equals(SceneManager.GetActiveScene().name, "Home", StringComparison.OrdinalIgnoreCase);
            bool transitioning = TransitionManager.Instance != null && TransitionManager.Instance.IsTransitioning;

            if (inHomeScene && !transitioning)
            {
                elapsed += Time.deltaTime;
            }

            yield return null;
        }

        _day1BedroomExploreTimer = null;

        if (_currentObjective != null &&
            _currentObjective.Id() == Day1BedroomExploreObjectiveId &&
            EventBus.Instance != null)
        {
            EventBus.Instance.Publish(Day1BedroomExploreCompleteEvent);
        }
    }

    /// <summary>
    /// Completes the Day 1 bedroom exploration timer immediately (used for debug/testing).
    /// </summary>
    public void CompleteDay1ExploreTimerImmediately()
    {
        if (_currentObjective != null &&
            _currentObjective.Id() == Day1BedroomExploreObjectiveId &&
            EventBus.Instance != null)
        {
            StopTimedObjective();
            EventBus.Instance.Publish(Day1BedroomExploreCompleteEvent);
        }
    }

    private void OnObjectiveProgressed()
    {
        if (_currentObjective == null) return;

        // If the objective is not active already
        if (!_currentObjective.IsActive()) return;
        _currentProgress++;
        
        Debug.Log($"[ObjectivesManager] Progressed objective '{_currentObjective.Id()}': {_currentProgress}/{_currentObjective.TargetCount()}");

        OnObjectiveUpdated?.Invoke(_currentObjective, _currentProgress);

        if (_currentProgress >= _currentObjective.TargetCount())
        {
            // Unsubscribe from the current event since it's completed
            UnsubscribeFromCurrentObjective();
            
            // Publish completion event if any
            if (!string.IsNullOrEmpty(_currentObjective.EventOnComplete()))
            {
                EventBus.Instance.Publish(_currentObjective.EventOnComplete());
            }

            int nextId = (_currentObjective.NextObjective() != -1)
                ? _currentObjective.NextObjective()
                : _currentObjective.Id() + 1;

            if (_objectives.TryGetValue(nextId, out Objective nextObj))
            {
                _currentObjective = nextObj;
                SubscribeToCurrentObjective();
            }
            else
            {
                _currentObjective = null;
                _currentProgress = 0;
                OnObjectiveUpdated?.Invoke(null, 0);
            }
        }
    }

    private void OnObjectiveActivated()
    {
        if (_currentObjective == null) return;
        _currentObjective.Activate();
        OnObjectiveUpdated?.Invoke(_currentObjective, _currentProgress);
    }

    /// <summary>
    /// Forces the system to change the current objective. Useful when changing scenes or jumping sections.
    /// </summary>
    public void ForceSetObjective(int objectiveId)
    {
        if (objectiveId != -1 && _objectives != null && _objectives.ContainsKey(objectiveId))
        {
            UnsubscribeFromCurrentObjective();
            
            _currentObjective = _objectives[objectiveId];
            SubscribeToCurrentObjective();
        }
        else
        {
            Debug.LogWarning($"[ObjectivesManager] Objective '{objectiveId}' not found!");
        }
    }

    private void ForceSetObjectiveEvent(int objectiveId)
    {
        ForceSetObjective(objectiveId);
    }

    private void Start()
    {
        EventBus.Instance.Subscribe("ForceSetObjective", (Action<int>)ForceSetObjectiveEvent);
        
        if (_currentObjective != null)
        {
            SubscribeToCurrentObjective();
        }
        else
        {
            Debug.LogWarning("[ObjectivesManager] No objectives loaded.");
        }
    }

    private void OnDestroy()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe("ForceSetObjective", (Action<int>)ForceSetObjectiveEvent);
        }
    }
}
