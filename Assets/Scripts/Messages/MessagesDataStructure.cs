using NUnit.Framework;
using System;
using UnityEngine;
using System.Collections.Generic;


public enum Sender
{
    Player,
    NPC
}

[System.Serializable]
public struct Date : IComparable<Date>
{
    public int day, month, year;

    public int hour, minute;

    public int CompareTo(Date other)
    {
        int result = year.CompareTo(other.year);
        if (result != 0) return result;

        result = month.CompareTo(other.month);
        if (result != 0) return result;

        result = day.CompareTo(other.day);
        if (result != 0) return result;

        result = hour.CompareTo(other.hour);
        if (result != 0) return result;

        return minute.CompareTo(other.minute);
    }

}

[System.Serializable]
public class Message
{
    [SerializeField]
    private string _content; //Content of the message
    [SerializeField]
    private Sender _sender; // Who sent the message 
    [SerializeField]
    private Date _timestamp; // Time when the message was sent

    public string Content() { return _content; }
    public Sender Sender() { return _sender; }
    public Date Timestamp() { return _timestamp; }


    public Message(string content, Sender sender, Date timestamp)
    {
        this._content = content;
        this._sender = sender;
        this._timestamp = timestamp;
    }
}

[Serializable]
public class Contact 
{
    [SerializeField]
    private string _contactName;

    [SerializeField]
    private string _contactImagePath;

    [SerializeField]
    private int _currentNarrative; //Current narrative node 

    [SerializeField]
    private Date _lastMessageDate; // Date of the last message sent or received

    #region Getters
    public int NarrativeNode() { return _currentNarrative; }
    public Date LastMessageDate() { return _lastMessageDate; }
    public string ImagePath() { return _contactImagePath; }
    public string Name() { return _contactName; }


    #endregion

    #region Setters
    public void SetNarrativeNode(int node) { _currentNarrative = node; }
    public void SetLastMessageDate(Date date) { _lastMessageDate = date; }
    public void SetImagePath(string imagePath) { _contactImagePath = imagePath; }
    public void SetName(string name) { _contactName = name; }

    #endregion

}


[System.Serializable]
public class ContactPhoneMessages
{
    [SerializeField]
    private string _contactName;

    [SerializeField]
    private List<Message> _messages;

    public List<Message> Messages() { return _messages; }

    public string ContactName() { return _contactName; }
}

[System.Serializable]
public class ContactListWrapper
{
    public List<Contact> contacts;
}


[System.Serializable]
public class PhoneMessageListWrapper
{
    public List<ContactPhoneMessages> contactPhoneMessages;
}


[System.Serializable]
public class NarrativeNodeListWrapper
{
    public List<NarrativeNode> narrativeNodes;
}