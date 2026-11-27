using System.Collections;
using UnityEngine;

public class StudentGroupConnectEncounter : IInteractable
{
    [Header("Encounter")]
    [SerializeField] private BoxCollider _overhearTrigger;
    [SerializeField] private string _contactName = "Student Group";
    [SerializeField] private int _overheardNodeId = 74;
    [SerializeField] private int _followUpNodeId = 75;

    [Header("Automatic Camera Focus")]
    [Tooltip("Optional explicit focus point. When empty, the midpoint of the student children is used.")]
    [SerializeField] private Transform _focusTarget;
    [SerializeField, Min(0f)] private float _focusTurnDuration = 0.65f;
    [SerializeField] private float _focusHeightOffset = 0.5f;

    [Header("Events")]
    [SerializeField] private string _classFinishedEvent = "SchoolClassFinished";
    [SerializeField] private string _overheardEvent = "D1_StudentConnectOverheard";
    [SerializeField] private string _conversationFinishedEvent = "D1_StudentConnectConversationComplete";
    [SerializeField] private string _connectProfileCompletedEvent = "D1_Profile_End";
    [SerializeField] private string _exitAttemptEvent = "D1_StudentConnectExitAttempt";

    private bool _encounterArmed;
    private bool _overheard;
    private bool _encounterComplete;
    private bool _startingOverheardEncounter;
    private MapObject _exitDoor;
    private Collider _playerCollider;
    private float _nextOverlapCheckTime;
    private BoxCollider _originalOverhearCollider;
    private BoxCollider _isolatedOverhearCollider;
    private NonInteractableTriggerZone _overhearZoneRelay;

    public override bool CanBeTargetedByCollider(Collider hitCollider)
    {
        if (hitCollider == null) return false;
        if (hitCollider == _overhearTrigger ||
            hitCollider == _isolatedOverhearCollider ||
            hitCollider == _originalOverhearCollider)
        {
            return false;
        }
        return true;
    }

    protected override void Awake()
    {
        _canInteract = false;
        _interactMessage = "Talk to students";
        _notAvailableMessage = "The students are busy talking.";

        if (_overhearTrigger == null)
        {
            foreach (BoxCollider candidate in GetComponents<BoxCollider>())
            {
                if (Mathf.Approximately(candidate.size.x, 20f) &&
                    Mathf.Approximately(candidate.size.y, 1f) &&
                    Mathf.Approximately(candidate.size.z, 1f))
                {
                    _overhearTrigger = candidate;
                    break;
                }
            }
        }

        if (_overhearTrigger != null)
        {
            _overhearTrigger.isTrigger = true;
            IsolateOverhearTriggerCollider();
        }

        base.Awake();
    }

    private void IsolateOverhearTriggerCollider()
    {
        if (_overhearTrigger == null) return;

        int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
        int targetLayer = ignoreRaycastLayer >= 0 ? ignoreRaycastLayer : 2;

        if (_overhearTrigger.gameObject != gameObject)
        {
            _overhearTrigger.gameObject.layer = targetLayer;
            _isolatedOverhearCollider = _overhearTrigger;
            _overhearZoneRelay = _overhearTrigger.GetComponent<NonInteractableTriggerZone>();
            if (_overhearZoneRelay == null)
            {
                _overhearZoneRelay = _overhearTrigger.gameObject.AddComponent<NonInteractableTriggerZone>();
            }
            _overhearZoneRelay.TriggerEntered -= HandleOverhearTriggerEntered;
            _overhearZoneRelay.TriggerEntered += HandleOverhearTriggerEntered;
            return;
        }

        _originalOverhearCollider = _overhearTrigger;

        GameObject zoneGO = new GameObject("OverhearTriggerZone");
        zoneGO.layer = targetLayer;
        zoneGO.transform.SetParent(transform, false);
        zoneGO.transform.localPosition = Vector3.zero;
        zoneGO.transform.localRotation = Quaternion.identity;
        zoneGO.transform.localScale = Vector3.one;

        BoxCollider cloned = zoneGO.AddComponent<BoxCollider>();
        cloned.center = _originalOverhearCollider.center;
        cloned.size = _originalOverhearCollider.size;
        cloned.isTrigger = true;
        cloned.enabled = _originalOverhearCollider.enabled;

        _overhearZoneRelay = zoneGO.AddComponent<NonInteractableTriggerZone>();
        _overhearZoneRelay.TriggerEntered += HandleOverhearTriggerEntered;

        _originalOverhearCollider.enabled = false;
        _isolatedOverhearCollider = cloned;
        _overhearTrigger = cloned;
    }

    protected override void Start()
    {
        base.Start();
        ResolveExitDoor();

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe(_classFinishedEvent, ArmEncounter);
            EventBus.Instance.Subscribe(_overheardEvent, EnableFollowUp);
            EventBus.Instance.Subscribe(_conversationFinishedEvent, CompleteEncounter);
            EventBus.Instance.Subscribe(_connectProfileCompletedEvent, EnableExitAfterConnectSetup);
            EventBus.Instance.Subscribe(_exitAttemptEvent, BeginOverheardEncounter);
        }

        SyncStateFromActiveObjective();
    }

    private void SyncStateFromActiveObjective()
    {
        if (!IsDayOne() || ObjectivesManager.Instance == null) return;

        Objective active = ObjectivesManager.Instance.GetCurrentObjective();
        if (active == null) return;

        int id = active.Id();
        if (id == 5)
        {
            _encounterArmed = true;
            _exitDoor?.DisableInteraction();
        }
        else if (id == 37)
        {
            _encounterArmed = false;
            _overheard = true;
            if (_overhearTrigger != null) _overhearTrigger.enabled = false;
            SetCanInteract(true);
            _exitDoor?.DisableInteraction();
        }
        else if (id == 39 || id == 40)
        {
            _encounterArmed = false;
            _overheard = true;
            _encounterComplete = true;
            if (_overhearTrigger != null) _overhearTrigger.enabled = false;
            SetCanInteract(false);
            _exitDoor?.DisableInteraction();
        }
        else if (id == 38)
        {
            _encounterArmed = false;
            _overheard = true;
            _encounterComplete = true;
            if (_overhearTrigger != null) _overhearTrigger.enabled = false;
            SetCanInteract(false);
            _exitDoor?.EnableInteraction();
        }
    }

    private void Update()
    {
        if (!_encounterArmed || _overheard || _encounterComplete || !IsDayOne()) return;
        if (_overhearTrigger == null || !_overhearTrigger.enabled) return;
        if (Time.unscaledTime < _nextOverlapCheckTime) return;

        _nextOverlapCheckTime = Time.unscaledTime + 0.1f;

        if (_playerCollider == null)
        {
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                _playerCollider = player.GetComponent<Collider>();
            }
        }

        if (_playerCollider != null &&
            _overhearTrigger.bounds.Intersects(_playerCollider.bounds))
        {
            BeginOverheardEncounter();
        }
    }

    private void OnDestroy()
    {
        SetOutlineActive(false);

        if (_overhearZoneRelay != null)
        {
            _overhearZoneRelay.TriggerEntered -= HandleOverhearTriggerEntered;
        }

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe(_classFinishedEvent, ArmEncounter);
            EventBus.Instance.Unsubscribe(_overheardEvent, EnableFollowUp);
            EventBus.Instance.Unsubscribe(_conversationFinishedEvent, CompleteEncounter);
            EventBus.Instance.Unsubscribe(_connectProfileCompletedEvent, EnableExitAfterConnectSetup);
            EventBus.Instance.Unsubscribe(_exitAttemptEvent, BeginOverheardEncounter);
        }
    }

    private void ArmEncounter()
    {
        if (!IsDayOne() || _encounterComplete) return;

        _encounterArmed = true;
        StartCoroutine(LockExitAfterClass());
    }

    private IEnumerator LockExitAfterClass()
    {
        // Wait one frame so this Day 1 lock overrides MainDoor's generic SchoolClassFinished unlock
        yield return null;
        ResolveExitDoor();
        _exitDoor?.DisableInteraction();
    }

    private void EnableFollowUp()
    {
        if (!IsDayOne() || _encounterComplete) return;

        _overheard = true;
        SetCanInteract(true);
    }

    private void CompleteEncounter()
    {
        if (!IsDayOne()) return;

        _encounterComplete = true;
        SetCanInteract(false);
        ResolveExitDoor();
        _exitDoor?.DisableInteraction();
    }

    private void EnableExitAfterConnectSetup()
    {
        if (!IsDayOne() || !_encounterComplete) return;

        ResolveExitDoor();
        _exitDoor?.EnableInteraction();
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleOverhearTriggerEntered(other);
    }

    private void HandleOverhearTriggerEntered(Collider other)
    {
        if (other == null || !_encounterArmed || _overheard || _encounterComplete || !IsDayOne()) return;
        if (other.GetComponentInParent<PlayerController>() == null &&
            other.GetComponentInParent<PlayerStateMachine>() == null) return;

        BeginOverheardEncounter();
    }

    private void BeginOverheardEncounter()
    {
        if (!_encounterArmed || _overheard || _encounterComplete ||
            _startingOverheardEncounter || !IsDayOne()) return;

        StartCoroutine(BeginOverheardEncounterRoutine());
    }

    private IEnumerator BeginOverheardEncounterRoutine()
    {
        _startingOverheardEncounter = true;
        _encounterArmed = false;

        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            PlayerStateMachine playerState = player.GetComponent<PlayerStateMachine>();
            playerState?.ChangeState(PlayerState.Interacting);

            if (InputManager.Instance != null)
            {
                InputManager.Instance.DisableMovementMap();
            }

            yield return player.FocusViewOn(GetFocusPosition(), _focusTurnDuration);
        }

        if (StartConversation(_overheardNodeId))
        {
            _overheard = true;

            if (_overhearTrigger != null)
            {
                _overhearTrigger.enabled = false;
            }
        }
        else
        {
            _encounterArmed = true;

            if (InputManager.Instance != null)
            {
                InputManager.Instance.EnableMovementMap();
            }

            ReturnPlayerToIdle();
        }

        _startingOverheardEncounter = false;
    }

    private Vector3 GetFocusPosition()
    {
        if (_focusTarget != null)
        {
            return _focusTarget.position;
        }

        if (transform.childCount == 0)
        {
            return transform.position + Vector3.up * _focusHeightOffset;
        }

        Vector3 midpoint = Vector3.zero;
        for (int i = 0; i < transform.childCount; i++)
        {
            midpoint += transform.GetChild(i).position;
        }

        midpoint /= transform.childCount;
        return midpoint + Vector3.up * _focusHeightOffset;
    }

    public override void Interact()
    {
        if (!_canInteract || !_overheard || _encounterComplete)
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

        if (StartConversation(_followUpNodeId))
        {
            SetCanInteract(false);
        }
        else
        {
            ReturnPlayerToIdle();
        }
    }

    private bool StartConversation(int narrativeNodeId)
    {
        if (MessagesManager.Instance == null || UIManager.Instance == null)
        {
            Debug.LogWarning("[StudentGroupConnectEncounter] Dialogue systems are unavailable.");
            return false;
        }

        MessagesManager.Instance.AdvanceContactNarrativeNodeByEvent(_contactName, narrativeNodeId);
        MessagesManager.Instance.SetCurrentContact(_contactName);
        UIManager.Instance.ChangeState((int)UIState.CONVERSATION);
        return true;
    }

    private void ResolveExitDoor()
    {
        if (_exitDoor != null) return;

        GameObject mainDoor = GameObject.Find("MainDoor");
        if (mainDoor != null)
        {
            _exitDoor = mainDoor.GetComponent<MapObject>();
        }
    }

    private static bool IsDayOne()
    {
        return DayManager.Instance == null || DayManager.Instance.GetCurrentDay() == 1;
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
