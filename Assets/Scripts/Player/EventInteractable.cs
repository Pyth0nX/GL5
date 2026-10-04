using UnityEngine;

public class EventInteractable : MonoBehaviour, IInteractable
{
    [Tooltip("The name of the event to publish when this object is interacted with. (e.g. 'BedInteracted')")]
    [SerializeField] private string _eventNameToPublish;

    [Tooltip("If true, the player state won't be stuck in 'Interacting' because we immediately revert it (useful for instant actions like turning on a light).")]
    [SerializeField] private bool _instantAction = true;

    public void Interact()
    {
        Debug.Log($"Interacting with EventInteractable. Publishing event: {_eventNameToPublish}");
        
        if (!string.IsNullOrEmpty(_eventNameToPublish))
        {
            EventBus.Instance.Publish(_eventNameToPublish);
        }

        if (_instantAction)
        {
            // The PlayerController sets the state to Interacting automatically after calling this.
            // If it's an instant action (no dialogue), we should tell the player to revert to Idle.
            PlayerStateMachine playerState = FindAnyObjectByType<PlayerStateMachine>();
            if (playerState != null)
            {
                // We delay by 1 frame or just set it back immediately. 
                // Actually, the PlayerController sets it AFTER calling this method.
                // So we can use a small Coroutine or just let the user handle state reset via another script.
                // For safety, let's just use an Invoke to reset it next frame.
                Invoke(nameof(RevertPlayerState), 0.1f);
            }
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
