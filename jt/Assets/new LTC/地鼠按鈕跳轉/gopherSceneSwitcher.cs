using UnityEngine;
using UnityEngine.SceneManagement;

public class gopherSceneSwitcher : MonoBehaviour
{
    // �����b�{�������w�ؼг����W�١A�o�̶�J 'gopher'
    public void LoadGopherScene()
    {
        LTC.Mobile.LTCSceneLoader.LoadScene("gopher");
    }
}
