using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
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
    
    private IInteractable _currentHoveredInteractable;
    private bool _isShowingHoverMessage;
    private PlayerInput _playerInput;
    private PlayerStateMachine _playerStateMachine;
    private Camera _playerCamera;
    private Rigidbody _rigidbody;
    private RaycastHit _raycastHit;
    private float _currentXRotation;

    private void Awake()
    {
        _playerCamera = GetComponentInChildren<Camera>();
        _playerInput = GetComponent<PlayerInput>();
        _playerStateMachine = GetComponent<PlayerStateMachine>();
        _rigidbody = GetComponent<Rigidbody>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Start()
    {
        _currentXRotation = 0f;
        GameManager.Instance.SetPlayerName(_playerName);
    }

    private void Update()
    {
        LookAround();

        CheckInteractableHover();

        if (InputManager.Instance.InteractWasPressed())
        {
            TryInteract();
        }

        if (InputManager.Instance.PhoneWasPressed())
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
            ClearHoveredInteractable();
            _playerStateMachine.ChangeState(PlayerState.Phone);
            UIManager.Instance.ChangeState(1);
        }
        else
        {
            _playerStateMachine.ChangeState(PlayerState.Idle);
            UIManager.Instance.ChangeState(0);
        }
    }

    private void OnDisable()
    {
        ClearHoveredInteractable();
    }

    private void ClearHoveredInteractable()
    {
        if (_currentHoveredInteractable != null)
        {
            _currentHoveredInteractable.SetOutlineActive(false);
            _currentHoveredInteractable = null;
        }

        if (_isShowingHoverMessage)
        {
            _isShowingHoverMessage = false;
            if (UIManager.Instance != null)
            {
                UIManager.Instance.HideMessage();
            }
        }
    }

    void MovePlayer()
    {
        Vector2 direction = InputManager.Instance.GetPlayerMovement();
        
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
        if (InputManager.Instance.PlayerLooked())
        {
            Vector2 lookVec = InputManager.Instance.GetPlayerLook();
            Quaternion yRotation = Quaternion.Euler(0f, lookVec.x * _mouseSensitivity, 0f);
            _rigidbody.MoveRotation(_rigidbody.rotation * yRotation);

            _currentXRotation -= lookVec.y * _mouseSensitivity;
            _currentXRotation = Mathf.Clamp(_currentXRotation, -_verticalClamp, _verticalClamp);

            _playerCamera.transform.localRotation = Quaternion.Euler(_currentXRotation, 0f, 0f);
        }
    }

    private void CheckInteractableHover()
    {
        // Don't detect or highlight interactables when the player is not in Idle state (phone, conversation, interacting, etc.)
        if (_playerStateMachine != null && _playerStateMachine.GetCurrentState() != PlayerState.Idle)
        {
            ClearHoveredInteractable();
            return;
        }

        IInteractable interactable = GetInteractableInSight();

        if (interactable != null)
        {
            if (_currentHoveredInteractable != interactable)
            {
                if (_currentHoveredInteractable != null)
                {
                    _currentHoveredInteractable.SetOutlineActive(false);
                }

                _currentHoveredInteractable = interactable;
                _isShowingHoverMessage = true;

                // Turn ON outline on the interactable
                _currentHoveredInteractable.SetOutlineActive(true);

                if (UIManager.Instance != null)
                {
                    string messageToShow = (interactable.CanInteract()) ? interactable.InteractionMessage() : interactable.NotAvailableMessage();
                    Color messageColor = (interactable.CanInteract()) ? Color.white : Color.darkRed;
                    UIManager.Instance.ShowMessage(messageToShow, messageColor);
                }
            }
        }
        else
        {
            ClearHoveredInteractable();
        }
    }

    public void TryInteract()
    {
        if (_playerStateMachine != null && _playerStateMachine.GetCurrentState() != PlayerState.Idle)
        {
            return;
        }

        IInteractable interactable = GetInteractableInSight();
        Debug.Log("Interacted tried");

        if (interactable != null)
        {
            // We interact. The script (e.g. EventInteractable) will handle if it's not available
            Debug.Log("Interacted");
            interactable.Interact();
            _playerStateMachine.ChangeState(PlayerState.Interacting);
        }
        else
        {
            // Situation 3: Press E and nothing in front
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowTemporaryMessage("Nothing to interact", new Color(0.3f, 0.3f, 0.3f), 2f);
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

            IInteractable interactable = hitObject.GetComponentInParent<IInteractable>();
            if (interactable != null)
            {
                return interactable;
            }

            // Hit an obstacle/wall that is not an interactable; stop ray
            break;
        }
        return null;
    }
}
