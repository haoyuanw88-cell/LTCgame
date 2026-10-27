using UnityEngine;
using UnityEngine.SceneManagement;

public class GoShopScene : MonoBehaviour
{
    public void GoToShop()
    {
        LTC.Mobile.LTCSceneLoader.LoadScene("shop");
    }
}
