using UnityEngine;

[System.Serializable]
public class PhoneMessage
{
    public string sender;

    [TextArea(2, 5)]
    public string message;
}