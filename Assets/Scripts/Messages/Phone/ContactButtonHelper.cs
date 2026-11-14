using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ContactButtonHelper : MonoBehaviour
{
    private string _contactName;
    private bool _listenersConfigured;

    void Start()
    {
        if (!_listenersConfigured)
        {
            SetButtonActions();
        }
    }

    /// <summary>
    /// Explicitly assigns the contact name and binds click behavior before badge text objects are inspected.
    /// </summary>
    public void Configure(string contactName)
    {
        _contactName = contactName;
        SetButtonActions();
    }

    private string ResolveContactName()
    {
        if (!string.IsNullOrEmpty(_contactName))
        {
            return _contactName;
        }

        Transform nameChild = transform.Find("Text (TMP)");
        TextMeshProUGUI nameTmp = nameChild != null
            ? nameChild.GetComponent<TextMeshProUGUI>()
            : GetComponentInChildren<TextMeshProUGUI>();

        return nameTmp != null ? nameTmp.text : string.Empty;
    }

    /// <summary>
    /// Sets the actions for the button when it is clicked: marks unread notifications as read,
    /// opens the messaging tab, and loads the contact's messages and active narrative options.
    /// </summary>
    private void SetButtonActions()
    {
        Button button = GetComponent<Button>();
        if (button == null || _listenersConfigured) return;

        _listenersConfigured = true;
        button.onClick.AddListener(OnContactClicked);
    }

    private void OnContactClicked()
    {
        if (MessagesManager.Instance == null) return;

        string contactName = ResolveContactName();
        MessagesManager.Instance.MarkContactNotificationsAsRead(contactName);
        MessagesManager.Instance.GetPhoneStateMachine()?.ChangeState((int)PhoneState.MESSAGING);
        MessagesManager.Instance.SetCurrentContact(contactName);

        MessagesUI ui = MessagesManager.Instance.GetMessagesUI();
        if (ui != null)
        {
            ui.SetContactName(contactName);
            ui.CreateMessages();
        }

        OptionsChanger optionsChanger = MessagesManager.Instance.GetOptionsChanger();
        if (optionsChanger != null)
        {
            optionsChanger.SetCurrentNode(MessagesManager.Instance.GetCurrentContactNarrativeNode());
        }

        ShowDecisionAvailable();
        ShowDialogueAvailable();
    }

    private void ShowDecisionAvailable()
    {
        NarrativeNode node = MessagesManager.Instance.GetCurrentContactNarrativeNode();
        int contactNarrativeId = node != null ? node.ID() : -1;
        if (contactNarrativeId != -1 && MessagesManager.Instance.GetNarrativeNode(contactNarrativeId).HasOptions())
        {
            MessagesManager.Instance.GetPhoneStateMachine()?.ShowDecisionsTab();
        }
    }

    private void ShowDialogueAvailable()
    {
        Contact current = MessagesManager.Instance.GetCurrentContact();
        if (current != null && current.HasNewMessages())
        {
            MessagesManager.Instance.GetPhoneStateMachine()?.ShowDialogueTab();
        }
    }
}
