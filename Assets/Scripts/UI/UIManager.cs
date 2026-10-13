using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using DG.Tweening;



public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("UI Elements")]
    [SerializeField] private GameObject _pauseMenuPanel;
    [SerializeField] private CanvasGroup _interactionCanvasGroup;
    [SerializeField] private TMP_Text _interactionText;
    [SerializeField] private GameObject _map;

    [Header("Messages Animations")]
    [SerializeField] private float _appearTime = 0.3f;
    [SerializeField] private float _disappearTime = 0.5f;

    private InputAction _menuAction;
    private bool _isPaused = false;
    private Tween _fadeTween;
    private Tween _delayTween;

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
        _isPaused = !_isPaused;

        if (_isPaused)
        {
            PauseGame();
        }
        else
        {
            ResumeGame();
        }

        if (_pauseMenuPanel != null)
        {
            _pauseMenuPanel.SetActive(_isPaused);
        }
    }

    private void PauseGame()
    {
        _pauseMenuPanel.SetActive(true);
        Time.timeScale = 0f;

        UnlockMouse();
        InputManager.Instance.DisableMovementMap();
        InputManager.Instance.DisablePhone();
        InputManager.Instance.DisableInteract();
    }

    public void ResumeGame()
    {
        _isPaused = false;
        _pauseMenuPanel.SetActive(false);
        Time.timeScale = 1f;

        LockMouse();
        InputManager.Instance.EnableMovementMap();
        InputManager.Instance.EnablePhone();
        InputManager.Instance.EnableInteract();
    }

    public void ShowMessage(string mensaje, Color colorTexto)
    {
        if (_interactionCanvasGroup == null || _interactionText == null) return;

        // Stop every previous animation
        _fadeTween?.Kill();
        _delayTween?.Kill();
        _interactionCanvasGroup.DOKill();

        // Update the info
        _interactionText.text = mensaje;
        _interactionText.color = colorTexto;

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
        _map.SetActive(true);
    }

    public void HideMap()
    {
        _map.SetActive(false);
    }
}