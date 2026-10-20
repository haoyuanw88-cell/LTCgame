using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LTC.DesktopAssistant
{
    /// <summary>
    /// 使用單一按鍵輪流切換玩家可用的寵物，避免下拉選單長時間佔據畫面。
    /// </summary>
    public sealed class AssistantPetSelector : MonoBehaviour
    {
        [SerializeField] Button changeButton;
        [SerializeField] TMP_Text buttonLabel;
        readonly List<LTCPetDefinition> pets = new List<LTCPetDefinition>();
        int selectedIndex;

        public event Action<LTCPetDefinition> SelectionChanged;

        void Awake()
        {
            if (changeButton != null) changeButton.onClick.AddListener(SelectNextPet);
        }

        void OnDestroy()
        {
            if (changeButton != null) changeButton.onClick.RemoveListener(SelectNextPet);
        }

        public void Configure(Button button, TMP_Text label)
        {
            changeButton = button;
            buttonLabel = label;
        }

        public void Initialize(IReadOnlyList<LTCPetDefinition> availablePets, string selectedPetId)
        {
            pets.Clear();
            selectedIndex = 0;

            for (int index = 0; index < availablePets.Count; index++)
            {
                LTCPetDefinition pet = availablePets[index];
                pets.Add(pet);
                if (pet.id == selectedPetId) selectedIndex = index;
            }
            UpdateLabel();
        }

        void SelectNextPet()
        {
            if (pets.Count == 0) return;
            selectedIndex = (selectedIndex + 1) % pets.Count;
            UpdateLabel();
            SelectionChanged?.Invoke(pets[selectedIndex]);
        }

        void UpdateLabel()
        {
            if (buttonLabel == null) return;
            buttonLabel.text = pets.Count == 0
                ? "沒有可用寵物"
                : "更換動物：" + pets[selectedIndex].displayName;
        }
    }
}
