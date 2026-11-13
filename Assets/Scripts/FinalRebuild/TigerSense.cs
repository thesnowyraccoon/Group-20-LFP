
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sanctuary.Rebuild
{
    public class TigerSense : MonoBehaviour
    {
        [Header("Highlight Settings")]
        [SerializeField] private Material highlightMaterial;
        [SerializeField] private GameObject[] targets;

        [Header("Activation Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip activationSound;

        private readonly Dictionary<Renderer, Material[]>
            originalMaterials = new Dictionary<Renderer, Material[]>();

        private bool senseActive;

        private void Awake()
        {
            CacheTargetMaterials();
        }

        private void CacheTargetMaterials()
        {
            originalMaterials.Clear();

            if (targets == null) return;

            foreach (GameObject target in targets)
            {
                if (target == null) continue;

                Renderer[] renderers =
                    target.GetComponentsInChildren<Renderer>(true);

                foreach (Renderer renderer in renderers)
                {
                    if (!originalMaterials.ContainsKey(renderer))
                    {
                        originalMaterials.Add(
                            renderer,
                            renderer.sharedMaterials
                        );
                    }
                }
            }
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            bool holdingSense =
                Keyboard.current.qKey.isPressed &&
                Time.timeScale > 0f;

            if (holdingSense == senseActive)
                return;

            senseActive = holdingSense;

            if (senseActive)
            {
                ApplyHighlight();

                if (audioSource != null &&
                    activationSound != null)
                {
                    audioSource.PlayOneShot(activationSound);
                }
            }
            else
            {
                RestoreMaterials();
            }
        }

        private void ApplyHighlight()
        {
            if (highlightMaterial == null)
                return;

            foreach (var entry in originalMaterials)
            {
                Renderer renderer = entry.Key;

                if (renderer == null ||
                    !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Material[] replacements =
                    new Material[entry.Value.Length];

                for (int i = 0; i < replacements.Length; i++)
                {
                    replacements[i] = highlightMaterial;
                }

                renderer.sharedMaterials = replacements;
            }
        }

        private void RestoreMaterials()
        {
            foreach (var entry in originalMaterials)
            {
                if (entry.Key != null)
                {
                    entry.Key.sharedMaterials = entry.Value;
                }
            }
        }

        private void OnDisable()
        {
            RestoreMaterials();
            senseActive = false;
        }
    }
}

