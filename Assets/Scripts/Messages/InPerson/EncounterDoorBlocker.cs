using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls a physical doorway blocker and slides encounter NPCs aside when unlocked,
/// configurable by doorway proximity, story days, daily placements, and EventBus/Objective rules.
/// </summary>
[DisallowMultipleComponent]
public class EncounterDoorBlocker : MonoBehaviour
{
    public enum DoorBlockingOverride
    {
        UseRules,
        ForceAllow,
        ForceDeny
    }

    [Serializable]
    public struct DoorParticipant
    {
        [Tooltip("Optional Inspector label for readability (e.g., Sophie, Emma).")]
        public string label;
        [Tooltip("NPC Transform to slide/rotate aside when the doorway clears.")]
        public Transform npcTransform;
        [Tooltip("Local position offset added relative to this participant's closed pose when separating.")]
        public Vector3 separateLocalOffset;
        [Tooltip("Optional local Euler rotation offset applied when separating.")]
        public Vector3 separateLocalEulerOffset;
    }

    [Serializable]
    public struct DailyPlacement
    {
        [Tooltip("1-based story day this entry applies to.")]
        public int day;
        [Tooltip("Move/rotate this encounter root to Local Position / Local Euler Angles on this day.")]
        public bool overrideTransform;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        [Tooltip("Optionally toggle this GameObject's active state on this day.")]
        public bool overrideActiveState;
        public bool isActive;
        [Tooltip("Per-day rule override for doorway blocking & NPC separation.")]
        public DoorBlockingOverride doorBlockingOverride;
    }

    private sealed class ParticipantRuntime
    {
        public Transform Transform;
        public Vector3 SeparateOffset;
        public Vector3 SeparateEulerOffset;
        public Vector3 InitialLocalPosition;
        public Quaternion InitialLocalRotation;
    }

    [Header("1. Doorway Location & Proximity")]
    [Tooltip("When enabled, blocking and separation only happen while this encounter is near the doorway.")]
    [SerializeField] private bool _requireAtDoorway = true;

    [Tooltip("Optional Trigger/Box Collider marking the classroom doorway zone (takes priority over Doorway Anchor).")]
    [SerializeField] private Collider _doorwayZoneCollider;

    [Tooltip("Reference Transform of the classroom doorway used for proximity checks.")]
    [SerializeField] private Transform _doorwayAnchor;

    [Tooltip("Optional fallback object name searched once in the scene if no Collider or Anchor is assigned.")]
    [SerializeField] private string _fallbackDoorwayObjectName = "ClassroomDoor";

    [Tooltip("If enabled and no Collider/Anchor is resolved, measures proximity to Fallback Doorway World Position.")]
    [SerializeField] private bool _useFallbackWorldPosition;

    [SerializeField] private Vector3 _fallbackDoorwayWorldPosition;

    [Tooltip("Maximum distance (world units) from the doorway to be considered blocking the classroom door.")]
    [SerializeField, Min(0.1f)] private float _doorwayProximityRadius = 6f;

    [Tooltip("Ignore vertical (Y) offset when checking doorway proximity.")]
    [SerializeField] private bool _useHorizontalDistanceOnly = true;

    [Header("2. Day, Objective & Event Conditions")]
    [Tooltip("When enabled, doorway blocking and separation only apply on days listed in Allowed Days.")]
    [SerializeField] private bool _restrictToAllowedDays;

    [SerializeField] private List<int> _allowedDays = new List<int> { 1 };

    [Tooltip("When enabled, physical blocking only activates while the current objective listens to one of Required Objective Events.")]
    [SerializeField] private bool _requireMatchingObjective = true;

    [SerializeField] private List<string> _requiredObjectiveEvents = new List<string> { "SchoolEncounterComplete" };

    [Tooltip("EventBus events that unlock passage and separate the NPCs (e.g., SchoolEncounterComplete).")]
    [SerializeField] private List<string> _unlockEvents = new List<string> { "SchoolEncounterComplete" };

    [Tooltip("If an unlock event fires while a dialogue box is still open, wait until Conversation_Ended before separating.")]
    [SerializeField] private bool _waitForConversationEndToSeparate = true;

    [Tooltip("EventBus events that restore initial closed poses and re-evaluate doorway rules for a new day.")]
    [SerializeField] private List<string> _resetEvents = new List<string>
    {
        "NextDay",
        "Start_Day1",
        "Start_Day2",
        "Start_Day3",
        "Start_Day4",
        "Start_Day5"
    };

    [Header("3. Daily Placements & Per-Day Overrides (Optional)")]
    [SerializeField] private bool _useDailyPlacements;
    [SerializeField] private List<DailyPlacement> _dailyPlacements = new List<DailyPlacement>();

    [Header("4. Participants (Scalable List)")]
    [SerializeField] private List<DoorParticipant> _participants = new List<DoorParticipant>();

    [Header("Legacy 2-NPC Fallback (used only if Participants list is empty)")]
    [SerializeField] private Transform _leftNpc;
    [SerializeField] private Transform _rightNpc;
    [SerializeField] private Vector3 _leftSeparateOffset = new Vector3(-1.35f, 0f, 0f);
    [SerializeField] private Vector3 _rightSeparateOffset = new Vector3(1.35f, 0f, 0f);

    [Header("5. Separation Motion & Physical Blocker")]
    [SerializeField, Min(0.05f)] private float _separateDuration = 0.65f;
    [SerializeField] private AnimationCurve _separateEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool _enablePhysicalBlocker = true;
    [SerializeField] private BoxCollider _blockerCollider;
    [SerializeField] private Vector3 _blockerCenter = new Vector3(-1f, 1f, 0f);
    [SerializeField] private Vector3 _blockerSize = new Vector3(6f, 5f, 1.2f);

    private readonly List<ParticipantRuntime> _runtimeParticipants = new List<ParticipantRuntime>();
    private readonly Dictionary<string, Action> _unlockHandlers = new Dictionary<string, Action>(StringComparer.Ordinal);
    private readonly Dictionary<string, Action> _resetHandlers = new Dictionary<string, Action>(StringComparer.Ordinal);

    private NPCConversationReaction _conversationReaction;
    private Transform _cachedFallbackDoorway;
    private Coroutine _moveRoutine;
    private Coroutine _deferredRefreshRoutine;
    private bool _hasCapturedInitialPoses;
    private bool _hasSeparatedThisEncounter;
    private bool _pendingSeparation;
    private bool _warnedMissingDoorway;
    private Vector3 _lastEvaluatedPosition;

    private void Awake()
    {
        _conversationReaction = GetComponent<NPCConversationReaction>();
        EnsureBlockerCollider();
        BuildParticipantRuntimeList();
    }

    private void OnEnable()
    {
        SubscribeEvents();
        ApplyDailyPlacementIfConfigured();
        RefreshEncounterState();
        ScheduleDeferredRefresh();
    }

    private void Start()
    {
        EnsureBlockerCollider();
        BuildParticipantRuntimeList();
        ApplyDailyPlacementIfConfigured();
        RefreshEncounterState();
        ScheduleDeferredRefresh();
    }

    private void LateUpdate()
    {
        if (_requireAtDoorway && (_lastEvaluatedPosition - transform.position).sqrMagnitude > 0.001f)
        {
            RefreshEncounterState();
        }
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
        StopAllLocalCoroutines();
    }

    /// <summary>
    /// Re-evaluates whether this encounter currently blocks the doorway and syncs the physical collider.
    /// </summary>
    public void RefreshEncounterState()
    {
        _lastEvaluatedPosition = transform.position;

        if (_blockerCollider == null)
        {
            return;
        }

        if (!_enablePhysicalBlocker || _hasSeparatedThisEncounter)
        {
            _blockerCollider.enabled = false;
            return;
        }

        _blockerCollider.enabled = ShouldBlockBeforeUnlock();
    }

    /// <summary>
    /// Forces all participants back to their closed local poses and clears separation state.
    /// </summary>
    public void ResetToClosedState()
    {
        StopMoveRoutine();
        _hasSeparatedThisEncounter = false;
        _pendingSeparation = false;

        BuildParticipantRuntimeList();
        foreach (ParticipantRuntime participant in _runtimeParticipants)
        {
            if (participant.Transform == null)
            {
                continue;
            }

            participant.Transform.localPosition = participant.InitialLocalPosition;
            participant.Transform.localRotation = participant.InitialLocalRotation;
            _conversationReaction?.UpdateParticipantRestingPose(
                participant.Transform,
                participant.InitialLocalPosition,
                participant.InitialLocalRotation);
        }

        ApplyDailyPlacementIfConfigured();
        RefreshEncounterState();
        ScheduleDeferredRefresh();
    }

    /// <summary>
    /// Returns true if doorway conditions (location/day/per-day override) currently permit separation.
    /// </summary>
    public bool IsDoorwayConditionMet()
    {
        int currentDay = GetCurrentDay();
        DoorBlockingOverride dayOverride = GetDailyOverride(currentDay);

        if (dayOverride == DoorBlockingOverride.ForceDeny)
        {
            return false;
        }

        if (dayOverride == DoorBlockingOverride.ForceAllow)
        {
            return true;
        }

        if (_restrictToAllowedDays && !_allowedDays.Contains(currentDay))
        {
            return false;
        }

        if (_requireAtDoorway && !IsPositionedAtDoorway())
        {
            return false;
        }

        return true;
    }

    private bool ShouldBlockBeforeUnlock()
    {
        if (!IsDoorwayConditionMet())
        {
            return false;
        }

        if (!_requireMatchingObjective)
        {
            return true;
        }

        return MatchesCurrentObjective();
    }

    private bool MatchesCurrentObjective()
    {
        if (ObjectivesManager.Instance == null)
        {
            return false;
        }

        Objective currentObjective = ObjectivesManager.Instance.GetCurrentObjective();
        if (currentObjective == null)
        {
            return false;
        }

        string listenedEvent = currentObjective.EventToListen();
        if (string.IsNullOrEmpty(listenedEvent))
        {
            return false;
        }

        return ContainsEventName(_requiredObjectiveEvents, listenedEvent);
    }

    private bool IsPositionedAtDoorway()
    {
        Vector3 referencePos = GetEncounterReferenceWorldPosition();

        if (_doorwayZoneCollider != null)
        {
            Vector3 closest = _doorwayZoneCollider.ClosestPoint(referencePos);
            return ComputeDistance(referencePos, closest) <= 0.1f;
        }

        if (TryGetDoorwayAnchorWorldPosition(out Vector3 doorwayPos))
        {
            return ComputeDistance(referencePos, doorwayPos) <= _doorwayProximityRadius;
        }

        if (!_warnedMissingDoorway)
        {
            _warnedMissingDoorway = true;
            Debug.LogWarning($"[EncounterDoorBlocker] '{name}' has Require At Doorway enabled, but no Doorway Zone Collider, Doorway Anchor, or Fallback Position is available. Blocking/separation will stay disabled until configured.", this);
        }

        return false;
    }

    private bool TryGetDoorwayAnchorWorldPosition(out Vector3 worldPos)
    {
        if (_doorwayAnchor != null)
        {
            worldPos = _doorwayAnchor.position;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(_fallbackDoorwayObjectName))
        {
            if (_cachedFallbackDoorway == null)
            {
                GameObject found = GameObject.Find(_fallbackDoorwayObjectName);
                if (found != null)
                {
                    _cachedFallbackDoorway = found.transform;
                }
            }

            if (_cachedFallbackDoorway != null)
            {
                worldPos = _cachedFallbackDoorway.position;
                return true;
            }
        }

        if (_useFallbackWorldPosition)
        {
            worldPos = _fallbackDoorwayWorldPosition;
            return true;
        }

        worldPos = default;
        return false;
    }

    private Vector3 GetEncounterReferenceWorldPosition()
    {
        BuildParticipantRuntimeList();
        Vector3 sum = Vector3.zero;
        int count = 0;

        foreach (ParticipantRuntime participant in _runtimeParticipants)
        {
            if (participant.Transform == null)
            {
                continue;
            }

            sum += participant.Transform.position;
            count++;
        }

        return count > 0 ? sum / count : transform.position;
    }

    private float ComputeDistance(Vector3 a, Vector3 b)
    {
        if (_useHorizontalDistanceOnly)
        {
            a.y = 0f;
            b.y = 0f;
        }

        return Vector3.Distance(a, b);
    }

    private void ApplyDailyPlacementIfConfigured()
    {
        if (!_useDailyPlacements || _dailyPlacements.Count == 0)
        {
            return;
        }

        int currentDay = GetCurrentDay();
        for (int i = 0; i < _dailyPlacements.Count; i++)
        {
            DailyPlacement placement = _dailyPlacements[i];
            if (placement.day != currentDay)
            {
                continue;
            }

            if (placement.overrideTransform)
            {
                transform.localPosition = placement.localPosition;
                transform.localRotation = Quaternion.Euler(placement.localEulerAngles);
                _lastEvaluatedPosition = transform.position;
            }

            if (placement.overrideActiveState && gameObject.activeSelf != placement.isActive)
            {
                gameObject.SetActive(placement.isActive);
            }

            break;
        }
    }

    private DoorBlockingOverride GetDailyOverride(int day)
    {
        if (!_useDailyPlacements || _dailyPlacements.Count == 0)
        {
            return DoorBlockingOverride.UseRules;
        }

        for (int i = 0; i < _dailyPlacements.Count; i++)
        {
            if (_dailyPlacements[i].day == day)
            {
                return _dailyPlacements[i].doorBlockingOverride;
            }
        }

        return DoorBlockingOverride.UseRules;
    }

    private static int GetCurrentDay()
    {
        return DayManager.Instance != null ? Mathf.Max(1, DayManager.Instance.GetCurrentDay()) : 1;
    }

    private void HandleUnlockEvent()
    {
        if (_hasSeparatedThisEncounter)
        {
            return;
        }

        // Always open the collider if an encounter just completed so the player is never trapped.
        if (_blockerCollider != null)
        {
            _blockerCollider.enabled = false;
        }

        if (!IsDoorwayConditionMet())
        {
            return;
        }

        bool dialogueStillOpen =
            (DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive) ||
            (UIManager.Instance != null && UIManager.Instance.GetCurrentState() == UIState.CONVERSATION);

        if (_waitForConversationEndToSeparate && dialogueStillOpen)
        {
            _pendingSeparation = true;
            return;
        }

        BeginSeparation();
    }

    private void HandleConversationEnded()
    {
        if (!_pendingSeparation)
        {
            return;
        }

        _pendingSeparation = false;
        if (!_hasSeparatedThisEncounter && IsDoorwayConditionMet())
        {
            BeginSeparation();
        }
    }

    private void BeginSeparation()
    {
        if (_hasSeparatedThisEncounter)
        {
            return;
        }

        _hasSeparatedThisEncounter = true;
        _pendingSeparation = false;

        if (_blockerCollider != null)
        {
            _blockerCollider.enabled = false;
        }

        BuildParticipantRuntimeList();
        if (_runtimeParticipants.Count == 0)
        {
            return;
        }

        StopMoveRoutine();
        _moveRoutine = StartCoroutine(SeparateParticipantsRoutine());
    }

    private IEnumerator SeparateParticipantsRoutine()
    {
        int count = _runtimeParticipants.Count;
        Vector3[] startPositions = new Vector3[count];
        Vector3[] targetPositions = new Vector3[count];
        Quaternion[] startRotations = new Quaternion[count];
        Quaternion[] targetRotations = new Quaternion[count];

        for (int i = 0; i < count; i++)
        {
            ParticipantRuntime p = _runtimeParticipants[i];
            if (p.Transform == null)
            {
                continue;
            }

            startPositions[i] = p.Transform.localPosition;
            targetPositions[i] = p.InitialLocalPosition + p.SeparateOffset;
            startRotations[i] = p.Transform.localRotation;
            targetRotations[i] = p.InitialLocalRotation * Quaternion.Euler(p.SeparateEulerOffset);

            // Prevent NPCConversationReaction from pulling separated NPCs back into the doorway.
            _conversationReaction?.UpdateParticipantRestingPose(p.Transform, targetPositions[i], targetRotations[i]);
        }

        float elapsed = 0f;
        float duration = Mathf.Max(0.05f, _separateDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float linearT = Mathf.Clamp01(elapsed / duration);
            float easedT = _separateEase != null ? _separateEase.Evaluate(linearT) : Mathf.SmoothStep(0f, 1f, linearT);

            for (int i = 0; i < count; i++)
            {
                Transform target = _runtimeParticipants[i].Transform;
                if (target == null)
                {
                    continue;
                }

                target.localPosition = Vector3.Lerp(startPositions[i], targetPositions[i], easedT);
                target.localRotation = Quaternion.Slerp(startRotations[i], targetRotations[i], easedT);
            }

            yield return null;
        }

        for (int i = 0; i < count; i++)
        {
            Transform target = _runtimeParticipants[i].Transform;
            if (target == null)
            {
                continue;
            }

            target.localPosition = targetPositions[i];
            target.localRotation = targetRotations[i];
        }

        _moveRoutine = null;
    }

    private void BuildParticipantRuntimeList()
    {
        if (_hasCapturedInitialPoses && _runtimeParticipants.Count > 0)
        {
            return;
        }

        _runtimeParticipants.Clear();

        if (_participants != null && _participants.Count > 0)
        {
            for (int i = 0; i < _participants.Count; i++)
            {
                DoorParticipant entry = _participants[i];
                AddParticipantRuntime(entry.npcTransform, entry.separateLocalOffset, entry.separateLocalEulerOffset);
            }
        }
        else
        {
            AddParticipantRuntime(_leftNpc, _leftSeparateOffset, Vector3.zero);
            AddParticipantRuntime(_rightNpc, _rightSeparateOffset, Vector3.zero);
        }

        _hasCapturedInitialPoses = _runtimeParticipants.Count > 0;
    }

    private void AddParticipantRuntime(Transform npcTransform, Vector3 separateOffset, Vector3 separateEulerOffset)
    {
        if (npcTransform == null)
        {
            return;
        }

        _runtimeParticipants.Add(new ParticipantRuntime
        {
            Transform = npcTransform,
            SeparateOffset = separateOffset,
            SeparateEulerOffset = separateEulerOffset,
            InitialLocalPosition = npcTransform.localPosition,
            InitialLocalRotation = npcTransform.localRotation
        });
    }

    private void EnsureBlockerCollider()
    {
        if (!_enablePhysicalBlocker)
        {
            if (_blockerCollider != null)
            {
                _blockerCollider.enabled = false;
            }

            return;
        }

        if (_blockerCollider == null)
        {
            BoxCollider[] existing = GetComponents<BoxCollider>();
            for (int i = 0; i < existing.Length; i++)
            {
                if (!existing[i].isTrigger)
                {
                    _blockerCollider = existing[i];
                    break;
                }
            }

            if (_blockerCollider == null)
            {
                _blockerCollider = gameObject.AddComponent<BoxCollider>();
                _blockerCollider.isTrigger = false;
                _blockerCollider.center = _blockerCenter;
                _blockerCollider.size = _blockerSize;
            }
        }
    }

    private void ScheduleDeferredRefresh()
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        if (_deferredRefreshRoutine != null)
        {
            StopCoroutine(_deferredRefreshRoutine);
        }

        _deferredRefreshRoutine = StartCoroutine(DeferredRefreshRoutine());
    }

    private IEnumerator DeferredRefreshRoutine()
    {
        yield return null;
        ApplyDailyPlacementIfConfigured();
        RefreshEncounterState();
        _deferredRefreshRoutine = null;
    }

    private void StopMoveRoutine()
    {
        if (_moveRoutine != null)
        {
            StopCoroutine(_moveRoutine);
            _moveRoutine = null;
        }
    }

    private void StopAllLocalCoroutines()
    {
        StopMoveRoutine();
        if (_deferredRefreshRoutine != null)
        {
            StopCoroutine(_deferredRefreshRoutine);
            _deferredRefreshRoutine = null;
        }
    }

    private void SubscribeEvents()
    {
        if (EventBus.Instance == null)
        {
            return;
        }

        UnsubscribeEvents();

        for (int i = 0; i < _unlockEvents.Count; i++)
        {
            string eventName = _unlockEvents[i];
            if (string.IsNullOrWhiteSpace(eventName) || _unlockHandlers.ContainsKey(eventName))
            {
                continue;
            }

            Action handler = HandleUnlockEvent;
            _unlockHandlers[eventName] = handler;
            EventBus.Instance.Subscribe(eventName, handler);
        }

        for (int i = 0; i < _resetEvents.Count; i++)
        {
            string eventName = _resetEvents[i];
            if (string.IsNullOrWhiteSpace(eventName) || _resetHandlers.ContainsKey(eventName))
            {
                continue;
            }

            Action handler = ResetToClosedState;
            _resetHandlers[eventName] = handler;
            EventBus.Instance.Subscribe(eventName, handler);
        }

        EventBus.Instance.Subscribe("Conversation_Ended", HandleConversationEnded);
    }

    private void UnsubscribeEvents()
    {
        if (EventBus.Instance == null)
        {
            return;
        }

        foreach (KeyValuePair<string, Action> kvp in _unlockHandlers)
        {
            EventBus.Instance.Unsubscribe(kvp.Key, kvp.Value);
        }
        _unlockHandlers.Clear();

        foreach (KeyValuePair<string, Action> kvp in _resetHandlers)
        {
            EventBus.Instance.Unsubscribe(kvp.Key, kvp.Value);
        }
        _resetHandlers.Clear();

        EventBus.Instance.Unsubscribe("Conversation_Ended", HandleConversationEnded);
    }

    private static bool ContainsEventName(List<string> events, string target)
    {
        if (events == null || string.IsNullOrEmpty(target))
        {
            return false;
        }

        for (int i = 0; i < events.Count; i++)
        {
            if (string.Equals(events[i], target, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        if (_requireAtDoorway)
        {
            if (_doorwayZoneCollider != null)
            {
                Gizmos.color = new Color(0.2f, 0.9f, 0.9f, 0.65f);
                Gizmos.DrawWireCube(_doorwayZoneCollider.bounds.center, _doorwayZoneCollider.bounds.size);
            }
            else if (TryGetDoorwayAnchorWorldPosition(out Vector3 doorwayPos))
            {
                Gizmos.color = new Color(0.2f, 0.9f, 0.9f, 0.65f);
                Gizmos.DrawWireSphere(doorwayPos, _doorwayProximityRadius);
            }
        }

        Gizmos.color = new Color(1f, 0.4f, 0.8f, 0.85f);
        if (_participants != null && _participants.Count > 0)
        {
            for (int i = 0; i < _participants.Count; i++)
            {
                DoorParticipant p = _participants[i];
                if (p.npcTransform == null) continue;
                Vector3 targetWorld = p.npcTransform.parent != null
                    ? p.npcTransform.parent.TransformPoint(p.npcTransform.localPosition + p.separateLocalOffset)
                    : p.npcTransform.position + p.separateLocalOffset;
                Gizmos.DrawLine(p.npcTransform.position, targetWorld);
                Gizmos.DrawWireSphere(targetWorld, 0.25f);
            }
        }
    }
}
