using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Name = System.String;

public enum MessagingChannel
{
    Phone,
    Connect
}

/// <summary>
/// Holds the list of contacts and their messages across both Phone SMS and Connect channels,
/// and manages the current contact being interacted with.
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
    private Dictionary<Name, List<Message>> _contactsMessages = new(); // Dictionary of SMS messages keyed by contact name

    [SerializeField]
    private Dictionary<Name, List<Message>> _connectMessages = new(); // Dictionary of Connect social messages keyed by contact name

    private MessagingChannel _currentMessagingChannel = MessagingChannel.Phone;

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

    void Start()
    {
        _messagesUI = GetComponent<MessagesUI>();
        _phoneStateMachine = FindAnyObjectByType<PhoneStateMachine>(FindObjectsInactive.Include);
        _optionsChanger = GetComponent<OptionsChanger>();
        _galleryManager = FindAnyObjectByType<GalleryManager>(FindObjectsInactive.Include);

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe("Campaign_Completed", OnNarrativeCompleted);
        }
    }

    private void OnDestroy()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe("Campaign_Completed", OnNarrativeCompleted);
        }
    }

    private void OnNarrativeCompleted()
    {
        PhoneNotificationController.Instance?.ShowNotification(
            PhoneApp.Connect,
            "Before You Send - Story Complete",
            null,
            5f);

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowTemporaryMessage(
                "You reached the end of Luna's five-day story.",
                new Color(1f, 0.92f, 0.35f),
                8f);
        }


        bool narrationShown = DialogueManager.Instance != null && DialogueManager.Instance.ShowNarration(
            "End of Luna's 5-Day Story. You have completed the Before You Send campaign.",
            () => UIManager.Instance?.ShowScenarioSystem());
        if (!narrationShown)
        {
            UIManager.Instance?.ShowScenarioSystem();
        }
    }

    /// <summary>
    /// Returns the contacts belonging to the active messaging channel, sorted by most recent message timestamp.
    /// </summary>
    public List<Contact> GetContacts()
    {
        SortContacts();

        // Both apps share the visual contact list widget while keeping separate channels and histories
        List<Contact> contactsList = _contacts
            .Where(contact => contact != null && GetChannelForContact(contact.Name()) == _currentMessagingChannel)
            .ToList();
        return contactsList;
    }

    public void SetMessagingChannel(MessagingChannel channel)
    {
        _currentMessagingChannel = channel;
        _currentContact = null;
        if (_messagesUI == null)
        {
            _messagesUI = GetComponent<MessagesUI>() ?? FindAnyObjectByType<MessagesUI>(FindObjectsInactive.Include);
        }
        _messagesUI?.RefreshContacts();
    }

    public MessagingChannel GetMessagingChannel()
    {
        return _currentMessagingChannel;
    }

    public static MessagingChannel GetChannelForContact(string contactName)
    {
        if (string.Equals(contactName, "Mom", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(contactName, "Dad", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(contactName, "Maya", StringComparison.OrdinalIgnoreCase))
        {
            return MessagingChannel.Phone;
        }

        return MessagingChannel.Connect;
    }

    public static bool IsNarration(Message message)
    {
        return message != null &&
               string.Equals(message.SpeakerName(), "Narration", StringComparison.OrdinalIgnoreCase);
    }

    private Dictionary<Name, List<Message>> GetMessageStore(string contactName)
    {
        return GetChannelForContact(contactName) == MessagingChannel.Phone
            ? _contactsMessages
            : _connectMessages;
    }

    /// <summary>
    /// Sets the current contact that the user is interacting with.
    /// </summary>
    public void SetCurrentContact(string contactName)
    {
        _currentContact = contactName;
    }

    /// <summary>
    /// Returns the current contact that the user is interacting with.
    /// </summary>
    public Contact GetCurrentContact()
    {
        return GetContactByName(_currentContact);
    }

    public Contact GetContactByName(string contactName)
    {
        if (string.IsNullOrEmpty(contactName)) return null;

        var found = _contacts.Find(contact => contact != null && string.Equals(contact.Name(), contactName, StringComparison.OrdinalIgnoreCase));
        if (found == null)
        {
            string normalized = contactName.Replace(" ", "").ToLower().Replace("unkown", "unknown");
            found = _contacts.Find(contact => contact != null && contact.Name().Replace(" ", "").ToLower().Replace("unkown", "unknown") == normalized);
        }
        return found;
    }

    public string GetCurrentContactName()
    {
        return _currentContact;
    }

    /// <summary>
    /// Returns the message history for the currently selected contact.
    /// </summary>
    public List<Message> GetCurrentContactMessages()
    {
        if (string.IsNullOrEmpty(_currentContact)) return new List<Message>();

        Contact currentContact = GetContactByName(_currentContact);
        if (currentContact != null)
        {
            Dictionary<Name, List<Message>> messageStore = GetMessageStore(currentContact.Name());
            if (!messageStore.ContainsKey(currentContact.Name()))
            {
                messageStore[currentContact.Name()] = new List<Message>();
            }
            return messageStore[currentContact.Name()];
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

        Dictionary<Name, List<Message>> messageStore = GetMessageStore(keyName);
        if (messageStore.TryGetValue(keyName, out List<Message> messages) && messages != null)
        {
            return messages;
        }

        return new List<Message>();
    }

    /// <summary>
    /// Adds a message directly to a contact's history, updating timestamp and sorting contacts.
    /// Also updates the active chat UI if this contact is currently open in the matching channel.
    /// </summary>
    public void AddMessageToContact(string contactName, Message message)
    {
        if (string.IsNullOrEmpty(contactName) || message == null) return;

        Contact contact = GetContactByName(contactName);
        string keyName = contact != null ? contact.Name() : contactName;

        Dictionary<Name, List<Message>> messageStore = GetMessageStore(keyName);
        if (!messageStore.ContainsKey(keyName))
        {
            messageStore[keyName] = new List<Message>();
        }

        messageStore[keyName].Add(message);

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

        // If this contact is currently open on screen in MessagesUI on the active channel, show it live
        if (_currentContact != null &&
            string.Equals(_currentContact, keyName, StringComparison.OrdinalIgnoreCase) &&
            GetChannelForContact(keyName) == _currentMessagingChannel &&
            _messagesUI != null)
        {
            _messagesUI.AddMessage(message);
        }
    }

    /// <summary>
    /// Advances a contact's narrative node as a result of a game event.
    /// If the new node belongs to a mobile conversation and starts with an NPC message,
    /// directly adds messages up to the first player message or narration boundary and fires a phone notification.
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
            GetMessageStore(contact.Name())[contact.Name()] = new List<Message>();
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
                    if (msg.Sender() == Sender.NPC && !IsNarration(msg))
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
                        // Player messages and narration lines are interactive boundaries
                        nextIndex = i;
                        break;
                    }
                }

                contact.SetCurrentMessageIndex(nextIndex);

                // Keep the conversation pending even when all NPC lines were preloaded into history so
                // opening the contact allows DialogueButton to complete the node and publish EventOnEnd.
                bool hasMore = addedCount > 0 || (nextIndex < dialogues.Count) || newNode.HasOptions();
                contact.SetHasNewMessages(hasMore);

                SortContacts();

                PhoneApp notificationApp = GetChannelForContact(contact.Name()) == MessagingChannel.Connect
                    ? PhoneApp.Connect
                    : PhoneApp.Messages;

                if (addedCount == 1 && firstNpcMessage != null)
                {
                    string rawSenderName = !string.IsNullOrEmpty(firstNpcMessage.SpeakerName())
                        ? firstNpcMessage.SpeakerName()
                        : (!string.IsNullOrEmpty(contact.Name()) ? contact.Name() : contactName);

                    string formattedContactName = FormatContactNameForNotification(rawSenderName);
                    string notificationBody = $"{formattedContactName}: {firstNpcMessage.Content()}";

                    PhoneNotificationController.Instance?.ShowNotification(notificationApp, notificationBody);
                }
                else if (addedCount > 1)
                {
                    PhoneNotificationController.Instance?.ShowNotification(notificationApp, "New unread messages");
                }
            }
            else
            {
                // First message is player, narration, or empty dialogue list with options
                contact.SetCurrentMessageIndex(0);
                contact.SetHasNewMessages((dialogues != null && dialogues.Count > 0) || newNode.HasOptions());
            }
        }
        else
        {
            // In-person conversation
            contact.SetHasNewMessages(false);
        }

        SortContacts();

        if (_messagesUI == null)
        {
            _messagesUI = GetComponent<MessagesUI>() ?? FindAnyObjectByType<MessagesUI>(FindObjectsInactive.Include);
        }
        _messagesUI?.RefreshContacts();

        Debug.Log($"[MessagesManager] Successfully advanced contact '{contactName}' to node {resolvedNodeId} by event.");
    }

    /// <summary>
    /// Moves a contact to a completed narrative node without adding messages, unread badges, or notifications.
    /// Used by the debug day-skipper so the following day starts from the same functional state as a played day.
    /// </summary>
    public void SetContactNarrativeStateSilently(string contactName, int narrativeNodeId)
    {
        Contact contact = GetContactByName(contactName);
        if (contact == null)
        {
            contact = new Contact();
            contact.SetName(contactName);
            contact.SetImagePath("Assets/Sprites/UnknownProfile.png");
            _contacts.Add(contact);
            GetMessageStore(contact.Name())[contact.Name()] = new List<Message>();
        }

        contact.SetNarrativeNode(narrativeNodeId);

        NarrativeNode node = GetNarrativeNode(narrativeNodeId);
        int completedMessageIndex = node?.GetActiveDialogues()?.Count ?? 0;
        contact.SetCurrentMessageIndex(completedMessageIndex);
        contact.SetHasNewMessages(false);

        SortContacts();

        if (_messagesUI == null)
        {
            _messagesUI = GetComponent<MessagesUI>() ?? FindAnyObjectByType<MessagesUI>(FindObjectsInactive.Include);
        }

        _messagesUI?.RefreshContacts();
        Debug.Log($"[MessagesManager] Silently set '{contactName}' to completed node {narrativeNodeId}.");
    }

    /// <summary>
    /// Clears only the Connect/social histories before rebuilding a debug day-skip snapshot.
    /// The original SMS conversations with Mom, Dad, and Maya are preserved.
    /// </summary>
    public void BeginTestSkipHistoryRebuild()
    {
        _connectMessages.Clear();

        foreach (Contact contact in _contacts)
        {
            if (contact == null || GetChannelForContact(contact.Name()) != MessagingChannel.Connect) continue;

            _connectMessages[contact.Name()] = new List<Message>();
            contact.SetLastMessageDate(default);
            contact.SetHasNewMessages(false);
            contact.SetCurrentMessageIndex(0);
        }
    }

    /// <summary>
    /// Adds completed phone dialogue to a contact without animations, notifications, or narrative events.
    /// Narration and in-person nodes are excluded because they are never chat bubbles during normal play.
    /// </summary>
    public void AppendTestSkipHistory(string contactName, int storyDay, params int[] narrativeNodeIds)
    {
        if (string.IsNullOrWhiteSpace(contactName) || narrativeNodeIds == null) return;

        Contact contact = GetContactByName(contactName);
        if (contact == null)
        {
            contact = new Contact();
            contact.SetName(contactName);
            contact.SetImagePath("Assets/Sprites/UnknownProfile.png");
            _contacts.Add(contact);
        }

        Dictionary<Name, List<Message>> messageStore = GetMessageStore(contact.Name());
        if (!messageStore.TryGetValue(contact.Name(), out List<Message> history))
        {
            history = new List<Message>();
            messageStore[contact.Name()] = history;
        }

        foreach (int nodeId in narrativeNodeIds)
        {
            NarrativeNode node = GetNarrativeNode(nodeId);
            if (node == null || node.DialogueType() != DialogueType.PHONE) continue;

            foreach (Message sourceMessage in node.GetActiveDialogues())
            {
                if (sourceMessage == null || IsNarration(sourceMessage)) continue;

                int messageNumber = history.Count;
                Date timestamp = sourceMessage.Timestamp().year != 0
                    ? sourceMessage.Timestamp()
                    : new()
                    {
                        day = Mathf.Clamp(storyDay, 1, 31),
                        month = 10,
                        year = 2026,
                        hour = 8 + Mathf.Min(14, messageNumber / 6),
                        minute = (messageNumber * 7) % 60
                    };

                Message historicalMessage = new(
                    sourceMessage.Content(),
                    sourceMessage.Sender(),
                    sourceMessage.SpeakerName(),
                    timestamp,
                    sourceMessage.ImagePath(),
                    sourceMessage.Type());

                history.Add(historicalMessage);
                contact.SetLastMessageDate(timestamp);
            }
        }

        contact.SetHasNewMessages(false);
    }

    public void CompleteTestSkipHistoryRebuild()
    {
        SortContacts();

        if (_messagesUI == null)
        {
            _messagesUI = GetComponent<MessagesUI>() ?? FindAnyObjectByType<MessagesUI>(FindObjectsInactive.Include);
        }

        _messagesUI?.RefreshContacts();
        Debug.Log("[MessagesManager] Rebuilt completed chat history for debug day skip.");
    }

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

    public IReadOnlyDictionary<int, NarrativeNode> GetAllNarrativeNodes() => _narrativeNodes;

    public IReadOnlyList<Contact> GetAllContacts() => _contacts;

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
        if (string.IsNullOrEmpty(contactName)) return false;

        Dictionary<Name, List<Message>> messageStore = GetMessageStore(contactName);
        if (messageStore.TryGetValue(contactName, out List<Message> messages) && messages != null)
        {
            return messages.Count > 0;
        }
        return false;
    }

    /// <summary>
    /// Returns true if the specified contact has at least one received message (Sender.NPC) in their channel history.
    /// </summary>
    public bool ContactHasReceivedMessages(string contactName)
    {
        if (string.IsNullOrEmpty(contactName)) return false;

        Contact contact = GetContactByName(contactName);
        string keyName = contact != null ? contact.Name() : contactName;

        Dictionary<Name, List<Message>> messageStore = GetMessageStore(keyName);
        if (messageStore.TryGetValue(keyName, out List<Message> messages) && messages != null)
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
    /// Connect contacts are included only once Connect is installed.
    /// </summary>
    public List<Contact> GetWritableContacts(bool onlyWithReceivedMessages = true)
    {
        SortContacts();
        List<Contact> writable = new List<Contact>();
        bool connectAvailable = UIManager.Instance == null || UIManager.Instance.IsConnectInstalled;

        for (int i = 0; i < _contacts.Count; i++)
        {
            Contact contact = _contacts[i];
            if (contact == null) continue;

            if (GetChannelForContact(contact.Name()) == MessagingChannel.Connect && !connectAvailable)
            {
                continue;
            }

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
            if (c != null && c.LastMessageDate().year == 2026 && c.LastMessageDate().day <= currentDay && c.LastMessageDate().CompareTo(candidate) >= 0)
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
    /// Opens the Messaging screen on the specified contact's conversation, switching to its channel,
    /// loading its historical messages, and restoring any pending narrative dialogue or decision UI.
    /// </summary>
    public void OpenContactConversation(string contactName)
    {
        if (string.IsNullOrEmpty(contactName)) return;

        Contact contact = GetContactByName(contactName);
        string resolvedName = contact != null ? contact.Name() : contactName;

        _currentMessagingChannel = GetChannelForContact(resolvedName);
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
            _messagesUI.RefreshContacts();
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

        OpenContactConversation(contactName);

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
    /// Updates the message list of the current contact with a new message.
    /// </summary>
    public void SendMessageToCurrentContact(Message message)
    {
        if (string.IsNullOrEmpty(_currentContact) || message == null)
        {
            Debug.LogWarning("No current contact selected or null message.");
            return;
        }

        Contact contact = GetContactByName(_currentContact);
        string keyName = contact != null ? contact.Name() : _currentContact;

        Dictionary<Name, List<Message>> messageStore = GetMessageStore(keyName);
        if (!messageStore.ContainsKey(keyName))
        {
            messageStore[keyName] = new List<Message>();
        }
        messageStore[keyName].Add(message);

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

    public PhoneStateMachine GetPhoneStateMachine()
    {
        if (_phoneStateMachine == null)
        {
            _phoneStateMachine = FindAnyObjectByType<PhoneStateMachine>(FindObjectsInactive.Include);
        }
        return _phoneStateMachine;
    }

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

    public MessagesUI GetMessagesUI()
    {
        if (_messagesUI == null)
        {
            _messagesUI = GetComponent<MessagesUI>() ?? FindAnyObjectByType<MessagesUI>(FindObjectsInactive.Include);
        }
        return _messagesUI;
    }

    public OptionsChanger GetOptionsChanger()
    {
        if (_optionsChanger == null)
        {
            _optionsChanger = GetComponent<OptionsChanger>();
        }
        return _optionsChanger;
    }

    private void CreateContacts()
    {
        _contacts = DataLoader.LoadContacts(contactsFilePath) ?? new List<Contact>();
    }

    private void CreateMessages()
    {
        List<ContactPhoneMessages> contactPhoneMessages = DataLoader.LoadPhoneMessages(phoneMessagesFilePath);

        if (contactPhoneMessages != null)
        {
            foreach (ContactPhoneMessages contactMessages in contactPhoneMessages)
            {
                if (contactMessages == null || string.IsNullOrEmpty(contactMessages.ContactName())) continue;

                Dictionary<Name, List<Message>> messageStore = GetMessageStore(contactMessages.ContactName());
                messageStore[contactMessages.ContactName()] = contactMessages.Messages() ?? new List<Message>();
                if (contactMessages.Messages() != null && contactMessages.Messages().Count > 0)
                {
                    Message lastMsg = contactMessages.Messages()[contactMessages.Messages().Count - 1];
                    Contact contact = GetContactByName(contactMessages.ContactName());
                    if (contact != null && lastMsg != null)
                    {
                        contact.SetLastMessageDate(lastMsg.Timestamp());
                    }
                }
            }
        }

        // Ensure every contact in _contacts has an initialized entry in its channel message store
        if (_contacts != null)
        {
            foreach (Contact contact in _contacts)
            {
                if (contact == null || string.IsNullOrEmpty(contact.Name())) continue;

                Dictionary<Name, List<Message>> messageStore = GetMessageStore(contact.Name());
                if (!messageStore.ContainsKey(contact.Name()))
                {
                    messageStore[contact.Name()] = new List<Message>();
                }
            }
        }
    }

    #region Save & Load Integration

    /// <summary>
    /// Resets all contacts, active narrative nodes, and chat histories across both Phone and Connect back to JSON defaults.
    /// </summary>
    public void ResetToInitialState()
    {
        _currentContact = null;
        _currentMessagingChannel = MessagingChannel.Phone;
        _contactsMessages.Clear();
        _connectMessages.Clear();
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
    /// Exports every contact's current narrative node ID, progress index, unread state, and chat history across both channels.
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
            Dictionary<Name, List<Message>> messageStore = GetMessageStore(contact.Name());
            if (messageStore.TryGetValue(contact.Name(), out List<Message> history) && history != null)
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
    /// Restores contacts' narrative node IDs, message indices, unread flags, and chat histories across both channels.
    /// </summary>
    public void RestoreContactsSaveData(List<ContactNarrativeSaveData> savedContacts)
    {
        _currentContact = null;
        _currentMessagingChannel = MessagingChannel.Phone;
        _contactsMessages.Clear();
        _connectMessages.Clear();
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
                    Dictionary<Name, List<Message>> messageStore = GetMessageStore(contact.Name());
                    messageStore[contact.Name()] = new List<Message>(entry.chatHistory);

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
