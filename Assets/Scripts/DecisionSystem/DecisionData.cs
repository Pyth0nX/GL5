using UnityEngine;
public enum DataType
{
    Integer,
    Float,
    String,
    Boolean,
    Custom
}

public struct DecisionVariable 
{
    //name of the data this contains
    public string name;
    // description of for what is this data used for
    public string description;
    // type of the data this contains (could be any type)
    public object value;
}
