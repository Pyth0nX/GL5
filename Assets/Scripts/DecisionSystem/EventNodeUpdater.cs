using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct ContactNodeUpdate
{
    public string contactName;
    public int newNarrativeNodeId;

    [Tooltip("Optional: If conditions are added, this node jump only applies when all conditions are met (e.g. Reputation GreaterThan 60).")]
    public List<Condition> conditions;
}

[System.Serializable]
public struct NodeUpdateRule
{
    [Tooltip("The event name to listen for (e.g. 'Day2_Start').")]
    public string triggerEventName;

    [Tooltip("0 = any day. Otherwise this rule only runs on the specified story day.")]
    public int requiredDay;
    
    [Tooltip("The list of contacts and the new narrative nodes they should be set to.")]
    public List<ContactNodeUpdate> contactUpdates;
}

public class EventNodeUpdater : MonoBehaviour
{
    [SerializeField] private List<NodeUpdateRule> _updateRules;

    private readonly List<KeyValuePair<string, System.Action>> _subscriptions = new List<KeyValuePair<string, System.Action>>();

    private void Start()
    {
        if (_updateRules != null)
        {
            foreach (var rule in _updateRules)
            {
                if (!string.IsNullOrEmpty(rule.triggerEventName))
                {
                    // Local copy for the lambda closure
                    var ruleCopy = rule;
                    System.Action action = () => ApplyUpdates(ruleCopy);
                    
                    _subscriptions.Add(new KeyValuePair<string, System.Action>(rule.triggerEventName, action));
                    EventBus.Instance.Subscribe(rule.triggerEventName, action);
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (EventBus.Instance != null)
        {
            foreach (var kvp in _subscriptions)
            {
                EventBus.Instance.Unsubscribe(kvp.Key, kvp.Value);
            }
            _subscriptions.Clear();
        }
    }

    private void ApplyUpdates(NodeUpdateRule rule)
    {
        if (rule.requiredDay > 0)
        {
            int currentDay = DayManager.Instance != null ? DayManager.Instance.GetCurrentDay() : 1;
            if (currentDay != rule.requiredDay)
            {
                return;
            }
        }

        if (rule.contactUpdates == null)
        {
            return;
        }

        Debug.Log($"[EventNodeUpdater] Event '{rule.triggerEventName}' triggered on Day {rule.requiredDay}. Evaluating {rule.contactUpdates.Count} contact updates.");
        
        if (MessagesManager.Instance == null)
        {
            Debug.LogError("[EventNodeUpdater] MessagesManager.Instance is null!");
            return;
        }

        foreach (var update in rule.contactUpdates)
        {
            if (ConditionsChecker.AreConditionsMet(update.conditions))
            {
                MessagesManager.Instance.AdvanceContactNarrativeNodeByEvent(update.contactName, update.newNarrativeNodeId);
            }
        }
    }
}
