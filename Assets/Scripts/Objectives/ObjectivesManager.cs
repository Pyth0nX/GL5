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

    private const int Day1GoToSchoolObjectiveId = 2;
    private const string Day1TutorialItemsCompleteEvent = "D1_TutorialItemsPicked";

    [SerializeField]
    private Dictionary<int, Objective> _objectives = new Dictionary<int, Objective>();
    private readonly Dictionary<string, int> _dynamicTargetCountOverrides =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private Objective _currentObjective;
    private int _currentProgress = 0;
    private string _subscribedListenEvent;
    private string _subscribedActivateEvent;
    private bool _hasSubscribedInitialObjective;

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
                    if (!string.IsNullOrWhiteSpace(objective.EventToListen()) &&
                        _dynamicTargetCountOverrides.TryGetValue(objective.EventToListen().Trim(), out int overrideCount))
                    {
                        objective.SetTargetCount(overrideCount);
                    }

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

    private void UnsubscribeFromCurrentObjective()
    {
        if (EventBus.Instance != null)
        {
            if (!string.IsNullOrEmpty(_subscribedListenEvent))
            {
                EventBus.Instance.Unsubscribe(_subscribedListenEvent, OnObjectiveProgressed);
            }

            if (!string.IsNullOrEmpty(_subscribedActivateEvent))
            {
                EventBus.Instance.Unsubscribe(_subscribedActivateEvent, OnObjectiveActivated);
            }
        }

        _subscribedListenEvent = null;
        _subscribedActivateEvent = null;
    }

    private void BindCurrentObjectiveEvents()
    {
        UnsubscribeFromCurrentObjective();

        if (_currentObjective == null || EventBus.Instance == null)
        {
            return;
        }

        string listenEvent = _currentObjective.EventToListen();
        if (!string.IsNullOrEmpty(listenEvent))
        {
            EventBus.Instance.Subscribe(listenEvent, OnObjectiveProgressed);
            _subscribedListenEvent = listenEvent;
        }

        string activateEvent = _currentObjective.EventToActivate();
        if (!string.IsNullOrEmpty(activateEvent))
        {
            EventBus.Instance.Subscribe(activateEvent, OnObjectiveActivated);
            _subscribedActivateEvent = activateEvent;
        }
    }

    /// <summary>
    /// Updates the target count for any objective listening to the given event (e.g. scalable pickup items in a scene)
    /// and preserves the override across Save/Load reloads.
    /// </summary>
    public void SyncObjectiveTargetCount(string listenEvent, int requiredCount)
    {
        if (string.IsNullOrWhiteSpace(listenEvent))
        {
            return;
        }

        string normalizedEvent = listenEvent.Trim();
        int clamped = Mathf.Max(1, requiredCount);
        _dynamicTargetCountOverrides[normalizedEvent] = clamped;

        if (_objectives != null)
        {
            foreach (Objective objective in _objectives.Values)
            {
                if (objective != null &&
                    string.Equals(objective.EventToListen(), normalizedEvent, StringComparison.OrdinalIgnoreCase))
                {
                    objective.SetTargetCount(clamped);
                }
            }
        }

        if (_currentObjective != null &&
            string.Equals(_currentObjective.EventToListen(), normalizedEvent, StringComparison.OrdinalIgnoreCase))
        {
            _currentObjective.SetTargetCount(clamped);
            _currentProgress = Mathf.Clamp(_currentProgress, 0, clamped);
            OnObjectiveUpdated?.Invoke(_currentObjective, _currentProgress);
        }
    }

    /// <summary>
    /// Resets all objectives back to their initial definitions from JSON (used when starting a New Game).
    /// </summary>
    public void ResetObjectives()
    {
        UnsubscribeFromCurrentObjective();
        _dynamicTargetCountOverrides.Clear();
        _currentProgress = 0;
        CreateObjectives();

        if (_currentObjective != null && EventBus.Instance != null)
        {
            SubscribeToCurrentObjective();
        }
        else
        {
            _hasSubscribedInitialObjective = true;
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
            BindCurrentObjectiveEvents();
        }
        else
        {
            _currentProgress = 0;
        }

        _hasSubscribedInitialObjective = true;
        OnObjectiveUpdated?.Invoke(_currentObjective, _currentProgress);
    }

    private void SubscribeToCurrentObjective()
    {
        _currentProgress = 0;
        _hasSubscribedInitialObjective = true;
        BindCurrentObjectiveEvents();

        OnObjectiveUpdated?.Invoke(_currentObjective, _currentProgress);
    }

    /// <summary>
    /// Skips the Day 1 morning tutorial objectives immediately and unlocks leaving for school (used for debug/testing).
    /// </summary>
    public void CompleteDay1ExploreTimerImmediately()
    {
        if (_currentObjective != null &&
            (_currentObjective.Id() == 0 || _currentObjective.Id() == 1) &&
            EventBus.Instance != null)
        {
            EventBus.Instance.Publish(Day1TutorialItemsCompleteEvent);
            ForceSetObjective(Day1GoToSchoolObjectiveId);
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
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe("ForceSetObjective", (Action<int>)ForceSetObjectiveEvent);
            EventBus.Instance.Subscribe("ForceSetObjective", (Action<int>)ForceSetObjectiveEvent);
        }

        if (!_hasSubscribedInitialObjective)
        {
            if (_currentObjective != null)
            {
                SubscribeToCurrentObjective();
            }
            else
            {
                Debug.LogWarning("[ObjectivesManager] No objectives loaded.");
            }
        }
    }

    private void OnDestroy()
    {
        UnsubscribeFromCurrentObjective();

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe("ForceSetObjective", (Action<int>)ForceSetObjectiveEvent);
        }
    }
}
