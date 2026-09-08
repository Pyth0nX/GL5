using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float rotationSpeed = 120f;

    void Update()
    {
        float move = 0f;
        float rotate = 0f;

        if (Keyboard.current.wKey.isPressed)
            move = 1f;

        if (Keyboard.current.sKey.isPressed)
            move = -1f;

        if (Keyboard.current.aKey.isPressed)
            rotate = -1f;

        if (Keyboard.current.dKey.isPressed)
            rotate = 1f;


        transform.Translate(Vector3.forward * move * moveSpeed * Time.deltaTime);
        transform.Rotate(Vector3.up * rotate * rotationSpeed * Time.deltaTime);
    }
}