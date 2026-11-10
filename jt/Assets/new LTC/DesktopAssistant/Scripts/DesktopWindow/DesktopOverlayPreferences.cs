using UnityEngine;

namespace LTC.DesktopAssistant.Windows
{
    /// <summary>保存桌面助理設定，不包含帳號、API Key 或擷取圖片。</summary>
    public static class DesktopOverlayPreferences
    {
        const string Prefix = "LTC_DesktopOverlay_";

        public static bool GetBool(string key, bool fallback) =>
            PlayerPrefs.GetInt(Prefix + key, fallback ? 1 : 0) == 1;

        public static void SetBool(string key, bool value)
        {
            PlayerPrefs.SetInt(Prefix + key, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static float GetFloat(string key, float fallback) =>
            PlayerPrefs.GetFloat(Prefix + key, fallback);

        public static void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(Prefix + key, value);
            PlayerPrefs.Save();
        }
    }
}
