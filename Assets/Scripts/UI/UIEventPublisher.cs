using UnityEngine;

/// <summary>
/// Attach this script to a UI Button or any Canvas object.
/// You can then link the PublishEvent method in the Button's OnClick() 
/// UnityEvent in the Inspector and type the string name of the event.
public class UIEventPublisher : MonoBehaviour
{
    [Header("Dynamic Event Settings")]
    [Tooltip("The event that this button will publish by default if you use PublishConfiguredEvent()")]
    [SerializeField] private string _currentEventToPublish;

    [System.Serializable]
    public struct EventChanger
    {
        [Tooltip("When this event is heard on the EventBus...")]
        public string eventToListen;
        [Tooltip("...Change the button so it now publishes THIS event instead.")]
        public string newEventToPublish;
    }

    [Tooltip("List of rules to change what this button does dynamically based on the story.")]
    [SerializeField] private System.Collections.Generic.List<EventChanger> _eventChangers;

    private void Start()
    {
        if (_eventChangers != null)
        {
            foreach (var changer in _eventChangers)
            {
                if (!string.IsNullOrEmpty(changer.eventToListen))
                {
                    // Create a local copy to avoid closure issues in the lambda
                    string newEvent = changer.newEventToPublish;
                    EventBus.Instance.Subscribe(changer.eventToListen, () => ChangeEventToPublish(newEvent));
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (EventBus.Instance != null && _eventChangers != null)
        {
            // We unsubscribe using the lambda signature, but to be completely safe from memory leaks 
            // with anonymous methods, it's better to just clear the dictionary or rely on the object being destroyed.
            // Since EventBus holds a static reference, we MUST unsubscribe correctly.
            // A better way is to use a dedicated method, but since this is a simple script, 
            // we will use a dictionary to track actions for clean unsubscription.
        }
    }
    
    // Tracking subscriptions for clean cleanup
    private System.Collections.Generic.Dictionary<string, System.Action> _subscriptions = new System.Collections.Generic.Dictionary<string, System.Action>();

    private void OnEnable()
    {
        if (_eventChangers != null && EventBus.Instance != null)
        {
            foreach (var changer in _eventChangers)
            {
                if (!string.IsNullOrEmpty(changer.eventToListen))
                {
                    string newEvent = changer.newEventToPublish;
                    System.Action action = () => ChangeEventToPublish(newEvent);
                    _subscriptions[changer.eventToListen] = action;
                    EventBus.Instance.Subscribe(changer.eventToListen, action);
                }
            }
        }
    }

    private void OnDisable()
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

    private void ChangeEventToPublish(string newEvent)
    {
        _currentEventToPublish = newEvent;
        Debug.Log($"[UIEventPublisher] {gameObject.name} button will now publish: {newEvent}");
    }

    /// <summary>
    /// Call this from OnClick (without parameters) to publish whatever event is currently configured.
    /// </summary>
    public void PublishConfiguredEvent()
    {
        if (!string.IsNullOrEmpty(_currentEventToPublish))
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.Publish(_currentEventToPublish);
                Debug.Log($"[UIEventPublisher] Button published event: {_currentEventToPublish}");
            }
        }
    }

    /// <summary>
    /// Call this to force a specific event (ignores the dynamic configuration).
    /// </summary>
    public void PublishEvent(string eventName)
    {
        if (!string.IsNullOrEmpty(eventName))
        {
            if (EventBus.Instance != null)
            {
                EventBus.Instance.Publish(eventName);
                Debug.Log($"[UIEventPublisher] Button force-published event: {eventName}");
            }
        }
    }
}
