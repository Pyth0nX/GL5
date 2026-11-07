using UnityEngine;

public class NPCData : IInteractable
{
    [SerializeField]
    private string _characterName; // Name of the character (must match contacts.json)

    [Tooltip("Optional ambient line shown without entering the story conversation system.")]
    [SerializeField, TextArea] private string _standaloneDialogue;

    [Header("Conditions")]

    [Tooltip("Optional: Events that enable this NPC (e.g. Day1_LockerEncounterEnd).")]
    [SerializeField] private System.Collections.Generic.List<string> _enableEvents;

    [Tooltip("Optional: Events that disable this NPC.")]
    [SerializeField] private System.Collections.Generic.List<string> _disableEvents;

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

        if (string.IsNullOrEmpty(_interactMessage))
        {
            _interactMessage = "Talk to " + _characterName;
        }

        if (string.IsNullOrEmpty(_notAvailableMessage))
        {
            _notAvailableMessage = "Can't talk now";
        }

        if (string.IsNullOrWhiteSpace(_standaloneDialogue) &&
            _disableEvents != null &&
            _disableEvents.Contains("SchoolEncounterComplete") &&
            ObjectivesManager.Instance != null &&
            ObjectivesManager.Instance.GetCurrentObjective() != null)
        {
            bool awaitingSchoolEncounter =
                ObjectivesManager.Instance.GetCurrentObjective().EventToListen() == "SchoolEncounterComplete";
            if (!awaitingSchoolEncounter)
            {
                _canInteract = false;
                UpdateHoveredOutlineColor();
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

    public void EnableInteraction()
    {
        _canInteract = true;
        UpdateHoveredOutlineColor();
    }

    public void DisableInteraction()
    {
        _canInteract = false;
        UpdateHoveredOutlineColor();
    }

    public override void Interact()
    {
        if (!_canInteract)
        {
            Debug.Log("[NPC]: Interaction unavailable");
            if (UIManager.Instance != null)
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

        Debug.Log("[NPC]: Interacted");

        if (!string.IsNullOrWhiteSpace(_standaloneDialogue))
        {
            // Ambient NPCs must not replace the active narrative contact.
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ChangeState((int)UIState.NONE);
            }

            bool opened = DialogueManager.Instance != null &&
                          DialogueManager.Instance.ShowStandaloneLine(
                              _characterName,
                              _standaloneDialogue,
                              EndStandaloneInteraction);

            if (!opened)
            {
                EndStandaloneInteraction();
            }

            return;
        }

        MessagesManager.Instance.SetCurrentContact(_characterName);
        UIManager.Instance.ChangeState(4);
    }

    private void EndStandaloneInteraction()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ChangeState((int)UIState.DEFAULT);
        }

        PlayerStateMachine playerState = FindAnyObjectByType<PlayerStateMachine>();
        if (playerState != null)
        {
            playerState.ChangeState(PlayerState.Idle);
        }
    }
}
