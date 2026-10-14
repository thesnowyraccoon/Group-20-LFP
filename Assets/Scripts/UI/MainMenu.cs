
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Audio;
using UnityEngine.UI;
using TMPro;

public class MainMenu : MonoBehaviour
{
    [Header("Menu Panels")]
    public GameObject controlsPanel;
    public GameObject settingsPanel;

    [Header("Volume Controls")]
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;
    public TMP_Text musicVolumeValue;
    public TMP_Text sfxVolumeValue;

    [Header("Display Settings")]
    public Toggle fullscreenToggle;

    [Header("Audio Mixer")]
    public AudioMixer audioMixer;

    [Header("Menu Sound Effects")]
    public AudioSource menuSFXSource;
    public AudioClip tigerRoar;
    public AudioClip stoneClick;

    private bool isStartingGame = false;

    private const string GameplayScene =
        "Sanctuary_Alpha_Hayley";

    private const string MusicKey = "MusicVolume";
    private const string SFXKey = "SFXVolume";
    private const string FullscreenKey = "FullscreenEnabled";

    private void Start()
    {
        // Restore saved audio preferences.
        float musicVolume =
            PlayerPrefs.GetFloat(MusicKey, 0.8f);

        float sfxVolume =
            PlayerPrefs.GetFloat(SFXKey, 0.8f);

        if (musicVolumeSlider != null)
            musicVolumeSlider.SetValueWithoutNotify(musicVolume);

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.SetValueWithoutNotify(sfxVolume);

        SetMusicVolume(musicVolume);
        SetSFXVolume(sfxVolume);

        // Restore saved fullscreen preference.
        bool fullscreen = PlayerPrefs.GetInt(
            FullscreenKey,
            Screen.fullScreen ? 1 : 0
        ) == 1;

        if (fullscreenToggle != null)
            fullscreenToggle.SetIsOnWithoutNotify(fullscreen);

        ApplyFullscreen(fullscreen);
    }

    public void PlayGame()
    {
        if (isStartingGame) return;

        isStartingGame = true;
        StartCoroutine(PlayRoarAndStart());
    }

    private IEnumerator PlayRoarAndStart()
    {
        if (menuSFXSource != null && tigerRoar != null)
        {
            menuSFXSource.PlayOneShot(tigerRoar);

            yield return new WaitForSecondsRealtime(
                tigerRoar.length
            );
        }

        SceneManager.LoadScene(GameplayScene);
    }

    private void PlayStoneClick()
    {
        if (menuSFXSource != null && stoneClick != null)
            menuSFXSource.PlayOneShot(stoneClick);
    }

    public void OpenControls()
    {
        PlayStoneClick();
        controlsPanel.SetActive(true);
    }

    public void CloseControls()
    {
        PlayStoneClick();
        controlsPanel.SetActive(false);
    }

    public void OpenSettings()
    {
        PlayStoneClick();
        settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        PlayStoneClick();
        settingsPanel.SetActive(false);
    }

    public void SetMusicVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);

        float decibels =
            Mathf.Log10(Mathf.Max(volume, 0.0001f)) * 20f;

        if (audioMixer != null)
            audioMixer.SetFloat("MusicVolume", decibels);

        if (musicVolumeValue != null)
            musicVolumeValue.text =
                Mathf.RoundToInt(volume * 100f) + "%";

        PlayerPrefs.SetFloat(MusicKey, volume);
    }

    public void SetSFXVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);

        float decibels =
            Mathf.Log10(Mathf.Max(volume, 0.0001f)) * 20f;

        if (audioMixer != null)
            audioMixer.SetFloat("SFXVolume", decibels);

        if (sfxVolumeValue != null)
            sfxVolumeValue.text =
                Mathf.RoundToInt(volume * 100f) + "%";

        PlayerPrefs.SetFloat(SFXKey, volume);
    }

    public void SetFullscreen(bool fullscreen)
    {
        ApplyFullscreen(fullscreen);
        PlayerPrefs.SetInt(
            FullscreenKey,
            fullscreen ? 1 : 0
        );
        PlayerPrefs.Save();
    }

    private void ApplyFullscreen(bool fullscreen)
    {
        Screen.fullScreen = fullscreen;
    }

    public void QuitGame()
    {
        PlayStoneClick();
        PlayerPrefs.Save();
        StartCoroutine(QuitAfterSound());
    }

    private IEnumerator QuitAfterSound()
    {
        if (stoneClick != null)
            yield return new WaitForSecondsRealtime(
                stoneClick.length
            );

        Application.Quit();
        Debug.Log("Quit Game");
    }

    private void OnApplicationQuit()
    {
        PlayerPrefs.Save();
    }
}
