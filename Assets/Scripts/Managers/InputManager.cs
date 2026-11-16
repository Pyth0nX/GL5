using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Interactions;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    [SerializeField]
    private InputActionAsset _playerAsset;

    private InputActionMap _pMovementMap;
    private InputActionMap _pInteractionsMap;

    #region MovementActions
    private InputAction _pMoveAction;
    private InputAction _pLookAction;
    #endregion

    #region InteractActions
    private InputAction _pPhoneAction;
    private InputAction _pInteractAction;
    private InputAction _pMenuAction;
    private InputAction _pContinueDialogueAction;
    private InputAction _pLeftClickAction;
    private InputAction _pF9Action;
    private InputAction _pF10Action;
    #endregion


    #region Flags
    private bool _menuWasTapped = false;
    private bool _menuWasHeld = false;
    #endregion

    [Header("Interaction Anti-Bounce")]
    [Tooltip("Seconds after closing a dialogue during which world Interact (E) is suppressed so rapid presses cannot immediately re-open it.")]
    [SerializeField] private float _postDialogueInteractCooldown = 0.25f;
    private int _suppressInteractUntilFrame = -1;
    private float _suppressInteractUntilTime = -1f;
    private int _suppressDialogueAdvanceUntilFrame = -1;
    private float _suppressDialogueAdvanceUntilTime = -1f;
    private int _lastDialogueAdvanceConsumedFrame = -1;

    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        BindActions();
    }

    /// <summary>
    /// Bind all the actions required to read them
    /// </summary>
    private void BindActions() 
    {
        
        _pMovementMap = _playerAsset.FindActionMap("Movement");
        _pMoveAction = _pMovementMap.FindAction("Move");
        _pLookAction = _pMovementMap.FindAction("Look");

        _pInteractionsMap = _playerAsset.FindActionMap("Interactions");
        _pPhoneAction = _pInteractionsMap.FindAction("Phone");
        _pInteractAction = _pInteractionsMap.FindAction("Interact");
        _pContinueDialogueAction = _pInteractionsMap.FindAction("ContinueDialogue");
        _pMenuAction = _pInteractionsMap.FindAction("Menu");
        _pLeftClickAction = _pInteractionsMap.FindAction("LeftClick");
        _pF9Action = _pInteractionsMap.FindAction("F9");
        _pF10Action = _pInteractionsMap.FindAction("F10");

        _pMenuAction.performed += OnMenuActionPerformed;
    }

    private void OnMenuActionPerformed(InputAction.CallbackContext context)
    {
        if (context.interaction is TapInteraction)
        {
            _menuWasTapped = true;
        }
        else if (context.interaction is HoldInteraction)
        {
            _menuWasHeld = true;
        }
    }

    private void LateUpdate()
    {
        //reset the flags
        _menuWasTapped = false;
        _menuWasHeld = false;
    }
    private void OnEnable()
    {
        if (_pMovementMap != null) _pMovementMap.Enable();
        if (_pInteractionsMap != null) _pInteractionsMap.Enable();
    }
    private void OnDisable()
    {
        if (_pMovementMap != null) _pMovementMap.Disable();
        if (_pInteractionsMap != null) _pInteractionsMap.Disable();
    }

    #region Getters
    public Vector2 GetPlayerMovement()
    {
        return _pMoveAction.ReadValue<Vector2>();
    }

    public bool PlayerMoved()
    {
        return GetPlayerMovement() != Vector2.zero;
    }

    public Vector2 GetPlayerLook()
    {
        return _pLookAction.ReadValue<Vector2>();
    }

    public bool PlayerLooked()
    {
        return GetPlayerLook() != Vector2.zero;
    }

    public bool MenuWasTapped()
    {
        return _menuWasTapped;
    }

    public bool MenuWasHeld()
    {
        return _menuWasHeld;
    }

    /// <summary>
    /// Temporarily blocks world Interact queries so closing a dialogue with E does not
    /// immediately re-trigger the NPC on the same frame or during rapid key presses.
    /// </summary>
    public void SuppressInteract(float cooldownSeconds = -1f, int minFrames = 2)
    {
        float resolvedCooldown = cooldownSeconds < 0f ? _postDialogueInteractCooldown : cooldownSeconds;
        int targetFrame = Time.frameCount + Mathf.Max(0, minFrames);
        if (targetFrame > _suppressInteractUntilFrame)
        {
            _suppressInteractUntilFrame = targetFrame;
        }

        if (resolvedCooldown > 0f)
        {
            float targetTime = Time.unscaledTime + resolvedCooldown;
            if (targetTime > _suppressInteractUntilTime)
            {
                _suppressInteractUntilTime = targetTime;
            }
        }
    }

    public bool IsInteractSuppressed()
    {
        return Time.frameCount <= _suppressInteractUntilFrame || Time.unscaledTime < _suppressInteractUntilTime;
    }

    /// <summary>
    /// Suppresses dialogue advance and animation skip inputs briefly so one click or key press cannot
    /// skip multiple lines or leak across UI transitions.
    /// </summary>
    public void ConsumeDialogueAdvance(float cooldownSeconds = 0.2f, int minFrames = 3)
    {
        _lastDialogueAdvanceConsumedFrame = Time.frameCount;
        int targetFrame = Time.frameCount + Mathf.Max(0, minFrames);
        if (targetFrame > _suppressDialogueAdvanceUntilFrame)
        {
            _suppressDialogueAdvanceUntilFrame = targetFrame;
        }

        if (cooldownSeconds > 0f)
        {
            float targetTime = Time.unscaledTime + cooldownSeconds;
            if (targetTime > _suppressDialogueAdvanceUntilTime)
            {
                _suppressDialogueAdvanceUntilTime = targetTime;
            }
        }
    }

    public bool IsDialogueAdvanceSuppressed()
    {
        return Time.frameCount <= _lastDialogueAdvanceConsumedFrame ||
               Time.frameCount <= _suppressDialogueAdvanceUntilFrame ||
               Time.unscaledTime < _suppressDialogueAdvanceUntilTime;
    }

    public bool TryConsumeDialogueAdvance(float cooldownSeconds = 0.2f, int minFrames = 3)
    {
        if (IsDialogueAdvanceSuppressed())
        {
            return false;
        }

        ConsumeDialogueAdvance(cooldownSeconds, minFrames);
        return true;
    }

    /// <summary>
    /// Raw Interact press check without post-dialogue suppression, used inside active dialogue UI.
    /// </summary>
    public bool RawInteractWasPressed()
    {
        if (_pInteractAction != null && _pInteractAction.WasPressedThisFrame())
        {
            return true;
        }

        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            return true;
        }

        return false;
    }

    public bool InteractWasPressed()
    {
        if (IsInteractSuppressed())
        {
            return false;
        }

        return RawInteractWasPressed();
    }

    public bool LeftClickWasPressed()
    {
        if (_pLeftClickAction != null && _pLeftClickAction.WasPressedThisFrame())
        {
            return true;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            return true;
        }

        return false;
    }

    public bool F9WasPressed()
    {
        if (_pF9Action != null && _pF9Action.WasPressedThisFrame())
        {
            return true;
        }

        if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
        {
            return true;
        }

        return false;
    }

    public bool F10WasPressed()
    {
        if (_pF10Action != null && _pF10Action.WasPressedThisFrame())
        {
            return true;
        }

        if (Keyboard.current != null && Keyboard.current.f10Key.wasPressedThisFrame)
        {
            return true;
        }

        return false;
    }

    public bool PhoneWasPressed()
    {
        return _pPhoneAction.WasPressedThisFrame();
    }

    public bool ContinueDialogueWasPressed()
    {
        return _pContinueDialogueAction.WasPressedThisFrame();
    }
    #endregion

    #region Enablers
    public void EnableMovementMap()
    {
        _pMovementMap.Enable();
    }

    public void EnableInteractionsMap()
    {
        _pInteractionsMap.Enable();
    }

    public void EnablePlayerMovement()
    {
        _pMoveAction.Enable();
    }

    public void EnablePlayerLook()
    {
        _pLookAction.Enable();
    } 

    public void EnablePhone()
    {
        _pPhoneAction.Enable();
    }

    public void EnableMenuAction()
    {
        _pMenuAction.Enable();
    }

    public void EnableInteract()
    {
        SuppressInteract(0f, 1);
        _pInteractAction.Enable();
    }

    public void EnableLeftClick()
    {
        _pLeftClickAction?.Enable();
    }

    public void EnableF9()
    {
        _pF9Action?.Enable();
    }

    public void EnableF10()
    {
        _pF10Action?.Enable();
    }
    #endregion

    #region Disablers

    public void DisableMovementMap()
    {
        _pMovementMap.Disable();
    }

    public void DisableInteractionsMap()
    {
        _pInteractionsMap.Disable();
    }

    public void DisablePlayerMovement()
    {
        _pMoveAction.Disable();
    }

    public void DisablePlayerLook()
    {
        _pLookAction.Disable();
    }

    public void DisablePhone()
    {
        _pPhoneAction.Disable();
    }

    public void DisableMenuAction()
    {
        _pMenuAction.Disable();
    }

    public void DisableInteract()
    {
        _pInteractAction.Disable();
    }

    public void DisableLeftClick()
    {
        _pLeftClickAction?.Disable();
    }

    public void DisableF9()
    {
        _pF9Action?.Disable();
    }

    public void DisableF10()
    {
        _pF10Action?.Disable();
    }
    #endregion
}
