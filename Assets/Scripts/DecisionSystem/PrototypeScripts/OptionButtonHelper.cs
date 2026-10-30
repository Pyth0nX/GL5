using UnityEngine;
using UnityEngine.UI;

public class OptionButtonHelper : MonoBehaviour
{
    [SerializeField]
    private int _optionIndex;
    private Button _button;

    public int OptionIndex => _optionIndex;

    public void SetOptionIndex(int index)
    {
        _optionIndex = index;
    }

    void Start()
    {
        _button = GetComponent<Button>();
        SetButtonCallbacks();
    }

    void SetButtonCallbacks()
    {
        if (_button != null)
        {
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() =>
            {
                if (MessagesManager.Instance != null && MessagesManager.Instance.GetOptionsChanger() != null)
                {
                    MessagesManager.Instance.GetOptionsChanger().ChangeOptions(_optionIndex);
                }
            });
        }
    }
}
