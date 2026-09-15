using UnityEngine;

[CreateAssetMenu(fileName = "NarrativeNode", menuName = "DecisionSystem/NarrativeNode")]
public class NarrativeNode : ScriptableObject
{
    [TextArea(3, 10)]
    public string narrativeText;

    public Option[] options;
}

[System.Serializable]
public struct Option
{
    public string optionText;
    public NarrativeNode nextNode;

    //public DecisionData[] decisionData;
}
