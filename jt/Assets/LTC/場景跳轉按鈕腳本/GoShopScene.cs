using UnityEngine;
using UnityEngine.SceneManagement;

public class GoShopScene : MonoBehaviour
{
    public void GoToShop()
    {
        SceneManager.LoadScene("shop");
    }
}
