using UnityEngine;
using UnityEngine.UI;

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

    private void SetButtonActions()
    {

        PhoneStateMachine phoneStateMachine = MessagesManager.Instance.GetPhoneStateMachine();

        _phoneTabButton.SetTabToOpen(phoneStateMachine.GetMessagingTab());

        _phoneTabButton.SetTabsToClose(new GameObject[] { phoneStateMachine.GetContactsTab() });

        string contactName = GetComponentInChildren<TMPro.TextMeshProUGUI>().text;


        GetComponent<Button>().onClick.AddListener(() => MessagesManager.Instance.GetPhoneStateMachine().ChangeState(1));
        GetComponent<Button>().onClick.AddListener(() => MessagesManager.Instance.SetCurrentContact(contactName));
        GetComponent<Button>().onClick.AddListener(() => MessagesManager.Instance.GetMessagesUI().CreateMessages());

    }
}
