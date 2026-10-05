using UnityEngine;

public class EventInteractable : MonoBehaviour, IInteractable
{
    [Tooltip("The name of the event to publish when this object is interacted with. (e.g. 'BedInteracted')")]
    [SerializeField] private string _eventNameToPublish;

    [Tooltip("If true, the player state won't be stuck in 'Interacting' because we immediately revert it (useful for instant actions like turning on a light).")]
    [SerializeField] private bool _instantAction = true;

    [Header("Conditions")]
    [Tooltip("Can this object be interacted with right now?")]
    [SerializeField] private bool _canInteract = true;

    [Tooltip("Optional: Events that enable this interactable (e.g. Day1_End, Day2_End).")]
    [SerializeField] private System.Collections.Generic.List<string> _enableEvents;

    [Tooltip("Optional: Events that disable this interactable (e.g. Start_Day2, Start_Day3).")]
    [SerializeField] private System.Collections.Generic.List<string> _disableEvents;

    [Tooltip("Optional: Event to publish if interacted when disabled (e.g. 'Show_NotTired_Message').")]
    [SerializeField] private string _disabledEventToPublish;

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
            if (!string.IsNullOrEmpty(_disabledEventToPublish))
            {
                EventBus.Instance.Publish(_disabledEventToPublish);
            }
            
            if (_instantAction) Invoke(nameof(RevertPlayerState), 0.1f);
            return;
        }

        Debug.Log($"Interacting with EventInteractable. Publishing event: {_eventNameToPublish}");
        
        if (!string.IsNullOrEmpty(_eventNameToPublish))
        {
            EventBus.Instance.Publish(_eventNameToPublish);
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
