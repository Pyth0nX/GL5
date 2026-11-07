using UnityEngine;

public class MapObject : IInteractable
{
    [Header("Conditions")]

    [Tooltip("Optional: Events that enable this door (e.g. Day3_ReadyToLeave).")]
    [SerializeField] private System.Collections.Generic.List<string> _enableEvents;

    [Tooltip("Optional: Events that disable this door.")]
    [SerializeField] private System.Collections.Generic.List<string> _disableEvents;

    [Tooltip("Optional: Event to publish if interacted when locked (e.g. 'Show_DoorLocked_Text').")]
    [SerializeField] private string _disabledEventToPublish;

    [Header("Direct Travel")]

    [Tooltip("Open the shared travel map instead of loading a scene immediately.")]
    [SerializeField] private bool _openTravelMap;

    [Tooltip("Optional: Load this scene directly instead of opening the travel map.")]
    [SerializeField] private string _directScene;

    [Tooltip("Optional: Event to publish immediately before direct travel.")]
    [SerializeField] private string _eventBeforeSceneChange;

    protected override void Start()
    {
        base.Start();

        if (EventBus.Instance != null)
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

        if (ObjectivesManager.Instance != null)
        {
            ObjectivesManager.Instance.OnObjectiveUpdated += SyncWithCurrentObjective;
            SyncWithCurrentObjective(
                ObjectivesManager.Instance.GetCurrentObjective(),
                ObjectivesManager.Instance.GetCurrentProgress());
        }
    }

    private void OnDestroy()
    {
        SetOutlineActive(false);

        if (ObjectivesManager.Instance != null)
        {
            ObjectivesManager.Instance.OnObjectiveUpdated -= SyncWithCurrentObjective;
        }

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

    private bool IsSchoolTravelExitDoor()
    {
        return _openTravelMap &&
               _enableEvents != null &&
               _enableEvents.Contains("SchoolClassFinished");
    }

    private void SyncWithCurrentObjective(Objective objective, int currentProgress)
    {
        if (!IsSchoolTravelExitDoor() || objective == null) return;

        bool canLeaveSchoolNow = objective.EventToListen() == "Day1_ComebackHome";
        _canInteract = canLeaveSchoolNow;
        UpdateHoveredOutlineColor();
    }

    public void EnableInteraction()
    {
        if (IsSchoolTravelExitDoor() && ObjectivesManager.Instance != null)
        {
            StartCoroutine(SyncSchoolExitNextFrame());
            return;
        }

        _canInteract = true;
        UpdateHoveredOutlineColor();
    }

    private System.Collections.IEnumerator SyncSchoolExitNextFrame()
    {
        yield return null;
        if (ObjectivesManager.Instance != null)
        {
            SyncWithCurrentObjective(
                ObjectivesManager.Instance.GetCurrentObjective(),
                ObjectivesManager.Instance.GetCurrentProgress());
        }
    }

    public void DisableInteraction()
    {
        _canInteract = false;
        UpdateHoveredOutlineColor();
    }

    public override void Interact()
    {
        // Door hierarchies may carry MapObject components on both the parent door and child panels.
        // Delegate to a configured parent door when hit via a child panel collider.
        MapObject parentDoor = transform.parent == null
            ? null
            : transform.parent.GetComponentInParent<MapObject>();

        if (parentDoor != null && parentDoor != this &&
            (parentDoor._openTravelMap || !string.IsNullOrEmpty(parentDoor._directScene)))
        {
            parentDoor.Interact();
            return;
        }

        if (!_canInteract)
        {
            Debug.Log($"[MapObject] Scene change disabled for {gameObject.name}.");
            if (!string.IsNullOrEmpty(_disabledEventToPublish) && EventBus.Instance != null)
            {
                EventBus.Instance.Publish(_disabledEventToPublish);
            }

            if (UIManager.Instance != null && !string.IsNullOrEmpty(_notAvailableMessage))
            {
                UIManager.Instance.ShowTemporaryMessage(
                    _notAvailableMessage,
                    UIManager.UnavailableInteractionColor,
                    2f);
            }

            PlayerStateMachine playerState = FindAnyObjectByType<PlayerStateMachine>();
            if (playerState != null && playerState.GetCurrentState() == PlayerState.Interacting)
            {
                playerState.ChangeState(PlayerState.Idle);
            }
            return;
        }

        if (_openTravelMap)
        {
            UIManager.Instance.ShowMap();
            return;
        }

        if (!string.IsNullOrEmpty(_directScene))
        {
            if (!string.IsNullOrEmpty(_eventBeforeSceneChange) && EventBus.Instance != null)
            {
                EventBus.Instance.Publish(_eventBeforeSceneChange);
            }

            GameManager.Instance.ChangeScene(_directScene);
            return;
        }

        UIManager.Instance.ChangeState(2);
    }
}
