using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manager that holds and manages decision variables for the decision system.
/// Allows adding, modifying, and retrieving decision variables, and notifies listeners on change.
/// </summary>
public class DecisionDataManager : MonoBehaviour
{
    public static DecisionDataManager Instance { get; private set; }

    /// <summary>
    /// Fired whenever a variable is added or modified: (variableName, newValue).
    /// </summary>
    public event Action<string, object> OnVariableChanged;

    /// <summary>
    /// Dictionary to hold variables grouped by their DataType
    /// </summary>
    private Dictionary<DataType, List<DecisionVariable>> _decisionData = new Dictionary<DataType, List<DecisionVariable>>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject); // Persist across scenes
            }
        }
        else if (Instance != this)
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
        if (string.IsNullOrEmpty(name)) return null;

        var result = FindDecisionVariableIndex(type, name);
        if (result.found)
        {
            return _decisionData[type][result.index].value;
        }
        return null;
    }

    /// <summary>
    /// Convenience method to retrieve an integer variable value, returning defaultValue if not registered.
    /// </summary>
    public int GetIntVariable(string name, int defaultValue = 0)
    {
        object raw = GetDecisionVariableValue(DataType.Integer, name);
        if (raw is int intVal)
        {
            return intVal;
        }
        if (raw != null && int.TryParse(raw.ToString(), out int parsed))
        {
            return parsed;
        }
        return defaultValue;
    }

    /// <summary>
    /// Tries to retrieve an integer variable value.
    /// </summary>
    public bool TryGetIntVariable(string name, out int value)
    {
        object raw = GetDecisionVariableValue(DataType.Integer, name);
        if (raw is int intVal)
        {
            value = intVal;
            return true;
        }
        if (raw != null && int.TryParse(raw.ToString(), out int parsed))
        {
            value = parsed;
            return true;
        }
        value = 0;
        return false;
    }

    /// <summary>
    /// Sets an integer variable if it already exists, or creates it if it does not.
    /// </summary>
    public void SetOrAddIntVariable(string name, int newValue, string description = "")
    {
        if (string.IsNullOrEmpty(name)) return;

        if (!ModifyVariable(DataType.Integer, name, newValue))
        {
            AddVariable(DataType.Integer, new DecisionVariable(name, newValue, description));
        }
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
            int index = _decisionData[type].FindIndex(v => string.Equals(v.name, name, StringComparison.OrdinalIgnoreCase));
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
            OnVariableChanged?.Invoke(variable.name, newValue);
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
        int existingIndex = _decisionData[type].FindIndex(v => string.Equals(v.name, variable.name, StringComparison.OrdinalIgnoreCase));
        if (existingIndex == -1)
        {
            _decisionData[type].Add(variable);
            OnVariableChanged?.Invoke(variable.name, variable.value);
        }
    }
}
