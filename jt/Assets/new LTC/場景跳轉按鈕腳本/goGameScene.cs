using UnityEngine;
using UnityEngine.SceneManagement;

public class goGameScene : MonoBehaviour
{
    public void GoToGopher()
    {
        SceneManager.LoadScene("gopher");
    }

    public void GoToGameScene()
    {
        SceneManager.LoadScene("GameScene");
    }
    public void GoToshop()
    {
        SceneManager.LoadScene("shop");
    }
    public void GoToMb()
    {
        SceneManager.LoadScene("mb");
    }
    public void GoToApplication()
    {
        SceneManager.LoadScene("application");
    }
}


