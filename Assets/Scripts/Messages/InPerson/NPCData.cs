using UnityEngine;

public class NPCData : MonoBehaviour, IInteractable
{
    [SerializeField]
    private string _characterName; // Name of the character (this need to be exactly the same as stated in the JSON)

    [Header("Conditions")]
    [Tooltip("Can this NPC be interacted with right now?")]
    [SerializeField] private bool _canInteract = true;

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
            Debug.Log($"Interaction disabled for NPC: {_characterName}");
            // Revert player state just in case it got stuck
            PlayerStateMachine playerState = FindAnyObjectByType<PlayerStateMachine>();
            if (playerState != null && playerState.GetCurrentState() == PlayerState.Interacting)
            {
                playerState.ChangeState(PlayerState.Idle);
            }
            return;
        }

        Debug.Log($"Interacting with NPC: {_characterName}");
        MessagesManager.Instance.SetCurrentContact(_characterName);
        DialogueManager.Instance.StartDialogue();
    }
}
