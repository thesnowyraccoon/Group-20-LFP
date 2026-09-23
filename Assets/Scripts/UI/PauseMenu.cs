using UnityEngine;
using UnityEngine.InputSystem;

public class PauseMenu : MonoBehaviour
{
    public GameObject pauseMenu;

    public void OnPause(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            pauseMenu.SetActive(!pauseMenu.activeSelf);

            Cursor.lockState = CursorLockMode.None;
        }
    }

    public void OnQuit()
    {
        Application.Quit();
    }
}
