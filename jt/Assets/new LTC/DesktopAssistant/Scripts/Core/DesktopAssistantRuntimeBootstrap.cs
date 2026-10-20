using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LTC.DesktopAssistant
{
    /// <summary>
    /// 監聽場景切換並在認知遊戲與小遊戲中自動顯示桌面助理。
    /// 不需要修改任何既有遊戲場景；返回主頁、商店或登入畫面時會自動移除。
    /// </summary>
    public static class DesktopAssistantRuntimeBootstrap
    {
        const string PrefabResourcePath = "DesktopAssistant";

        static readonly HashSet<string> SupportedGameScenes = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            "js",
            "mb",
            "mb2",
            "gopher",
            "CardsGame",
            "PipeGame",
            "SupermarketGame",
            "TextPuzzleGame"
        };

        static GameObject assistantInstance;

        /// <summary>
        /// 關閉 Domain Reload 時也要清除靜態狀態，避免重複註冊場景事件。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            assistantInstance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Initialize()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            ApplySceneVisibility(SceneManager.GetActiveScene());
        }

        static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplySceneVisibility(scene);
        }

        static void ApplySceneVisibility(Scene scene)
        {
            if (SupportedGameScenes.Contains(scene.name))
            {
                ShowAssistant();
                return;
            }

            HideAssistant();
        }

        static void ShowAssistant()
        {
            if (assistantInstance != null)
            {
                assistantInstance.SetActive(true);
                return;
            }

            GameObject prefab = Resources.Load<GameObject>(PrefabResourcePath);
            if (prefab == null)
            {
                Debug.LogError("找不到桌面 AI 助理 Resources Prefab：" + PrefabResourcePath);
                return;
            }

            assistantInstance = UnityEngine.Object.Instantiate(prefab);
            assistantInstance.name = "DesktopAssistant (Runtime)";
            UnityEngine.Object.DontDestroyOnLoad(assistantInstance);
        }

        static void HideAssistant()
        {
            if (assistantInstance == null) return;
            UnityEngine.Object.Destroy(assistantInstance);
            assistantInstance = null;
        }

        /// <summary>
        /// 提供測試與日後設定頁查詢某場景是否會顯示助理。
        /// </summary>
        public static bool SupportsScene(string sceneName)
        {
            return !string.IsNullOrWhiteSpace(sceneName) && SupportedGameScenes.Contains(sceneName);
        }
    }
}
