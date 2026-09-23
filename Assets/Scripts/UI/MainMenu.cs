using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenu : MonoBehaviour
{
    public GameObject controlsPanel;
    public GameObject settingsPanel;
    public TMP_Text musicVolumeValue;

    public void PlayGame()
    {
        SceneManager.LoadScene("RoomGenerationExperiment");
    }

    public void OpenControls()
    {
        controlsPanel.SetActive(true);
    }

    public void CloseControls()
    {
        controlsPanel.SetActive(false);
    }

    public void OpenSettings()
    {
        settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        settingsPanel.SetActive(false);
    }

    public void SetMusicVolume(float volume)
    {
        AudioListener.volume = volume;

        int percentage = Mathf.RoundToInt(volume * 100);
        musicVolumeValue.text = percentage + "%";
    }

    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("Quit Game");
    }
}