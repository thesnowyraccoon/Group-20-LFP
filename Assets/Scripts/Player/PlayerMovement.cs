using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    CharacterController cc;

    float moveScale = 1f;
    float normalScale = 1f;

    [Header("Movement")]
    public float walkMod = 8f;
    public float sprintMod = 16f;
    public float crouchMod = 4f;

    public float jumpForce = 8f;
    public float gravity = -9.8f;

    float standingHeight;
    float crouchingHeight;
}
