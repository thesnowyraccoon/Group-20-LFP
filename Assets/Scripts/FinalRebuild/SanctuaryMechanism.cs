
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Sanctuary.Rebuild
{
    public class SanctuaryMechanism : MonoBehaviour
    {
        [Header("Room References")]
        public Transform player;
        public GameObject gate;
        public Transform exitPoint;
        public GameObject keyVisual;

        [Header("HUD")]
        public TMP_Text objectiveText;

        [Header("Interaction Distances")]
        public float interactionRange = 1.5f;
        public float exitRange = 1.2f;

        [Header("Interaction Audio")]
        [SerializeField] private AudioSource interactionAudio;
        [SerializeField] private AudioClip keyPickupSound;
        [SerializeField] private AudioClip gateOpenSound;

        [Header("Room Progression")]
        [SerializeField] private string nextSceneName =
            "Unease_Blockout_v1";

        private bool hasKey;
        private bool gateUnlocked;
        private bool completed;

        private void Start()
        {
            UpdateObjective();
        }

        private void UpdateObjective()
        {
            if (objectiveText == null)
                return;

            if (completed)
            {
                objectiveText.text = "Leaving Sanctuary...";
            }
            else if (gateUnlocked)
            {
                objectiveText.text =
                    "Walk through the gate to leave Sanctuary.";
            }
            else if (hasKey)
            {
                objectiveText.text =
                    "Key secured — find the exit!";
            }
            else
            {
                objectiveText.text =
                    "Find the key to unlock the exit gate.";
            }
        }

        private bool NearKey()
        {
            if (player == null)
                return false;

            Vector3 centre =
                player.position + Vector3.up * 0.9f;

            return Vector3.Distance(
                centre,
                transform.position
            ) <= interactionRange;
        }

        private bool NearExit()
        {
            if (player == null || exitPoint == null)
                return false;

            Vector3 offset =
                player.position - exitPoint.position;

            if (Mathf.Abs(offset.y) > 0.75f)
                return false;

            offset.y = 0f;

            return offset.sqrMagnitude
                <= exitRange * exitRange;
        }

        private void PlayInteractionSound(AudioClip clip)
        {
            if (interactionAudio != null && clip != null)
            {
                interactionAudio.PlayOneShot(clip);
            }
        }

        private void Update()
        {
            if (completed || Keyboard.current == null)
                return;

            if (!Keyboard.current.eKey.wasPressedThisFrame)
                return;

            // Collect the key with E.
            if (!hasKey)
            {
                if (!NearKey() || keyVisual == null)
                    return;

                hasKey = true;
                keyVisual.SetActive(false);

                PlayInteractionSound(keyPickupSound);
                UpdateObjective();

                Debug.Log("Sanctuary: key collected.");
                return;
            }

            // Unlock the gate with E.
            if (!gateUnlocked)
            {
                if (!NearExit() || gate == null)
                    return;

                gateUnlocked = true;
                gate.SetActive(false);

                PlayInteractionSound(gateOpenSound);
                UpdateObjective();

                Debug.Log("Sanctuary: gate unlocked.");
            }

            // No E action is required after unlocking.
            // Entering the exit trigger handles progression.
        }

        public void TryCompleteExit(Transform enteringPlayer)
        {
            if (completed || !gateUnlocked)
                return;

            if (player == null || enteringPlayer != player)
                return;

            if (string.IsNullOrWhiteSpace(nextSceneName))
            {
                Debug.LogError(
                    "Sanctuary: next scene name is missing."
                );
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(
                nextSceneName))
            {
                Debug.LogError(
                    "Sanctuary: cannot load scene '" +
                    nextSceneName +
                    "'. Check Build Profiles > Scene List."
                );
                return;
            }

            completed = true;
            UpdateObjective();

            Debug.Log(
                "Sanctuary: entering " + nextSceneName
            );

            // Ensure the next room isn't paused.
            Time.timeScale = 1f;

            // The next room's camera can lock the cursor.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            SceneManager.LoadScene(nextSceneName);
        }

        private void OnGUI()
        {
            if (completed)
                return;

            string message;

            if (gateUnlocked && NearExit())
            {
                message =
                    "Walk through the gate to leave Sanctuary.";
            }
            else if (hasKey && NearExit())
            {
                message = "Press E to unlock the gate.";
            }
            else if (!hasKey && NearKey())
            {
                message = "Press E to collect the key.";
            }
            else if (!hasKey && NearExit())
            {
                message = "Gate locked. Find the key.";
            }
            else
            {
                return;
            }

            float scale = Mathf.Min(
                Screen.width / 1920f,
                Screen.height / 1080f
            );

            GUIStyle style =
                new GUIStyle(GUI.skin.box);

            style.fontSize = Mathf.Max(
                1,
                Mathf.RoundToInt(32f * scale)
            );

            style.alignment =
                TextAnchor.MiddleCenter;

            style.wordWrap = true;
            style.normal.textColor = Color.white;

            float width = 720f * scale;
            float height = 90f * scale;

            GUI.Box(
                new Rect(
                    (Screen.width - width) / 2f,
                    Screen.height - height -
                    110f * scale,
                    width,
                    height
                ),
                message,
                style
            );
        }
    }
}

