using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scalable, reusable interactable for collection/pickup objectives (such as packing school items).
/// Any active instances in the same scene sharing the same Progress Event Name automatically
/// update the objective's required targetCount and persist their collected state across saves.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class ObjectivePickupInteractable : IInteractable
{
    [Header("Objective Pickup")]
    [Tooltip("Unique identifier for this item within its objective. Defaults to GameObject name if empty.")]
    [SerializeField] private string _itemId;

    [Tooltip("EventBus event published when picked up (must match the objective's eventToListen).")]
    [SerializeField] private string _progressEventName = "D1_SchoolItemPicked";

    [Tooltip("0 = any day. Otherwise this pickup belongs to the specified story day and hides on later days.")]
    [SerializeField] private int _requiredDay = 1;

    [Tooltip("If true, deactivates this GameObject immediately when interacted with.")]
    [SerializeField] private bool _deactivateOnPickup = true;

    [Tooltip("If true, automatically counts all active ObjectivePickupInteractable objects in this scene sharing the same Progress Event Name and updates the objective's targetCount.")]
    [SerializeField] private bool _autoSyncSceneTargetCount = true;

    private static readonly Dictionary<string, HashSet<ObjectivePickupInteractable>> ActiveScenePickupsByEvent =
        new Dictionary<string, HashSet<ObjectivePickupInteractable>>(StringComparer.OrdinalIgnoreCase);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry()
    {
        ActiveScenePickupsByEvent.Clear();
    }

    private bool _isRegistered;
    private bool _isCollectedLocal;

    private string NormalizedEventName => string.IsNullOrWhiteSpace(_progressEventName)
        ? string.Empty
        : _progressEventName.Trim();

    private string EffectiveItemId => !string.IsNullOrWhiteSpace(_itemId)
        ? _itemId.Trim()
        : gameObject.name.Trim();

    protected override void Awake()
    {
        if (string.IsNullOrWhiteSpace(_interactMessage))
        {
            _interactMessage = "Pick up " + gameObject.name.ToLowerInvariant();
        }

        if (string.IsNullOrWhiteSpace(_notAvailableMessage))
        {
            _notAvailableMessage = "Check your messages first";
        }

        base.Awake();

        string eventKey = NormalizedEventName;
        if (!string.IsNullOrEmpty(eventKey))
        {
            if (!ActiveScenePickupsByEvent.TryGetValue(eventKey, out HashSet<ObjectivePickupInteractable> set))
            {
                set = new HashSet<ObjectivePickupInteractable>();
                ActiveScenePickupsByEvent[eventKey] = set;
            }
            set.Add(this);
            _isRegistered = true;
        }

        _canInteract = false;
    }

    protected override void Start()
    {
        base.Start();

        if (ObjectivesManager.Instance != null)
        {
            ObjectivesManager.Instance.OnObjectiveUpdated += SyncWithCurrentObjective;
        }

        if (_autoSyncSceneTargetCount)
        {
            SyncScenePickupTargetCount(NormalizedEventName);
        }

        if (IsAlreadyCollected() || ShouldHideForCurrentDay())
        {
            SetCanInteract(false);
            SetOutlineActive(false);
            if (_deactivateOnPickup)
            {
                gameObject.SetActive(false);
            }
            return;
        }

        if (ObjectivesManager.Instance != null)
        {
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

        string eventKey = NormalizedEventName;
        if (_isRegistered &&
            !string.IsNullOrEmpty(eventKey) &&
            ActiveScenePickupsByEvent.TryGetValue(eventKey, out HashSet<ObjectivePickupInteractable> set))
        {
            set.Remove(this);
        }
        _isRegistered = false;
    }

    private static void SyncScenePickupTargetCount(string eventKey)
    {
        if (string.IsNullOrEmpty(eventKey) ||
            ObjectivesManager.Instance == null ||
            !ActiveScenePickupsByEvent.TryGetValue(eventKey, out HashSet<ObjectivePickupInteractable> set))
        {
            return;
        }

        set.RemoveWhere(item => item == null || !item.gameObject.scene.isLoaded);

        int count = 0;
        foreach (ObjectivePickupInteractable item in set)
        {
            if (item != null && !item.ShouldHideForCurrentDay())
            {
                count++;
            }
        }

        if (count > 0)
        {
            ObjectivesManager.Instance.SyncObjectiveTargetCount(eventKey, count);
        }
    }

    private void SyncWithCurrentObjective(Objective objective, int currentProgress)
    {
        if (IsAlreadyCollected())
        {
            SetCanInteract(false);
            if (_deactivateOnPickup && gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
            return;
        }

        string eventKey = NormalizedEventName;
        bool isTargetObjective = objective != null &&
                                 objective.IsActive() &&
                                 !string.IsNullOrEmpty(eventKey) &&
                                 string.Equals(objective.EventToListen(), eventKey, StringComparison.OrdinalIgnoreCase);

        bool matchesDay = _requiredDay <= 0 ||
                          DayManager.Instance == null ||
                          DayManager.Instance.GetCurrentDay() <= 0 ||
                          DayManager.Instance.GetCurrentDay() == _requiredDay;

        SetCanInteract(isTargetObjective && matchesDay);
    }

    public override void Interact()
    {
        if (!_canInteract || IsAlreadyCollected())
        {
            if (UIManager.Instance != null && !string.IsNullOrWhiteSpace(_notAvailableMessage))
            {
                UIManager.Instance.ShowTemporaryMessage(
                    _notAvailableMessage,
                    UIManager.UnavailableInteractionColor,
                    2f);
            }

            ReturnPlayerToIdle();
            return;
        }

        _isCollectedLocal = true;

        if (DecisionDataManager.Instance != null)
        {
            DecisionDataManager.Instance.SetOrAddIntVariable(
                GetCollectedVariableKey(),
                1,
                "Tutorial/Objective item collected");
        }

        SetCanInteract(false);
        SetOutlineActive(false);
        UIManager.Instance?.HideMessage();
        InputManager.Instance?.SuppressInteract();

        // Must restore Idle synchronously before SetActive(false) because Invoke() won't run on disabled objects.
        ReturnPlayerToIdle();

        string eventKey = NormalizedEventName;
        if (!string.IsNullOrEmpty(eventKey) && EventBus.Instance != null)
        {
            EventBus.Instance.Publish(eventKey);
        }

        if (_deactivateOnPickup)
        {
            gameObject.SetActive(false);
        }
    }

    private string GetCollectedVariableKey()
    {
        string eventKey = NormalizedEventName;
        if (string.IsNullOrEmpty(eventKey))
        {
            eventKey = "Item";
        }
        return $"ObjectivePickup_{eventKey}_{EffectiveItemId}";
    }

    private bool IsAlreadyCollected()
    {
        if (_isCollectedLocal)
        {
            return true;
        }

        return DecisionDataManager.Instance != null &&
               DecisionDataManager.Instance.GetIntVariable(GetCollectedVariableKey(), 0) > 0;
    }

    private bool ShouldHideForCurrentDay()
    {
        return _requiredDay > 0 &&
               DayManager.Instance != null &&
               DayManager.Instance.GetCurrentDay() > _requiredDay;
    }

    private static void ReturnPlayerToIdle()
    {
        PlayerStateMachine playerState = FindAnyObjectByType<PlayerStateMachine>();
        if (playerState != null && playerState.GetCurrentState() == PlayerState.Interacting)
        {
            playerState.ChangeState(PlayerState.Idle);
        }
    }
}
