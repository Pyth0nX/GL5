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
    private InteractableOutline _currentHoveredStandaloneOutline;
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

        if (EventBus.Instance != null)
        {
            EventBus.Instance.Subscribe("Conversation_Ended", OnConversationEnded);
        }
    }

    private void OnDestroy()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.Unsubscribe("Conversation_Ended", OnConversationEnded);
        }
    }

    private void OnConversationEnded()
    {
        if (_playerStateMachine != null && _playerStateMachine.GetCurrentState() == PlayerState.Interacting)
        {
            _playerStateMachine.ChangeState(PlayerState.Idle);
        }
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

        if (_currentHoveredStandaloneOutline != null)
        {
            _currentHoveredStandaloneOutline.SetOutlineActive(false);
            _currentHoveredStandaloneOutline = null;
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
        // Safety recovery: If UI returned to DEFAULT mode, ensure player state is back to Idle
        if (_playerStateMachine != null && _playerStateMachine.GetCurrentState() == PlayerState.Interacting)
        {
            if (UIManager.Instance != null && UIManager.Instance.GetCurrentState() == UIState.DEFAULT)
            {
                _playerStateMachine.ChangeState(PlayerState.Idle);
            }
        }

        // Don't detect or highlight interactables when the player is not in Idle state (phone, conversation, interacting, etc.)
        if (_playerStateMachine != null && _playerStateMachine.GetCurrentState() != PlayerState.Idle)
        {
            ClearHoveredInteractable();
            return;
        }

        IInteractable interactable = GetInteractableInSight();

        if (interactable != null)
        {
            // Clear any standalone outline we were hovering
            if (_currentHoveredStandaloneOutline != null)
            {
                _currentHoveredStandaloneOutline.SetOutlineActive(false);
                _currentHoveredStandaloneOutline = null;
            }

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
            else
            {
                _currentHoveredInteractable.UpdateHoveredOutlineColor();
            }
        }
        else
        {
            // If no IInteractable in sight, check if looking at a standalone object with InteractableOutline
            InteractableOutline standaloneOutline = GetStandaloneOutlineInSight();

            if (standaloneOutline != null)
            {
                if (_currentHoveredInteractable != null)
                {
                    _currentHoveredInteractable.SetOutlineActive(false);
                    _currentHoveredInteractable = null;
                }

                if (_currentHoveredStandaloneOutline != standaloneOutline)
                {
                    if (_currentHoveredStandaloneOutline != null)
                    {
                        _currentHoveredStandaloneOutline.SetOutlineActive(false);
                    }

                    _currentHoveredStandaloneOutline = standaloneOutline;
                    _currentHoveredStandaloneOutline.SetOutlineActive(true);
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
            else
            {
                ClearHoveredInteractable();
            }
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
            Debug.Log("Interacted");
            if (interactable.CanInteract())
            {
                _playerStateMachine.ChangeState(PlayerState.Interacting);
                interactable.Interact();
            }
            else
            {
                interactable.Interact();
            }
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

    private InteractableOutline GetStandaloneOutlineInSight()
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

            InteractableOutline outline = hitObject.GetComponentInParent<InteractableOutline>();
            if (outline != null)
            {
                return outline;
            }

            // Hit an obstacle/wall that has no outline; stop ray
            break;
        }
        return null;
    }
}
