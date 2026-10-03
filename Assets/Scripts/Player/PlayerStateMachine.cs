using UnityEngine;

public enum PlayerState
{
    Idle,
    Phone,
    Running,
    Interacting
}

public class PlayerStateMachine : MonoBehaviour
{

    private PlayerState _currentState = PlayerState.Idle;

    private bool _isPhoneOpen = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }



    public void ChangeState(PlayerState newState)
    {
        if (_currentState != newState)
        {
            _currentState = newState;
        }
    }

    public PlayerState GetCurrentState()
    {
        return _currentState;
    }


    public void SetPhoneOpen(bool isOpen)
    {
        Debug.Log($"Setting phone open state to: {isOpen}");
        _isPhoneOpen = isOpen;
    }

    public bool IsPhoneOpen()
    {
        return _isPhoneOpen;
    }
}
