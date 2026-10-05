using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct ContactNodeUpdate
{
    public string contactName;
    public int newNarrativeNodeId;
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
        Debug.Log($"[EventNodeUpdater] Event '{rule.triggerEventName}' triggered. Updating {rule.contactUpdates.Count} contacts.");
        
        foreach (var update in rule.contactUpdates)
        {
            var contact = MessagesManager.Instance.GetContactByName(update.contactName);
            if (contact != null)
            {
                contact.SetNarrativeNode(update.newNarrativeNodeId);
                contact.SetHasNewMessages(true);
                Debug.Log($"[EventNodeUpdater] Updated {update.contactName} to node {update.newNarrativeNodeId}.");
            }
            else
            {
                Debug.LogWarning($"[EventNodeUpdater] Contact '{update.contactName}' not found!");
            }
        }
    }
}
