
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sanctuary.Rebuild
{
    [RequireComponent(typeof(BoxCollider))]
    public class UneaseExitTrigger : MonoBehaviour
    {
        [Header("Room References")]
        [SerializeField]
        private UneaseMechanism uneaseMechanism;

        [Header("Destination")]
        [SerializeField]
        private string escapeSceneName = "Escape_Blockout_v1";

        private bool isLoading;

        private void Reset()
        {
            Collider col = GetComponent<Collider>();

            if (col != null)
                col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            TryExit(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryExit(other);
        }

        private void TryExit(Collider other)
        {
            if (isLoading)
                return;

            if (uneaseMechanism == null ||
                !uneaseMechanism.GateUnlocked)
                return;

            CharacterController controller =
                other.GetComponentInParent<CharacterController>();

            if (controller == null)
                return;

            if (controller.GetComponent<CalibrationMotor>() == null)
                return;

            string destination = escapeSceneName.Trim();

            Debug.Log(
                "UNEASE EXIT: Tiger reached exit. " +
                "Destination = " + destination
            );

            Debug.Log(
                "UNEASE EXIT: Scenes available to runtime = " +
                SceneManager.sceneCountInBuildSettings
            );

            for (int i = 0;
                 i < SceneManager.sceneCountInBuildSettings;
                 i++)
            {
                Debug.Log(
                    "BUILD SCENE " + i + ": " +
                    SceneUtility.GetScenePathByBuildIndex(i)
                );
            }

            if (!Application.CanStreamedLevelBeLoaded(destination))
            {
                Debug.LogError(
                    "UNEASE EXIT: Destination not available: " +
                    destination +
                    ". Check the ACTIVE Build Profile."
                );

                return;
            }

            isLoading = true;
            Time.timeScale = 1f;

            Debug.Log("UNEASE EXIT: Loading Escape...");

            SceneManager.LoadScene(
                destination,
                LoadSceneMode.Single
            );
        }
    }
}

