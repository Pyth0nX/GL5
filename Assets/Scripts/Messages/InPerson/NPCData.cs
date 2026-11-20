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

    [Header("Class Transition Loops")]
    [Tooltip("Starting narrative node for the before-class dialogue loop (-1 if unused).")]
    [SerializeField] private int _beforeClassNodeId = -1;

    [Tooltip("Starting narrative node for the after-class dialogue loop (-1 if unused).")]
    [SerializeField] private int _afterClassNodeId = -1;

    [Header("Dialogue UI")]
    [Tooltip("If enabled, uses Custom Name Box Color for this NPC's in-person dialogue speaker tag.")]
    [SerializeField] private bool _useCustomNameBoxColor = false;
    [SerializeField] private Color _customNameBoxColor = new Color(0.22f, 0.40f, 0.62f, 0.96f);

    private static bool _isClassFinishedToday = false;
    private bool _isInConversation;
    private bool _isInStandaloneInteraction;
    private string _registeredCustomColorKey;

    public event System.Action InteractionStarted;
    public event System.Action InteractionEnded;

    protected override void Awake()
    {
        base.Awake();
        AutoConfigureClassroomNPC();
        RegisterCustomNameBoxColorIfEnabled();
    }

    private void RegisterCustomNameBoxColorIfEnabled()
    {
        if (_useCustomNameBoxColor && !string.IsNullOrWhiteSpace(_characterName))
        {
            _registeredCustomColorKey = _characterName.Trim();
            DialogueStyleResolver.RegisterRuntimeOverride(_registeredCustomColorKey, _customNameBoxColor);
        }
    }

    private void AutoConfigureClassroomNPC()
    {
        string objName = gameObject.name.Trim();
        if (objName == "NPC (5)" || objName == "NPC(5)")
        {
            if (string.IsNullOrEmpty(_characterName) || _characterName == "Classmate") _characterName = "Leo";
            _standaloneDialogue = "";
            if (_beforeClassNodeId < 0) _beforeClassNodeId = 100;
            if (_afterClassNodeId < 0) _afterClassNodeId = 103;
        }
        else if (objName == "NPC (6)" || objName == "NPC(6)")
        {
            if (string.IsNullOrEmpty(_characterName) || _characterName == "Classmate") _characterName = "Chloe";
            _standaloneDialogue = "";
            if (_beforeClassNodeId < 0) _beforeClassNodeId = 110;
            if (_afterClassNodeId < 0) _afterClassNodeId = 113;
        }
        else if (objName == "NPC (8)" || objName == "NPC(8)")
        {
            if (string.IsNullOrEmpty(_characterName) || _characterName == "Classmate") _characterName = "Liam";
            _standaloneDialogue = "";
            if (_beforeClassNodeId < 0) _beforeClassNodeId = 120;
            if (_afterClassNodeId < 0) _afterClassNodeId = 123;
        }
        else if (objName == "NPC (9)" || objName == "NPC(9)")
        {
            if (string.IsNullOrEmpty(_characterName) || _characterName == "Classmate") _characterName = "Mia";
            _standaloneDialogue = "";
            if (_beforeClassNodeId < 0) _beforeClassNodeId = 130;
            if (_afterClassNodeId < 0) _afterClassNodeId = 133;
        }
        else if (objName == "NPC (10)" || objName == "NPC(10)")
        {
            if (string.IsNullOrEmpty(_characterName) || _characterName == "Classmate") _characterName = "Ethan";
            _standaloneDialogue = "";
            if (_beforeClassNodeId < 0) _beforeClassNodeId = 140;
            if (_afterClassNodeId < 0) _afterClassNodeId = 143;
        }
        else if (objName == "Teacher")
        {
            if (string.IsNullOrEmpty(_characterName)) _characterName = "Mr. Henderson";
            _standaloneDialogue = "";
            if (_beforeClassNodeId < 0) _beforeClassNodeId = 150;
            if (_afterClassNodeId < 0) _afterClassNodeId = 153;
            return;
        }
        else
        {
            return;
        }

        if (GetComponent<NPCDailySchedule>() == null)
        {
            gameObject.AddComponent<NPCDailySchedule>();
        }
    }

    public static void ResetClassFinishedState()
    {
        _isClassFinishedToday = false;
    }

    public static bool IsSchoolClassFinished()
    {
        if (ObjectivesManager.Instance != null && ObjectivesManager.Instance.GetCurrentObjective() != null)
        {
            string ev = ObjectivesManager.Instance.GetCurrentObjective().EventToListen();
            if (ev == "SchoolClassFinished" || ev == "SchoolEncounterComplete" || ev == "Day1_GoToSchool")
            {
                return false;
            }

            if (ev == "Day1_ComebackHome" || ev == "D1_StudentConnectOverheard")
            {
                return true;
            }
        }

        return _isClassFinishedToday;
    }

    private void OnSchoolClassFinished()
    {
        _isClassFinishedToday = true;
        int afterNode = ResolveDayAfterClassNodeId();
        if (!string.IsNullOrEmpty(_characterName) && afterNode >= 0 && MessagesManager.Instance != null)
        {
            MessagesManager.Instance.AdvanceContactNarrativeNodeByEvent(_characterName, afterNode);
        }
    }

    private void OnDayReset()
    {
        _isClassFinishedToday = false;
        int beforeNode = ResolveDayBeforeClassNodeId();
        if (!string.IsNullOrEmpty(_characterName) && beforeNode >= 0 && MessagesManager.Instance != null)
        {
            MessagesManager.Instance.AdvanceContactNarrativeNodeByEvent(_characterName, beforeNode);
        }

        if (string.IsNullOrWhiteSpace(_standaloneDialogue) &&
            _disableEvents != null &&
            _disableEvents.Contains("SchoolEncounterComplete") &&
            ObjectivesManager.Instance != null &&
            ObjectivesManager.Instance.GetCurrentObjective() != null &&
            ObjectivesManager.Instance.GetCurrentObjective().EventToListen() == "SchoolEncounterComplete")
        {
            _canInteract = true;
            UpdateHoveredOutlineColor();
        }
    }

    private int ResolveDayBeforeClassNodeId() => ResolveDayAdjustedNodeId(_beforeClassNodeId);
    private int ResolveDayAfterClassNodeId() => ResolveDayAdjustedNodeId(_afterClassNodeId);

    private static readonly System.Collections.Generic.Dictionary<int, int[]> DayAdjustedNodeTable =
        new System.Collections.Generic.Dictionary<int, int[]>
        {
            { 100, new[] { 196, 232, 268, 304 } },
            { 103, new[] { 199, 235, 271, 307 } },
            { 110, new[] { 202, 238, 274, 310 } },
            { 113, new[] { 205, 241, 277, 313 } },
            { 120, new[] { 208, 244, 280, 316 } },
            { 123, new[] { 211, 247, 283, 319 } },
            { 130, new[] { 214, 250, 286, 322 } },
            { 133, new[] { 217, 253, 289, 325 } },
            { 140, new[] { 220, 256, 292, 327 } },
            { 143, new[] { 223, 259, 295, 330 } },
            { 150, new[] { 226, 262, 298, 332 } },
            { 153, new[] { 229, 265, 301, 335 } },
        };

    private static int ResolveDayAdjustedNodeId(int baseNodeId)
    {
        if (baseNodeId < 100 || baseNodeId > 159)
        {
            return baseNodeId;
        }

        int currentDay = DayManager.Instance != null ? DayManager.Instance.GetCurrentDay() : 1;
        if (currentDay < 2)
        {
            return baseNodeId;
        }

        int dayIndex = Mathf.Clamp(currentDay, 2, 5) - 2;
        if (DayAdjustedNodeTable.TryGetValue(baseNodeId, out int[] dayNodes) &&
            dayIndex >= 0 && dayIndex < dayNodes.Length)
        {
            int mapped = dayNodes[dayIndex];
            if (MessagesManager.Instance == null || MessagesManager.Instance.GetNarrativeNode(mapped) != null)
            {
                return mapped;
            }
        }

        int legacyCandidate = 1000 + Mathf.Clamp(currentDay, 2, 5) * 100 + (baseNodeId - 100);
        if (MessagesManager.Instance != null && MessagesManager.Instance.GetNarrativeNode(legacyCandidate) != null)
        {
            return legacyCandidate;
        }

        return baseNodeId;
    }

    private static bool IsNodeInLoopChain(int targetId, int chainStartId)
    {
        if (targetId < 0 || chainStartId < 0)
        {
            return false;
        }

        if (MessagesManager.Instance == null)
        {
            return targetId == chainStartId;
        }

        int cur = chainStartId;
        for (int i = 0; i < 6 && cur >= 0; i++)
        {
            if (cur == targetId)
            {
                return true;
            }

            NarrativeNode node = MessagesManager.Instance.GetNarrativeNode(cur);
            if (node == null)
            {
                break;
            }

            cur = node.ResolveNextNode();
            if (cur == chainStartId)
            {
                break;
            }
        }

        return false;
    }

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

            EventBus.Instance.Subscribe("SchoolClassFinished", OnSchoolClassFinished);
            EventBus.Instance.Subscribe("Conversation_Ended", HandleConversationEnded);
            EventBus.Instance.Subscribe("NextDay", OnDayReset);
            EventBus.Instance.Subscribe("Day1_GoToSchool", OnDayReset);
            EventBus.Instance.Subscribe("Start_Day1", OnDayReset);
            EventBus.Instance.Subscribe("Start_Day2", OnDayReset);
            EventBus.Instance.Subscribe("Start_Day3", OnDayReset);
            EventBus.Instance.Subscribe("Start_Day4", OnDayReset);
            EventBus.Instance.Subscribe("Start_Day5", OnDayReset);
        }

        int beforeNode = ResolveDayBeforeClassNodeId();
        int afterNode = ResolveDayAfterClassNodeId();
        if (!string.IsNullOrEmpty(_characterName) && beforeNode >= 0 && MessagesManager.Instance != null)
        {
            bool classDone = IsSchoolClassFinished();
            int targetNode = classDone && afterNode >= 0 ? afterNode : beforeNode;
            Contact contact = MessagesManager.Instance.GetContactByName(_characterName);
            if (contact != null)
            {
                int curr = contact.NarrativeNode();

                if (curr < 0)
                {
                    contact.SetNarrativeNode(targetNode);
                    contact.SetCurrentMessageIndex(0);
                }
                else if (classDone && afterNode >= 0 && !IsNodeInLoopChain(curr, afterNode))
                {
                    contact.SetNarrativeNode(targetNode);
                    contact.SetCurrentMessageIndex(0);
                }
                else if (!classDone && !IsNodeInLoopChain(curr, beforeNode))
                {
                    contact.SetNarrativeNode(targetNode);
                    contact.SetCurrentMessageIndex(0);
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
        if (!string.IsNullOrEmpty(_registeredCustomColorKey))
        {
            DialogueStyleResolver.UnregisterRuntimeOverride(_registeredCustomColorKey);
            _registeredCustomColorKey = null;
        }

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
            EventBus.Instance.Unsubscribe("SchoolClassFinished", OnSchoolClassFinished);
            EventBus.Instance.Unsubscribe("Conversation_Ended", HandleConversationEnded);
            EventBus.Instance.Unsubscribe("NextDay", OnDayReset);
            EventBus.Instance.Unsubscribe("Day1_GoToSchool", OnDayReset);
            EventBus.Instance.Unsubscribe("Start_Day1", OnDayReset);
            EventBus.Instance.Unsubscribe("Start_Day2", OnDayReset);
            EventBus.Instance.Unsubscribe("Start_Day3", OnDayReset);
            EventBus.Instance.Unsubscribe("Start_Day4", OnDayReset);
            EventBus.Instance.Unsubscribe("Start_Day5", OnDayReset);
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

            if (opened)
            {
                BeginActiveInteraction(isStoryConversation: false);
            }
            else
            {
                EndStandaloneInteraction();
            }

            return;
        }

        if (MessagesManager.Instance != null)
        {
            Contact contact = MessagesManager.Instance.GetContactByName(_characterName);
            if (contact != null && contact.NarrativeNode() < 0)
            {
                Debug.Log($"[NPC]: Interaction unavailable - '{_characterName}' has no active narrative node.");
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
        }

        BeginActiveInteraction(isStoryConversation: true);
        MessagesManager.Instance.SetCurrentContact(_characterName);
        UIManager.Instance.ChangeState(4);
    }

    private void BeginActiveInteraction(bool isStoryConversation)
    {
        _isInConversation = isStoryConversation;
        _isInStandaloneInteraction = !isStoryConversation;
        InteractionStarted?.Invoke();
    }

    private void HandleConversationEnded()
    {
        if (!_isInConversation)
        {
            return;
        }

        _isInConversation = false;
        InteractionEnded?.Invoke();
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

        if (_isInStandaloneInteraction)
        {
            _isInStandaloneInteraction = false;
            InteractionEnded?.Invoke();
        }
    }
}
