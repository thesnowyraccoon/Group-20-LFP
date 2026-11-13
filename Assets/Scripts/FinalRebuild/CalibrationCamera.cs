using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace Sanctuary.Rebuild
{
    public class CalibrationCamera : MonoBehaviour
    {
        public Transform target;
        public Renderer playerVisual;
        public float distance = 5f;
        public float sensitivity = 0.12f;
        public float Yaw { get; private set; }
        float pitch = 12f;
        float actualDistance = 5f;
        RoomPauseMenu pauseMenu;
        readonly List<RaycastResult> uiHits = new List<RaycastResult>();

        void Start()
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                pauseMenu = root.GetComponentInChildren<RoomPauseMenu>();
                if (pauseMenu != null) break;
            }
            ResetView();
            if (pauseMenu == null || !pauseMenu.IsPaused) Capture();
        }
        void Capture() { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
        public void ResetView() { Yaw = 0; pitch = 12f; actualDistance = distance; }
        void Update()
        {
            if (Time.timeScale == 0f || (pauseMenu != null && pauseMenu.IsPaused)) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                // The room menu owns Escape, including the frame it resumes us.
                if (pauseMenu == null || !pauseMenu.isActiveAndEnabled)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                return;
            }
            if (Mouse.current == null) return;
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame && !PointerOverUI()) Capture();
                return;
            }
            Vector2 delta = Mouse.current.delta.ReadValue();
            Yaw += delta.x * sensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * sensitivity, -10f, 65f);
            distance = Mathf.Clamp(distance - Mouse.current.scroll.ReadValue().y * 0.005f, 3f, 6f);
        }
        bool PointerOverUI()
        {
            if (EventSystem.current == null) return false;
            // Raycast this frame's position; the input module may update after us.
            uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current)
            {
                position = Mouse.current.position.ReadValue()
            }, uiHits);
            return uiHits.Exists(hit => hit.module is UnityEngine.UI.GraphicRaycaster);
        }
        void LateUpdate()
        {
            if (target == null) return;
            Vector3 pivot = target.position + Vector3.up * 1.2f;
            Quaternion rotation = Quaternion.Euler(pitch, Yaw, 0);
            Vector3 backward = rotation * Vector3.back;
            float allowed = distance;
            foreach (var hit in Physics.SphereCastAll(pivot, 0.22f, backward, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform == target || hit.transform.IsChildOf(target)) continue;
                allowed = Mathf.Min(allowed, Mathf.Max(0, hit.distance - 0.08f));
            }
            actualDistance = allowed < actualDistance ? allowed : Mathf.MoveTowards(actualDistance, allowed, 4f * Time.deltaTime);
            transform.SetPositionAndRotation(pivot + backward * actualDistance, rotation);
            // Hide the placeholder only when obstruction forces the view inside it.
            if (playerVisual != null) playerVisual.enabled = actualDistance > 0.8f;
        }
        void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (playerVisual != null) playerVisual.enabled = true;
        }
    }
}
