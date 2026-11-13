
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

namespace Sanctuary.Rebuild
{
    public class EscapeKeyPickup : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform player;
        [SerializeField] private GameObject keyVisual;
        [SerializeField] private TMP_Text objectiveText;

        [Header("Interaction")]
        [SerializeField] private float interactionRange = 2.5f;
        [SerializeField] private float maximumHeightDifference = 1.4f;

        [Header("Sound")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip pickupSound;

        public bool HasKey { get; private set; }

        private void Start()
        {
            if (keyVisual == null)
                keyVisual = gameObject;
        }

        private void Update()
        {
            if (HasKey || player == null)
                return;

            if (Keyboard.current == null)
                return;

            if (!IsPlayerNearKey())
                return;

            if (Keyboard.current.eKey.wasPressedThisFrame)
                CollectKey();
        }

        private bool IsPlayerNearKey()
        {
            Vector3 difference =
                player.position - transform.position;

            if (Mathf.Abs(difference.y) >
                maximumHeightDifference)
                return false;

            difference.y = 0f;

            return difference.sqrMagnitude <=
                   interactionRange * interactionRange;
        }

        private void CollectKey()
        {
            HasKey = true;

            if (audioSource != null && pickupSound != null)
                audioSource.PlayOneShot(pickupSound);

            if (objectiveText != null)
                objectiveText.text =
                    "Key collected! Find the Speed Boost.";

            if (keyVisual != null)
            {
                // Preserve the pickup controller and audio.
                // Disable only the visible key.
                if (keyVisual != gameObject)
                    keyVisual.SetActive(false);
                else
                {
                    foreach (Renderer renderer in
                             GetComponentsInChildren<Renderer>())
                        renderer.enabled = false;
                }
            }

            Debug.Log("ESCAPE: Golden key collected.");
        }

        private void OnGUI()
        {
            if (HasKey || !IsPlayerNearKey())
                return;

            GUI.Label(
                new Rect(
                    Screen.width / 2f - 130f,
                    Screen.height - 130f,
                    300f,
                    40f
                ),
                "Press E to collect the key"
            );
        }
    }
}
