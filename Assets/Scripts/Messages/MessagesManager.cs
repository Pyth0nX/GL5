using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;


/// <summary>
/// Holds the list of contacts and their messages, and manages the current contact being interacted with.
/// </summary>
public class MessagesManager : MonoBehaviour
{
    public static MessagesManager Instance { get; private set; }

    private List<Contact> _contacts; // List of all contacts in the game

    private Contact _currentContact; // The contact that is currently being interacted with

    private MessagesUI _messagesUI; // Reference to the MessagesUI component for updating the UI

    private PhoneStateMachine _phoneStateMachine; // Reference to the PhoneStateMachine component

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        CreateContacts();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Gets the MessagesUI component attached to the same GameObject as this script
        _messagesUI = GetComponent<MessagesUI>();

        // Gets the PhoneStateMachine component attached to the same GameObject as this script
        _phoneStateMachine = GetComponent<PhoneStateMachine>();
    }


    // Update is called once per frame
    void Update()
    {
        
    }

    /// <summary>
    /// Returns the list of all contacts in the game.
    /// </summary>
    /// <returns></returns>
    public List<Contact> GetContacts()
    {
        return _contacts;
    }

    public void AddContact(Contact contact)
    {
        if (_contacts == null)
        {
            _contacts = new List<Contact>();
        }
        _contacts.Add(contact);

        // sort the contacts list so it is always in order of the last message date
        SortContacts();
    }


    /// <summary>
    /// Sets the current contact that the user is interacting with.
    /// </summary>
    /// <param name="contactName"></param>
    public void SetCurrentContact(string contactName)
    {
        _currentContact = _contacts.Find(c => c.GetName() == contactName);
    }

    /// <summary>
    /// Returns the current contact that the user is interacting with.
    /// </summary>
    /// <returns></returns>
    public Contact GetCurrentContact()
    {
        return _currentContact;
    }


    /// <summary>
    /// Update the message list of the current contact with a new message
    /// </summary>
    /// <param name="message"></param>
    public void SendMessageToCurrentContact(Message message)
    {
        if (_currentContact == null)
        {
            Debug.LogWarning("No current contact selected.");
            return;
        }
        _currentContact.GetMessages().Add(message);


        // Update the last message date for the current contact
        _currentContact.GetLastMessageDate() = message.timestamp;

        // Add the message to the UI
        _messagesUI.AddMessage(message);
        SortContacts();
    }

    /// <summary>
    /// Sorts the contacts list based on the last message date, with the most recent messages appearing first.
    /// </summary>
    private void SortContacts() { 
        _contacts.Sort((c1, c2) => c2.GetLastMessageDate().CompareTo(c1.GetLastMessageDate()));
    }

    /// <summary>
    /// Returns the PhoneStateMachine component attached to the same GameObject as this script.
    /// </summary>
    /// <returns></returns>
    public PhoneStateMachine GetPhoneStateMachine()
    {
        return _phoneStateMachine;
    }

    /// <summary>
    /// Returns the MessagesUI component attached to the same GameObject as this script.
    /// </summary>
    /// <returns></returns>
    public MessagesUI GetMessagesUI()
    {
        return _messagesUI;
    }

    private void CreateContacts()
    {
        Contact contact1 = new Contact();
        contact1.SetName("Alice");
        Message message1_1 = new Message("Hello!", Sender.Player, new Date());
        Message message1_2 = new Message("How are you?", Sender.Player, new Date());
        Message message1_3 = new Message("I'm doing well, thanks!", Sender.NPC, new Date());
        Message message1_4 = new Message("What about you?", Sender.NPC, new Date());
        Message message1_5 = new Message("I'm good too!", Sender.Player, new Date());
        contact1.SetMessages(new List<Message> { message1_1, message1_2, message1_3, message1_4, message1_5 });
        AddContact(contact1);

        Contact contact2 = new Contact();
        contact2.SetName("Unknown Number");
        Message message2_1 = new Message("Why did you even show up to school today?", Sender.NPC, new Date());
        Message message2_2 = new Message("Everyone was talking about you behind your back.", Sender.NPC, new Date());
        Message message2_3 = new Message("Who is this? Leave me alone.", Sender.Player, new Date());
        Message message2_4 = new Message("Don't play dumb. Nobody wants you in our group.", Sender.NPC, new Date());
        Message message2_5 = new Message("If you post that picture, I'm calling the principal.", Sender.Player, new Date());
        Message message2_6 = new Message("Go ahead and try. Nobody will believe you anyway.", Sender.NPC, new Date());
        contact2.SetMessages(new List<Message> { message2_1, message2_2, message2_3, message2_4, message2_5, message2_6 });
        AddContact(contact2);


        Contact contact3 = new Contact();
        contact3.SetName("Mom");
        Message message3_1 = new Message("Don't forget to take the keys with you!", Sender.NPC, new Date());
        Message message3_2 = new Message("Got them! Will be home around 6 PM.", Sender.Player, new Date());
        Message message3_3 = new Message("Perfect, dinner will be ready by then.", Sender.NPC, new Date());
        contact3.SetMessages(new List<Message> { message3_1, message3_2, message3_3 });
        AddContact(contact3);


        Contact contact4 = new Contact();
        contact4.SetName("Mark (History Group)");
        Message message4_1 = new Message("Hey, did you finish your part of the presentation?", Sender.NPC, new Date());
        Message message4_2 = new Message("Almost done, just adding the last slides.", Sender.Player, new Date());
        Message message4_3 = new Message("Awesome, send it over when you're done so I can review it.", Sender.NPC, new Date());
        Message message4_4 = new Message("Will do in 10 minutes!", Sender.Player, new Date());
        contact4.SetMessages(new List<Message> { message4_1, message4_2, message4_3, message4_4 });
        AddContact(contact4);
    }

}
