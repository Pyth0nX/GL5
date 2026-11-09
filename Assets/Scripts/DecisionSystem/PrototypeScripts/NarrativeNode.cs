using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public enum DialogueType
{
    IN_PERSON,
    PHONE
}

/// <summary>
/// Defines how a narrative option behaves in the interface when its reputation conditions are NOT met.
/// </summary>
[System.Serializable]
public enum UnavailableOptionBehavior
{
    Hide = 0,          // Hides the option and automatically readjusts the layout of remaining buttons
    DisableButton = 1  // Shows the disabled button (not clickable) with optional lock text
}

/// <summary>
/// Conditional branch leading to another narrative node, evaluated using ConditionsChecker.
/// Branches are evaluated in order and the first one with matching conditions is selected.
/// </summary>
[System.Serializable]
public class ConditionalNodeBranch
{
    [Tooltip("Optional descriptive note to identify the branch in the Inspector (e.g., 'High Reputation >= 70').")]
    public string description;

    [Tooltip("List of conditions (logical AND) that must be met to take this branch.")]
    public List<Condition> conditions = new List<Condition>();

    [Tooltip("Node ID of the destination NarrativeNode if all conditions are met.")]
    public int nextNode = -1;
}

[CreateAssetMenu(fileName = "NarrativeNode", menuName = "DecisionSystem/NarrativeNode")]
[System.Serializable]
public class NarrativeNode
{
    [SerializeField]
    private int _id;

    [SerializeField]
    private int _nextNode = -1; // The next node to go to after this one, if none it's -1. Used as fallback if no conditional branch matches.

    [SerializeField]
    private Option[] _options = new Option[2];

    [SerializeField]
    // List of dialogues to show when this node is reached (for both, in person and phone conversations)
    private List<Message> _dialogues = new List<Message>(); 

    [SerializeField]
    private bool _isEndNode = false; // Whether this node is an end node or not (end of the current state)

    [Tooltip("If true, finishing the interaction with this node will return to this node (or _loopNode), allowing NPCs to repeat default phrases.")]
    [SerializeField]
    private bool _loop = false; // Whether this node loops back upon finishing

    [SerializeField]
    private int _loopNode = -1; // The node to loop back to, if none it's -1

    [SerializeField]
    private DialogueType _dialogueType;

    [SerializeField]
    private bool _hasOptions = false; // Whether this node has options to choose from or not

    [SerializeField]
    private string _eventOnEnd; // Event to fire when this node's dialogue finishes

    [SerializeField]
    private EventDataType _eventDataType;
    [SerializeField]
    private string _eventDataRawValue;

    [Header("Reputation / Safety — Impact on Finishing the Node")]
    [Tooltip("Optional operation on Reputation/Safety when finishing this narrative node.")]
    [SerializeField]
    private ReputationOperation _reputationOperationOnEnd = ReputationOperation.None;

    [Tooltip("Value of Reputation/Safety to add, subtract or set when finishing this node.")]
    [SerializeField]
    private int _reputationAmountOnEnd = 0;

    [Header("Reputation / Safety — Conditional Branches")]
    [Tooltip("Optional: Redirects immediately to another node BEFORE playing this node if the conditions are met (useful when starting a conversation with an NPC).")]
    [SerializeField]
    private List<ConditionalNodeBranch> _entryBranches = new List<ConditionalNodeBranch>();

    [Tooltip("Optional: Evaluates these branches in order when finishing the node. If none match, jumps to '_nextNode'.")]
    [SerializeField]
    private List<ConditionalNodeBranch> _conditionalNextNodes = new List<ConditionalNodeBranch>();

    #region Getters
    public int ID() { return _id; }

    public string EventOnEnd() { return _eventOnEnd; }

    public EventDataType EndEventDataType() { return _eventDataType; }

    public int NextNode() { return _nextNode; }
    
    public Option[] Options() { return _options; }

    public bool HasOptions() { return _hasOptions; }

    public List<Message> Dialogues() { return _dialogues; }

    /// <summary>
    /// Returns only the dialogue lines whose Reputation/State conditions are currently met.
    /// If no lines define conditions, returns the original list intact.
    /// </summary>
    public List<Message> GetActiveDialogues()
    {
        if (_dialogues == null || _dialogues.Count == 0)
        {
            return new List<Message>();
        }

        // Optimization: if no message has conditions, return the list as is
        bool anyConditional = false;
        for (int i = 0; i < _dialogues.Count; i++)
        {
            var msg = _dialogues[i];
            if (msg != null && msg.Conditions() != null && msg.Conditions().Count > 0)
            {
                anyConditional = true;
                break;
            }
        }

        if (!anyConditional)
        {
            return _dialogues;
        }

        List<Message> filtered = new List<Message>(_dialogues.Count);
        for (int i = 0; i < _dialogues.Count; i++)
        {
            var msg = _dialogues[i];
            if (msg != null && msg.AreConditionsMet())
            {
                filtered.Add(msg);
            }
        }
        return filtered;
    }
    
    public bool IsEndNode() { return _isEndNode; }
    
    public bool Loop() { return _loop || _loopNode >= 0; }

    public bool IsLoop() { return _loop || _loopNode >= 0; }

    public int LoopNode() { return _loopNode; }

    /// <summary>
    /// Returns the target node ID to loop back to. Prefers _loopNode if specified; otherwise uses _nextNode if set, or defaults to this node's own ID.
    /// </summary>
    public int GetLoopTargetNodeId()
    {
        if (_loopNode >= 0) return _loopNode;
        if (_nextNode >= 0) return _nextNode;
        return _id;
    }
    
    public DialogueType DialogueType() { return _dialogueType; }

    public ReputationOperation ReputationOperationOnEnd() { return _reputationOperationOnEnd; }

    public int ReputationAmountOnEnd() { return _reputationAmountOnEnd; }

    public List<ConditionalNodeBranch> EntryBranches() { return _entryBranches; }

    public List<ConditionalNodeBranch> ConditionalNextNodes() { return _conditionalNextNodes; }

    /// <summary>
    /// If this node defines entry redirection branches (_entryBranches) and one is met,
    /// returns the alternative node ID; otherwise returns this node's own _id.
    /// </summary>
    public int ResolveEntryNodeId()
    {
        if (_entryBranches != null)
        {
            for (int i = 0; i < _entryBranches.Count; i++)
            {
                var branch = _entryBranches[i];
                if (branch != null && branch.nextNode >= 0 && ConditionsChecker.AreConditionsMet(branch.conditions))
                {
                    return branch.nextNode;
                }
            }
        }
        return _id;
    }

    /// <summary>
    /// Evaluates _conditionalNextNodes in order using ConditionsChecker.
    /// Returns the first nextNode whose conditions are met, or _nextNode by default.
    /// </summary>
    public int ResolveNextNode()
    {
        if (_conditionalNextNodes != null)
        {
            for (int i = 0; i < _conditionalNextNodes.Count; i++)
            {
                var branch = _conditionalNextNodes[i];
                if (branch != null && ConditionsChecker.AreConditionsMet(branch.conditions))
                {
                    return branch.nextNode;
                }
            }
        }
        return _nextNode;
    }

    /// <summary>
    /// Applies the Reputation/Safety impact configured upon finishing this node (if any).
    /// </summary>
    public void ApplyEndReputationImpact()
    {
        if (_reputationOperationOnEnd != ReputationOperation.None && ReputationManager.Instance != null)
        {
            ReputationManager.Instance.ApplyOperation(_reputationOperationOnEnd, _reputationAmountOnEnd);
        }
    }

    public object EventDataValue()
    {
        return _eventDataType switch
        {
            EventDataType.INT => int.Parse(_eventDataRawValue),
            EventDataType.FLOAT => float.Parse(_eventDataRawValue, System.Globalization.CultureInfo.InvariantCulture),
            EventDataType.BOOL => bool.Parse(_eventDataRawValue),
            EventDataType.STRING => _eventDataRawValue,
            EventDataType.NONE => null,
            _ => null
        };
    }
    #endregion

    #region Setters & Helpers
    public void SetLoop(bool value)
    {
        _loop = value;
    }

    public void SetLoopNode(int node)
    {
        _loopNode = node;
    }

    public void AddDialogue(Message message)
    {
        if (_dialogues == null) _dialogues = new List<Message>();
        _dialogues.Add(message);
    }

    public void AddImageDialogue(string imagePath, Sender sender, string caption = "", string speakerName = "")
    {
        if (_dialogues == null) _dialogues = new List<Message>();
        _dialogues.Add(new Message(caption, sender, speakerName, new Date(), imagePath, MessageType.IMAGE));
    }
    #endregion
}

[System.Serializable]
public class Option
{
    public string text;
    public int nextNode;

    public Message[] messagesToSend;
    public string eventToPublish; // Optional event to publish when this option is chosen
    public EventDataType eventDataType = EventDataType.NONE; // The type of data of the event
    public string eventDataRawValue; // raw value of the event data

    [Header("Reputation / Safety — Impact on Choosing Option")]
    [Tooltip("Operation on Reputation/Safety when choosing this decision (None, Add, Subtract, Set).")]
    public ReputationOperation reputationOperation = ReputationOperation.None;

    [Tooltip("Numeric value to add, subtract or set in Reputation/Safety.")]
    public int reputationAmount = 0;

    [Header("Reputation / Safety — Conditions for Unlocking")]
    [Tooltip("Conditions necessary for this option to be available. Empty = always available.")]
    public List<Condition> conditions = new List<Condition>();

    [Tooltip("What happens if the conditions are not met: Hide (hides the button) or DisableButton (shows as blocked).")]
    public UnavailableOptionBehavior unavailableBehavior = UnavailableOptionBehavior.Hide;

    [Tooltip("Alternative text optional when unavailableBehavior == DisableButton and the option is locked.")]
    public string lockedText;

    [Header("Reputation / Safety — Conditional Branches")]
    [Tooltip("Optional: Allows choosing this same option to lead to different nodes based on current Reputation. If no branch matches, uses 'nextNode'.")]
    public List<ConditionalNodeBranch> conditionalNextNodes = new List<ConditionalNodeBranch>();

    /// <summary>
    /// Checks if all conditions for this option are currently met using ConditionsChecker.
    /// </summary>
    public bool AreConditionsMet()
    {
        return ConditionsChecker.AreConditionsMet(conditions);
    }

    /// <summary>
    /// Applies the reputation/safety modification configured for this option.
    /// </summary>
    public void ApplyReputationImpact()
    {
        if (reputationOperation != ReputationOperation.None && ReputationManager.Instance != null)
        {
            ReputationManager.Instance.ApplyOperation(reputationOperation, reputationAmount);
        }
    }

    /// <summary>
    /// Resolves the next node by evaluating conditionalNextNodes first and using nextNode as the default value.
    /// </summary>
    public int ResolveNextNode()
    {
        if (conditionalNextNodes != null)
        {
            for (int i = 0; i < conditionalNextNodes.Count; i++)
            {
                var branch = conditionalNextNodes[i];
                if (branch != null && ConditionsChecker.AreConditionsMet(branch.conditions))
                {
                    return branch.nextNode;
                }
            }
        }
        return nextNode;
    }

    public object EventDataValue()
    {
        return eventDataType switch
        {
            EventDataType.INT => int.Parse(eventDataRawValue),
            EventDataType.FLOAT => float.Parse(eventDataRawValue, System.Globalization.CultureInfo.InvariantCulture),
            EventDataType.BOOL => bool.Parse(eventDataRawValue),
            EventDataType.STRING => eventDataRawValue,
            EventDataType.NONE => null,
            _ => null
        };
    }
}
