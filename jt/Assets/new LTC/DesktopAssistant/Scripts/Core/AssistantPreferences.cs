using System;
using System.Linq;
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

        /// <summary>目前助理寵物變更時通知所有名稱與外觀顯示元件。</summary>
        public static event Action<string> SelectedPetChanged;

        public static string SelectedPetId
        {
            get => PlayerPrefs.GetString(PetKey, string.Empty);
            set
            {
                string normalized = value ?? string.Empty;
                if (PlayerPrefs.GetString(PetKey, string.Empty) == normalized) return;
                PlayerPrefs.SetString(PetKey, normalized);
                PlayerPrefs.Save();
                SelectedPetChanged?.Invoke(normalized);
            }
        }

        /// <summary>將寵物 ID 轉換為收藏系統中的正式顯示名稱。</summary>
        public static string GetPetDisplayName(string petId, string fallback = "寵物")
        {
            LTCPetDefinition definition = LTCPetCollectionService.AllPets
                .FirstOrDefault(pet => pet.id == petId);
            return definition == null || string.IsNullOrWhiteSpace(definition.displayName)
                ? fallback
                : definition.displayName;
        }

        /// <summary>取得目前選擇的寵物名稱；尚未選擇時使用第一隻已擁有寵物。</summary>
        public static string GetSelectedPetDisplayName(string fallback = "寵物")
        {
            string selectedId = SelectedPetId;
            if (!string.IsNullOrWhiteSpace(selectedId)) return GetPetDisplayName(selectedId, fallback);
            LTCPetDefinition owned = LTCPetCollectionService.GetOwnedPets().FirstOrDefault();
            return owned == null ? fallback : owned.displayName;
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
