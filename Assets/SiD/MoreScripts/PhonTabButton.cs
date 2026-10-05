using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class PhoneTabButton : MonoBehaviour
{
    public GameObject tabToOpen;
    public List<GameObject> tabsToClose;

    [SerializeField]
    private int _targetStateId = -1;

    private void Start()
    {
        if (_targetStateId != -1)
        {
            if (MessagesManager.Instance != null)
            {
                PhoneStateMachine phoneStateMachine = MessagesManager.Instance.GetComponent<PhoneStateMachine>();
                if (phoneStateMachine != null)
                {
                    Button button = GetComponent<Button>();
                    if (button != null)
                    {
                        button.onClick.AddListener(() => phoneStateMachine.ChangeState(_targetStateId));
                    }
                }
            }
        }
    }

    public void OpenTab()
    {
        foreach (GameObject tab in tabsToClose)
        {
            if (tab != null)
                tab.SetActive(false);
        }

        if (tabToOpen != null)
            tabToOpen.SetActive(true);
    }

    public void SetTabToOpen(GameObject tab)
    {
        tabToOpen = tab;
    }

    public void SetTabsToClose(List<GameObject> tabs)
    {
        tabsToClose = tabs;
    }


    public List<GameObject> GetTabsToClose()
    {
        return tabsToClose;
    }
}