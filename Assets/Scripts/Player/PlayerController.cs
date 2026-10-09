using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private InputAction _moveAction;
    [SerializeField] private InputAction _lookAction;
    [SerializeField] private InputAction _interactAction;
    [SerializeField] private InputAction _phoneAction;
    [SerializeField] private GameObject _phoneUI;

    [Header("Settings")]
    [Tooltip("Player Name")]
    [SerializeField] private string _playerName = "Luna";

    [Tooltip("Movement Speed")]
    [SerializeField] private float _movementSpeed = 5f;
    [Tooltip("Mouse Sensitivity")]
    [SerializeField] private float _mouseSensitivity = 1f;

    [Tooltip("Interact Distance")]
    [SerializeField] private float _interactDistance = 5f;

    [Tooltip("Vertical Clamp Limits: 0° = Look down, 90° = Look up!")]
    [Range(0f, 90f)]
    [SerializeField] private float _verticalClamp = 80f;
    
    private PlayerInput _playerInput;
    private PlayerStateMachine _playerStateMachine;
    private Camera _playerCamera;
    private Rigidbody _rigidbody;
    private RaycastHit _raycastHit;
    private Vector2 _lookInput;
    private float _currentXRotation;

    private void Awake()
    {
        _playerCamera = GetComponentInChildren<Camera>();
        _playerInput = GetComponent<PlayerInput>();
        _playerStateMachine = GetComponent<PlayerStateMachine>();
        _rigidbody = GetComponent<Rigidbody>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;


        _moveAction = _playerInput.actions["Move"];
        _lookAction = _playerInput.actions["Look"];
        _interactAction = _playerInput.actions["Interact"];
        _phoneAction = _playerInput.actions["Phone"];
    }

    private void Start()
    {
        _currentXRotation = 0f;
    }

    private void Update()
    {
        LookAround();

        CheckInteractableHover();

        if(_interactAction.WasPressedThisFrame())
        {
            TryInteract();
        }

        if(_phoneAction.WasPressedThisFrame())
        {
            HandlePhone();
        }
    }

    private void FixedUpdate()
    {
        MovePlayer();
    }

    private void HandlePhone()
    {
        _playerStateMachine.SetPhoneOpen(!_playerStateMachine.IsPhoneOpen());
        if (_playerStateMachine.IsPhoneOpen())
        {
            _playerStateMachine.ChangeState(PlayerState.Phone);
            DisableLook();
            DisableMovement();
            UnlockMouse();
            _phoneUI.SetActive(true);
        }
        else
        {
            _playerStateMachine.ChangeState(PlayerState.Idle);
            EnableLook();
            EnableMovement();
            LockMouse();
            _phoneUI.SetActive(false);
        }
    }

    void MovePlayer()
    {
        Vector2 direction = _moveAction.ReadValue<Vector2>();
        
        Vector3 moveDirection = transform.right * direction.x + transform.forward * direction.y;
        moveDirection.y = 0f;
        
        if (moveDirection.magnitude > 1f)
        {
            moveDirection.Normalize();
        }

        Vector3 targetVelocity = moveDirection * _movementSpeed;
        targetVelocity.y = _rigidbody.linearVelocity.y;

        _rigidbody.linearVelocity = targetVelocity;
    }

    private void LookAround()
    {
        if (_lookAction != null)
        {
            _lookInput = _lookAction.ReadValue<Vector2>();
        }

        if (_lookInput != Vector2.zero)
        {
            Quaternion yRotation = Quaternion.Euler(0f, _lookInput.x * _mouseSensitivity, 0f);
            _rigidbody.MoveRotation(_rigidbody.rotation * yRotation);

            _currentXRotation -= _lookInput.y * _mouseSensitivity ;
            _currentXRotation = Mathf.Clamp(_currentXRotation, -_verticalClamp, _verticalClamp);

            _playerCamera.transform.localRotation = Quaternion.Euler(_currentXRotation, 0f, 0f);
        }
    }

    private IInteractable _currentHoveredInteractable;
    private bool _isShowingHoverMessage;

    private void CheckInteractableHover()
    {
        IInteractable interactable = GetInteractableInSight();

        if (interactable != null)
        {
            if (_currentHoveredInteractable != interactable)
            {
                _currentHoveredInteractable = interactable;
                _isShowingHoverMessage = true;
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.ShowMessage("Press E to interact", Color.white);
                }
            }
        }
        else
        {
            if (_isShowingHoverMessage)
            {
                _currentHoveredInteractable = null;
                _isShowingHoverMessage = false;
                if (UIManager.Instance != null)
                {
                    UIManager.Instance.HideMessage();
                }
            }
        }
    }

    public void TryInteract()
    {
        IInteractable interactable = GetInteractableInSight();

        if (interactable != null)
        {
            // We interact. The script (e.g. EventInteractable) will handle if it's not available
            interactable.Interact();
            _playerStateMachine.ChangeState(PlayerState.Interacting);
        }
        else
        {
            // Situation 3: Press E and nothing in front
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowTemporaryMessage("Can't Interact", new Color(0.3f, 0.3f, 0.3f), 2f);
            }
        }
    }

    private IInteractable GetInteractableInSight()
    {
        Vector3 origin = _playerCamera.transform.position;
        Vector3 dir = _playerCamera.transform.forward;

        RaycastHit[] hits = Physics.RaycastAll(origin, dir, _interactDistance);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            GameObject hitObject = hit.collider.gameObject;

            if (hitObject == gameObject || hitObject.transform.root == transform.root) continue;
            if (hit.collider.isTrigger) continue;

            bool hasTag = hitObject.tag.StartsWith("Interactable") || 
                         (hitObject.transform.parent != null && hitObject.transform.parent.tag.StartsWith("Interactable"));

            if (hasTag)
            {
                IInteractable interactable = hitObject.GetComponentInParent<IInteractable>();
                if (interactable != null)
                {
                    return interactable;
                }
            }
            break;
        }
        return null;
    }

    public string GetPlayerName()
    {
        return _playerName;
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

    public void DisableMovement()
    {
        _moveAction.Disable();
    }

    public void EnableMovement()
    {
        _moveAction.Enable();
    }

    public void DisableLook()
    {
        _lookAction.Disable();
    }

    public void EnableLook()
    {
        _lookAction.Enable();
    }

    public void DisableInput()
    {
        _moveAction.Disable();
        _lookAction.Disable();
        _interactAction.Disable();
        _phoneAction.Disable();
    }

    public void EnableInput()
    {
        _moveAction.Enable();
        _lookAction.Enable();
        _interactAction.Enable();
        _phoneAction.Enable();
    }
}
