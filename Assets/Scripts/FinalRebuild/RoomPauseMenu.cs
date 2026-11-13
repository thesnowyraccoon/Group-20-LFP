using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Sanctuary.Rebuild
{
    [DefaultExecutionOrder(-100)]
    public class RoomPauseMenu : MonoBehaviour
    {
        [Header("Panels")]
        public GameObject pauseOverlay;
        public GameObject pauseWindow;
        public GameObject homeConfirmation;

        [Header("Volume")]
        public Slider volumeSlider;

        [Header("Gameplay Scripts")]
        public CalibrationMotor motor;
        public CalibrationCamera cameraController;
        public SanctuaryMechanism mechanism;

        [Header("Scene")]
        public string homeSceneName = "HomeMenu";

        private bool paused;
        private bool leaving;
        private bool motorWasEnabled;
        private bool cameraWasEnabled;
        private bool mechanismWasEnabled;
        private float previousTimeScale;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;

        private const string VolumePreference = "MasterVolume";
        private const string MutedPreference = "MasterMuted";
        private float savedVolume;
        private bool muted;

        public bool IsPaused => paused;

        private void Awake()
        {
            // Prefab assets cannot retain references to objects in a room.
            // Resolve only within this HUD's scene, preserving explicit overrides.
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                if (motor == null) motor = root.GetComponentInChildren<CalibrationMotor>();
                if (cameraController == null) cameraController = root.GetComponentInChildren<CalibrationCamera>();
                if (mechanism == null) mechanism = root.GetComponentInChildren<SanctuaryMechanism>();
            }
        }

        private void Start()
        {
            pauseOverlay.SetActive(false);
            pauseWindow.SetActive(true);
            homeConfirmation.SetActive(false);

            savedVolume = Mathf.Clamp01(
                PlayerPrefs.GetFloat(VolumePreference, 1f)
            );
            muted = PlayerPrefs.GetInt(MutedPreference, 0) != 0;
            AudioListener.volume = muted ? 0f : savedVolume;

            if (volumeSlider != null)
            {
                volumeSlider.SetValueWithoutNotify(AudioListener.volume);
                volumeSlider.onValueChanged.AddListener(SetVolume);
            }
        }

        private void Update()
        {
            if (leaving || Keyboard.current == null) return;

            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (!paused)
                    OpenPause();
                else if (homeConfirmation.activeSelf)
                    CancelHome();
                else
                    Resume();
            }
        }

        private void BeginPause()
        {
            if (paused || leaving) return;

            paused = true;
            previousTimeScale = Time.timeScale;
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;

            motorWasEnabled = motor != null && motor.enabled;
            cameraWasEnabled =
                cameraController != null && cameraController.enabled;
            mechanismWasEnabled =
                mechanism != null && mechanism.enabled;

            if (motor != null)
                motor.enabled = false;

            if (cameraController != null)
                cameraController.enabled = false;

            if (mechanism != null)
                mechanism.enabled = false;

            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            pauseOverlay.SetActive(true);
        }

        public void OpenPause()
        {
            if (leaving) return;

            BeginPause();
            pauseWindow.SetActive(true);
            homeConfirmation.SetActive(false);
        }

        public void TogglePause()
        {
            if (paused) Resume();
            else OpenPause();
        }

        public void ToggleAudio()
        {
            if (leaving) return;
            muted = !muted;
            // A zero slider setting must still be recoverable with Unmute.
            if (!muted && savedVolume <= 0f) savedVolume = 1f;
            AudioListener.volume = muted ? 0f : savedVolume;
            if (volumeSlider != null)
                volumeSlider.SetValueWithoutNotify(AudioListener.volume);
            PlayerPrefs.SetFloat(VolumePreference, savedVolume);
            PlayerPrefs.SetInt(MutedPreference, muted ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void OpenAudio()
        {
            OpenPause();

            if (volumeSlider != null)
                volumeSlider.Select();
        }

        public void AskHome()
        {
            if (leaving) return;

            BeginPause();
            pauseWindow.SetActive(false);
            homeConfirmation.SetActive(true);
        }

        public void CancelHome()
        {
            if (!paused || leaving) return;

            homeConfirmation.SetActive(false);
            pauseWindow.SetActive(true);
        }

        public void Resume()
        {
            if (!paused || leaving) return;

            pauseOverlay.SetActive(false);
            RestoreGameplay();
            paused = false;

            PlayerPrefs.Save();
        }

        private void RestoreGameplay()
        {
            Time.timeScale = previousTimeScale;

            if (motor != null)
                motor.enabled = motorWasEnabled;

            if (cameraController != null)
                cameraController.enabled = cameraWasEnabled;

            if (mechanism != null)
                mechanism.enabled = mechanismWasEnabled;

            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
        }

        public void SetVolume(float value)
        {
            float volume = Mathf.Clamp01(value);
            if (volume > 0f) savedVolume = volume;
            muted = volume <= 0f;
            AudioListener.volume = volume;
            PlayerPrefs.SetFloat(VolumePreference, savedVolume);
            PlayerPrefs.SetInt(MutedPreference, muted ? 1 : 0);
        }

        public void ConfirmHome()
        {
            if (leaving) return;

            if (!Application.CanStreamedLevelBeLoaded(homeSceneName))
            {
                Debug.LogError(
                    "Cannot load " + homeSceneName +
                    ". Add it to the active Build Profile scene list."
                );
                return;
            }

            leaving = true;
            PlayerPrefs.Save();

            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            SceneManager.LoadScene(homeSceneName);
        }

        private void OnDestroy()
        {
            if (volumeSlider != null)
                volumeSlider.onValueChanged.RemoveListener(SetVolume);

            if (paused && !leaving)
                RestoreGameplay();
        }
    }
}
