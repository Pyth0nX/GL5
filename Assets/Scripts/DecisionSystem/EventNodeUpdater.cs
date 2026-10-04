using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct ContactNodeUpdate
{
    public string contactName;
    public int newNarrativeNodeId;
}

public class EventNodeUpdater : MonoBehaviour
{
    [Tooltip("The event name to listen for (e.g. 'BedInteracted' or 'ArrivedAtSchool').")]
    [SerializeField] private string _triggerEventName;

    [Tooltip("The list of contacts and the new narrative nodes they should be set to.")]
    [SerializeField] private List<ContactNodeUpdate> _contactUpdates;

    private void Start()
    {
        if (!string.IsNullOrEmpty(_triggerEventName))
        {
            EventBus.Instance.Subscribe(_triggerEventName, ApplyUpdates);
        }
    }

    private void OnDestroy()
    {
        if (!string.IsNullOrEmpty(_triggerEventName) && EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe(_triggerEventName, ApplyUpdates);
        }
    }

    private void ApplyUpdates()
    {
        Debug.Log($"[EventNodeUpdater] Event '{_triggerEventName}' triggered. Updating { _contactUpdates.Count } contacts.");
        
        foreach (var update in _contactUpdates)
        {
            var contact = MessagesManager.Instance.GetContactByName(update.contactName);
            if (contact != null)
            {
                contact.SetNarrativeNode(update.newNarrativeNodeId);
                Debug.Log($"[EventNodeUpdater] Updated {update.contactName} to node {update.newNarrativeNodeId}.");
            }
            else
            {
                Debug.LogWarning($"[EventNodeUpdater] Contact '{update.contactName}' not found!");
            }
        }
    }
}
