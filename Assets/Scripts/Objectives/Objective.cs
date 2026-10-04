using System;
using System.Collections.Generic;

[Serializable]
public class Objective
{
    public string id; // Unique identifier for the objective
    public string description; // The text shown in the UI
    public int targetCount; // Number of times the event needs to happen
    public string eventToListen; // Event name to listen for progress
    public string eventOnComplete; // Event name to fire when completed
}

[Serializable]
public class ObjectiveListWrapper
{
    public List<Objective> objectives;
}
