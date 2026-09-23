using UnityEngine;

public class TigerAnimationController : MonoBehaviour
{
    private Animator animator;
    private CharacterController characterController;

    void Start()
    {
        animator = GetComponent<Animator>();
        characterController = GetComponentInParent<CharacterController>();
    }

    void Update()
    {
        if (animator == null || characterController == null)
            return;

        Vector3 horizontalVelocity = characterController.velocity;
        horizontalVelocity.y = 0f;

        bool isMoving = horizontalVelocity.magnitude > 0.1f;
        bool isJumping = !characterController.isGrounded;

        animator.SetBool("IsMoving", isMoving);
        animator.SetBool("IsJumping", isJumping);
    }
}