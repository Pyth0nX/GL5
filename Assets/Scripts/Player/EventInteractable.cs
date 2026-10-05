using UnityEngine;
using System.Collections.Generic;

public class EventInteractable : MonoBehaviour, IInteractable
{
    [Tooltip("The names of the events to publish when this object is interacted with. (e.g. 'BedInteracted')")]
    [SerializeField] private List<string> _eventsToPublish = new List<string>();

    [Tooltip("If true, the player state won't be stuck in 'Interacting' because we immediately revert it (useful for instant actions like turning on a light).")]
    [SerializeField] private bool _instantAction = true;

    [Header("Conditions")]
    [Tooltip("Can this object be interacted with right now?")]
    [SerializeField] private bool _canInteract = true;

    [Tooltip("Optional: Events that enable this interactable (e.g. Day1_End, Day2_End).")]
    [SerializeField] private List<string> _enableEvents = new List<string>();

    [Tooltip("Optional: Events that disable this interactable (e.g. Start_Day2, Start_Day3).")]
    [SerializeField] private List<string> _disableEvents = new List<string>();

    [Tooltip("Optional: Events to publish if interacted when disabled (e.g. 'Show_NotTired_Message').")]
    [SerializeField] private List<string> _disabledEventsToPublish = new List<string>();

    private void Start()
    {
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

    public void Interact()
    {
        if (!_canInteract)
        {
            Debug.Log($"Interaction disabled for {gameObject.name}.");
            if (_disabledEventsToPublish != null)
            {
                foreach (var ev in _disabledEventsToPublish)
                {
                    if (!string.IsNullOrEmpty(ev))
                    {
                        EventBus.Instance.Publish(ev);
                    }
                }
            }
            
            if (_instantAction) Invoke(nameof(RevertPlayerState), 0.1f);
            return;
        }

        Debug.Log($"Interacting with EventInteractable. Publishing events.");
        
        if (_eventsToPublish != null)
        {
            foreach (var ev in _eventsToPublish)
            {
                if (!string.IsNullOrEmpty(ev))
                {
                    EventBus.Instance.Publish(ev);
                }
            }
        }

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
}
