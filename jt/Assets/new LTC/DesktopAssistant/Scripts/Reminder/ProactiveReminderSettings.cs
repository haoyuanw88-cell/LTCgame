using UnityEngine;

namespace LTC.DesktopAssistant.Reminders
{
    /// <summary>第四階段主動提醒的門檻與冷卻設定。</summary>
    [CreateAssetMenu(fileName = "ProactiveReminderSettings", menuName = "LTC/桌面 AI 助理/主動提醒設定")]
    public sealed class ProactiveReminderSettings : ScriptableObject
    {
        [Header("掛機判斷")]
        [Tooltip("多久沒有任何 Windows 鍵盤或滑鼠輸入後視為掛機。")]
        [Min(10f)] public float idleThresholdSeconds = 300f;
        [Range(.1f, 5f)] public float idleCheckIntervalSeconds = 1f;

        [Header("提醒冷卻")]
        [Min(1f)] public float globalCooldownSeconds = 30f;
        [Min(1f)] public float sameReminderCooldownSeconds = 180f;

        [Header("可信度政策")]
        [Range(.1f, 1f)] public float minimumOverallConfidence = .7f;
        [Range(.1f, 1f)] public float minimumEventConfidence = .72f;
        [Range(2, 20)] public int maximumHistoryEntries = 8;
    }
}
