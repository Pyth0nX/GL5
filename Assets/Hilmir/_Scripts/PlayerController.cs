using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private InputAction moveAction;
    [SerializeField] private InputAction lookAction;
    [SerializeField] private InputAction interactAction;
    
    [Header("Settings")]
    [Tooltip("Movement Speed")]
    [SerializeField] private float movementSpeed = 5f;
    [Tooltip("Mouse Sensitivity")]
    [SerializeField] private float mouseSensitivity = 5f;
    
    [Tooltip("Vertical Clamp Limits: 0° = Look down, 90° = Look up!")]
    [Range(0f, 90f)]
    [SerializeField] private float verticalClamp = 80f;
    
    private PlayerInput _playerInput;
    private Camera _playerCamera;
    private RaycastHit _raycastHit;
    private Vector2 _lookInput;
    private float _currentYRotation;

    private void Awake()
    {
        _playerCamera = GetComponentInChildren<Camera>();
        _playerInput = GetComponent<PlayerInput>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Start()
    {
        _currentYRotation = 0f;
    }

    private void Update()
    {
        MovePlayer();
        LookAround();
        DetectInteractable();
    }

    void MovePlayer()
    {
        moveAction = _playerInput.actions.FindAction("Move");
        Vector2 direction = moveAction.ReadValue<Vector2>();
        Vector3 test = direction.x * transform.right + direction.y * transform.forward;
        transform.position += new Vector3(test.x, 0, test.z) * (movementSpeed * Time.deltaTime);
    }

    private void LookAround()
    {
        lookAction = InputSystem.actions.FindAction("Look");
        // If it's found the Look Action, it reads mouse inputs.
        if (lookAction != null)
        {
            _lookInput = lookAction.ReadValue<Vector2>();
        }

        if (_lookInput.x != 0 || _lookInput.y != 0)
        {
            // Rotates the camera on the Y axis.
            transform.Rotate(Vector3.up, _lookInput.x * mouseSensitivity * Time.deltaTime, Space.Self);
            
            // Rotates the camera on the X axis.
            _currentYRotation -= _lookInput.y * mouseSensitivity * Time.deltaTime;
            _currentYRotation = Mathf.Clamp(_currentYRotation, -verticalClamp, verticalClamp);
            
            // Clamps the camera at 180°.
            transform.localRotation = Quaternion.Euler(_currentYRotation, transform.localEulerAngles.y, 0f);
        }
    }

    public void DetectInteractable()
    {
        interactAction = InputSystem.actions.FindAction("Interact");
        Vector3 origin = transform.position;
        Vector3 dir = transform.forward;

        if (Keyboard.current.eKey.isPressed)
        {
            var ray = _playerCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            RaycastHit hit;
            
            if (Physics.Raycast(origin, dir, out hit, 100f))
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
                    }
                }
            }
        }
    }

    public void UnlockMouse()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
