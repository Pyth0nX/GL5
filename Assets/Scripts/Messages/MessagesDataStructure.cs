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
    public string content; //Content of the message
    public Sender sender; // Who sent the message 
    public Date timestamp; // Time when the message was sent


    public Message(string content, Sender sender, Date timestamp)
    {
        this.content = content;
        this.sender = sender;
        this.timestamp = timestamp;
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
    private bool _decisionAvailable; // Whether the contact has a decision available for the player

    [SerializeField]
    private bool _isActive; // Whether the contact is currently active in the game

    private int _currentNarrative; //Current narrative node 

    [SerializeField]
    private Date _lastMessageDate; // Date of the last message sent or received

    [SerializeField]
    private List<Message> _messages; // All time messages with this contact

    #region Getters
    public int NarrativeNode() { return _currentNarrative; }
    public bool IsDecisionAvailable() { return _decisionAvailable; }
    public Date LastMessageDate() { return _lastMessageDate; }
    public List<Message> Messages() { return _messages; }
    public string ImagePath() { return _contactImagePath; }
    public string Name() { return _contactName; }

    public bool IsActive() { return _isActive; }

    #endregion

    #region Setters
    public void SetNarrativeNode(int node) { _currentNarrative = node; }
    public void SetLastMessageDate(Date date) { _lastMessageDate = date; }
    public void SetDecisionAvailable(bool available) { _decisionAvailable = available; }
    public void SetMessages(List<Message> messages) { _messages = messages; }
    public void SetImagePath(string imagePath) { _contactImagePath = imagePath; }
    public void SetName(string name) { _contactName = name; }
    public void SetActive(bool isActive) { _isActive = isActive; }

    #endregion

}


[System.Serializable]
public class ContactListWrapper
{
    public List<Contact> contacts;
}


[System.Serializable]
public class MessageListWrapper
{
    public List<Message> messages;
}


[System.Serializable]
public class NarrativeNodeListWrapper
{
    public List<NarrativeNode> narrativeNodes;
}