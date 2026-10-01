using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

public class MessagesUI : MonoBehaviour
{
    [Header("UI Containers")]
    [SerializeField] private Transform contactListContainer;
    [SerializeField] private Transform messagesContainer;

    [Header("Prefabs")]
    [SerializeField] private GameObject messagePrefab;
    [SerializeField] private GameObject contactPrefab;

    [Header("Messages Colors")]
    [SerializeField] private Color playerMessageColor;
    [SerializeField] private Color NPCMessageColor;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        CreateContacts(); 
    }

    // Update is called once per frame
    void Update()
    {
        
    }


   

    /// <summary>
    /// Add a new contact element to the contacts container
    /// </summary>
    /// <param name="contact"></param>
    public void AddContact(Contact contact)
    {
        InstantiateContact(contact);
    }

    /// <summary>
    /// Create UI elements for each contact in the contact list container.
    /// </summary>
    private void CreateContacts()
    {
        List<Contact> contacts = MessagesManager.Instance.GetContacts();
        if(contacts.Count > 0)
        {
            foreach (Contact contact in contacts)
            {
                if(contact.IsActive())
                {
                    InstantiateContact(contact);
                }
            }
        }
    }

    private void InstantiateContact(Contact contact)
    {
        GameObject contactGO = Instantiate(contactPrefab, contactListContainer);
        TextMeshProUGUI contactNameText = contactGO.GetComponentInChildren<TextMeshProUGUI>();
        contactNameText.text = contact.Name();

    }


    /// <summary>
    /// Create all messages related to the current contact
    /// </summary>
    public void CreateMessages()
    {
        Contact currentContact = MessagesManager.Instance.GetCurrentContact();
        ClearMessages();
        foreach (Message message in currentContact.Messages())
        {
            InstantiateMessage(message);
        }
    }

    /// <summary>
    /// Destroy all message UI elements in the messages container.
    /// </summary>
    private void ClearMessages()
    {
        for (int i = messagesContainer.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(messagesContainer.GetChild(i).gameObject);
        }
    }

    /// <summary>
    /// Add a new message to the current conversation and instantiate its UI element in the messages container.
    /// </summary>
    /// <param name="message"></param>
    public void AddMessage(Message message) 
    {
        InstantiateMessage(message);
    }

    /// <summary>
    /// Create a Message Prefab in the messages container with the information given.
    /// </summary>
    /// <param name="message"></param>
    private void InstantiateMessage(Message message)
    {
        GameObject messageGO = Instantiate(messagePrefab, messagesContainer);
        TextMeshProUGUI messageText = messageGO.GetComponentInChildren<TextMeshProUGUI>();
        messageText.text = message.content;

        Image image = messageGO.GetComponent<Image>();

        if (message.sender == Sender.Player)
        {
            image.color = playerMessageColor; 
        }
        else
        {
            image.color = NPCMessageColor; 
        }
    }
}
