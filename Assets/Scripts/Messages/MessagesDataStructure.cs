using NUnit.Framework;
using System;
using UnityEngine;
using System.Collections.Generic;


public enum Sender
{
    Player,
    NPC
}

[Serializable]
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

[Serializable]
public class Message
{
    public string messageContent; //Content of the message
    public Sender sender; // Who sent the message 
    public Date timestamp; // Time when the message was sent


    public Message(string content, Sender sender, Date timestamp)
    {
        this.messageContent = content;
        this.sender = sender;
        this.timestamp = timestamp;
    }
}

[Serializable]
public class Contact 
{
    private string _contactName;

    private Sprite _contactImage;

    private List<Message> _messages; // All time messages with this contact

    private Date _lastMessageDate; // Date of the last message sent or received

    public string GetName() { return _contactName; }
    public void SetName(string name) { _contactName = name; }

    public Sprite GetImage() { return _contactImage; }

    public void SetImage(Sprite image) { _contactImage = image; }

    public List<Message> GetMessages() { return _messages; }

    public void SetMessages(List<Message> messages) { _messages = messages; }

    public ref Date GetLastMessageDate() { return ref _lastMessageDate; }

    public void SetLastMessageDate(Date date) { _lastMessageDate = date; }
}


