
using UnityEngine;

public class TigerAnimationDriver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator tigerAnimator;
    [SerializeField] private CharacterController playerController;
    [SerializeField] private Transform tigerVisual;

    [Header("Movement")]
    [SerializeField] private float turnSpeed = 10f;
    [SerializeField] private float movingThreshold = 0.1f;

    [Header("Footstep Audio")]
    [SerializeField] private AudioSource footstepSource;
    [SerializeField] private AudioClip footstepClip;
    [SerializeField] private float footstepInterval = 0.38f;

    private Vector3 lastPosition;
    private float footstepTimer;

    private void Start()
    {
        lastPosition = transform.position;

        if (tigerAnimator == null)
        {
            tigerAnimator = GetComponentInChildren<Animator>();
        }

        if (playerController == null)
        {
            playerController = GetComponent<CharacterController>();
        }

        footstepTimer = 0f;
    }

    private void LateUpdate()
    {
        if (tigerAnimator == null || playerController == null)
        {
            return;
        }

        // Measure how far the player moved this frame.
        Vector3 displacement = transform.position - lastPosition;
        lastPosition = transform.position;

        // Ignore vertical movement when checking walking speed.
        Vector3 horizontalMovement = new Vector3(
            displacement.x,
            0f,
            displacement.z
        );

        float speed = horizontalMovement.magnitude /
                      Mathf.Max(Time.deltaTime, 0.0001f);

        bool isMoving = speed > movingThreshold;
        bool isJumping = !playerController.isGrounded;

        // Update the Animator parameters.
        tigerAnimator.SetBool("IsMoving", isMoving);
        tigerAnimator.SetBool("IsJumping", isJumping);

        // Rotate the visible tiger toward movement direction.
        if (isMoving && tigerVisual != null)
        {
            Quaternion targetRotation = Quaternion.LookRotation(
                horizontalMovement.normalized,
                Vector3.up
            );

            tigerVisual.rotation = Quaternion.Slerp(
                tigerVisual.rotation,
                targetRotation,
                turnSpeed * Time.deltaTime
            );
        }

        // Play footsteps only while walking on the ground.
        if (isMoving && !isJumping)
        {
            footstepTimer -= Time.deltaTime;

            if (footstepTimer <= 0f)
            {
                if (footstepSource != null && footstepClip != null)
                {
                    footstepSource.PlayOneShot(footstepClip);
                }

                footstepTimer = footstepInterval;
            }
        }
        else
        {
            // Reset so the next movement starts with a step.
            footstepTimer = 0f;
        }
    }
}

