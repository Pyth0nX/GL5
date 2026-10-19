using UnityEngine;

public class NPCData : IInteractable
{
    [SerializeField]
    private string _characterName; // Name of the character (this need to be exactly the same as stated in the JSON)

    [Header("Conditions")]

    [Tooltip("Optional: Events that enable this NPC (e.g. Day1_LockerEncounterEnd).")]
    [SerializeField] private System.Collections.Generic.List<string> _enableEvents;

    [Tooltip("Optional: Events that disable this NPC.")]
    [SerializeField] private System.Collections.Generic.List<string> _disableEvents;

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

        _interactMessage = "Talk to " + _characterName;
        _notAvailableMessage = "Can't talk now";
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

    public override void Interact()
    {
        if (!_canInteract)
        {
            Debug.Log("[NPC]: Interacted failed");
            // Revert player state just in case it got stuck
            PlayerStateMachine playerState = FindAnyObjectByType<PlayerStateMachine>();
            if (playerState != null && playerState.GetCurrentState() == PlayerState.Interacting)
            {
                playerState.ChangeState(PlayerState.Idle);
            }
            return;
        }

        Debug.Log("[NPC]: Interacted");

        MessagesManager.Instance.SetCurrentContact(_characterName);
        UIManager.Instance.ChangeState(4);
    }
}
