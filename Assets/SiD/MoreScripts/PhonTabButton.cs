using UnityEngine;

public class PhoneTabButton : MonoBehaviour
{
    public GameObject tabToOpen;
    public GameObject[] tabsToClose;

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
}