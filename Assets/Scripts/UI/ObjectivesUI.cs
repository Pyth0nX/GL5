using UnityEngine;
using TMPro;

public class ObjectivesUI : MonoBehaviour
{
    [Header("UI Components")]
    [Tooltip("The TextMeshPro text element that will show the objective description.")]
    [SerializeField] private TextMeshProUGUI _objectiveText;
    
    [Tooltip("The GameObject representing the whole objectives panel (so it can be hidden if there are no objectives).")]
    [SerializeField] private GameObject _panel;

    private void Start()
    {
        // Subscribe to the objectives manager event
        if (ObjectivesManager.Instance != null)
        {
            ObjectivesManager.Instance.OnObjectiveUpdated += UpdateUI;
            // Manually update UI once to ensure it doesn't say "New Text"
            UpdateUI(ObjectivesManager.Instance.GetCurrentObjective(), ObjectivesManager.Instance.GetCurrentProgress());
        }
        else
        {
            Debug.LogWarning("[ObjectivesUI] ObjectivesManager instance not found!");
        }
    }

    private void OnDestroy()
    {
        // Always unsubscribe to prevent memory leaks
        if (ObjectivesManager.Instance != null)
        {
            ObjectivesManager.Instance.OnObjectiveUpdated -= UpdateUI;
        }
    }

    private void UpdateUI(Objective currentObj, int progress)
    {
        // If the objective is null, we have completed the game or there are no objectives left
        if (currentObj == null)
        {
            if (_panel != null) _panel.SetActive(false);
            if (_objectiveText != null) _objectiveText.text = "";
            return;
        }

        // Ensure the panel is visible if we have an active objective
        if (_panel != null && !_panel.activeSelf)
        {
            _panel.SetActive(true);
        }

        // If the target count is greater than 1, we show a progress counter like (1/3)
        if (currentObj.targetCount > 1)
        {
            _objectiveText.text = $"{currentObj.description} ({progress}/{currentObj.targetCount})";
        }
        else
        {
            // If the target is just 1 (e.g. "Go to class"), we just show the description
            _objectiveText.text = currentObj.description;
        }
    }
}
