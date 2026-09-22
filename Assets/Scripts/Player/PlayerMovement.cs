using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    CharacterController cc;
    Transform cam;

    float moveScale = 1f;

    Vector2 moveInput;
    bool sprintInput;
    bool jumpInput;

    // Set by RoomChainManager while a room transition is playing, so the 
    // player can't walk/jump during the fade.
    bool inputLocked;

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
        cc = GetComponent<CharacterController>();       // finds character controller
        cam = FindAnyObjectByType<Camera>().transform;  // finds player camera

        moveScale = walkMod;    // sets movement speed to default walk speed

        Cursor.lockState = CursorLockMode.Locked;   // locks cursor to screen
    }

    void Update()
    {
        HandleMovement();   // handles player movement
    }

    public void OnMovement(InputAction.CallbackContext context)     // detects movement input 
    {
        moveInput = context.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext context)     // detects jump input
    {
        jumpInput = context.performed;
    }

    public void OnSprint(InputAction.CallbackContext context)   // detects sprint key input
    {
        sprintInput = context.ReadValue<float>() > 0;
    }

    /// <summary>
    /// Called by RoomChainManager to freeze/unfreeze movement while a
    /// room transition (fade + teleport) is playing.
    /// </summary>
    public void SetInputLocked(bool locked)
    {
        inputLocked = locked;

        if (locked)
        {
            moveInput = Vector2.zero;
            sprintInput = false;
            jumpInput = false;
        }
    }

    void HandleMovement()   // handles player movement, jumping, sprinting
    {
        if (inputLocked)
        {
            return;
        }

        moveScale = sprintInput ? sprintMod : walkMod;  // checks whether player is sprinting

        Vector3 direction = new Vector3(moveInput.x, 0f, moveInput.y).normalized;   // direction of movement 

        if (cc.isGrounded)  // checks if player is on the ground and applies forces accordingly
        {
            //Debug.Log("GROUND");

            verticalVelocity.y = -1f;   // small force ensuring player is grounded

            if (jumpInput)
            {
                verticalVelocity.y = jumpForce;     // adds jump force when jump input is detected
            }
        }
        else
        {
            verticalVelocity.y += gravity * Time.deltaTime;     // if player is not on the ground apply gravity to them
        }

        cc.Move(verticalVelocity * Time.deltaTime);     // vertical force applied to player

        if (direction.magnitude >= 0.1f)    // if input is detected, move player
        {
            // angle of movement according to camera direction
            float targetAngle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg + cam.eulerAngles.y;
            float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref turnSmoothVelocity, turnSmoothTime);

            transform.rotation = Quaternion.Euler(0f, angle, 0f);   // turn player based on movement angle

            Vector3 moveDirection = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;    // direction of movement

            cc.Move(moveScale * Time.deltaTime * moveDirection.normalized); // apply horizontal movement
        }
    }
}
