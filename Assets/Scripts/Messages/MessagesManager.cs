using NUnit.Framework;
using NUnit.Framework.Interfaces;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Name = System.String;



/// <summary>
/// Holds the list of contacts and their messages, and manages the current contact being interacted with.
/// </summary>
public class MessagesManager : MonoBehaviour
{
    public static MessagesManager Instance { get; private set; }


    private string _currentContact; // The contact that is currently being interacted with

    private MessagesUI _messagesUI; // Reference to the MessagesUI component for updating the UI

    private PhoneStateMachine _phoneStateMachine; // Reference to the PhoneStateMachine component

    private OptionsChanger _optionsChanger; // Reference to the OptionsChanger component for updating the options UI

    [SerializeField]
    private Dictionary<Name, List<Message>> _contactsMessages = new(); // Dictionary of List of messages for each contact, keyed by contact name

    [SerializeField]
    private List<Contact> _contacts = new(); // List of all contacts in the game

    [SerializeField]
    private Dictionary<int, NarrativeNode> _narrativeNodes = new(); // All the narrative nodes in the game, keyed by node ID

    [SerializeField]
    private string contactsFilePath = "contacts.json";

    [SerializeField]
    private string narrativeNodesFilePath = "narrativeNodes.json";
    
    [SerializeField]
    private string phoneMessagesFilePath = "messages.json";

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

        CreateNarrativeNodes();
        CreateContacts();
        CreateMessages();
    }

    private void CreateNarrativeNodes()
    {
        List<NarrativeNode> nodesList = DataLoader.LoadNarrativeNodes(narrativeNodesFilePath);

        if (nodesList != null)
        {
            foreach (NarrativeNode node in nodesList)
            {
                _narrativeNodes[node.ID()] = node;
            }
        }
        else
        {
            _narrativeNodes = new Dictionary<int, NarrativeNode>();
            Debug.LogError("No se pudieron cargar los nodos narrativos.");
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Gets the MessagesUI component attached to the same GameObject as this script
        _messagesUI = GetComponent<MessagesUI>();

        // Gets the PhoneStateMachine component attached to the same GameObject as this script
        _phoneStateMachine = GetComponent<PhoneStateMachine>();

        // Gets the OptionsChanger component attached to the same GameObject as this script
        _optionsChanger = GetComponent<OptionsChanger>();
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
        //Sort the contacts list so it is always in order of the last message date
        SortContacts();

        // Return a new list of contacts from the dictionary values
        List<Contact> contactsList = new List<Contact>(_contacts);
        return contactsList;
    }

    /// <summary>
    /// Sets the current contact that the user is interacting with.
    /// </summary>
    /// <param name="contactName"></param>
    public void SetCurrentContact(string contactName)
    {
        _currentContact = contactName;
    }

    /// <summary>
    /// Returns the current contact that the user is interacting with.
    /// </summary>
    /// <returns></returns>
    public Contact GetCurrentContact()
    {
        return _contacts.Find(contact => contact.Name() == _currentContact);
    }

    public Contact GetContactByName(string contactName)
    {
        return _contacts.Find(contact => contact.Name() == contactName);
    }

    public string GetCurrentContactName()
    {
        return _currentContact;
    }

    /// <summary>
    /// Returns the current contact that the user is interacting with.
    /// </summary>
    /// <returns></returns>
    public List<Message> GetCurrentContactMessages()
    {
        Contact currentContact = _contacts.Find(contact => contact.Name() == _currentContact);
        if (currentContact != null)
        {
            return _contactsMessages[currentContact.Name()];
        }
        return new List<Message>();
    }


    public NarrativeNode GetNarrativeNode(int nodeId)
    {
        if (_narrativeNodes.ContainsKey(nodeId))
        {
            return _narrativeNodes[nodeId];
        }
        else
        {
            Debug.LogWarning($"Narrative node with ID {nodeId} not found.");
            return null;
        }
    }

    public NarrativeNode GetCurrentContactNarrativeNode()
    {
        Contact currentContact = GetCurrentContact();
        if (currentContact != null)
        {
            return GetNarrativeNode(currentContact.NarrativeNode());
        }
        else
        {
            Debug.LogWarning("No current contact selected.");
            return null;
        }
    }

    public bool ContactHasMessages(string contactName)
    {
        if (_contactsMessages.ContainsKey(contactName))
        {
            return _contactsMessages[contactName].Count > 0;
        }
        return false;
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
        _contactsMessages[_currentContact].Add(message);


        // Update the last message date for the current contact
        _contacts.Find(contact => contact.Name() == _currentContact).SetLastMessageDate(message.Timestamp());

        // Add the message to the UI
        _messagesUI.AddMessage(message);
        SortContacts();
    }

    /// <summary>
    /// Sorts the contacts list based on the last message date, with the most recent messages appearing first.
    /// </summary>
    private void  SortContacts() { 
        _contacts = _contacts.OrderByDescending(val => val.LastMessageDate()).ToList();
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


    public OptionsChanger GetOptionsChanger()
    {
        return _optionsChanger;
    }

    /// <summary>
    /// Creates the contacts by loading them from the specified JSON file and adding them to the contacts dictionary.
    /// </summary>
    private void CreateContacts()
    {
        // read the contacts info from the JSON
        _contacts = DataLoader.LoadContacts(contactsFilePath);
    }

    private void CreateMessages()
    {
        List<ContactPhoneMessages> contactPhoneMessages = DataLoader.LoadPhoneMessages(phoneMessagesFilePath);

        foreach (ContactPhoneMessages contactMessages in contactPhoneMessages)
        {
            _contactsMessages[contactMessages.ContactName()] = contactMessages.Messages();
            if (contactMessages.Messages().Count > 0)
            {
                Message lastMsg = contactMessages.Messages()[contactMessages.Messages().Count - 1];
                Contact contact = _contacts.Find(c => c.Name() == contactMessages.ContactName());
                if (contact != null)
                {
                    contact.SetLastMessageDate(lastMsg.Timestamp());
                }
            }
        }
    }

}
