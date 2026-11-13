
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sanctuary.Rebuild
{
    public class UneaseMechanism : MonoBehaviour
    {
        [Header("Room References")]
        [SerializeField] private Transform player;
        [SerializeField] private GameObject keyVisual;
        [SerializeField] private GameObject gateOpenable;
        [SerializeField] private Transform gateInteractionPoint;

        [Header("Optional HUD")]
        [SerializeField] private TMP_Text objectiveText;

        [Header("Interaction Settings")]
        [SerializeField] private float keyRange = 2.5f;
        [SerializeField] private float gateRange = 2.5f;
        [SerializeField] private float maximumHeightDifference = 1.5f;

        [Header("Audio")]
        [SerializeField] private AudioSource interactionAudio;
        [SerializeField] private AudioClip keyPickupSound;
        [SerializeField] private AudioClip gateOpenSound;

        private bool hasKey;
        private bool gateUnlocked;

        public bool GateUnlocked => gateUnlocked;

        private void Start()
        {
            UpdateObjective();
        }

        private void Update()
        {
            if (Keyboard.current == null)
                return;

            if (Time.timeScale == 0f)
                return;

            if (Cursor.lockState != CursorLockMode.Locked)
                return;

            if (!Keyboard.current.eKey.wasPressedThisFrame)
                return;

            if (!hasKey)
            {
                if (keyVisual != null &&
                    NearTarget(keyVisual.transform, keyRange))
                {
                    CollectKey();
                }

                return;
            }

            if (!gateUnlocked &&
                gateInteractionPoint != null &&
                NearTarget(gateInteractionPoint, gateRange))
            {
                UnlockGate();
            }
        }

        private bool NearTarget(Transform target, float range)
        {
            if (player == null || target == null)
                return false;

            Vector3 difference =
                player.position - target.position;

            // Prevent collecting the elevated key
            // while standing on the floor below it.
            if (Mathf.Abs(difference.y) >
                maximumHeightDifference)
            {
                return false;
            }

            difference.y = 0f;

            return difference.sqrMagnitude <= range * range;
        }

        private void CollectKey()
        {
            if (hasKey)
                return;

            hasKey = true;

            PlaySound(keyPickupSound);

            keyVisual.SetActive(false);

            UpdateObjective();

            Debug.Log("Unease: key collected.");
        }

        private void UnlockGate()
        {
            if (gateUnlocked || gateOpenable == null)
                return;

            gateUnlocked = true;

            PlaySound(gateOpenSound);

            // Only remove the iron gate.
            // The stone arch remains in the room.
            gateOpenable.SetActive(false);

            UpdateObjective();

            Debug.Log("Unease: gate unlocked.");
        }

        private void PlaySound(AudioClip clip)
        {
            if (interactionAudio != null && clip != null)
            {
                interactionAudio.PlayOneShot(clip);
            }
        }

        private void UpdateObjective()
        {
            if (objectiveText == null)
                return;

            if (gateUnlocked)
            {
                objectiveText.text =
                    "Walk through the gate to escape Unease.";
            }
            else if (hasKey)
            {
                objectiveText.text =
                    "Key secured. Find the hidden east gate.";
            }
            else
            {
                objectiveText.text =
                    "Find the Jump Boost and reach the elevated key.";
            }
        }

        private void OnGUI()
        {
            if (player == null)
                return;

            string message = null;

            if (!hasKey && keyVisual != null &&
                NearTarget(keyVisual.transform, keyRange))
            {
                message = "Press E to collect the key.";
            }
            else if (hasKey && !gateUnlocked &&
                gateInteractionPoint != null &&
                NearTarget(gateInteractionPoint, gateRange))
            {
                message = "Press E to unlock the gate.";
            }

            if (message == null)
                return;

            float scale = Mathf.Min(
                Screen.width / 1920f,
                Screen.height / 1080f
            );

            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.alignment = TextAnchor.MiddleCenter;
            style.fontSize = Mathf.Max(
                1,
                Mathf.RoundToInt(30f * scale)
            );
            style.normal.textColor = Color.white;

            float width = 650f * scale;
            float height = 80f * scale;

            GUI.Box(
                new Rect(
                    (Screen.width - width) / 2f,
                    Screen.height - height - 115f * scale,
                    width,
                    height
                ),
                message,
                style
            );
        }
    }
}
