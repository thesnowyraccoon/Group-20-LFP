using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    CharacterController cc;

    float moveScale = 1f;

    Vector2 moveInput;
    bool sprintInput;
    bool jumpInput;

    [Header("Movement")]
    public float walkMod = 8f;
    public float sprintMod = 16f;

    public float jumpForce = 8f;
    public float gravity = -9.8f;

    float verticalVelocity;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        moveScale = walkMod;
    }

    void Update()
    {
        HandleMovement();

        Debug.DrawRay(transform.position, Vector3.forward, Color.purple);
    }

    public void OnMovement(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        jumpInput = context.performed;
    }

    public void OnSprint(InputAction.CallbackContext context)
    {
        sprintInput = context.ReadValue<float>() > 0;
    }

    void HandleMovement()
    {
        moveScale = sprintInput ? sprintMod : walkMod;

        Vector3 move = (transform.right * moveInput.x + transform.forward * moveInput.y) * moveScale;

        if (cc.isGrounded)
        {
            verticalVelocity = -1f;

            if (jumpInput)
            {
                verticalVelocity = jumpForce;
            }
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        move.y = verticalVelocity;
        
        cc.Move(move * Time.deltaTime);
    }
}
