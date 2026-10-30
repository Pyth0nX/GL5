using NUnit.Framework;
using NUnit.Framework.Interfaces;
using System;
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
            return;
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
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Gets the MessagesUI component attached to the same GameObject as this script
        _messagesUI = GetComponent<MessagesUI>();

        // Gets the PhoneStateMachine component attached to the same GameObject as this script
        _phoneStateMachine = FindAnyObjectByType<PhoneStateMachine>(FindObjectsInactive.Include);

        // Gets the OptionsChanger component attached to the same GameObject as this script
        _optionsChanger = GetComponent<OptionsChanger>();
    }

    /// <summary>
    /// Returns the list of all contacts in the game.
    /// </summary>
    /// <returns></returns>
    public List<Contact> GetContacts()
    {
        // Sort the contacts list so it is always in order of the last message date
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
        return GetContactByName(_currentContact);
    }

    public Contact GetContactByName(string contactName)
    {
        if (string.IsNullOrEmpty(contactName)) return null;

        var found = _contacts.Find(contact => string.Equals(contact.Name(), contactName, StringComparison.OrdinalIgnoreCase));
        if (found == null)
        {
            string normalized = contactName.Replace(" ", "").ToLower().Replace("unkown", "unknown");
            found = _contacts.Find(contact => contact.Name().Replace(" ", "").ToLower().Replace("unkown", "unknown") == normalized);
        }
        return found;
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
        if (string.IsNullOrEmpty(_currentContact)) return new List<Message>();

        Contact currentContact = GetContactByName(_currentContact);
        if (currentContact != null)
        {
            if (!_contactsMessages.ContainsKey(currentContact.Name()))
            {
                _contactsMessages[currentContact.Name()] = new List<Message>();
            }
            return _contactsMessages[currentContact.Name()];
        }
        return new List<Message>();
    }

    /// <summary>
    /// Adds a message directly to a contact's history, updating timestamp and sorting contacts.
    /// Also updates the active chat UI if this contact is currently open.
    /// </summary>
    public void AddMessageToContact(string contactName, Message message)
    {
        if (string.IsNullOrEmpty(contactName) || message == null) return;

        Contact contact = GetContactByName(contactName);
        string keyName = contact != null ? contact.Name() : contactName;

        if (!_contactsMessages.ContainsKey(keyName))
        {
            _contactsMessages[keyName] = new List<Message>();
        }

        _contactsMessages[keyName].Add(message);

        if (contact != null)
        {
            contact.SetLastMessageDate(message.Timestamp());
        }

        SortContacts();

        if (_messagesUI == null)
        {
            _messagesUI = GetComponent<MessagesUI>() ?? FindFirstObjectByType<MessagesUI>(FindObjectsInactive.Include);
        }

        // If this contact is currently open on screen in MessagesUI, show it live
        if (_currentContact != null && string.Equals(_currentContact, keyName, StringComparison.OrdinalIgnoreCase) && _messagesUI != null)
        {
            _messagesUI.AddMessage(message);
        }
    }

    /// <summary>
    /// Advances a contact's narrative node as a result of a game event.
    /// If the new node belongs to a mobile conversation and starts with an NPC message,
    /// directly adds messages up to the first player message and fires a phone notification.
    /// </summary>
    public void AdvanceContactNarrativeNodeByEvent(string contactName, int newNarrativeNodeId)
    {
        Contact contact = GetContactByName(contactName);
        if (contact == null)
        {
            Debug.Log($"[MessagesManager] Contact '{contactName}' not found in contacts list. Creating new Contact.");
            contact = new Contact();
            contact.SetName(contactName);
            contact.SetImagePath("Assets/Sprites/UnknownProfile.png");
            _contacts.Add(contact);
            _contactsMessages[contact.Name()] = new List<Message>();
        }

        contact.SetNarrativeNode(newNarrativeNodeId);
        contact.SetCurrentMessageIndex(0);

        NarrativeNode newNode = GetNarrativeNode(newNarrativeNodeId);
        if (newNode == null)
        {
            Debug.LogWarning($"[MessagesManager] NarrativeNode '{newNarrativeNodeId}' not found for contact '{contactName}'.");
            contact.SetHasNewMessages(false);
            return;
        }

        // Check if the new narrative node belongs to a mobile phone conversation
        bool isPhoneConversation = newNode.DialogueType() == DialogueType.PHONE;
        if (!isPhoneConversation && (string.Equals(contact.Name(), "Unknown Number", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(contact.Name(), "Unkown Number", StringComparison.OrdinalIgnoreCase)))
        {
            isPhoneConversation = true;
        }

        if (isPhoneConversation)
        {
            List<Message> dialogues = newNode.Dialogues();
            if (dialogues != null && dialogues.Count > 0 && dialogues[0].Sender() == Sender.NPC)
            {
                int addedCount = 0;
                Message firstNpcMessage = null;
                int nextIndex = 0;

                for (int i = 0; i < dialogues.Count; i++)
                {
                    Message msg = dialogues[i];
                    if (msg.Sender() == Sender.NPC)
                    {
                        if (firstNpcMessage == null)
                        {
                            firstNpcMessage = msg;
                        }
                        AddMessageToContact(contact.Name(), msg);
                        addedCount++;
                        nextIndex = i + 1;
                    }
                    else
                    {
                        // Stop at the first player message
                        nextIndex = i;
                        break;
                    }
                }

                contact.SetCurrentMessageIndex(nextIndex);

                // If there are remaining dialogues (player's turn) or options, mark has new messages
                bool hasMore = (nextIndex < dialogues.Count) || newNode.HasOptions();
                contact.SetHasNewMessages(hasMore);

                SortContacts();

                // Trigger Phone Notification
                if (addedCount == 1 && firstNpcMessage != null)
                {
                    string rawSenderName = !string.IsNullOrEmpty(firstNpcMessage.SpeakerName()) 
                        ? firstNpcMessage.SpeakerName() 
                        : (!string.IsNullOrEmpty(contact.Name()) ? contact.Name() : contactName);

                    string formattedContactName = FormatContactNameForNotification(rawSenderName);
                    string notificationBody = $"{formattedContactName}: {firstNpcMessage.Content()}";

                    PhoneNotificationController.Instance?.ShowNotification(PhoneApp.Messages, notificationBody);
                }
                else if (addedCount > 1)
                {
                    PhoneNotificationController.Instance?.ShowNotification(PhoneApp.Messages, "New unread messages");
                }
            }
            else
            {
                // First message is player or no dialogues
                contact.SetCurrentMessageIndex(0);
                contact.SetHasNewMessages(dialogues != null && dialogues.Count > 0);
            }
        }
        else
        {
            // In-person conversation
            contact.SetHasNewMessages(false);
        }

        SortContacts();

        // Refresh contact list in MessagesUI so new/updated contact appears immediately
        if (_messagesUI == null)
        {
            _messagesUI = GetComponent<MessagesUI>() ?? FindAnyObjectByType<MessagesUI>(FindObjectsInactive.Include);
        }
        _messagesUI?.RefreshContacts();

        Debug.Log($"[MessagesManager] Successfully advanced contact '{contactName}' to node {newNarrativeNodeId} by event.");
    }

    /// <summary>
    /// Formats a contact or speaker name for single-message notifications:
    /// Maximum 10 characters. If greater than 10 characters, truncates to 9 characters and appends '-'.
    /// </summary>
    private string FormatContactNameForNotification(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Unknown";
        string trimmed = name.Trim();
        if (trimmed.Length > 10)
        {
            return trimmed.Substring(0, 9) + "-";
        }
        return trimmed;
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
        if (string.IsNullOrEmpty(_currentContact) || message == null)
        {
            Debug.LogWarning("No current contact selected or null message.");
            return;
        }

        Contact contact = GetContactByName(_currentContact);
        string keyName = contact != null ? contact.Name() : _currentContact;

        if (!_contactsMessages.ContainsKey(keyName))
        {
            _contactsMessages[keyName] = new List<Message>();
        }
        _contactsMessages[keyName].Add(message);

        if (contact != null)
        {
            contact.SetLastMessageDate(message.Timestamp());
        }

        if (_messagesUI == null)
        {
            _messagesUI = GetComponent<MessagesUI>() ?? FindFirstObjectByType<MessagesUI>(FindObjectsInactive.Include);
        }

        _messagesUI?.AddMessage(message);
        SortContacts();
        _messagesUI?.RefreshContacts();
    }

    /// <summary>
    /// Sorts the contacts list based on the last message date, with the most recent messages appearing first.
    /// </summary>
    private void SortContacts()
    { 
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

        if (contactPhoneMessages != null)
        {
            foreach (ContactPhoneMessages contactMessages in contactPhoneMessages)
            {
                _contactsMessages[contactMessages.ContactName()] = contactMessages.Messages() ?? new List<Message>();
                if (contactMessages.Messages() != null && contactMessages.Messages().Count > 0)
                {
                    Message lastMsg = contactMessages.Messages()[contactMessages.Messages().Count - 1];
                    Contact contact = GetContactByName(contactMessages.ContactName());
                    if (contact != null)
                    {
                        contact.SetLastMessageDate(lastMsg.Timestamp());
                    }
                }
            }
        }

        // Ensure every contact in _contacts has an entry in _contactsMessages
        if (_contacts != null)
        {
            foreach (Contact contact in _contacts)
            {
                if (!_contactsMessages.ContainsKey(contact.Name()))
                {
                    _contactsMessages[contact.Name()] = new List<Message>();
                }
            }
        }
    }
}
