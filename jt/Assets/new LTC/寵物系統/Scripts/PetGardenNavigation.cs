using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class PetGardenNavigation : MonoBehaviour
{
    private const string MainMenuSceneName = "GameScene";

    public void ReturnToMainMenu()
    {
        LTC.Mobile.LTCSceneLoader.LoadScene(MainMenuSceneName);
    }
}
