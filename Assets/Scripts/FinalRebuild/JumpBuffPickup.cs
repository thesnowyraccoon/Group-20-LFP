
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sanctuary.Rebuild
{
    public class JumpBuffPickup : MonoBehaviour
    {
        [Header("Player")]
        [SerializeField]
        private CalibrationMotor playerMotor;

        [Header("Jump Boost")]
        [SerializeField]
        private float boostedJumpHeight = 3.2f;

        [SerializeField]
        private float interactionRange = 2f;

        [Header("Optional Audio")]
        [SerializeField]
        private AudioSource pickupAudioSource;

        [SerializeField]
        private AudioClip pickupSound;

        private bool collected;

        private void Update()
        {
            if (collected || playerMotor == null)
                return;

            if (Keyboard.current == null)
                return;

            if (Time.timeScale == 0f)
                return;

            if (Cursor.lockState != CursorLockMode.Locked)
                return;

            if (!PlayerIsNear())
                return;

            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                Collect();
            }
        }

        private bool PlayerIsNear()
        {
            Vector3 playerPosition =
                playerMotor.transform.position +
                Vector3.up * 0.9f;

            Vector3 pickupPosition =
                transform.position +
                Vector3.up * 0.9f;

            return Vector3.Distance(
                playerPosition,
                pickupPosition
            ) <= interactionRange;
        }

        private void Collect()
        {
            if (collected)
                return;

            collected = true;

            playerMotor.ActivateJumpBoost(
                boostedJumpHeight
            );

            if (
                pickupAudioSource != null &&
                pickupSound != null
            )
            {
                pickupAudioSource.PlayOneShot(
                    pickupSound
                );
            }

            Debug.Log(
                "Unease: Jump Boost collected."
            );

            // Hide the collectible, including its label.
            // Keep this script object active for audio.
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(false);
            }
        }

        private void OnGUI()
        {
            if (collected || !PlayerIsNear())
                return;

            float scale = Mathf.Min(
                Screen.width / 1920f,
                Screen.height / 1080f
            );

            GUIStyle style =
                new GUIStyle(GUI.skin.box);

            style.fontSize = Mathf.Max(
                1,
                Mathf.RoundToInt(30f * scale)
            );

            style.alignment =
                TextAnchor.MiddleCenter;

            style.normal.textColor = Color.white;

            float width = 660f * scale;
            float height = 80f * scale;

            GUI.Box(
                new Rect(
                    (Screen.width - width) / 2f,
                    Screen.height - height -
                    115f * scale,
                    width,
                    height
                ),
                "Press E to collect Jump Boost",
                style
            );
        }
    }
}
