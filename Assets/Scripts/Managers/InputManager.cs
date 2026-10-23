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
    #endregion


    #region Flags
    private bool _menuWasTapped = false;
    private bool _menuWasHeld = false;
    #endregion

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

    public bool InteractWasPressed() 
    {
        return _pInteractAction.WasPressedThisFrame();
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
        _pInteractAction.Enable();
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
    #endregion
}
