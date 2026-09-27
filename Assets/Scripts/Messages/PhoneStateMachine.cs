using System;
using UnityEngine;
using UnityEngine.UI;

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
    /// Sets the callbacks for the buttons based on the current state of the phone.
    /// </summary>
    private void SetButtonsCallbacks()
    {

    }

    public void ChangeState(int newState)
    {
        if (_currentState != (PhoneState)newState)
        {
            _currentState = (PhoneState)newState;
            ChangeButtonsCalls();
        }
    }

    private void ChangeButtonsCalls()
    {
        switch (_currentState)
        {
            case PhoneState.Idle:
                backButton.SetTabsToClose(new GameObject[] {});
                backButton.SetTabToOpen(null);
                _backButtonState = 0;
                break;
            case PhoneState.Contacts:
                homeButton.SetTabsToClose(new GameObject[] { contactsTab });
                backButton.SetTabsToClose(new GameObject[] {contactsTab});
                backButton.SetTabToOpen(startTab);
                _backButtonState = 0;
                break;
            case PhoneState.Messaging:
                homeButton.SetTabsToClose(new GameObject[] { messagingTab });
                backButton.SetTabsToClose(new GameObject[] { messagingTab });
                backButton.SetTabToOpen(contactsTab);
                _backButtonState = 2;
                break;
            case PhoneState.Gallery:
                homeButton.SetTabsToClose(new GameObject[] { galleryTab });
                backButton.SetTabsToClose(new GameObject[] { galleryTab });
                backButton.SetTabToOpen(startTab);
                _backButtonState = 0;
                break;
            case PhoneState.Email:
                homeButton.SetTabsToClose(new GameObject[] { emailTab });
                backButton.SetTabsToClose(new GameObject[] { emailTab });
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

}
