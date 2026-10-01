using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NarrativeNode", menuName = "DecisionSystem/NarrativeNode")]
[System.Serializable]
public class NarrativeNode
{
    [SerializeField]
    private int id;

    [SerializeField]
    private Option[] options = new Option[2];

    public int ID() { return id; }

    public Option[] Options() { return options; }
}

[System.Serializable]
public struct Option
{
    public string text;
    public int nextNode;

    public Message[] messagesToSend;
}
