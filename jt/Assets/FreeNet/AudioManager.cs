using UnityEngine;

public class AudioManager : MonoBehaviour
{
    // 讓其他腳本可以輕鬆訪問這個管理器
    public static AudioManager instance;

    public AudioSource bgmSource; // 專門播 BGM 的喇叭
    public AudioClip backgroundMusic; // 拖入你的 BGM 檔案

    void Awake()
    {
        // 單例模式：確保場景中只有一個管理器，且換關卡時不銷毀
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject); 
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        if (backgroundMusic != null && bgmSource != null)
        {
            bgmSource.clip = backgroundMusic;
            bgmSource.loop = true; // BGM 通常要循環
            bgmSource.playOnAwake = true;
            bgmSource.Play();
        }
    }
}