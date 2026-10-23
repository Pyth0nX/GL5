using System;
using System.Collections.Generic;
using UnityEngine;

public enum EventDataType
{
    NONE,       // For events without additional data
    STRING,
    INT,
    FLOAT,
    BOOL

}

[System.Serializable]
public class EventPayload
{
    [SerializeField] private string _eventName;
    [SerializeField] private EventDataType _dataType = EventDataType.NONE;

    [SerializeField] private string _stringValue;
    [SerializeField] private int _intValue;
    [SerializeField] private float _floatValue;
    [SerializeField] private bool _boolValue;

    public string EventName() { return _eventName; }
    public EventDataType DataType() { return _dataType; }

    public object GetValue()
    {
        switch (_dataType)
        {
            case EventDataType.NONE:
                return null;
            case EventDataType.STRING:
                return _stringValue;
            case EventDataType.INT:
                return _intValue;
            case EventDataType.FLOAT:
                return _floatValue;
            case EventDataType.BOOL:
                return _boolValue;
            default:
                return null;
        }
    }
}


public class EventInteractable : IInteractable
{
    [Tooltip("The names of the events to publish when this object is interacted with.")]
    [SerializeField] private List<EventPayload> _eventsToPublish = new List<EventPayload>();

    [Tooltip("If true, the player state won't be stuck in 'Interacting' because we immediately revert it (useful for instant actions like turning on a light).")]
    [SerializeField] private bool _instantAction = true;

    [Header("Conditions")]

    [Tooltip("Events that enable this interactable.")]
    [SerializeField] private List<string> _enableEvents = new List<string>();

    [Tooltip("Events that disable this interactable.")]
    [SerializeField] private List<string> _disableEvents = new List<string>();

    [Tooltip("Events to publish if interacted when disabled.")]
    [SerializeField] private List<EventPayload> _disabledEventsToPublish = new List<EventPayload>();



    protected override void Start()
    {
        base.Start();

        if (_enableEvents != null)
        {
            foreach (var ev in _enableEvents)
            {
                if (!string.IsNullOrEmpty(ev)) EventBus.Instance.Subscribe(ev, EnableInteraction);
            }
        }
        
        if (_disableEvents != null)
        {
            foreach (var ev in _disableEvents)
            {
                if (!string.IsNullOrEmpty(ev)) EventBus.Instance.Subscribe(ev, DisableInteraction);
            }
        }
    }

    private void OnDestroy()
    {
        SetOutlineActive(false);

        if (EventBus.Instance != null)
        {
            if (_enableEvents != null)
            {
                foreach (var ev in _enableEvents)
                {
                    if (!string.IsNullOrEmpty(ev)) EventBus.Instance.Unsubscribe(ev, EnableInteraction);
                }
            }
            
            if (_disableEvents != null)
            {
                foreach (var ev in _disableEvents)
                {
                    if (!string.IsNullOrEmpty(ev)) EventBus.Instance.Unsubscribe(ev, DisableInteraction);
                }
            }
        }
    }

    public void EnableInteraction() => _canInteract = true;
    public void DisableInteraction() => _canInteract = false;

    public override void Interact()
    {
        
        if (!_canInteract)
        {
            PublishEventList(_disabledEventsToPublish);

            // Show Not Available message in dark red for 2 seconds
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowTemporaryMessage(_notAvailableMessage, Color.darkRed, 2f);
            }

            if (_instantAction) Invoke(nameof(RevertPlayerState), 0.1f);
            return;
        }

        PublishEventList(_eventsToPublish);


        if (_instantAction)
        {
            Invoke(nameof(RevertPlayerState), 0.1f);
        }
    }

    private void RevertPlayerState()
    {
        PlayerStateMachine playerState = FindAnyObjectByType<PlayerStateMachine>();
        if (playerState != null && playerState.GetCurrentState() == PlayerState.Interacting)
        {
            playerState.ChangeState(PlayerState.Idle);
        }
    }

    private void PublishEventList(List<EventPayload> eventList)
    {
        if (eventList == null || EventBus.Instance == null) return;

        foreach (var item in eventList)
        {
            if (string.IsNullOrEmpty(item.EventName())) continue;

            switch (item.DataType())
            {
                case EventDataType.NONE:
                    EventBus.Instance.Publish(item.EventName());
                    break;

                case EventDataType.STRING:
                    EventBus.Instance.Publish<string>(item.EventName(), (string)item.GetValue());
                    break;

                case EventDataType.INT:
                    EventBus.Instance.Publish<int>(item.EventName(), (int)item.GetValue());
                    break;

                case EventDataType.FLOAT:
                    EventBus.Instance.Publish<float>(item.EventName(), (float)item.GetValue());
                    break;

                case EventDataType.BOOL:
                    EventBus.Instance.Publish<bool>(item.EventName(), (bool)item.GetValue());
                    break;
            }
        }
    }
}
