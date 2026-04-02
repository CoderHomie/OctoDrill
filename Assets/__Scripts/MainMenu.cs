using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void StartButton()
    {
        SceneManager.LoadScene("__Scene_0");
    }
    public void ExitButton()
    {
        Application.Quit();
    }
}
