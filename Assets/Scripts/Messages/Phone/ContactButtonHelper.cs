using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
public class ContactButtonHelper : MonoBehaviour
{
    private PhoneTabButton _phoneTabButton; // Reference to the PhoneTabButton component

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Gets the PhoneTabButton component attached to the same GameObject 
        _phoneTabButton = GetComponent<PhoneTabButton>();

        SetButtonActions();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    /// <summary>
    /// Sets the actions for the button when it is clicked. It configures the button to open the messaging tab, close the contacts tab, and set the current contact based on the button's text.
    /// </summary>
    private void SetButtonActions()
    {

        PhoneStateMachine phoneStateMachine = MessagesManager.Instance.GetPhoneStateMachine();

        _phoneTabButton.SetTabToOpen(phoneStateMachine.GetMessagingTab());

        _phoneTabButton.SetTabsToClose(new List<GameObject> { phoneStateMachine.GetContactsTab() });

        string contactName = GetComponentInChildren<TMPro.TextMeshProUGUI>().text;

        Button button = GetComponent<Button>();
        button.onClick.AddListener(() => MessagesManager.Instance.GetPhoneStateMachine().ChangeState(1));
        button.onClick.AddListener(() => MessagesManager.Instance.SetCurrentContact(contactName));
        button.onClick.AddListener(() => MessagesManager.Instance.GetMessagesUI().CreateMessages());
        button.onClick.AddListener(() => MessagesManager.Instance.GetOptionsChanger().SetCurrentNode(MessagesManager.Instance.GetCurrentContactNarrativeNode()));
        button.onClick.AddListener(() => ShowDecisionAvailable());
        button.onClick.AddListener(() => ShowDialogueAvailable());

    }


    private void ShowDecisionAvailable()
    {
        int contactNarrativeId = (MessagesManager.Instance.GetCurrentContactNarrativeNode() != null ? MessagesManager.Instance.GetCurrentContactNarrativeNode().ID() : -1);
        if (contactNarrativeId != -1 && MessagesManager.Instance.GetNarrativeNode(contactNarrativeId).HasOptions())
        {
            MessagesManager.Instance.GetPhoneStateMachine().ShowDecisionsTab();
        }
    }

    private void ShowDialogueAvailable()
    {
        if(MessagesManager.Instance.GetCurrentContact().HasNewMessages())
        {
            MessagesManager.Instance.GetPhoneStateMachine().ShowDialogueTab();
        }
    }
}
