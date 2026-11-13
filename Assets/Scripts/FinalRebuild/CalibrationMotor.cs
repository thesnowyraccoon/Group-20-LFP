
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sanctuary.Rebuild
{
    [RequireComponent(typeof(CharacterController))]
    public class CalibrationMotor : MonoBehaviour
    {
        [Header("Camera")]
        public CalibrationCamera view;

        [Header("Movement")]
        public float speed = 4.5f;
        public float acceleration = 35f;
        public float braking = 45f;
        public float airAcceleration = 18f;

        [Header("Jumping")]
        public float jumpHeight = 1.4f;
        public float gravity = 24f;
        public float coyoteTime = 0.1f;
        public float jumpBuffer = 0.12f;

        private CharacterController controller;
        private Vector3 horizontal;
        private Vector3 spawn;
        private float vertical;
        private float lastGround = -100f;
        private float lastJumpPress = -100f;
        private bool jumpConsumed;

        private float normalJumpHeight;
        private bool jumpBoostActive;

        public bool JumpBoostActive => jumpBoostActive;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            spawn = transform.position;

            normalJumpHeight = jumpHeight;
            jumpBoostActive = false;
        }

        // Called by a collectible to improve jumping.
        public void ActivateJumpBoost(float boostedHeight)
        {
            if (boostedHeight <= 0f)
            {
                Debug.LogWarning(
                    "Jump Boost: height must be greater than zero."
                );
                return;
            }

            jumpHeight = Mathf.Max(
                normalJumpHeight,
                boostedHeight
            );

            jumpBoostActive = true;

            Debug.Log(
                "Jump Boost activated! Jump Height: " +
                jumpHeight
            );
        }

        // Available for future levels or ability resets.
        public void RemoveJumpBoost()
        {
            jumpHeight = normalJumpHeight;
            jumpBoostActive = false;

            Debug.Log("Jump Boost removed.");
        }

        private void Update()
        {
            var keyboard = Keyboard.current;

            if (keyboard == null || view == null)
                return;

            if (
                keyboard.rKey.wasPressedThisFrame ||
                transform.position.y < -10f
            )
            {
                controller.enabled = false;

                transform.SetPositionAndRotation(
                    spawn,
                    Quaternion.identity
                );

                controller.enabled = true;

                horizontal = Vector3.zero;
                vertical = 0f;

                lastGround = -100f;
                lastJumpPress = -100f;

                jumpConsumed = false;

                view.ResetView();
                return;
            }

            bool grounded = controller.isGrounded;

            if (grounded && vertical <= 0f)
            {
                lastGround = Time.time;
                jumpConsumed = false;
                vertical = -2f;
            }

            bool active =
                Cursor.lockState == CursorLockMode.Locked;

            if (
                active &&
                keyboard.spaceKey.wasPressedThisFrame
            )
            {
                lastJumpPress = Time.time;
            }

            Vector2 input = active
                ? new Vector2(
                    (keyboard.dKey.isPressed ? 1 : 0) -
                    (keyboard.aKey.isPressed ? 1 : 0),

                    (keyboard.wKey.isPressed ? 1 : 0) -
                    (keyboard.sKey.isPressed ? 1 : 0)
                )
                : Vector2.zero;

            input = Vector2.ClampMagnitude(input, 1f);

            Vector3 direction =
                Quaternion.Euler(0f, view.Yaw, 0f) *
                new Vector3(input.x, 0f, input.y);

            float rate = grounded
                ? (
                    input.sqrMagnitude > 0f
                        ? acceleration
                        : braking
                )
                : airAcceleration;

            horizontal = Vector3.MoveTowards(
                horizontal,
                direction * speed,
                rate * Time.deltaTime
            );

            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation =
                    Quaternion.RotateTowards(
                        transform.rotation,
                        Quaternion.LookRotation(direction),
                        900f * Time.deltaTime
                    );
            }

            if (
                !jumpConsumed &&
                Time.time - lastGround <= coyoteTime &&
                Time.time - lastJumpPress <= jumpBuffer
            )
            {
                vertical = Mathf.Sqrt(
                    2f * gravity * jumpHeight
                );

                jumpConsumed = true;

                lastGround = -100f;
                lastJumpPress = -100f;
            }

            vertical = Mathf.Max(
                vertical - gravity * Time.deltaTime,
                -40f
            );

            var collisions = controller.Move(
                (horizontal + Vector3.up * vertical) *
                Time.deltaTime
            );

            if (
                (collisions & CollisionFlags.Above) != 0 &&
                vertical > 0f
            )
            {
                vertical = 0f;
            }
        }
    }
}

