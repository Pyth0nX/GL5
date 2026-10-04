using UnityEngine;
using UnityEngine.InputSystem;

public class UIManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject _pauseMenuPanel;

    [Header("References")]
    [SerializeField] private PlayerController _playerController;
    [SerializeField] private PlayerInput _playerInput;

    private InputAction _menuAction;
    private bool _isPaused = false;

    private void Start()
    {
        if (_pauseMenuPanel != null)
        {
            _pauseMenuPanel.SetActive(false);
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
            Debug.Log("[UIManager] Pausing the game.");
            PauseGame();
        }
        else
        {
            Debug.Log("[UIManager] Resuming the game.");
            ResumeGame();
        }

        // Force sync the UI state just in case something else touched it
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
}