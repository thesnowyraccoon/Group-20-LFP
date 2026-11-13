using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Sanctuary.Rebuild;

// Temporary editor-only repair/verification harness; never included in a player build.
[InitializeOnLoad]
public static class TransitionVerification
{
    const string Command = "Temp/TransitionVerification.command";
    const string Report = "Temp/TransitionVerification.txt";
    const string Unease = "Assets/Scenes/Final_Rebuild/Unease_Blockout_v1.unity";
    static double deadline;
    static double next;
    static int phase;
    static void Log(string s) { File.AppendAllText(Report, DateTime.Now.ToString("O") + " " + s + "\n"); }
    static TransitionVerification() { EditorApplication.update += Tick; }
    static object Field(object o, string n) => o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(o, v);
    static void Invoke(object o, string n) => o.GetType().GetMethod(n, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(o, null);
    static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    static void Require(bool value, string message) { if (!value) throw new Exception(message); Log("PASS " + message); }
    static void Move(Vector3 position)
    {
        var motor = Find<CalibrationMotor>();
        var cc = motor.GetComponent<CharacterController>();
        cc.enabled = false; motor.transform.position = position; cc.enabled = true;
        Physics.SyncTransforms(); cc.Move(Vector3.down * 0.01f);
    }
    static void Arrival(string scene)
    {
        Require(SceneManager.GetActiveScene().name == scene, "Loaded " + scene);
        var motor = Find<CalibrationMotor>(); var camera = Find<CalibrationCamera>(); var hud = Find<RoomPauseMenu>();
        Require(motor != null && motor.enabled && motor.GetComponent<CharacterController>().enabled, "Tiger controller active");
        Require(camera != null && camera.enabled && camera.target == motor.transform && motor.view == camera, "Camera and tiger references connected");
        Require(hud != null && hud.motor == motor && hud.cameraController == camera && !hud.IsPaused && !hud.pauseOverlay.activeSelf, "Reusable HUD bound and unpaused");
        Require(Mathf.Approximately(Time.timeScale, 1f), "Time.timeScale = 1");
        Log("Spawn=" + motor.transform.position + "; recorded Awake spawn=" + Field(motor, "spawn") + "; main camera=" + (Camera.main == null ? "NONE" : Camera.main.name));
        Require(Vector3.Distance(motor.transform.position, (Vector3)Field(motor, "spawn")) < 2f, "Tiger remains near authored spawn");
        Require(Physics.Raycast(motor.transform.position + Vector3.up, Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore), "Solid surface beneath spawn");
        int missing = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
        Require(missing == 0, "No missing scripts in loaded scene");
    }
    static void Tick()
    {
        try
        {
            if (File.Exists(Command))
            {
                string command = File.ReadAllText(Command).Trim(); File.Delete(Command);
                if (command == "repair")
                {
                    Require(!EditorApplication.isPlaying && !SceneManager.GetActiveScene().isDirty, "Scene saved before repair");
                    string old = "Assets/Scenes/Final_Rebuild/Esacpe_Blockout_v1.unity";
                    string target = "Assets/Scenes/Final_Rebuild/Escape_Blockout_v1.unity";
                    string error = AssetDatabase.MoveAsset(old, target); Require(string.IsNullOrEmpty(error), "Rename Escape preserving GUID: " + error);
                    var scenes = EditorBuildSettings.scenes;
                    foreach (var s in scenes) if (s.path == old) s.path = target;
                    EditorBuildSettings.scenes = scenes;
                    string text = File.ReadAllText(Unease);
                    Require(text.Contains("5158778f993faac49bb30d2fd6f8b1e1"), "Found obsolete trigger GUID");
                    File.WriteAllText(Unease, text.Replace("5158778f993faac49bb30d2fd6f8b1e1", "53cb145a9b4c48744a7c60e14423e0d1"));
                    AssetDatabase.ImportAsset(Unease, ImportAssetOptions.ForceUpdate);
                    EditorSceneManager.OpenScene(Unease);
                    var trigger = Find<UneaseExitTrigger>();
                    Require(trigger != null && Field(trigger, "uneaseMechanism") != null && trigger.GetComponent<BoxCollider>().isTrigger, "Unease trigger restored with mechanism and collider");
                    Log("SCRIPT class=" + AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Scripts/FinalRebuild/UneaseExitTrigger.cs").GetClass().AssemblyQualifiedName);
                    var profile = UnityEditor.Build.Profile.BuildProfile.GetActiveBuildProfile();
                    Log("Active custom build profile=" + (profile == null ? "none (shared scene list)" : AssetDatabase.GetAssetPath(profile)));
                    foreach (var s in EditorBuildSettings.scenes) Log("BUILD enabled=" + s.enabled + " " + s.path);
                    Log("REPAIR COMPLETE");
                }
                if (command == "test")
                {
                    Require(!EditorApplication.isPlaying && !SceneManager.GetActiveScene().isDirty, "Safe to start Play Mode test");
                    EditorSceneManager.OpenScene("Assets/Scenes/Sanctuary_Production_v1.unity");
                    SessionState.SetBool("TransitionVerification.running", true);
                    EditorApplication.isPlaying = true;
                }
            }
            if (!SessionState.GetBool("TransitionVerification.running", false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (phase == 0) { deadline = EditorApplication.timeSinceStartup + 60; next = EditorApplication.timeSinceStartup + 3; phase = 1; }
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Play Mode verification timed out at phase " + phase);
            if (EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + 2;
            if (phase == 1)
            {
                Arrival("Sanctuary_Production_v1");
                for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++) Log("RUNTIME " + i + " " + SceneUtility.GetScenePathByBuildIndex(i));
                Require(Application.CanStreamedLevelBeLoaded("Escape_Blockout_v1"), "Escape resolves in runtime build scene list");
                Move(Find<SanctuaryExitTrigger>().GetComponent<BoxCollider>().bounds.center);
                phase = 2;
            }
            else if (phase == 2)
            {
                Require(SceneManager.GetActiveScene().name == "Sanctuary_Production_v1", "Locked Sanctuary gate blocks transition");
                var m = Find<SanctuaryMechanism>();
                // Test fixture sets progression state, then physics invokes the actual trigger.
                Set(m, "hasKey", true); Set(m, "gateUnlocked", true); m.gate.SetActive(false);
                Log("FIXTURE Sanctuary gate unlocked while tiger overlaps trigger"); phase = 3;
            }
            else if (phase == 3)
            {
                Arrival("Unease_Blockout_v1");
                Move(Find<UneaseExitTrigger>().GetComponent<BoxCollider>().bounds.center); phase = 4;
            }
            else if (phase == 4)
            {
                Require(SceneManager.GetActiveScene().name == "Unease_Blockout_v1", "Locked Unease gate blocks transition");
                var m = Find<UneaseMechanism>(); Invoke(m, "CollectKey"); Invoke(m, "UnlockGate");
                Require(m.GateUnlocked, "Unease gate unlocked via existing mechanism");
                Log("FIXTURE Unease key collected and gate unlocked while tiger overlaps trigger"); phase = 5;
            }
            else if (phase == 5)
            {
                Arrival("Escape_Blockout_v1"); Log("PLAY MODE TRANSITION TEST COMPLETE");
                SessionState.SetBool("TransitionVerification.running", false); EditorApplication.isPlaying = false; phase = 0;
            }
        }
        catch (Exception e)
        {
            Log("FAIL phase=" + phase + " " + e); SessionState.SetBool("TransitionVerification.running", false); phase = 0;
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
        }
    }
}
