using UnityEngine;
using UnityEngine.SceneManagement; // 這是跳轉場景必備的

public class SceneManagerScript : MonoBehaviour
{
    // 這個方法要給「開始遊戲」按鈕呼叫
    public void StartGame()
    {
        // 確保引號內的名稱跟你在 Build Settings 看到的一模一樣
        SceneManager.LoadScene("GameScene");
    }
}