using System;
using UnityEngine;
using System.Collections.Generic;

[Serializable]
public class Objective
{
    [SerializeField]
    private int id; // Unique identifier for the objective
    [SerializeField]
    private int nextObjectiveId = -1; // Unique identifier for the objective
    [SerializeField]
    private string description; // The text shown in the UI
    [SerializeField]
    private bool isActive = true; // if the objective is active from the begining
    [SerializeField]
    private int targetCount; // Number of times the event needs to happen
    [SerializeField]
    private string eventToListen; // Event name to listen for progress
    [SerializeField]
    private string eventToActivate; // Event name to listen for to activate the objective
    [SerializeField]
    private string eventOnComplete; // Event name to fire when completed

    public bool IsActive() { return isActive; }

    public bool Activate() { return isActive = true; }

    public int Id() { return id; }
    public int NextObjective() { return nextObjectiveId; }
    public int TargetCount() { return targetCount; }
    public string Description() { return description; }
    public string EventToListen() { return eventToListen; }
    public string EventToActivate() { return eventToActivate; }
    public string EventOnComplete() { return eventOnComplete; }
}

[Serializable]
public class ObjectiveListWrapper
{
    public List<Objective> objectives;
}
