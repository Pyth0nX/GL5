using System;
using UnityEngine;

public enum DataType
{
    Integer,
    Float,
    String,
    Boolean,
    Custom
}

[Serializable]
public struct DecisionVariable
{
    // Name of the data this contains (e.g. "Reputation")
    public string name;

    // Description of what this data is used for
    public string description;

    // Runtime value of the data this contains (could be any type)
    [NonSerialized]
    public object value;

    public DecisionVariable(string name, object value, string description = "")
    {
        this.name = name;
        this.description = description;
        this.value = value;
    }
}
