using UnityEngine;
using System.Collections.Generic;

public class DecisionDataManager : MonoBehaviour
{
    public static DecisionDataManager Instance { get; private set; }

    // Dictionary to hold variables grouped by their DataType
    private Dictionary<DataType, List<DecisionVariable>> _decisionData = new Dictionary<DataType, List<DecisionVariable>>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Persist across scenes
        }
        else
        {
            Destroy(gameObject); // Ensure only one instance exists
        }
    }

    // Get the raw value as an object
    public object GetDecisionVariableValue(DataType type, string name)
    {
        var result = FindDecisionVariableIndex(type, name);
        if (result.found)
        {
            return _decisionData[type][result.index].value;
        }
        return null;
    }

    // Helper to find the index inside the list so we can modify it directly
    private (bool found, int index) FindDecisionVariableIndex(DataType type, string name)
    {
        if (_decisionData.ContainsKey(type))
        {
            int index = _decisionData[type].FindIndex(v => v.name == name);
            if (index != -1)
            {
                return (true, index);
            }
        }
        return (false, -1);
    }

    // Modifies the value of the variable safely (Directly on the list item)
    public bool ModifyVariable(DataType type, string name, object newValue)
    {
        var result = FindDecisionVariableIndex(type, name);
        if (result.found)
        {
            // We modify it directly from the dictionary list reference
            DecisionVariable variable = _decisionData[type][result.index];
            variable.value = newValue;
            _decisionData[type][result.index] = variable; // Assign back the modified struct
            return true;
        }
        return false;
    }

    // Adds the given variable to the list of the given type
    public void AddVariable(DataType type, DecisionVariable variable)
    {
        if (!_decisionData.ContainsKey(type))
        {
            _decisionData[type] = new List<DecisionVariable>();
        }

        // Prevent duplicates if needed
        int existingIndex = _decisionData[type].FindIndex(v => v.name == variable.name);
        if (existingIndex == -1)
        {
            _decisionData[type].Add(variable);
        }
    }
}