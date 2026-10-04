using UnityEngine;
using TMPro; 

public class ObjectivesUI : MonoBehaviour
{
    public TextMeshProUGUI _objectiveText;

    private void Start()
    {
        ObjectivesManager.Instance.OnObjectiveUpdated += UpdateUI;
    }

    private void UpdateUI(Objective currentObj, int progress)
    {
        if (currentObj == null)
        {
            _objectiveText.text = "All Objectives Complete!";
        }
        else
        {
            _objectiveText.text = $"{currentObj.description} ({progress}/{currentObj.targetCount})";
        }
    }
}