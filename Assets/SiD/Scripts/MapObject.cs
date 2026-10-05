using UnityEngine;
using UnityEngine.SceneManagement;

public class MapObject : MonoBehaviour , IInteractable
{
    [SerializeField]
    private GameObject _map;

    [Header("Conditions")]
    [Tooltip("Can this door/object change scenes right now?")]
    [SerializeField] private bool _canInteract = true;

    [Tooltip("Optional: Events that enable this door (e.g. Day3_ReadyToLeave).")]
    [SerializeField] private System.Collections.Generic.List<string> _enableEvents;

    [Tooltip("Optional: Events that disable this door.")]
    [SerializeField] private System.Collections.Generic.List<string> _disableEvents;
    
    [Tooltip("Optional: Event to publish if interacted when locked (e.g. 'Show_DoorLocked_Text').")]
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
            Debug.Log($"Scene change disabled for {gameObject.name}.");
            if (!string.IsNullOrEmpty(_disabledEventToPublish))
            {
                EventBus.Instance.Publish(_disabledEventToPublish);
            }
            
            // Revert player state just in case it got stuck
            PlayerStateMachine playerState = FindAnyObjectByType<PlayerStateMachine>();
            if (playerState != null && playerState.GetCurrentState() == PlayerState.Interacting)
            {
                playerState.ChangeState(PlayerState.Idle);
            }
            return;
        }

        _map.SetActive(true);

        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            player.UnlockMouse();
        }
    }
}