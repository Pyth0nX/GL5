using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using DG.Tweening;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("UI Elements")]
    [SerializeField] private GameObject _pauseMenuPanel;
    [SerializeField] private CanvasGroup _interaccionCanvasGroup;
    [SerializeField] private TMP_Text _textoInteraccion;

    [Header("Animación Mensajes")]
    [SerializeField] private float _tiempoAparicion = 0.3f;
    [SerializeField] private float _tiempoDesaparicion = 0.5f;

    [Header("References")]
    [SerializeField] private PlayerController _playerController;
    [SerializeField] private PlayerInput _playerInput;

    private InputAction _menuAction;
    private bool _isPaused = false;
    private Tween _fadeTween;
    private Tween _delayTween;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
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

        if (_interaccionCanvasGroup != null)
        {
            _interaccionCanvasGroup.alpha = 0f;
        }

        if (_playerInput != null)
        {
            _menuAction = _playerInput.actions["Menu"];

            if (_menuAction == null)
            {
                Debug.LogError("[UIManager] Couldn't find the 'Menu' action in the PlayerInput.");
            }
        }
        else
        {
            Debug.LogError("[UIManager] Need to assign the PlayerInput reference in the Inspector.");
        }
    }

    private void Update()
    {
        if(_menuAction.WasPressedThisFrame())
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

        if (_playerController != null)
        {
            _playerController.UnlockMouse();
            _playerController.DisableInput();
        }
    }

    public void ResumeGame()
    {
        _isPaused = false;
        _pauseMenuPanel.SetActive(false);
        Time.timeScale = 1f;

        if (_playerController != null)
        {
            _playerController.LockMouse();
            _playerController.EnableInput(); 
        }
    }

    public void ShowMessage(string mensaje, Color colorTexto)
    {
        if (_interaccionCanvasGroup == null || _textoInteraccion == null) return;

        // 1. Detenemos cualquier animación previa para evitar conflictos
        _fadeTween?.Kill();
        _delayTween?.Kill();
        _interaccionCanvasGroup.DOKill();

        // 2. Actualizamos la información
        _textoInteraccion.text = mensaje;
        _textoInteraccion.color = colorTexto;

        // 3. Animamos el Alpha hasta 1
        _fadeTween = _interaccionCanvasGroup.DOFade(1f, _tiempoAparicion);
    }

    public void HideMessage()
    {
        if (_interaccionCanvasGroup == null) return;

        // 1. Detenemos cualquier animación previa
        _fadeTween?.Kill();
        _delayTween?.Kill();
        _interaccionCanvasGroup.DOKill();

        // 2. Animamos el Alpha hasta 0
        _fadeTween = _interaccionCanvasGroup.DOFade(0f, _tiempoDesaparicion);
    }

    public void ShowTemporaryMessage(string mensaje, Color colorTexto, float duracion)
    {
        if (_interaccionCanvasGroup == null || _textoInteraccion == null) return;

        ShowMessage(mensaje, colorTexto);

        // Tras 'duracion' segundos, ocultamos el mensaje
        _delayTween = DOVirtual.DelayedCall(duracion, () => 
        {
            HideMessage();
        });
    }
}