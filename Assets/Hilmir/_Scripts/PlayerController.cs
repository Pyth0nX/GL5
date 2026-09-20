using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private InputAction moveAction;
    [SerializeField] private InputAction lookAction;
    
    [Header("Settings")]
    [Tooltip("Movement Speed")]
    [SerializeField] private float movementSpeed = 5f;
    [Tooltip("Mouse Sensitivity")]
    [SerializeField] private float mouseSensitivity = 20f;
    
    [Tooltip("Vertical Clamp Limits: 0° = Look down, 90° = Look up!")]
    [Range(0f, 90f)]
    [SerializeField] private float verticalClamp = 80f;
    
    private PlayerInput _playerInput;
    private Camera _playerCamera;
    private Vector2 _lookInput;
    private float _currentYRotation;

    private void Awake()
    {
        _playerCamera = GetComponent<Camera>();
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
    }

    void MovePlayer()
    {
        moveAction = _playerInput.actions.FindAction("Move");
        Vector2 direction = moveAction.ReadValue<Vector2>();
        Vector3 test = direction.x * transform.right + direction.y * transform.forward;
        transform.position += new Vector3(test.x, 0, test.z) * movementSpeed * Time.deltaTime;
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
}
