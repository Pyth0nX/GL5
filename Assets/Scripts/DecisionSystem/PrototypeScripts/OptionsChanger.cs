using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class OptionsChanger : MonoBehaviour
{
    [SerializeField]
    private GameObject _decisionsPanel;

    [SerializeField]
    private NarrativeNode _currentNode;

    [SerializeField]
    private TextMeshProUGUI _mainText;

    [SerializeField]
    private Button _option1Button;
    [SerializeField]
    private Button _option2Button;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        UpdateButtonsTexts();
    }

    /// <summary>
    /// Update the buttons texts with the current node info
    /// </summary>
    private void UpdateButtonsTexts()
    {
        //set the initial narrative text and button texts based on the current node
        _option1Button.GetComponentInChildren<TextMeshProUGUI>().text = _currentNode.Options()[0].text;
        _option2Button.GetComponentInChildren<TextMeshProUGUI>().text = _currentNode.Options()[1].text;
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void ChangeOptions(int selectedOptionId)
    {
        Option selectedOption =  _currentNode.Options()[selectedOptionId];

        foreach (var message in selectedOption.messagesToSend)
        {
            MessagesManager.Instance.SendMessageToCurrentContact(message);
        }

        //_currentNode = selectedOption.nextNode;

        if(_currentNode == null)
        {
            ActiveDecisionsPanel(false);
            return;
        }

        UpdateButtonsTexts();
    }

    public void ActiveDecisionsPanel(bool isActive)
    {
        _decisionsPanel.SetActive(isActive);
    }
}
