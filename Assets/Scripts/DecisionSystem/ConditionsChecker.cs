using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public enum ComparisonOperator
{
    Equal = 0,              // Equal to (==)
    NotEqual = 1,           // Not equal to (!=)
    GreaterThan = 2,        // Greater than (>)
    LessThan = 3,           // Less than (<)
    GreaterThanOrEqual = 4, // Greater than or equal to (>=)
    LessThanOrEqual = 5,    // Less than or equal to (<=)
    Between = 6             // Between [value, maxValue] inclusive
}

[Serializable]
public enum ConditionTargetType
{
    ReputationValue = 0, // Evaluates the numeric Reputation / Safety value
    ReputationTier = 1,  // Evaluates the current qualitative category (VeryLow, Low, Neutral, High, VeryHigh)
    DecisionVariable = 2 // Evaluates an integer variable by name in DecisionDataManager
}

[Serializable]
public struct Condition
{
    [Tooltip("Which global state to evaluate: numeric Reputation value, Reputation tier, or a general DecisionVariable.")]
    public ConditionTargetType targetType;

    [Tooltip("Variable in DecisionDataManager (used when targetType == DecisionVariable).")]
    public DecisionVariable decisionStat;

    [Tooltip("Logical comparison operator.")]
    public ComparisonOperator comparisonOperator;

    [Tooltip("Target comparison value (or minimum inclusive limit if using 'Between').")]
    public int value;

    [Tooltip("Maximum inclusive limit when comparisonOperator is 'Between'.")]
    public int maxValue;

    [Tooltip("Target category when targetType is 'ReputationTier'.")]
    public ReputationTier targetTier;
}

[Serializable]
public struct EventTrigger
{
    public UnityEvent eventToTrigger;

    [Tooltip("Execution priority (higher value executes earlier; equal priority preserves list order).")]
    public int priority;
}

[Serializable]
public class EventCondition
{
    [Tooltip("Optional descriptive name to identify this rule in the Inspector.")]
    public string description;

    // List of conditions that need to be met for the event to trigger
    [Header("List of conditions to trigger the event")]
    public List<Condition> possibleConditions = new List<Condition>();

    // Event to trigger if all conditions are met
    [Header("List of triggered events in case all conditions met at once")]
    public List<EventTrigger> eventsToTrigger = new List<EventTrigger>();

    [Tooltip("If true, re-triggers whenever conditions are evaluated and satisfied.")]
    public bool repeatable = false;

    // Flag to check if the event has already been triggered
    [HideInInspector]
    public bool alreadyTriggered = false;
}

/// <summary>
/// Central condition evaluator for the decision and reputation system.
/// Works both as a scene component (firing EventConditions reactively or on demand)
/// and as a static engine reusable from NarrativeNode, Option, EventNodeUpdater, and ReputationWorldTrigger.
/// </summary>
public class ConditionsChecker : MonoBehaviour
{
    [Header("Evaluation Settings")]
    [Tooltip("Automatically evaluates conditions upon scene start (Start).")]
    [SerializeField] private bool _checkOnStart = false;

    [Tooltip("Automatically evaluates conditions whenever Reputation or a DecisionVariable changes.")]
    [SerializeField] private bool _autoCheckOnStateChange = true;

    // List of event conditions to check
    [SerializeField]
    private List<EventCondition> eventConditions = new List<EventCondition>();

    private void Start()
    {
        if (_checkOnStart)
        {
            CheckConditions();
        }
    }

    private void OnEnable()
    {
        if (_autoCheckOnStateChange)
        {
            if (ReputationManager.Instance != null)
            {
                ReputationManager.Instance.OnReputationChanged += HandleReputationChanged;
            }
            if (DecisionDataManager.Instance != null)
            {
                DecisionDataManager.Instance.OnVariableChanged += HandleVariableChanged;
            }
        }
    }

    private void OnDisable()
    {
        // Use FindAnyObjectByType without forcing instantiation during scene shutdown
        var repMgr = FindAnyObjectByType<ReputationManager>(FindObjectsInactive.Include);
        if (repMgr != null)
        {
            repMgr.OnReputationChanged -= HandleReputationChanged;
        }

        var dataMgr = FindAnyObjectByType<DecisionDataManager>(FindObjectsInactive.Include);
        if (dataMgr != null)
        {
            dataMgr.OnVariableChanged -= HandleVariableChanged;
        }
    }

    private void HandleReputationChanged(int _)
    {
        if (_autoCheckOnStateChange)
        {
            CheckConditions();
        }
    }

    private void HandleVariableChanged(string _, object __)
    {
        if (_autoCheckOnStateChange)
        {
            CheckConditions();
        }
    }

    /// <summary>
    /// Evaluates all EventConditions configured on this component and executes their UnityEvents ordered by priority.
    /// </summary>
    public void CheckConditions()
    {
        if (eventConditions == null) return;

        foreach (var eventCondition in eventConditions)
        {
            if (eventCondition == null) continue;
            if (!eventCondition.repeatable && eventCondition.alreadyTriggered) continue;

            // In scene rules, require at least 1 configured condition to prevent unintended empty triggers
            if (eventCondition.possibleConditions != null &&
                eventCondition.possibleConditions.Count > 0 &&
                AreAllConditionsMet(eventCondition.possibleConditions))
            {
                InvokeOrderedTriggers(eventCondition.eventsToTrigger);
                eventCondition.alreadyTriggered = true;
            }
        }
    }

    /// <summary>
    /// Resets the alreadyTriggered flag for all rules on this component.
    /// </summary>
    public void ResetTriggeredStates()
    {
        if (eventConditions == null) return;
        foreach (var ec in eventConditions)
        {
            if (ec != null) ec.alreadyTriggered = false;
        }
    }

    private static void InvokeOrderedTriggers(List<EventTrigger> triggers)
    {
        if (triggers == null || triggers.Count == 0) return;

        // Copy ordered by descending priority (higher priority first) preserving relative order
        List<EventTrigger> ordered = new List<EventTrigger>(triggers);
        ordered.Sort((a, b) => b.priority.CompareTo(a.priority));

        foreach (var trigger in ordered)
        {
            trigger.eventToTrigger?.Invoke();
        }
    }

    public bool AreAllConditionsMet(List<Condition> conditions)
    {
        return AreConditionsMet(conditions);
    }

    public bool IsConditionMet(int decisionValue, ComparisonOperator comparisonOperator, int value, int maxValue = 0)
    {
        return IsValueComparisonMet(decisionValue, comparisonOperator, value, maxValue);
    }

    #region Static Evaluation Engine (Shared by NarrativeNodes, Options & World Triggers)

    /// <summary>
    /// Evaluates a list of conditions (logical AND). Returns true if the list is null or empty.
    /// </summary>
    public static bool AreConditionsMet(IList<Condition> conditions)
    {
        if (conditions == null || conditions.Count == 0)
        {
            return true;
        }

        for (int i = 0; i < conditions.Count; i++)
        {
            if (!EvaluateCondition(conditions[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Evaluates a single condition against the current state of ReputationManager or DecisionDataManager.
    /// </summary>
    public static bool EvaluateCondition(Condition condition)
    {
        switch (condition.targetType)
        {
            case ConditionTargetType.ReputationValue:
            {
                int currentRep = ReputationManager.Instance != null
                    ? ReputationManager.Instance.CurrentReputation
                    : 0;
                return IsValueComparisonMet(currentRep, condition.comparisonOperator, condition.value, condition.maxValue);
            }

            case ConditionTargetType.ReputationTier:
            {
                int currentTierInt = ReputationManager.Instance != null
                    ? (int)ReputationManager.Instance.CurrentTier
                    : (int)ReputationTier.Neutral;

                if (condition.comparisonOperator == ComparisonOperator.Between)
                {
                    return IsValueComparisonMet(currentTierInt, ComparisonOperator.Between, condition.value, condition.maxValue);
                }

                return IsValueComparisonMet(currentTierInt, condition.comparisonOperator, (int)condition.targetTier);
            }

            case ConditionTargetType.DecisionVariable:
            default:
            {
                string varName = condition.decisionStat.name;
                if (string.IsNullOrWhiteSpace(varName) ||
                    string.Equals(varName, ReputationManager.DefaultDecisionVariableName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(varName, "Seguridad", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(varName, "Safety", StringComparison.OrdinalIgnoreCase))
                {
                    int currentRep = ReputationManager.Instance != null
                        ? ReputationManager.Instance.CurrentReputation
                        : 0;
                    return IsValueComparisonMet(currentRep, condition.comparisonOperator, condition.value, condition.maxValue);
                }

                int variableValue = DecisionDataManager.Instance != null
                    ? DecisionDataManager.Instance.GetIntVariable(varName, 0)
                    : 0;

                return IsValueComparisonMet(variableValue, condition.comparisonOperator, condition.value, condition.maxValue);
            }
        }
    }

    /// <summary>
    /// Performs mathematical comparison (including inclusive range checks with Between).
    /// </summary>
    public static bool IsValueComparisonMet(int currentValue, ComparisonOperator comparisonOperator, int value, int maxValue = 0)
    {
        switch (comparisonOperator)
        {
            case ComparisonOperator.Equal:
                return currentValue == value;
            case ComparisonOperator.NotEqual:
                return currentValue != value;
            case ComparisonOperator.GreaterThan:
                return currentValue > value;
            case ComparisonOperator.LessThan:
                return currentValue < value;
            case ComparisonOperator.GreaterThanOrEqual:
                return currentValue >= value;
            case ComparisonOperator.LessThanOrEqual:
                return currentValue <= value;
            case ComparisonOperator.Between:
            {
                int min = Mathf.Min(value, maxValue);
                int max = Mathf.Max(value, maxValue);
                return currentValue >= min && currentValue <= max;
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(comparisonOperator), comparisonOperator, null);
        }
    }

    #endregion
}
