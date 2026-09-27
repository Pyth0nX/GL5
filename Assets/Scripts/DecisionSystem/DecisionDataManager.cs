using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manager that holds and manages decision variables for the decision system.
/// Allows adding, modifying, and retrieving decision variables.
/// </summary>
public class DecisionDataManager : MonoBehaviour
{
    public static DecisionDataManager Instance { get; private set; }

    /// <summary>
    /// Dictionary to hold variables grouped by their DataType
    /// </summary>
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

    /// <summary>
    /// Gets the raw value of a decision variable as an object.
    /// </summary>
    /// <param name="type">The data type of the variable.</param>
    /// <param name="name">The name of the variable.</param>
    /// <returns>The value of the variable, or null if not found.</returns>
    public object GetDecisionVariableValue(DataType type, string name)
    {
        var result = FindDecisionVariableIndex(type, name);
        if (result.found)
        {
            return _decisionData[type][result.index].value;
        }
        return null;
    }

    /// <summary>
    /// Helper to find the index inside the list so we can modify it directly
    /// </summary>
    /// <param name="type">The data type of the variable.</param>
    /// <param name="name">The name of the variable.</param>
    /// <returns>A tuple indicating whether the variable was found and its index.</returns>
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

    /// <summary>
    /// Modifies the value of the variable safely (Directly on the list item)
    /// </summary>
    /// <param name="type">The data type of the variable.</param>
    /// <param name="name">The name of the variable.</param>
    /// <param name="newValue">The new value for the variable.</param>
    /// <returns>True if the variable was found and modified, false otherwise.</returns>
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

    /// <summary>
    /// Adds the given variable to the list of the given type
    /// </summary>
    /// <param name="type">The data type of the variable.</param>
    /// <param name="variable">The variable to add.</param>
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