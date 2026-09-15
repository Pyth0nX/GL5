using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class OptionsChanger : MonoBehaviour
{
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
        //set the initial narrative text and button texts based on the current node
        _mainText.text = _currentNode.narrativeText;
        _option1Button.GetComponentInChildren<TextMeshProUGUI>().text = _currentNode.options[0].optionText;
        _option2Button.GetComponentInChildren<TextMeshProUGUI>().text = _currentNode.options[1].optionText;
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void ChangeOptions(int selectedOption)
    {
        _currentNode = _currentNode.options[selectedOption].nextNode;

        _mainText.text = _currentNode.narrativeText;

        // Update the button texts
        _option1Button.GetComponentInChildren<TextMeshProUGUI>().text = _currentNode.options[0].optionText;
        _option2Button.GetComponentInChildren<TextMeshProUGUI>().text = _currentNode.options[1].optionText;
    }
}
