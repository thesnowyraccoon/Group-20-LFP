using UnityEngine;

namespace Sanctuary.Rebuild
{
    public class FloatingKeyVisual : MonoBehaviour
    {
        public float spinSpeed = 45f;
        public float bobHeight = 0.12f;
        public float bobFrequency = 0.6f;

        private Vector3 startingPosition;

        private void Awake()
        {
            startingPosition = transform.localPosition;
        }

        private void Update()
        {
            transform.Rotate(
                Vector3.up,
                spinSpeed * Time.deltaTime,
                Space.Self
            );

            float bob = Mathf.Sin(
                Time.time * Mathf.PI * 2f * bobFrequency
            ) * bobHeight;

            transform.localPosition =
                startingPosition + Vector3.up * bob;
        }
    }
}