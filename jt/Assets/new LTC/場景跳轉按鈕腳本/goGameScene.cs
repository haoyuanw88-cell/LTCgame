using UnityEngine;
using UnityEngine.SceneManagement;

public class goGameScene : MonoBehaviour
{
    public void GoToGopher()
    {
        LTC.Mobile.LTCSceneLoader.LoadScene("gopher");
    }

    public void GoToGameScene()
    {
        LTC.Mobile.LTCSceneLoader.LoadScene("GameScene");
    }
    public void GoToshop()
    {
        LTC.Mobile.LTCSceneLoader.LoadScene("shop");
    }

public void GoToPetGarden()
    {
        LTC.Mobile.LTCSceneLoader.LoadScene("PetGarden");
    }

    public void GoToMb()
    {
        LTC.Mobile.LTCSceneLoader.LoadScene("mb");
    }
    public void GoToApplication()
    {
        LTC.Mobile.LTCSceneLoader.LoadScene("application");
    }
}


