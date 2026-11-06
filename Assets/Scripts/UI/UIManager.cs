using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
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
    MAIN_MENU,
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
    [SerializeField] private GameObject _mainMenuUI;

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

    private UIState _currentState = UIState.NONE;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            ResolveMainMenuReference();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        ResolveMainMenuReference();

        if (_pauseMenuPanel != null)
        {
            _pauseMenuPanel.SetActive(false);
        }

        if (_interactionCanvasGroup != null)
        {
            _interactionCanvasGroup.alpha = 0f;
        }

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe("OpenMap", ShowMap);
            EventBus.Instance.Subscribe("CloseMap", HideMap);
        }

        SyncInitialSceneState();
    }

    private void OnDisable()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe("OpenMap", ShowMap);
            EventBus.Instance.Unsubscribe("CloseMap", HideMap);
        }
    }

    private void Update()
    {
        if (InputManager.Instance != null && InputManager.Instance.MenuWasTapped())
        {
            TogglePause();
        }
    }

    private void ResolveMainMenuReference()
    {
        if (_mainMenuUI != null) return;

        Transform[] children = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != null && string.Equals(children[i].name, "MainMenuUI", System.StringComparison.OrdinalIgnoreCase))
            {
                _mainMenuUI = children[i].gameObject;
                break;
            }
        }
    }

    private void SyncInitialSceneState()
    {
        // SceneManager.sceneLoaded does not fire for the initial startup scene unless already transitioned
        if (_currentState != UIState.NONE) return;

        string activeScene = SceneManager.GetActiveScene().name;
        bool isMainMenu = string.Equals(activeScene, "MainMenu", System.StringComparison.OrdinalIgnoreCase);

        if (_mainMenuUI != null)
        {
            _mainMenuUI.SetActive(isMainMenu);
        }

        ChangeState(isMainMenu ? (int)UIState.MAIN_MENU : (int)UIState.DEFAULT);
    }

    public UIState GetCurrentState() => _currentState;

    public GameObject GetPauseMenuPanel() => _pauseMenuPanel;
    public GameObject GetMainMenuUI() => _mainMenuUI;

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
        ResolveMainMenuReference();

        UIState targetState = (UIState)newState;
        if (targetState == _currentState)
        {
            // Ensure visual panel visibility stays strictly synced even on redundant state calls
            if (_mainMenuUI != null)
            {
                _mainMenuUI.SetActive(targetState == UIState.MAIN_MENU);
            }
            return;
        }

        Debug.Log("[UIManager] Previous:" + _currentState);
        ExitCurrentState();
        _currentState = targetState;
        Debug.Log("[UIManager] Current:" + _currentState);
        EnterNewState();
        HideMessage();
    }

    private void ExitCurrentState()
    {
        switch (_currentState)
        {
            case UIState.DEFAULT:
                {
                    if (InputManager.Instance != null)
                    {
                        InputManager.Instance.DisableMovementMap();
                        InputManager.Instance.DisableInteract();
                    }
                    UnlockMouse();
                }
                break;
            case UIState.PHONE:
                {
                    if (_phone != null) _phone.SetActive(false);
                    EnableBlur(false);
                    if (EventBus.Instance != null) EventBus.Instance.Publish("Phone_Closed", 0);
                }
                break;
            case UIState.MAP:
                {
                    if (_map != null) _map.SetActive(false);
                }
                break;
            case UIState.CONVERSATION:
                {
                    if (EventBus.Instance != null) EventBus.Instance.Publish("Conversation_Ended");
                }
                break;
            case UIState.MENU:
                {
                    Time.timeScale = 1f;
                    if (_pauseMenuPanel != null) _pauseMenuPanel.SetActive(false);
                    if (EventBus.Instance != null) EventBus.Instance.Publish("PauseMenu_Closed");
                }
                break;
            case UIState.MAIN_MENU:
                {
                    if (_mainMenuUI != null) _mainMenuUI.SetActive(false);
                }
                break;
        }
    }

    private void EnterNewState()
    {
        // Guarantee MainMenuUI is always hidden in every non-MAIN_MENU state
        if (_currentState != UIState.MAIN_MENU && _mainMenuUI != null && _mainMenuUI.activeSelf)
        {
            _mainMenuUI.SetActive(false);
        }

        switch (_currentState)
        {
            case UIState.DEFAULT:
                {
                    if (InputManager.Instance != null)
                    {
                        InputManager.Instance.EnableMovementMap();
                        InputManager.Instance.EnablePhone();
                        InputManager.Instance.EnableMenuAction();
                        InputManager.Instance.EnableInteract();
                    }
                    LockMouse();
                }
                break;
            case UIState.PHONE:
                {
                    if (EventBus.Instance != null) EventBus.Instance.Publish("Phone_Opened");
                    if (_phone != null) _phone.SetActive(true);
                    EnableBlur(true);
                    if (InputManager.Instance != null)
                    {
                        InputManager.Instance.DisableInteract();
                        InputManager.Instance.DisableMenuAction();
                    }
                    UnlockMouse();
                }
                break;
            case UIState.MAP:
                {
                    if (_map != null) _map.SetActive(true);
                    if (InputManager.Instance != null)
                    {
                        InputManager.Instance.DisableInteract();
                        InputManager.Instance.DisablePhone();
                    }
                    UnlockMouse();
                }
                break;
            case UIState.CONVERSATION:
                {
                    Debug.Log("[UIManager] Conversation");
                    if (EventBus.Instance != null) EventBus.Instance.Publish("Conversation_Started");
                    if (InputManager.Instance != null)
                    {
                        InputManager.Instance.DisableInteract();
                        InputManager.Instance.DisablePhone();
                    }
                    UnlockMouse();
                }
                break;
            case UIState.MENU:
                {
                    if (_pauseMenuPanel != null) _pauseMenuPanel.SetActive(true);
                    if (InputManager.Instance != null)
                    {
                        InputManager.Instance.DisableInteract();
                        InputManager.Instance.DisablePhone();
                    }
                    UnlockMouse();
                }
                break;
            case UIState.MAIN_MENU:
                {
                    if (_mainMenuUI != null) _mainMenuUI.SetActive(true);
                    if (InputManager.Instance != null)
                    {
                        InputManager.Instance.DisableMovementMap();
                        InputManager.Instance.DisableInteract();
                        InputManager.Instance.DisablePhone();
                        InputManager.Instance.DisableMenuAction();
                    }
                    UnlockMouse();
                }
                break;
        }
    }

    private void EnableBlur(bool enable)
    {
        if (_blurVolume == null)
        {
            Debug.LogWarning("UIManager: _blurVolume not assigned");
            return;
        }

        _blurTween?.Kill();
        float targetWeight = enable ? 1f : 0f;

        DOTween.To(() => _blurVolume.weight, x => _blurVolume.weight = x, targetWeight, _fadeDuration).SetUpdate(true);
    }
}
