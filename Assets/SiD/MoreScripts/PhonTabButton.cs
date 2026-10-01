using UnityEngine;
using System.Collections.Generic;

public class PhoneTabButton : MonoBehaviour
{
    public GameObject tabToOpen;
    public List<GameObject> tabsToClose;

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