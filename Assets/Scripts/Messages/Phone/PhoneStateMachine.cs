using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[Serializable]
public enum PhoneState
{
    Idle,       //0
    Messaging,  //1
    Contacts,   //2
    Gallery,    //3
    Email,      //4
}

/// <summary>
/// State machine for the phone interface, managing different states and transitions.
/// </summary>
public class PhoneStateMachine : MonoBehaviour
{
    private PhoneState _currentState = PhoneState.Idle;

    [Header("Buttons")]
    [SerializeField]
    private PhoneTabButton _backButton;

    [SerializeField]
    private PhoneTabButton _homeButton;

    [SerializeField]
    private PhoneTabButton _tabButton;


    [Header("Tabs")]
    [SerializeField]
    private GameObject _startTab;
    [SerializeField]
    private GameObject _contactsTab;

    [SerializeField]
    private GameObject _messagingTab;

    [SerializeField]
    private GameObject _galleryTab;

    [SerializeField]
    private GameObject _emailTab;

    [SerializeField]
    private GameObject _decisionsTab;

    [SerializeField]
    private GameObject _dialogueTab;


    private int _backButtonState = 0; // State to change to when the back button is pressed

    private int _tabButtonState = 0; // State to change to when the tab button is pressed

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _backButton.GetComponent<Button>().onClick.AddListener(() => ChangeState(_backButtonState));
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    /// <summary>
    /// Changes the current state to the specified new state and updates the button callbacks accordingly.
    /// </summary>
    /// <param name="newState"></param>
    public void ChangeState(int newState)
    {
        if (_currentState != (PhoneState)newState)
        {
            _currentState = (PhoneState)newState;
            SetButtonsCallbacks();
        }
    }

    /// <summary>
    /// Sets the callbacks for the buttons based on the current state of the phone.
    /// </summary>
    private void SetButtonsCallbacks()
    {
        switch (_currentState)
        {
            case PhoneState.Idle:
                Debug.Log("[PhoneStateMachine] Idle state entered.");
                _backButton.SetTabsToClose(new List<GameObject> { });
                _backButton.SetTabToOpen(null);
                _backButtonState = 0;
                break;
            case PhoneState.Contacts:
                Debug.Log("[PhoneStateMachine] Contacts state entered. Refreshing contacts UI.");
                _homeButton.SetTabsToClose(new List<GameObject> { _contactsTab });
                _backButton.SetTabsToClose(new List<GameObject> { _contactsTab});
                _backButton.SetTabToOpen(_startTab);
                _backButtonState = 0;
                
                if (MessagesManager.Instance != null && MessagesManager.Instance.GetMessagesUI() != null)
                {
                    MessagesManager.Instance.GetMessagesUI().RefreshContacts();
                }
                break;
            case PhoneState.Messaging:
                Debug.Log("[PhoneStateMachine] Messaging state entered. Refreshing messages UI.");
                _homeButton.SetTabsToClose(new List<GameObject> { _messagingTab });
                _backButton.SetTabsToClose(new List<GameObject> { _messagingTab });
                _backButton.SetTabToOpen(_contactsTab);
                _backButtonState = 2;
                break;
            case PhoneState.Gallery:
                Debug.Log("[PhoneStateMachine] Gallery state entered.");
                _homeButton.SetTabsToClose(new List<GameObject> { _galleryTab });
                _backButton.SetTabsToClose(new List<GameObject> { _galleryTab });
                _backButton.SetTabToOpen(_startTab);
                _backButtonState = 0;
                break;
            case PhoneState.Email:
                _homeButton.SetTabsToClose(new List<GameObject> { _emailTab });
                _backButton.SetTabsToClose(new List<GameObject> { _emailTab });
                _backButton.SetTabToOpen(_startTab);
                _backButtonState = 0;
                break;
        }
    }

    #region Getters
    public GameObject GetStartTab()
    {
        return _startTab;
    }

    public GameObject GetContactsTab()
    {
        return _contactsTab;
    }

    public GameObject GetMessagingTab()
    {
        return _messagingTab;
    }

    public GameObject GetGalleryTab()
    {
        return _galleryTab;
    }

    public GameObject GetEmailTab()
    {
        return _emailTab;
    }
    #endregion

    /// <summary>
    /// Show the decision tab and include it in the list of tabs to close when coming back to other tabs
    /// </summary>
    public void ShowDecisionsTab()
    {
        _homeButton.GetTabsToClose().Add(_decisionsTab);
        _backButton.GetTabsToClose().Add(_decisionsTab);
        _decisionsTab.SetActive(true);
    }

    public void ShowDialogueTab()
    {
        _homeButton.GetTabsToClose().Add(_dialogueTab);
        _backButton.GetTabsToClose().Add(_dialogueTab);
        _dialogueTab.SetActive(true);
    }
}
