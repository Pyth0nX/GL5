using UnityEngine;
using UnityEngine.UI;

public class OptionButtonHelper : MonoBehaviour
{
    [SerializeField]
    private int _optionIndex;
    private Button _button;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _button = GetComponent<Button>();
        SetButtonCallbacks();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void SetButtonCallbacks()
    {
        _button.onClick.AddListener(() => MessagesManager.Instance.GetOptionsChanger().ChangeOptions(_optionIndex));
    }
}
