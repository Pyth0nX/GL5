using System;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[Serializable]
public enum PhoneState
{
    IDLE,       //0
    MESSAGING,  //1
    CONTACTS,   //2
    GALLERY,    //3
    EMAIL,      //4
}

/// <summary>
/// State machine for the phone interface, managing different states and transitions.
/// </summary>
public class PhoneStateMachine : MonoBehaviour
{
    private PhoneState _currentState = PhoneState.IDLE;

    [Header("Buttons")]
    [SerializeField]
    private Button _backButton;

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
    private GameObject _quickGalleryButton;

    [SerializeField]
    private GameObject _dialogueTab;

    [Header("App Managers (Optional - Auto-Detected)")]
    [SerializeField]
    private GalleryManager _galleryManager;


    private int _backButtonState = 0; // State to change to when the back button is pressed

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (_backButton != null)
        {
            _backButton.onClick.AddListener(OnBackButtonPressed);
        }

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe<int>("Phone_Closed", ChangeState);
        }
    }

    private void OnDestroy()
    {
        if (_backButton != null)
        {
            _backButton.onClick.RemoveListener(OnBackButtonPressed);
        }

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe<int>("Phone_Closed", ChangeState);
        }
    }

    private void OnBackButtonPressed()
    {
        if (_currentState == PhoneState.GALLERY)
        {
            EnsureGalleryManager();
            if (_galleryManager != null && _galleryManager.TryHandleBackButton())
            {
                return;
            }
        }

        ChangeState(_backButtonState);
    }

    private void EnsureGalleryManager()
    {
        if (_galleryManager == null && _galleryTab != null)
        {
            _galleryManager = _galleryTab.GetComponentInChildren<GalleryManager>(true);
        }

        if (_galleryManager == null)
        {
            _galleryManager = GalleryManager.Instance;
        }
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
        PhoneState targetState = (PhoneState)newState;
        if (_currentState == targetState) return;

        _backButtonState = (int)_currentState;
        ExitCurrentState(targetState);
        _currentState = targetState;
        EnterNewState();
    }

    public void SetBackButtonState(int state)
    {
        _backButtonState = state;
    }

    public PhoneState GetCurrentState()
    {
        return _currentState;
    }

    /// <summary>
    /// Disable the tab associated to the current state
    /// </summary>
    private void ExitCurrentState(PhoneState nextState = PhoneState.IDLE)
    {
        switch (_currentState)
        {
            case PhoneState.IDLE:
                _startTab?.SetActive(false);
                break;
            case PhoneState.CONTACTS:
                _contactsTab?.SetActive(false);
                break;
            case PhoneState.MESSAGING:
                _messagingTab?.SetActive(false);
                _quickGalleryButton?.SetActive(false);
                _dialogueTab?.SetActive(false);

                // Hide decisions panel if it was open during mobile messaging when switching tabs
                OptionsChanger optionsChanger = MessagesManager.Instance?.GetOptionsChanger();
                if (optionsChanger != null && optionsChanger.IsDecisionsPanelActive())
                {
                    optionsChanger.ActiveDecisionsPanel(false);
                }

                // Keep MESSAGING as return state if navigating to GALLERY (Quick Select); otherwise return to IDLE
                if (nextState != PhoneState.GALLERY)
                {
                    _backButtonState = (int)PhoneState.IDLE;
                }
                break;
            case PhoneState.GALLERY:
                _galleryTab?.SetActive(false);
                break;
            case PhoneState.EMAIL:
                break;
        }

    }

    /// <summary>
    /// Enable the tab associated with the new state and calls the required methods if needed
    /// </summary>
    private void EnterNewState()
    {
        switch (_currentState)
        {
            case PhoneState.IDLE:
                _startTab?.SetActive(true);
                break;
            case PhoneState.CONTACTS:
                _contactsTab?.SetActive(true);
                MessagesManager.Instance?.GetMessagesUI()?.RefreshContacts();
                break;
            case PhoneState.MESSAGING:
                _messagingTab?.SetActive(true);
                _quickGalleryButton?.SetActive(true);
                break;
            case PhoneState.GALLERY:
                _galleryTab?.SetActive(true);
                break;
            case PhoneState.EMAIL:
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

    
    #endregion

    /// <summary>
    /// Show the decision tab and include it in the list of tabs to close when coming back to other tabs
    /// </summary>
    public void ShowDecisionsTab()
    {
        //_homeButton.GetTabsToClose().Add(_decisionsTab);
        //_backButton.GetTabsToClose().Add(_decisionsTab);
        //_decisionsTab.SetActive(true);
    }

    public void ShowDialogueTab()
    {
        _dialogueTab.SetActive(true);
    }

}
