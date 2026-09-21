using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    CharacterController cc;
    Transform cam;

    float moveScale = 1f;

    Vector2 moveInput;
    bool sprintInput;
    bool jumpInput;

    [Header("Movement")]
    public float walkMod = 5f;
    public float sprintMod = 10f;
    public float turnSmoothTime = 0.1f;
    public float jumpForce = 5f;
    public float gravity = -9.8f;

    Vector3 verticalVelocity;
    float turnSmoothVelocity;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
        cam = FindAnyObjectByType<Camera>().transform;

        moveScale = walkMod;

        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        HandleMovement();

        Debug.DrawRay(transform.position, transform.forward * 3f, Color.purple);
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
        Vector3 direction = new Vector3(moveInput.x, 0f, moveInput.y).normalized;

        if (cc.isGrounded)
        {
            Debug.Log("GROUND");

            verticalVelocity.y = -1f;

            if (jumpInput)
            {
                verticalVelocity.y = jumpForce;
            }
        }
        else
        {
            verticalVelocity.y += gravity * Time.deltaTime;
        }

        cc.Move(verticalVelocity * Time.deltaTime);

        //move.y = verticalVelocity;

        //cc.Move(move * Time.deltaTime);

        if (direction.magnitude >= 0.1f)
        {
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + cam.eulerAngles.y;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);

            transform.rotation = Quaternion.Euler(0f, angle, 0f);

            Vector3 moveDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

            cc.Move(moveScale * Time.deltaTime * moveDirection.normalized);
        }
    }
}
