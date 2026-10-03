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
    private RaycastHit _raycastHit;
    private Vector2 _lookInput;
    private float _currentXRotation;

    private void Awake()
    {
        _playerCamera = GetComponentInChildren<Camera>();
        _playerInput = GetComponent<PlayerInput>();
        _playerStateMachine = GetComponent<PlayerStateMachine>();
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
        MovePlayer();
        LookAround();

        if(_interactAction.WasPressedThisFrame())
        {
            DetectInteractable();
        }

        if(_phoneAction.WasPressedThisFrame())
        {
            HandlePhone();
        }
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
        Vector3 test = direction.x * transform.right + direction.y * transform.forward;
        transform.position += new Vector3(test.x, 0, test.z) * (_movementSpeed * Time.deltaTime);
    }

    private void LookAround()
    {
        // If it's found the Look Action, it reads mouse inputs.
        if (_lookAction != null)
        {
            _lookInput = _lookAction.ReadValue<Vector2>();
        }

        if (_lookInput != Vector2.zero)
        {
            transform.Rotate(Vector3.up, _lookInput.x * _mouseSensitivity);

            _currentXRotation -= _lookInput.y * _mouseSensitivity ;
            _currentXRotation = Mathf.Clamp(_currentXRotation, -_verticalClamp, _verticalClamp);

            _playerCamera.transform.localRotation = Quaternion.Euler(_currentXRotation, 0f, 0f);
        }

    }

    public void DetectInteractable()
    {

        Vector3 origin = _playerCamera.transform.position;
        Vector3 dir = _playerCamera.transform.forward;

        RaycastHit hit;
            
        if (Physics.Raycast(origin, dir, out hit, _interactDistance))
        {
            GameObject hitObject = hit.collider.gameObject;
            var isInteractable = hitObject.tag.StartsWith("Interactable");
            Debug.Log("Hit: " + hitObject);

            if (isInteractable)
            {
                // Handle interaction with the hit object
                if(hitObject.GetComponent<NPCData>() != null)
                {
                    hitObject.GetComponent<NPCData>().Interact();
                    _playerStateMachine.ChangeState(PlayerState.Interacting);
                }

            }
        }
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
