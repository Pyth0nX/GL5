using UnityEngine;

public class ClassDesk : IInteractable
{
    [Header("Legacy Test Scene")]
    public SchoolQuestManager schoolQuest;

    private const string EncounterFinished = "SchoolEncounterComplete";
    private const string ClassFinished = "SchoolClassFinished";

    protected override void Start()
    {
        base.Start();

        _canInteract = false;
        if (string.IsNullOrEmpty(_interactMessage))
        {
            _interactMessage = "Sit at your desk and attend class";
        }

        if (string.IsNullOrEmpty(_notAvailableMessage))
        {
            _notAvailableMessage = "Deal with Emma and Sophie first";
        }

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe(EncounterFinished, EnableDesk);
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
            EventBus.Instance.Unsubscribe(EncounterFinished, EnableDesk);
        }
    }

    private void SyncWithCurrentObjective(Objective objective, int currentProgress)
    {
        if (objective == null) return;
        SetCanInteract(objective.EventToListen() == ClassFinished);
    }

    private void EnableDesk()
    {
        SetCanInteract(true);
    }

    public override void Interact()
    {
        if (!_canInteract)
        {
            if (UIManager.Instance != null && !string.IsNullOrEmpty(_notAvailableMessage))
            {
                UIManager.Instance.ShowTemporaryMessage(
                    _notAvailableMessage,
                    UIManager.UnavailableInteractionColor,
                    2f);
            }
            ReturnPlayerToIdle();
            return;
        }

        SetCanInteract(false);
        string completionEvent = ClassFinished;

        if (TransitionManager.Instance != null)
        {
            TransitionManager.Instance.PlaySchoolClass(completionEvent);
            return;
        }

        if (schoolQuest != null)
        {
            schoolQuest.StartClass();
        }

        EventBus.Instance?.Publish(completionEvent);
        ReturnPlayerToIdle();
    }

    private static void ReturnPlayerToIdle()
    {
        PlayerStateMachine playerState = FindAnyObjectByType<PlayerStateMachine>();
        if (playerState != null)
        {
            playerState.ChangeState(PlayerState.Idle);
        }
    }
}
