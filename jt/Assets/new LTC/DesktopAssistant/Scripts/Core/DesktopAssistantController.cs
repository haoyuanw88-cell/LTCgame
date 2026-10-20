using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LTC.DesktopAssistant
{
    /// <summary>
    /// 第一階段的核心協調器：初始化角色、處理聊天、切換動畫狀態。
    /// 它只提供資訊與建議，不會操作其他遊戲。
    /// </summary>
    public sealed class DesktopAssistantController : MonoBehaviour
    {
        [SerializeField] AssistantSettings settings;
        [SerializeField] PetCatalogAdapter petCatalog;
        [SerializeField] AssistantCharacterView characterView;
        [SerializeField] AssistantDragHandler dragHandler;
        [SerializeField] AssistantStateMenu stateMenu;

        IReadOnlyList<LTCPetDefinition> availablePets;
        Coroutine returnToIdleRoutine;

        public AssistantState CurrentState => characterView == null
            ? AssistantState.Idle
            : characterView.CurrentState;

        void Start()
        {
            if (settings == null || petCatalog == null || characterView == null ||
                stateMenu == null)
            {
                Debug.LogError("桌面 AI 助理的 Inspector 參考尚未完整設定。", this);
                enabled = false;
                return;
            }

            characterView.SetAnimationSpeed(settings.animationFramesPerSecond);
            dragHandler.SetMargin(settings.screenMargin);
            availablePets = petCatalog.GetAvailablePets(settings.fallbackPetId);
            string savedPet = AssistantPreferences.SelectedPetId;
            LTCPetDefinition initialPet = availablePets.FirstOrDefault(pet => pet.id == savedPet)
                ?? availablePets.FirstOrDefault();
            SelectPet(initialPet);

            SetState(AssistantState.Idle);
        }

        void OnDestroy()
        {
        }

        public void Configure(AssistantSettings assistantSettings, PetCatalogAdapter catalog,
            AssistantCharacterView character, AssistantDragHandler drag,
            AssistantStateMenu menu)
        {
            settings = assistantSettings;
            petCatalog = catalog;
            characterView = character;
            dragHandler = drag;
            stateMenu = menu;
        }

        public void SetIdleState()
        {
            SetState(AssistantState.Idle);
            stateMenu.Close();
        }

        public void SetTalkingState()
        {
            SetState(AssistantState.Talking);
            characterView.ShowBubble("你好，我是小樂！", settings.bubbleDuration);
            stateMenu.Close();
            ScheduleReturnToIdle();
        }

        public void SetReminderState()
        {
            SetState(AssistantState.Reminder);
            characterView.ShowBubble("提醒測試：我只會提供建議，不會自動操作遊戲。", settings.bubbleDuration);
            stateMenu.Close();
            ScheduleReturnToIdle();
        }

        void SelectPet(LTCPetDefinition pet)
        {
            if (pet == null) return;
            characterView.SetPet(pet, petCatalog.GetIdleSprite(pet), petCatalog.GetAnimationFrames(pet));
            AssistantPreferences.SelectedPetId = pet.id;
        }

        void SetState(AssistantState state)
        {
            if (returnToIdleRoutine != null)
            {
                StopCoroutine(returnToIdleRoutine);
                returnToIdleRoutine = null;
            }
            characterView.SetState(state);
        }

        void ScheduleReturnToIdle()
        {
            if (returnToIdleRoutine != null) StopCoroutine(returnToIdleRoutine);
            returnToIdleRoutine = StartCoroutine(ReturnToIdleAfterDelay());
        }

        IEnumerator ReturnToIdleAfterDelay()
        {
            yield return new WaitForSecondsRealtime(settings.talkingDuration);
            returnToIdleRoutine = null;
            characterView.SetState(AssistantState.Idle);
        }
    }
}
