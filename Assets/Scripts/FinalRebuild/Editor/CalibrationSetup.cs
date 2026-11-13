using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Sanctuary.Rebuild.Editor
{
    public static class CalibrationSetup
    {
        [MenuItem("Tools/Sanctuary/Set Up Calibration Player")]
        public static void Create()
        {
            var scene = SceneManager.GetActiveScene();
            if (Application.isPlaying || scene.path != "Assets/Scenes/Final_Rebuild/Traversal_Calibration.unity")
            {
                EditorUtility.DisplayDialog("Calibration setup", "Open the saved Traversal_Calibration scene outside Play mode first.", "OK");
                return;
            }
            if (Object.FindAnyObjectByType<CalibrationMotor>() != null)
            {
                EditorUtility.DisplayDialog("Already set up", "A calibration player already exists. Select it to adjust its settings.", "OK");
                return;
            }
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Set up calibration player");
            foreach (var camera in Object.FindObjectsByType<Camera>())
            {
                if (camera.gameObject.scene != scene) continue;
                Undo.RecordObject(camera.gameObject, "Disable previous camera");
                camera.gameObject.SetActive(false);
            }
            var player = new GameObject("Calibration_Player");
            Undo.RegisterCreatedObjectUndo(player, "Create player");
            player.transform.position = new Vector3(0, 0.05f, 0);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = Vector3.up * 0.9f;
            cc.stepOffset = 0.25f;
            cc.skinWidth = 0.035f;
            cc.minMoveDistance = 0;
            var visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "Placeholder_Visual";
            visual.transform.SetParent(player.transform, false);
            visual.transform.localPosition = Vector3.up * 0.9f;
            visual.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Facing_Marker";
            marker.transform.SetParent(visual.transform, false);
            marker.transform.localPosition = new Vector3(0, 0.3f, 0.5f);
            marker.transform.localScale = new Vector3(0.3f, 0.15f, 0.3f);
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            var cameraObject = new GameObject("Calibration_Camera");
            Undo.RegisterCreatedObjectUndo(cameraObject, "Create camera");
            cameraObject.tag = "MainCamera";
            var newCamera = cameraObject.AddComponent<Camera>();
            newCamera.fieldOfView = 60;
            newCamera.nearClipPlane = 0.05f;
            cameraObject.AddComponent<AudioListener>();
            var orbit = cameraObject.AddComponent<CalibrationCamera>();
            orbit.target = player.transform;
            orbit.playerVisual = visual.GetComponent<Renderer>();
            cameraObject.transform.SetPositionAndRotation(new Vector3(0, 2.795f, -4.755f), Quaternion.Euler(18, 0, 0));
            player.AddComponent<CalibrationMotor>().view = orbit;
            Selection.activeGameObject = player;
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(group);
            Debug.Log("Calibration ready. Save scene. WASD move, Space jump, mouse orbit, wheel zoom, R reset, Escape release cursor. Click Game view to resume.");
        }
    }
}
