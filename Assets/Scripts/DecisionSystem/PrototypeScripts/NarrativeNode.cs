using System;
using System.Collections.Generic;
using UnityEngine;
[System.Serializable]
public enum DialogueType
{
    IN_PERSON,
    PHONE
}


[CreateAssetMenu(fileName = "NarrativeNode", menuName = "DecisionSystem/NarrativeNode")]
[System.Serializable]
public class NarrativeNode
{
    [SerializeField]
    private int _id;

    [SerializeField]
    private int _nextNode = -1; // The next node to go to after this one, if none it's -1. Onky used if there are no options to choose from, otherwise the next node is determined by the option chosen.

    [SerializeField]
    private Option[] _options = new Option[2];

    [SerializeField]
    // List of dialogues to show when this node is reached (for both , in person and phone conversations)
    private List<Message> _dialogues = new List<Message>(); 

    [SerializeField]
    private bool _isEndNode = false; // Whether this node is an end node or not (end of the current state)

    [SerializeField]
    private int _loopNode = -1; // The node to loop back to , if none it's -1

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

    #region Getters
    public int ID() { return _id; }

    public string EventOnEnd() { return _eventOnEnd; }

    public int NextNode() { return _nextNode; }
    
    public Option[] Options() { return _options; }

    public bool HasOptions() { return _hasOptions; }

    public List<Message> Dialogues() { return _dialogues; }
    
    public bool IsEndNode() { return _isEndNode; }
    
    public int LoopNode() { return _loopNode; }
    
    public DialogueType DialogueType() { return _dialogueType; }

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
