
using UnityEngine;

namespace Sanctuary.Rebuild
{
    [RequireComponent(typeof(BoxCollider))]
    public class SanctuaryExitTrigger : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private SanctuaryMechanism mechanism;

        [SerializeField]
        private Transform player;

        private BoxCollider triggerCollider;

        private void Awake()
        {
            triggerCollider =
                GetComponent<BoxCollider>();

            triggerCollider.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            CheckPlayer(other);
        }

        private void OnTriggerStay(Collider other)
        {
            // The gate may be unlocked while the tiger already overlaps the exit.
            CheckPlayer(other);
        }

        private void CheckPlayer(Collider other)
        {
            if (mechanism == null || player == null)
                return;

            CharacterController controller =
                other.GetComponent<CharacterController>();

            if (controller == null)
                return;

            if (controller.transform != player)
                return;

            mechanism.TryCompleteExit(player);
        }
    }
}
