using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public enum AnimatorParamType
{
    Trigger = 0,
    Bool = 1,
    Int = 2,
    Float = 3
}

/// <summary>
/// Declarative action to modify character postures or animation states (NPCs or Player).
/// </summary>
[Serializable]
public struct AnimatorParameterAction
{
    [Tooltip("Target character Animator whose posture or animation will change.")]
    public Animator targetAnimator;

    [Tooltip("Exact parameter name in the Animator Controller.")]
    public string parameterName;

    [Tooltip("Parameter type in the Animator.")]
    public AnimatorParamType parameterType;

    public bool boolValue;
    public int intValue;
    public float floatValue;

    public void Execute()
    {
        if (targetAnimator == null || string.IsNullOrEmpty(parameterName)) return;

        switch (parameterType)
        {
            case AnimatorParamType.Trigger:
                targetAnimator.SetTrigger(parameterName);
                break;
            case AnimatorParamType.Bool:
                targetAnimator.SetBool(parameterName, boolValue);
                break;
            case AnimatorParamType.Int:
                targetAnimator.SetInteger(parameterName, intValue);
                break;
            case AnimatorParamType.Float:
                targetAnimator.SetFloat(parameterName, floatValue);
                break;
        }
    }
}

/// <summary>
/// Modular collection of world actions (zones, NPCs, animations, narrative nodes, EventBus, and UnityEvents).
/// </summary>
[Serializable]
public class WorldActionGroup
{
    [Header("1. Zones and Objects (Activate / Deactivate)")]
    [Tooltip("GameObjects to activate (e.g. intervening ally NPC, unlocked safe route).")]
    public List<GameObject> objectsToActivate = new List<GameObject>();

    [Tooltip("GameObjects to deactivate (e.g. zone barrier, avoided bully group).")]
    public List<GameObject> objectsToDeactivate = new List<GameObject>();

    [Header("2. Interactables and NPCs (Enable / Disable)")]
    [Tooltip("Interactables (NPCData, doors, lockers) to make available for interaction.")]
    public List<IInteractable> interactablesToEnable = new List<IInteractable>();

    [Tooltip("Interactables to lock / disable from interaction.")]
    public List<IInteractable> interactablesToDisable = new List<IInteractable>();

    [Header("3. Character Postures and Animations")]
    [Tooltip("Animator parameter changes (e.g. defensive, intimidating, or friendly posture).")]
    public List<AnimatorParameterAction> animatorActions = new List<AnimatorParameterAction>();

    [Header("4. Narrative Node Updates (NPCs / Phone)")]
    [Tooltip("Updates the current narrative node of contacts/NPCs (and triggers phone notification if applicable).")]
    public List<ContactNodeUpdate> contactNodeUpdates = new List<ContactNodeUpdate>();

    [Header("5. Global Events (EventBus)")]
    [Tooltip("Events with or without payload to publish on the global EventBus.")]
    public List<EventPayload> eventsToPublish = new List<EventPayload>();

    [Header("6. Custom Callbacks (UnityEvent)")]
    [Tooltip("Arbitrary additional actions configurable from the Inspector.")]
    public UnityEvent onExecuted = new UnityEvent();

    public void Execute()
    {
        if (objectsToActivate != null)
        {
            foreach (var go in objectsToActivate)
            {
                if (go != null) go.SetActive(true);
            }
        }

        if (objectsToDeactivate != null)
        {
            foreach (var go in objectsToDeactivate)
            {
                if (go != null) go.SetActive(false);
            }
        }

        if (interactablesToEnable != null)
        {
            foreach (var inter in interactablesToEnable)
            {
                if (inter != null) inter.SetCanInteract(true);
            }
        }

        if (interactablesToDisable != null)
        {
            foreach (var inter in interactablesToDisable)
            {
                if (inter != null) inter.SetCanInteract(false);
            }
        }

        if (animatorActions != null)
        {
            foreach (var animAction in animatorActions)
            {
                animAction.Execute();
            }
        }

        if (contactNodeUpdates != null && contactNodeUpdates.Count > 0 && MessagesManager.Instance != null)
        {
            foreach (var update in contactNodeUpdates)
            {
                if (!string.IsNullOrEmpty(update.contactName) &&
                    ConditionsChecker.AreConditionsMet(update.conditions))
                {
                    MessagesManager.Instance.AdvanceContactNarrativeNodeByEvent(update.contactName, update.newNarrativeNodeId);
                }
            }
        }

        PublishEventPayloads(eventsToPublish);
        onExecuted?.Invoke();
    }

    private static void PublishEventPayloads(List<EventPayload> payloads)
    {
        if (payloads == null || payloads.Count == 0 || EventBus.Instance == null) return;

        foreach (var item in payloads)
        {
            if (item == null || string.IsNullOrEmpty(item.EventName())) continue;

            switch (item.DataType())
            {
                case EventDataType.NONE:
                    EventBus.Instance.Publish(item.EventName());
                    break;
                case EventDataType.STRING:
                    EventBus.Instance.Publish<string>(item.EventName(), (string)item.GetValue());
                    break;
                case EventDataType.INT:
                    EventBus.Instance.Publish<int>(item.EventName(), (int)item.GetValue());
                    break;
                case EventDataType.FLOAT:
                    EventBus.Instance.Publish<float>(item.EventName(), (float)item.GetValue());
                    break;
                case EventDataType.BOOL:
                    EventBus.Instance.Publish<bool>(item.EventName(), (bool)item.GetValue());
                    break;
            }
        }
    }
}

/// <summary>
/// Decoupled world action trigger based on Reputation / Safety conditions.
/// Can be evaluated on scene start, on real-time reputation changes,
/// upon key story events on the EventBus, or when the player enters a Trigger Collider.
/// </summary>
public class ReputationWorldTrigger : MonoBehaviour
{
    [Header("When to Evaluate (Triggers)")]
    [Tooltip("Evaluates conditions upon scene start (Start). Useful for setting initial postures or zones.")]
    [SerializeField] private bool _evaluateOnStart = true;

    [Tooltip("Listens to real-time reputation changes and evaluates when crossing thresholds.")]
    [SerializeField] private bool _evaluateOnReputationChange = true;

    [Tooltip("Evaluates when the player enters a Collider marked as 'Is Trigger' on this GameObject.")]
    [SerializeField] private bool _evaluateOnPlayerTriggerEnter = false;

    [Tooltip("Names of story events on the EventBus that trigger evaluation (e.g. 'Start_Day2', 'Day1_Convo1_End').")]
    [SerializeField] private List<string> _storyEventsToListen = new List<string>();

    [Header("Trigger Behavior")]
    [Tooltip("If true, actions execute only once after conditions are met.")]
    [SerializeField] private bool _triggerOnce = true;

    [Tooltip("If true, executes the 'Fallback Actions' block when conditions are NOT met.")]
    [SerializeField] private bool _executeFallbackWhenNotMet = false;

    [Header("Required Conditions (Logical AND via ConditionsChecker)")]
    [SerializeField] private List<Condition> _conditions = new List<Condition>();

    [Header("World Actions (If Conditions ARE Met)")]
    [SerializeField] private WorldActionGroup _actionsOnMet = new WorldActionGroup();

    [Header("Fallback World Actions (Optional: If Conditions Are NOT Met)")]
    [SerializeField] private WorldActionGroup _actionsOnNotMet = new WorldActionGroup();

    private bool _hasTriggered = false;
    private readonly Dictionary<string, Action> _eventSubscriptions = new Dictionary<string, Action>();

    private void Start()
    {
        SubscribeStoryEvents();

        if (_evaluateOnStart)
        {
            EvaluateAndExecute();
        }
    }

    private void OnEnable()
    {
        if (_evaluateOnReputationChange && ReputationManager.Instance != null)
        {
            ReputationManager.Instance.OnReputationChanged += HandleReputationChanged;
        }
    }

    private void OnDisable()
    {
        var repMgr = FindAnyObjectByType<ReputationManager>(FindObjectsInactive.Include);
        if (repMgr != null)
        {
            repMgr.OnReputationChanged -= HandleReputationChanged;
        }
    }

    private void OnDestroy()
    {
        UnsubscribeStoryEvents();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_evaluateOnPlayerTriggerEnter) return;
        if (_triggerOnce && _hasTriggered) return;

        if (IsPlayerCollider(other))
        {
            EvaluateAndExecute();
        }
    }

    private static bool IsPlayerCollider(Collider other)
    {
        if (other == null) return false;
        if (other.CompareTag("Player")) return true;
        return other.GetComponentInParent<PlayerController>() != null;
    }

    private void SubscribeStoryEvents()
    {
        if (_storyEventsToListen == null || EventBus.Instance == null) return;

        foreach (string eventName in _storyEventsToListen)
        {
            if (string.IsNullOrEmpty(eventName) || _eventSubscriptions.ContainsKey(eventName))
                continue;

            Action callback = EvaluateAndExecute;
            _eventSubscriptions[eventName] = callback;
            EventBus.Instance.Subscribe(eventName, callback);
        }
    }

    private void UnsubscribeStoryEvents()
    {
        if (EventBus.Instance == null) return;

        foreach (var kvp in _eventSubscriptions)
        {
            EventBus.Instance.Unsubscribe(kvp.Key, kvp.Value);
        }
        _eventSubscriptions.Clear();
    }

    private void HandleReputationChanged(int _)
    {
        if (_evaluateOnReputationChange)
        {
            EvaluateAndExecute();
        }
    }

    /// <summary>
    /// Evaluates configured conditions via ConditionsChecker and executes corresponding world actions.
    /// Can also be invoked manually from other scripts or UnityEvents.
    /// </summary>
    public void EvaluateAndExecute()
    {
        if (_triggerOnce && _hasTriggered)
        {
            return;
        }

        bool met = ConditionsChecker.AreConditionsMet(_conditions);
        if (met)
        {
            _hasTriggered = true;
            Debug.Log($"[ReputationWorldTrigger] Conditions met on '{gameObject.name}'. Executing World Actions.");
            _actionsOnMet?.Execute();
        }
        else if (_executeFallbackWhenNotMet)
        {
            Debug.Log($"[ReputationWorldTrigger] Conditions NOT met on '{gameObject.name}'. Executing Fallback World Actions.");
            _actionsOnNotMet?.Execute();
        }
    }

    /// <summary>
    /// Resets the one-time trigger flag to allow the trigger to fire again.
    /// </summary>
    public void ResetTrigger()
    {
        _hasTriggered = false;
    }
}
