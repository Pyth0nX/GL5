using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    private InputSystem_Actions.InputSystem_Actions _inputActions;
    private Vector2 _movement;
    private InputAction.CallbackContext _actionContext;

    private void Awake()
    {
        
        _inputActions = new InputSystem_Actions.InputSystem_Actions();
    }

    void Start()
    {
        Debug.Log("PlayerController is active on: " + gameObject.name);
    }

    private void OnEnable()
    {
        _inputActions.Enable();

        _inputActions.Player.Move.performed += OnMove;
        _inputActions.Player.Move.canceled += OnMove;
        _inputActions.Player.Jump.performed += OnJump;
    }

    private void OnDisable()
    {

        _inputActions.Player.Move.performed -= OnMove;
        _inputActions.Player.Move.canceled -= OnMove;
        _inputActions.Player.Jump.performed -= OnJump;
        
        _inputActions.Disable();
        _inputActions.Dispose();
    }

    // Update is called once per frame
    private void Update()
    {
        transform.Translate(_movement * Time.deltaTime);
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        Vector2 move = context.ReadValue<Vector2>();
        Debug.Log("Player is walking!");
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        Debug.Log("Player has jumped!");
    }
}
