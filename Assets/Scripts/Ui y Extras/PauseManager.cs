using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PauseManager : MonoBehaviour
{
    [SerializeField] private GameObject pauseMenu;

    private InputSystem_Actions inputs;
    private bool isPaused = false;

    private void Awake()
    {
        inputs = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        inputs.Player.PauseGame.performed += OnPauseToggle;
        inputs.Player.Enable();
    }

    private void OnDisable()
    {
        inputs.Player.PauseGame.performed -= OnPauseToggle;
        inputs.Player.Disable();
    }

    private void OnPauseToggle(InputAction.CallbackContext context)
    {
        ExecuteToggle();
    }

    public void pauseToggleButton()
    {
        ExecuteToggle();
    }

    private void ExecuteToggle()
    {
        isPaused = !isPaused;
        pauseMenu.SetActive(isPaused);
        if (isPaused)
        {
            Pausegame();
        }
        else
        {
            Resumegame();
        }
    }

    public void Pausegame()
    {
        pauseMenu.SetActive(true);
        Time.timeScale = 0f;
    }

    public void Resumegame()
    {
        pauseMenu.SetActive(false);
        Time.timeScale = 1f;
    }

    public void ResetScene()
    {
        Time.timeScale = 1f;

        Scene currentScene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(currentScene.name);
    }

}