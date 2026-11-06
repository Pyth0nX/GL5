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

    private GalleryManager _galleryManager; // Reference to the GalleryManager component for bidirectional photo sharing

    [SerializeField]
    private Dictionary<Name, List<Message>> _contactsMessages = new(); // Dictionary of List of messages for each contact, keyed by contact name

    [SerializeField]
    private List<Contact> _contacts = new(); // List of all contacts in the game

    [SerializeField]
    private Dictionary<int, NarrativeNode> _narrativeNodes = new(); // All the narrative nodes in the game, keyed by node ID

    [Header("Inspector Narrative Nodes (Visual Editor & Overrides)")]
    [Tooltip("Allows visualizing, adding, or overriding NarrativeNodes directly from the Unity Inspector (use component context menu to import/export with narrativeNodes.json).")]
    [SerializeField]
    private List<NarrativeNode> _inspectorNarrativeNodes = new();

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
        _narrativeNodes = new Dictionary<int, NarrativeNode>();
        List<NarrativeNode> nodesList = DataLoader.LoadNarrativeNodes(narrativeNodesFilePath);

        if (nodesList != null)
        {
            foreach (NarrativeNode node in nodesList)
            {
                if (node != null)
                {
                    _narrativeNodes[node.ID()] = node;
                }
            }
        }

        // If there are nodes configured or overridden in the Inspector, they take precedence over the base JSON
        if (_inspectorNarrativeNodes != null && _inspectorNarrativeNodes.Count > 0)
        {
            foreach (NarrativeNode inspectorNode in _inspectorNarrativeNodes)
            {
                if (inspectorNode != null)
                {
                    _narrativeNodes[inspectorNode.ID()] = inspectorNode;
                }
            }
        }
    }

    [ContextMenu("Narrative Nodes: Import from JSON to Inspector")]
    private void ImportNarrativeNodesFromJsonToInspector()
    {
        List<NarrativeNode> loaded = DataLoader.LoadNarrativeNodes(narrativeNodesFilePath);
        _inspectorNarrativeNodes = loaded ?? new List<NarrativeNode>();
        Debug.Log($"[MessagesManager] Imported {_inspectorNarrativeNodes.Count} narrative nodes from '{narrativeNodesFilePath}' to Inspector.");
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    [ContextMenu("Narrative Nodes: Save Inspector to JSON")]
    private void ExportInspectorNarrativeNodesToJson()
    {
        if (_inspectorNarrativeNodes == null || _inspectorNarrativeNodes.Count == 0)
        {
            Debug.LogWarning("[MessagesManager] '_inspectorNarrativeNodes' is empty. Import from JSON first before saving.");
            return;
        }

        NarrativeNodeListWrapper wrapper = new NarrativeNodeListWrapper
        {
            narrativeNodes = _inspectorNarrativeNodes
        };

        string path = System.IO.Path.Combine(Application.streamingAssetsPath, narrativeNodesFilePath);
        string json = JsonUtility.ToJson(wrapper, true);
        System.IO.File.WriteAllText(path, json);
        Debug.Log($"[MessagesManager] Saved {_inspectorNarrativeNodes.Count} narrative nodes to '{path}'.");
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

        // Gets the GalleryManager component in the phone hierarchy (including inactive tabs)
        _galleryManager = FindAnyObjectByType<GalleryManager>(FindObjectsInactive.Include);
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
    /// Returns the stored message history for a specific contact without changing the active contact.
    /// </summary>
    public List<Message> GetContactMessages(string contactName)
    {
        if (string.IsNullOrEmpty(contactName)) return new List<Message>();

        Contact contact = GetContactByName(contactName);
        string keyName = contact != null ? contact.Name() : contactName;

        if (_contactsMessages.TryGetValue(keyName, out List<Message> messages) && messages != null)
        {
            return messages;
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

        if (message.HasImage() && !string.IsNullOrEmpty(message.ImagePath()))
        {
            GetGalleryManager()?.NotifyChatImageReceived(message.ImagePath());
        }

        if (_messagesUI == null)
        {
            _messagesUI = GetComponent<MessagesUI>() ?? FindAnyObjectByType<MessagesUI>(FindObjectsInactive.Include);
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

        int resolvedNodeId = ResolveEntryRedirects(newNarrativeNodeId);
        contact.SetNarrativeNode(resolvedNodeId);
        contact.SetCurrentMessageIndex(0);

        NarrativeNode newNode = GetNarrativeNode(resolvedNodeId);
        if (newNode == null)
        {
            Debug.LogWarning($"[MessagesManager] NarrativeNode '{resolvedNodeId}' not found for contact '{contactName}'.");
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
            List<Message> dialogues = newNode.GetActiveDialogues();
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
        if (nodeId < 0)
        {
            return null;
        }

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

    /// <summary>
    /// Follows any conditional entry branches (_entryBranches) defined on the target node
    /// based on current Reputation/Safety, with cycle protection.
    /// </summary>
    public int ResolveEntryRedirects(int startNodeId)
    {
        if (startNodeId < 0) return startNodeId;

        int currentId = startNodeId;
        const int maxHops = 8;

        for (int hop = 0; hop < maxHops; hop++)
        {
            if (!_narrativeNodes.TryGetValue(currentId, out NarrativeNode node) || node == null)
            {
                break;
            }

            int redirectedId = node.ResolveEntryNodeId();
            if (redirectedId == currentId || redirectedId < 0)
            {
                break;
            }

            currentId = redirectedId;
        }

        return currentId;
    }

    /// <summary>
    /// Resolves conditional entry redirects for the current contact and returns the resulting NarrativeNode.
    /// </summary>
    public NarrativeNode ResolveAndGetCurrentContactNode()
    {
        Contact currentContact = GetCurrentContact();
        if (currentContact == null)
        {
            Debug.LogWarning("No current contact selected.");
            return null;
        }

        int originalId = currentContact.NarrativeNode();
        int resolvedId = ResolveEntryRedirects(originalId);
        if (resolvedId != originalId)
        {
            currentContact.SetNarrativeNode(resolvedId);
            currentContact.SetCurrentMessageIndex(0);
        }

        return GetNarrativeNode(resolvedId);
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
    /// Returns true if the specified contact has at least one received message (Sender.NPC) in their phone history.
    /// </summary>
    public bool ContactHasReceivedMessages(string contactName)
    {
        if (string.IsNullOrEmpty(contactName)) return false;

        Contact contact = GetContactByName(contactName);
        string keyName = contact != null ? contact.Name() : contactName;

        if (_contactsMessages.TryGetValue(keyName, out List<Message> messages) && messages != null)
        {
            for (int i = 0; i < messages.Count; i++)
            {
                if (messages[i] != null && messages[i].Sender() == Sender.NPC)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Returns all active/writable contacts sorted by most recent message date.
    /// Filters contacts that already have received messages (or any message history if onlyWithReceivedMessages is false).
    /// </summary>
    public List<Contact> GetWritableContacts(bool onlyWithReceivedMessages = true)
    {
        SortContacts();
        List<Contact> writable = new List<Contact>();

        for (int i = 0; i < _contacts.Count; i++)
        {
            Contact contact = _contacts[i];
            if (contact == null) continue;

            bool isValid = onlyWithReceivedMessages
                ? ContactHasReceivedMessages(contact.Name())
                : ContactHasMessages(contact.Name());

            if (isValid)
            {
                writable.Add(contact);
            }
        }

        return writable;
    }

    /// <summary>
    /// Generates a coherent Date timestamp strictly >= the latest recorded phone message so SortContacts()
    /// places the updated conversation at the top of the list.
    /// </summary>
    public Date GenerateNextMessageTimestamp()
    {
        int currentDay = DayManager.Instance != null ? Mathf.Max(1, DayManager.Instance.GetCurrentDay()) : 1;
        Date candidate = new Date
        {
            year = 2026,
            month = 10,
            day = currentDay,
            hour = 12,
            minute = 0
        };

        for (int i = 0; i < _contacts.Count; i++)
        {
            Contact c = _contacts[i];
            if (c != null && c.LastMessageDate().CompareTo(candidate) >= 0)
            {
                candidate = c.LastMessageDate();
            }
        }

        candidate.minute += 1;
        if (candidate.minute >= 60)
        {
            candidate.minute = 0;
            candidate.hour += 1;
            if (candidate.hour >= 24)
            {
                candidate.hour = 0;
                candidate.day += 1;
            }
        }

        return candidate;
    }

    /// <summary>
    /// Opens the Messaging screen on the specified contact's conversation, loading its historical messages
    /// and restoring any pending narrative dialogue or decision UI.
    /// </summary>
    public void OpenContactConversation(string contactName)
    {
        if (string.IsNullOrEmpty(contactName)) return;

        Contact contact = GetContactByName(contactName);
        string resolvedName = contact != null ? contact.Name() : contactName;

        SetCurrentContact(resolvedName);

        PhoneStateMachine stateMachine = GetPhoneStateMachine();
        if (stateMachine != null)
        {
            stateMachine.ChangeState((int)PhoneState.MESSAGING);
            stateMachine.SetBackButtonState((int)PhoneState.CONTACTS);
        }

        if (_messagesUI == null)
        {
            _messagesUI = GetComponent<MessagesUI>() ?? FindAnyObjectByType<MessagesUI>(FindObjectsInactive.Include);
        }

        if (_messagesUI != null)
        {
            _messagesUI.SetContactName(resolvedName);
            _messagesUI.CreateMessages();
        }

        NarrativeNode currentNode = GetCurrentContactNarrativeNode();
        if (_optionsChanger == null)
        {
            _optionsChanger = GetComponent<OptionsChanger>();
        }

        if (_optionsChanger != null)
        {
            _optionsChanger.SetCurrentNode(currentNode);
        }

        if (contact != null && contact.HasNewMessages())
        {
            stateMachine?.ShowDialogueTab();
        }
        else if (currentNode != null && currentNode.DialogueType() == DialogueType.PHONE && currentNode.HasOptions())
        {
            _optionsChanger?.ActiveDecisionsPanel(true);
            stateMachine?.ShowDecisionsTab();
        }
    }

    /// <summary>
    /// Sends an image message from the Player to the specified contact and redirects the phone UI to that conversation.
    /// </summary>
    public void SendImageMessageToContactAndOpenChat(string contactName, string imageKey, Sprite sprite = null, string caption = "")
    {
        if (string.IsNullOrEmpty(contactName) || string.IsNullOrEmpty(imageKey))
        {
            Debug.LogWarning("[MessagesManager] Cannot send image message: invalid contact name or image key.");
            return;
        }

        if (sprite != null)
        {
            MessagesUI.RegisterSpriteInCache(imageKey, sprite);
        }

        // 1. Open the contact conversation loading its previous history
        OpenContactConversation(contactName);

        // 2. Build and inject the player's new image message
        string playerName = GameManager.Instance != null ? (GameManager.Instance.GetPlayerName() ?? "") : "";
        Date timestamp = GenerateNextMessageTimestamp();
        Message imageMessage = new Message(
            caption ?? "",
            Sender.Player,
            playerName,
            timestamp,
            imageKey,
            MessageType.IMAGE);

        SendMessageToCurrentContact(imageMessage);
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
            _messagesUI = GetComponent<MessagesUI>() ?? FindAnyObjectByType<MessagesUI>(FindObjectsInactive.Include);
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
        if (_phoneStateMachine == null)
        {
            _phoneStateMachine = FindAnyObjectByType<PhoneStateMachine>(FindObjectsInactive.Include);
        }
        return _phoneStateMachine;
    }

    /// <summary>
    /// Returns the GalleryManager component in the phone interface.
    /// </summary>
    public GalleryManager GetGalleryManager()
    {
        if (_galleryManager == null)
        {
            _galleryManager = GalleryManager.Instance != null
                ? GalleryManager.Instance
                : FindAnyObjectByType<GalleryManager>(FindObjectsInactive.Include);
        }
        return _galleryManager;
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

    #region Save & Load Integration

    /// <summary>
    /// Resets all contacts, active narrative nodes, and chat histories back to their initial JSON state.
    /// </summary>
    public void ResetToInitialState()
    {
        _currentContact = null;
        _contactsMessages.Clear();
        CreateContacts();
        CreateMessages();
        SortContacts();

        if (_optionsChanger == null)
        {
            _optionsChanger = GetComponent<OptionsChanger>();
        }
        if (_optionsChanger != null && _optionsChanger.IsDecisionsPanelActive())
        {
            _optionsChanger.ActiveDecisionsPanel(false);
        }

        if (_messagesUI == null)
        {
            _messagesUI = GetComponent<MessagesUI>() ?? FindAnyObjectByType<MessagesUI>(FindObjectsInactive.Include);
        }
        _messagesUI?.RefreshContacts();
    }

    /// <summary>
    /// Exports every contact's current narrative node ID, progress index, unread state, and chat history.
    /// </summary>
    public List<ContactNarrativeSaveData> ExportContactsSaveData()
    {
        List<ContactNarrativeSaveData> savedList = new List<ContactNarrativeSaveData>();
        if (_contacts == null) return savedList;

        for (int i = 0; i < _contacts.Count; i++)
        {
            Contact contact = _contacts[i];
            if (contact == null || string.IsNullOrEmpty(contact.Name())) continue;

            List<Message> historyCopy = new List<Message>();
            if (_contactsMessages.TryGetValue(contact.Name(), out List<Message> history) && history != null)
            {
                historyCopy.AddRange(history);
            }

            savedList.Add(new ContactNarrativeSaveData
            {
                contactName = contact.Name(),
                currentNarrativeNodeId = contact.NarrativeNode(),
                currentMessageIndex = contact.CurrentMessageIndex(),
                hasNewMessages = contact.HasNewMessages(),
                lastMessageDate = contact.LastMessageDate(),
                contactImagePath = contact.ImagePath(),
                chatHistory = historyCopy
            });
        }

        return savedList;
    }

    /// <summary>
    /// Restores contacts' narrative node IDs, message indices, unread flags, and phone chat histories from saved data.
    /// </summary>
    public void RestoreContactsSaveData(List<ContactNarrativeSaveData> savedContacts)
    {
        _currentContact = null;
        _contactsMessages.Clear();
        CreateContacts();
        CreateMessages();

        if (savedContacts != null)
        {
            for (int i = 0; i < savedContacts.Count; i++)
            {
                ContactNarrativeSaveData entry = savedContacts[i];
                if (entry == null || string.IsNullOrEmpty(entry.contactName)) continue;

                Contact contact = GetContactByName(entry.contactName);
                if (contact == null)
                {
                    contact = new Contact();
                    contact.SetName(entry.contactName);
                    contact.SetImagePath(!string.IsNullOrEmpty(entry.contactImagePath)
                        ? entry.contactImagePath
                        : "Assets/Sprites/UnknownProfile.png");
                    _contacts.Add(contact);
                }
                else if (!string.IsNullOrEmpty(entry.contactImagePath))
                {
                    contact.SetImagePath(entry.contactImagePath);
                }

                contact.SetNarrativeNode(entry.currentNarrativeNodeId);
                contact.SetCurrentMessageIndex(entry.currentMessageIndex);
                contact.SetHasNewMessages(entry.hasNewMessages);
                contact.SetLastMessageDate(entry.lastMessageDate);

                if (entry.chatHistory != null)
                {
                    _contactsMessages[contact.Name()] = new List<Message>(entry.chatHistory);

                    for (int m = 0; m < entry.chatHistory.Count; m++)
                    {
                        Message msg = entry.chatHistory[m];
                        if (msg != null && msg.HasImage() && !string.IsNullOrEmpty(msg.ImagePath()))
                        {
                            GetGalleryManager()?.NotifyChatImageReceived(msg.ImagePath());
                        }
                    }
                }
            }
        }

        SortContacts();

        if (_optionsChanger == null)
        {
            _optionsChanger = GetComponent<OptionsChanger>();
        }
        if (_optionsChanger != null && _optionsChanger.IsDecisionsPanelActive())
        {
            _optionsChanger.ActiveDecisionsPanel(false);
        }

        if (_messagesUI == null)
        {
            _messagesUI = GetComponent<MessagesUI>() ?? FindAnyObjectByType<MessagesUI>(FindObjectsInactive.Include);
        }
        _messagesUI?.RefreshContacts();
    }

    #endregion
}
