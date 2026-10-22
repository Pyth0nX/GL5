using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;

public class ObjectivesManager : MonoBehaviour
{
    public static ObjectivesManager Instance { get; private set; }

    [SerializeField] private string _objectivesFileName = "objectives.json";

    [SerializeField]
    private Dictionary<int, Objective> _objectives;
    private Objective _currentObjective;
    private int _currentObjectiveIndex = 0;
    private int _currentProgress = 0;

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
        // Load objectives early so UI can access them in Start
        List<Objective> allObjectives = DataLoader.LoadObjectives(_objectivesFileName);
        foreach(Objective objective in allObjectives)
        {
            _objectives.Add(objective.Id(),objective);
        }
        _currentObjective = _objectives[0];

        if(_currentObjective == null) Debug.LogWarning("[ObjectivesManager] No objectives loaded.");
    }

    public Objective GetCurrentObjective()
    {
        return _currentObjective;
    }
    
    public int GetCurrentProgress()
    {
        return _currentProgress;
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
    }

    private void OnObjectiveProgressed()
    {
        // If the objective is not active already
        if (!_currentObjective.IsActive()) return;
        _currentProgress++;
        
        Debug.Log($"[ObjectivesManager] Progressed objective '{_currentObjective.Id()}': {_currentProgress}/{_currentObjective.TargetCount()}");

        OnObjectiveUpdated?.Invoke(_currentObjective, _currentProgress);

        if (_currentProgress >= _currentObjective.TargetCount())
        {
            // Unsubscribe from the current event since it's completed
            if (!string.IsNullOrEmpty(_currentObjective.EventToListen()))
            {
                EventBus.Instance.Unsubscribe(_currentObjective.EventToListen(), OnObjectiveProgressed);
            }
            
            // Publish completion event if any
            if (!string.IsNullOrEmpty(_currentObjective.EventOnComplete()))
            {
                EventBus.Instance.Publish(_currentObjective.EventOnComplete());
            }

            _currentObjective = (_currentObjective.NextObjective() != -1) ? 
                                _objectives[_currentObjective.NextObjective()] : 
                                _objectives[_currentObjective.Id() + 1];

            SubscribeToCurrentObjective();
        }
    }

    private void OnObjectiveActivated()
    {
        _currentObjective.Activate();
        OnObjectiveUpdated?.Invoke(_currentObjective, _currentProgress);
    }

    /// <summary>
    /// Forces the system to change the current objective. Useful when changing scenes or jumping sections.
    /// </summary>
    public void ForceSetObjective(int objectiveId)
    {
        if (objectiveId != -1)
        {
            if (_currentObjective != null)
            {
                EventBus.Instance.Unsubscribe(_currentObjective.EventToListen(), OnObjectiveProgressed);
            }
            
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
