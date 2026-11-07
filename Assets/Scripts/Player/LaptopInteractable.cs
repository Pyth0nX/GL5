using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class LaptopInteractable : IInteractable
{
    private const int CloseLaptopChoice = 0;
    private const int PlayAllDayChoice = 1;

    [Header("Laptop Preview")]
    [Tooltip("How long the temporary Laptop message remains on the black screen.")]
    [SerializeField, Min(0f)] private float _laptopScreenDuration = 2f;

    [Header("Future Day-Skip Prototype")]
    [Tooltip("Disabled by default. Enable to expose the neutral laptop day-skip choice.")]
    [SerializeField] private bool _enableDaySkipPrototype;

    private sealed class ContactState
    {
        public readonly string ContactName;
        public readonly int CompletedNodeId;

        public ContactState(string contactName, int completedNodeId)
        {
            ContactName = contactName;
            CompletedNodeId = completedNodeId;
        }
    }

    private sealed class DaySkipPreset
    {
        public readonly int SleepObjectiveId;
        public readonly string DayCompletionEvent;
        public readonly string[] NeutralChoiceEvents;
        public readonly ContactState[] CompletedContacts;

        public DaySkipPreset(
            int sleepObjectiveId,
            string dayCompletionEvent,
            string[] neutralChoiceEvents,
            ContactState[] completedContacts)
        {
            SleepObjectiveId = sleepObjectiveId;
            DayCompletionEvent = dayCompletionEvent;
            NeutralChoiceEvents = neutralChoiceEvents;
            CompletedContacts = completedContacts;
        }
    }

    protected override void Awake()
    {
        if (string.IsNullOrWhiteSpace(_interactMessage))
        {
            _interactMessage = "Use laptop";
        }

        if (string.IsNullOrWhiteSpace(_notAvailableMessage))
        {
            _notAvailableMessage = "Laptop is unavailable";
        }

        base.Awake();
    }

    public override void Interact()
    {
        if (!_canInteract)
        {
            ReturnPlayerToIdle();
            return;
        }

        if (!_enableDaySkipPrototype)
        {
            if (TransitionManager.Instance != null)
            {
                TransitionManager.Instance.PlayMessageCard("<b>Laptop</b>", _laptopScreenDuration);
            }
            else
            {
                ReturnPlayerToIdle();
            }

            return;
        }

        if (UIManager.Instance == null)
        {
            ReturnPlayerToIdle();
            return;
        }

        bool opened = UIManager.Instance.ShowSimpleChoices(
            "<b>Laptop</b>\nWhat should Luna do?",
            "Close the Laptop",
            "Play Video Games All Day",
            HandleLaptopChoice);

        if (!opened)
        {
            ReturnPlayerToIdle();
        }
    }

    private void HandleLaptopChoice(int choiceIndex)
    {
        if (choiceIndex == PlayAllDayChoice)
        {
            SkipCurrentDay();
        }
        else if (choiceIndex != CloseLaptopChoice)
        {
            Debug.LogWarning($"[LaptopInteractable] Unknown laptop choice index {choiceIndex}.");
        }

        ReturnPlayerToIdle();
    }

    private void SkipCurrentDay()
    {
        if (DayManager.Instance == null || EventBus.Instance == null)
        {
            UIManager.Instance?.ShowTemporaryMessage("The day system is not ready.", Color.red, 2.5f);
            return;
        }

        int currentDay = DayManager.Instance.GetCurrentDay();
        if (!TryGetSkipPreset(currentDay, out DaySkipPreset preset))
        {
            UIManager.Instance?.ShowTemporaryMessage(
                "Day 5 is the final playable day. There is no later day to skip to.",
                Color.white,
                3.5f);
            return;
        }

        if (!DayManager.Instance.TryMarkDaySkipped(currentDay))
        {
            UIManager.Instance?.ShowTemporaryMessage(
                $"Day {currentDay} is already skipped. Go to sleep to continue.",
                Color.white,
                3f);
            return;
        }

        foreach (string choiceEvent in preset.NeutralChoiceEvents)
        {
            EventBus.Instance.Publish(choiceEvent);
        }

        if (MessagesManager.Instance != null)
        {
            foreach (ContactState contactState in preset.CompletedContacts)
            {
                MessagesManager.Instance.SetContactNarrativeStateSilently(
                    contactState.ContactName,
                    contactState.CompletedNodeId);
            }
        }

        EventBus.Instance.Publish(preset.DayCompletionEvent);

        if (ObjectivesManager.Instance != null)
        {
            ObjectivesManager.Instance.ForceSetObjective(preset.SleepObjectiveId);
        }
        else
        {
            EventBus.Instance.Publish("ForceSetObjective", preset.SleepObjectiveId);
        }

        EventBus.Instance.Publish("Laptop_DaySkipped", currentDay);
        UIManager.Instance?.ShowTemporaryMessage(
            $"Day {currentDay} skipped with the neutral story preset. Go to sleep.",
            Color.white,
            4f);
    }

    private static bool TryGetSkipPreset(int day, out DaySkipPreset preset)
    {
        switch (day)
        {
            case 1:
                preset = new DaySkipPreset(
                    7,
                    "D1_Evening_End",
                    new[] { "Choice1_Neutral", "Choice2_Neutral" },
                    new[]
                    {
                        new ContactState("Student Group", 2801),
                        new ContactState("Connect", 1002),
                        new ContactState("Emma", 1100),
                        new ContactState("Alex", 1200)
                    });
                return true;

            case 2:
                preset = new DaySkipPreset(
                    15,
                    "D2_NoahTeaser_End",
                    new[] { "Choice3_Neutral", "Choice4_Neutral", "Choice5_Neutral" },
                    new[]
                    {
                        new ContactState("Alex", 1300),
                        new ContactState("Emma", 1402),
                        new ContactState("Jamie", 1512),
                        new ContactState("Noah", 1600)
                    });
                return true;

            case 3:
                preset = new DaySkipPreset(
                    22,
                    "D3_Noah_End",
                    new[] { "Choice6_Neutral", "Choice7_Neutral", "Choice8_Neutral" },
                    new[]
                    {
                        new ContactState("Emma", 1800),
                        new ContactState("Noah", 1912),
                        new ContactState("Unknown", 1950)
                    });
                return true;

            case 4:
                preset = new DaySkipPreset(
                    30,
                    "D4_RileyIntro_End",
                    new[] { "Choice9_Neutral", "Choice10_Neutral" },
                    new[]
                    {
                        new ContactState("Emma", 2100),
                        new ContactState("Unknown", 2212),
                        new ContactState("Riley", 2300)
                    });
                return true;

            default:
                preset = null;
                return false;
        }
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
