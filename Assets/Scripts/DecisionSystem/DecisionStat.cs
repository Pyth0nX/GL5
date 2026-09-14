using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "DecisionStat", menuName = "DecisionSystem/DecisionStat")]
public class DecisionStat : ScriptableObject
{
    [TextArea] public string description;
}
