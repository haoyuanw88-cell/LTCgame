using UnityEngine;
using UnityEngine.SceneManagement;

public class gopherSceneSwitcher : MonoBehaviour
{
    // 直接在程式中指定目標場景名稱，這裡填入 'gopher'
    public void LoadGopherScene()
    {
        SceneManager.LoadScene("gopher");
    }
}
