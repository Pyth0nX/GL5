using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;
using System;

public enum ComparisonOperator
{
    Equal,
    NotEqual,
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual
}


[System.Serializable]
public struct Condition
{
    public DecisionVariable decisionStat;
    public ComparisonOperator comparisonOperator;
    public int value;
}


[System.Serializable]
public struct EventTrigger
{
    public UnityEvent eventToTrigger;

    public int priority; // Priority to determine the order of triggering events
}

[System.Serializable]
public class EventCondition
{
    // List of conditions that need to be met for the event to trigger
    [Header("List of conditions to trigger the event")]
    public List<Condition> possibleConditions = new List<Condition>();

    // Event to trigger if all conditions are met
    [Header("List of triggered events in case all conditions met at once")]
    public List<EventTrigger> eventsToTrigger = new List<EventTrigger>();

    // Flag to check if the event has already been triggered
    [HideInInspector]
    public bool alreadyTriggered = false; 
}

public class ConditionsChecker : MonoBehaviour
{
    //List of event conditions to check
    [SerializeField]
    private List<EventCondition> eventConditions = new List<EventCondition>();

    // Update is called once per frame
    void Update()
    {
        
    }

    public void CheckConditions()
    {
        foreach (var eventCondition in eventConditions)
        {
            if (!eventCondition.alreadyTriggered && AreAllConditionsMet(eventCondition.possibleConditions))
            {
                foreach (var events in eventCondition.eventsToTrigger)
                {
                    events.eventToTrigger.Invoke();
                }
                eventCondition.alreadyTriggered = true; // Mark the event as triggered
            }
        }
    }

    bool AreAllConditionsMet(List<Condition> conditions)
    {
        foreach (var condition in conditions)
        {
            return false; // If any condition is not met, return false
            //int decisionValue = DecisionManager.Instance.GetDecisionValue(condition.decisionStat);
            //if (!IsConditionMet(decisionValue, condition.comparisonOperator, condition.value))
            //{
            //}
        }
        return true; // All conditions are met
    }

    bool IsConditionMet(int decisionValue, ComparisonOperator comparisonOperator, int value)
    {
        return comparisonOperator switch
        {
            ComparisonOperator.Equal => decisionValue == value,
            ComparisonOperator.NotEqual => decisionValue != value,
            ComparisonOperator.GreaterThan => decisionValue > value,
            ComparisonOperator.LessThan => decisionValue < value,
            ComparisonOperator.GreaterThanOrEqual => decisionValue >= value,
            ComparisonOperator.LessThanOrEqual => decisionValue <= value,
            _ => throw new ArgumentOutOfRangeException(nameof(comparisonOperator), comparisonOperator, null)
        };
    }

}
