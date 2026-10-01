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


    private Contact _currentContact; // The contact that is currently being interacted with

    private MessagesUI _messagesUI; // Reference to the MessagesUI component for updating the UI

    private PhoneStateMachine _phoneStateMachine; // Reference to the PhoneStateMachine component

    [SerializeField]
    private Dictionary<Name, Contact> _contacts = new(); // Dictionary of all contacts in the game, keyed by contact name

    [SerializeField]
    private Dictionary<int, NarrativeNode> _narrativeNodes = new(); // All the narrative nodes in the game, keyed by node ID

    [SerializeField]
    private string contactsFilePath = "contacts.json";

    [SerializeField]
    private string narrativeNodesFilePath = "narrativeNodes.json";

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
    }

    private void CreateNarrativeNodes()
    {
        List<NarrativeNode> nodesList = DataLoader.LoadNarrativeNodes(narrativeNodesFilePath);

        if (nodesList != null)
        {
            foreach (NarrativeNode node in nodesList)
            {
                Debug.Log(node);
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
        List<Contact> contactsList = new List<Contact>(_contacts.Values);
        return contactsList;
    }

    public void AddContact(Contact contact)
    {
        _contacts[contact.Name()] = contact;
        Debug.Log(contact.Messages());
        // sort the contacts list so it is always in order of the last message date
        SortContacts();
    }


    /// <summary>
    /// Sets the current contact that the user is interacting with.
    /// </summary>
    /// <param name="contactName"></param>
    public void SetCurrentContact(string contactName)
    {
        _currentContact = _contacts[contactName];
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
        _currentContact.Messages().Add(message);


        // Update the last message date for the current contact
        _currentContact.SetLastMessageDate(message.timestamp);

        // Add the message to the UI
        _messagesUI.AddMessage(message);
        SortContacts();
    }

    /// <summary>
    /// Sorts the contacts list based on the last message date, with the most recent messages appearing first.
    /// </summary>
    private void  SortContacts() { 
        _contacts = _contacts.OrderByDescending(kvp => kvp.Value.LastMessageDate()).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
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

    /// <summary>
    /// Creates the contacts by loading them from the specified JSON file and adding them to the contacts dictionary.
    /// </summary>
    private void CreateContacts()
    {
        // read the contacts info from the JSON
        List<Contact> contacts = DataLoader.LoadContacts(contactsFilePath);

        foreach (Contact contact in contacts)
        {
            AddContact(contact);
            Debug.Log(contact);

        }
    }

}
