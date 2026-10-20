using UnityEngine;

namespace LTC.DesktopAssistant
{
    /// <summary>
    /// 保存助理外觀與位置。使用獨立鍵值，不會改動寵物收藏或玩家資料。
    /// </summary>
    public static class AssistantPreferences
    {
        const string PetKey = "LTC_Assistant_SelectedPet";
        const string PositionXKey = "LTC_Assistant_PositionX";
        const string PositionYKey = "LTC_Assistant_PositionY";
        const string HasPositionKey = "LTC_Assistant_HasPosition";

        public static string SelectedPetId
        {
            get => PlayerPrefs.GetString(PetKey, string.Empty);
            set
            {
                PlayerPrefs.SetString(PetKey, value ?? string.Empty);
                PlayerPrefs.Save();
            }
        }

        public static void SavePosition(Vector2 anchoredPosition)
        {
            PlayerPrefs.SetFloat(PositionXKey, anchoredPosition.x);
            PlayerPrefs.SetFloat(PositionYKey, anchoredPosition.y);
            PlayerPrefs.SetInt(HasPositionKey, 1);
            PlayerPrefs.Save();
        }

        public static bool TryLoadPosition(out Vector2 anchoredPosition)
        {
            anchoredPosition = new Vector2(
                PlayerPrefs.GetFloat(PositionXKey),
                PlayerPrefs.GetFloat(PositionYKey));
            return PlayerPrefs.GetInt(HasPositionKey, 0) == 1;
        }
    }
}
