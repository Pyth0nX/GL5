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
    private PhoneTabButton backButton;

    [SerializeField]
    private PhoneTabButton homeButton;

    [SerializeField]
    private PhoneTabButton tabButton;


    [Header("Tabs")]
    [SerializeField]
    private GameObject startTab;
    [SerializeField]
    private GameObject contactsTab;

    [SerializeField]
    private GameObject messagingTab;

    [SerializeField]
    private GameObject galleryTab;

    [SerializeField]
    private GameObject emailTab;

    [SerializeField]
    private GameObject decisionsTab;


    private int _backButtonState = 0; // State to change to when the back button is pressed

    private int _tabButtonState = 0; // State to change to when the tab button is pressed

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        backButton.GetComponent<Button>().onClick.AddListener(() => ChangeState(_backButtonState));
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
                backButton.SetTabsToClose(new List<GameObject> { });
                backButton.SetTabToOpen(null);
                _backButtonState = 0;
                break;
            case PhoneState.Contacts:
                homeButton.SetTabsToClose(new List<GameObject> { contactsTab });
                backButton.SetTabsToClose(new List<GameObject> { contactsTab});
                backButton.SetTabToOpen(startTab);
                _backButtonState = 0;
                break;
            case PhoneState.Messaging:
                homeButton.SetTabsToClose(new List<GameObject> { messagingTab });
                backButton.SetTabsToClose(new List<GameObject> { messagingTab });
                backButton.SetTabToOpen(contactsTab);
                _backButtonState = 2;
                break;
            case PhoneState.Gallery:
                homeButton.SetTabsToClose(new List<GameObject> { galleryTab });
                backButton.SetTabsToClose(new List<GameObject> { galleryTab });
                backButton.SetTabToOpen(startTab);
                _backButtonState = 0;
                break;
            case PhoneState.Email:
                homeButton.SetTabsToClose(new List<GameObject> { emailTab });
                backButton.SetTabsToClose(new List<GameObject> { emailTab });
                backButton.SetTabToOpen(startTab);
                _backButtonState = 0;
                break;
        }
    }

    #region Getters
    public GameObject GetStartTab()
    {
        return startTab;
    }

    public GameObject GetContactsTab()
    {
        return contactsTab;
    }

    public GameObject GetMessagingTab()
    {
        return messagingTab;
    }

    public GameObject GetGalleryTab()
    {
        return galleryTab;
    }

    public GameObject GetEmailTab()
    {
        return emailTab;
    }
    #endregion

    /// <summary>
    /// Show the decision tab and include it in the list of tabs to close when coming back to other tabs
    /// </summary>
    public void ShowDecisionsTab()
    {
        homeButton.GetTabsToClose().Add(decisionsTab);
        backButton.GetTabsToClose().Add(decisionsTab);
        decisionsTab.SetActive(true);
    }
}
