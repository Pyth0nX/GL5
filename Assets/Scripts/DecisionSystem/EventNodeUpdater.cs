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
    
    [Tooltip("The list of contacts and the new narrative nodes they should be set to.")]
    public List<ContactNodeUpdate> contactUpdates;
}

public class EventNodeUpdater : MonoBehaviour
{
    [SerializeField] private List<NodeUpdateRule> _updateRules;

    private Dictionary<string, System.Action> _subscriptions = new Dictionary<string, System.Action>();

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
                    
                    _subscriptions[rule.triggerEventName] = action;
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
        Debug.Log($"[EventNodeUpdater] Event '{rule.triggerEventName}' triggered. Evaluating {rule.contactUpdates.Count} contact updates.");
        
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
