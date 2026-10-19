using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using DG.Tweening;
using UnityEngine.Rendering;

public enum UIState
{
    DEFAULT,
    PHONE,
    MAP,
    MENU,
    CONVERSATION,
    NONE

}

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("UI Elements")]
    [SerializeField] private GameObject _pauseMenuPanel;
    [SerializeField] private GameObject _phone;
    [SerializeField] private CanvasGroup _interactionCanvasGroup;
    [SerializeField] private TMP_Text _interactionText;
    [SerializeField] private GameObject _map;

    [Header("Messages Animations")]
    [SerializeField] private float _appearTime = 0.3f;
    [SerializeField] private float _disappearTime = 0.5f;

    [Header("Blur Options")]
    [SerializeField] private Volume _blurVolume;
    [SerializeField] private float _fadeDuration;
    private InputAction _menuAction;
    private Tween _fadeTween;
    private Tween _blurTween;
    private Tween _delayTween;

    private UIState _currentState = UIState.DEFAULT;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (_pauseMenuPanel != null)
        {
            _pauseMenuPanel.SetActive(false);
        }

        if (_interactionCanvasGroup != null)
        {
            _interactionCanvasGroup.alpha = 0f;
        }

        EventBus.Instance.Subscribe("OpenMap", ShowMap);
        EventBus.Instance.Subscribe("CloseMap", HideMap);

    }

    private void OnDisable()
    {
        EventBus.Instance.Unsubscribe("OpenMap", ShowMap);
        EventBus.Instance.Unsubscribe("CloseMap", HideMap);
    }

    private void Update()
    {
        if(InputManager.Instance.MenuWasPressed())
        {
            TogglePause();
        }
    }

    public void TogglePause()
    {
        if (_currentState == UIState.DEFAULT) ChangeState(3);
        else if (_currentState == UIState.MENU) ChangeState(0);
    }

    public void ShowMessage(string message, Color colorText)
    {
        if (_interactionCanvasGroup == null || _interactionText == null) return;

        // Stop every previous animation
        _fadeTween?.Kill();
        _delayTween?.Kill();
        _interactionCanvasGroup.DOKill();

        // Update the info
        _interactionText.text = message;
        _interactionText.color = colorText;

        // animate until the alpha is 1
        _fadeTween = _interactionCanvasGroup.DOFade(1f, _appearTime);
    }

    public void HideMessage()
    {
        if (_interactionCanvasGroup == null) return;

        // Stop every previous animation
        _fadeTween?.Kill();
        _delayTween?.Kill();
        _interactionCanvasGroup.DOKill();

        // Animate until the alpha is 0
        _fadeTween = _interactionCanvasGroup.DOFade(0f, _disappearTime);
    }

    public void ShowTemporaryMessage(string message, Color textColor, float duration)
    {
        if (_interactionCanvasGroup == null || _interactionText == null) return;

        ShowMessage(message, textColor);

        // After 'duration' it hides the text
        _delayTween = DOVirtual.DelayedCall(duration, () => 
        {
            HideMessage();
        });
    }

    public void LockMouse()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void UnlockMouse()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ShowMap()
    {
        ChangeState(2);
    }

    public void HideMap()
    {
        ChangeState(0);
    }


    public void ChangeState(int newState) 
    {
        if ((UIState)newState == _currentState) return;

        Debug.Log("[UIManager] Current:" + _currentState);
        ExitCurrentState();
        _currentState = (UIState)newState;
        EnterNewState();
    }



    private void ExitCurrentState()
    {
        switch (_currentState)
        {
            case UIState.DEFAULT:
                {
                    InputManager.Instance.DisableMovementMap();
                    InputManager.Instance.DisableInteract();
                    UnlockMouse();
                }
                break;
            case UIState.PHONE:
                {
                    _phone.SetActive(false);
                    EnableBlur(false);
                    EventBus.Instance.Publish("Phone_Closed",0);
                }
                break;
            case UIState.MAP:
                {
                    _map.SetActive(false);
                }
                break;
            case UIState.CONVERSATION:
                {
                    EventBus.Instance.Publish("Conversation_Ended");
                }
                break;
            case UIState.MENU:
                {
                    Time.timeScale = 1f;
                    _pauseMenuPanel.SetActive(false);
                }
                break;
        }
    }

    private void EnterNewState()
    {
        switch (_currentState)
        {
            case UIState.DEFAULT:
                {
                    InputManager.Instance.EnableMovementMap();
                    InputManager.Instance.EnablePhone();
                    InputManager.Instance.EnableMenuAction();
                    InputManager.Instance.EnableInteract();
                    LockMouse();
                }
                break;
            case UIState.PHONE:
                {
                    EventBus.Instance.Publish("Phone_Opened");
                    _phone.SetActive(true);
                    EnableBlur(true);
                    InputManager.Instance.DisableInteract();
                    InputManager.Instance.DisableMenuAction();
                }
                break;
            case UIState.MAP:
                {
                    _map.SetActive(true);
                    InputManager.Instance.DisableInteract();
                    InputManager.Instance.DisablePhone();
                }
                break;
            case UIState.CONVERSATION:
                {
                    EventBus.Instance.Publish("Conversation_Started");
                    InputManager.Instance.DisableInteract();
                    InputManager.Instance.DisablePhone();
                }
                break;
            case UIState.MENU:
                {
                    _pauseMenuPanel.SetActive(true);
                    InputManager.Instance.DisableInteract();
                    InputManager.Instance.DisablePhone();
                }
                break;
        }

    }


    private void EnableBlur(bool enable)
    {
        if (_blurVolume == null) {
            Debug.LogWarning("UIManager: _blurVolume not assigned");
            return;
        }

        _blurTween?.Kill();
        float targetWeight = enable ? 1f : 0f;

        DOTween.To(() => _blurVolume.weight, x => _blurVolume.weight = x, targetWeight, _fadeDuration).SetUpdate(true);
    }
}