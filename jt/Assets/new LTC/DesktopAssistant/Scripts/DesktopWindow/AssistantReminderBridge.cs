using LTC.DesktopAssistant.Reminders;
using UnityEngine;

namespace LTC.DesktopAssistant.Windows
{
    /// <summary>把第四階段提醒轉交給寵物氣泡與提醒動畫。</summary>
    public sealed class AssistantReminderBridge : MonoBehaviour
    {
        [SerializeField] ProactiveReminderController reminderController;
        [SerializeField] DesktopAssistantController assistantController;

        public void Configure(ProactiveReminderController reminder, DesktopAssistantController assistant)
        {
            reminderController = reminder;
            assistantController = assistant;
        }

        void Start()
        {
            reminderController.ReminderRaised += HandleReminder;
            reminderController.SetLargePopupEnabled(false);
        }

        void OnDestroy()
        {
            if (reminderController != null) reminderController.ReminderRaised -= HandleReminder;
        }

        void HandleReminder(string message)
        {
            if (assistantController != null) assistantController.ShowReminderMessage(message);
        }
    }
}
