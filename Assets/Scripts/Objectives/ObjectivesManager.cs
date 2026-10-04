using UnityEngine;
using System.Collections.Generic;
using System;

public class ObjectivesManager : MonoBehaviour
{
    public static ObjectivesManager Instance { get; private set; }

    [SerializeField] private string _objectivesFileName = "objectives.json";

    private List<Objective> _allObjectives;
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
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        _allObjectives = DataLoader.LoadObjectives(_objectivesFileName);
        
        if (_allObjectives != null && _allObjectives.Count > 0)
        {
            SubscribeToCurrentObjective();
        }
        else
        {
            Debug.LogWarning("[ObjectivesManager] No objectives loaded.");
        }
    }

    private void SubscribeToCurrentObjective()
    {
        if (_currentObjectiveIndex < _allObjectives.Count)
        {
            Objective current = _allObjectives[_currentObjectiveIndex];
            _currentProgress = 0;
            
            // Subscribe to the event required to progress this objective
            if (!string.IsNullOrEmpty(current.eventToListen))
            {
                EventBus.Instance.Subscribe(current.eventToListen, OnObjectiveProgressed);
            }
            
            OnObjectiveUpdated?.Invoke(current, _currentProgress);
        }
        else
        {
            Debug.Log("[ObjectivesManager] All objectives completed!");
            OnObjectiveUpdated?.Invoke(null, 0);
        }
    }

    private void OnObjectiveProgressed()
    {
        Objective current = _allObjectives[_currentObjectiveIndex];
        _currentProgress++;
        
        Debug.Log($"[ObjectivesManager] Progressed objective '{current.id}': {_currentProgress}/{current.targetCount}");

        OnObjectiveUpdated?.Invoke(current, _currentProgress);

        if (_currentProgress >= current.targetCount)
        {
            // Unsubscribe from the current event since it's completed
            if (!string.IsNullOrEmpty(current.eventToListen))
            {
                EventBus.Instance.Unsubscribe(current.eventToListen, OnObjectiveProgressed);
            }
            
            // Publish completion event if any
            if (!string.IsNullOrEmpty(current.eventOnComplete))
            {
                EventBus.Instance.Publish(current.eventOnComplete);
            }

            // Move to next objective
            _currentObjectiveIndex++;
            SubscribeToCurrentObjective();
        }
    }
    
    /// <summary>
    /// Forces the system to change the current objective. Useful when changing scenes or jumping sections.
    /// </summary>
    public void ForceSetObjective(string objectiveId)
    {
        int index = _allObjectives.FindIndex(o => o.id == objectiveId);
        if (index != -1)
        {
            if (_currentObjectiveIndex < _allObjectives.Count)
            {
                string oldEvent = _allObjectives[_currentObjectiveIndex].eventToListen;
                if (!string.IsNullOrEmpty(oldEvent))
                {
                    EventBus.Instance.Unsubscribe(oldEvent, OnObjectiveProgressed);
                }
            }
            
            _currentObjectiveIndex = index;
            SubscribeToCurrentObjective();
        }
        else
        {
            Debug.LogWarning($"[ObjectivesManager] Objective '{objectiveId}' not found!");
        }
    }

    private void ForceSetObjectiveEvent(string objectiveId)
    {
        ForceSetObjective(objectiveId);
    }

    private void OnEnable()
    {
        EventBus.Instance.Subscribe("ForceSetObjective", (Action<string>)ForceSetObjectiveEvent);
    }

    private void OnDisable()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe("ForceSetObjective", (Action<string>)ForceSetObjectiveEvent);
        }
    }
}
