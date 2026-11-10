using System.Collections.Generic;

namespace LTC.DesktopAssistant.Reminders
{
    /// <summary>記錄全域與個別提醒的顯示時間，避免短時間重複打擾。</summary>
    public sealed class ReminderCooldownRegistry
    {
        readonly Dictionary<string, float> lastShownByKey = new Dictionary<string, float>();
        float lastGlobalShownAt = float.NegativeInfinity;

        public bool CanShow(string key, float now, float globalCooldown, float sameReminderCooldown,
            out float remainingSeconds)
        {
            remainingSeconds = 0f;
            float globalRemaining = globalCooldown - (now - lastGlobalShownAt);
            if (globalRemaining > 0f) remainingSeconds = globalRemaining;

            if (lastShownByKey.TryGetValue(key, out float lastShown))
            {
                float sameRemaining = sameReminderCooldown - (now - lastShown);
                if (sameRemaining > remainingSeconds) remainingSeconds = sameRemaining;
            }
            return remainingSeconds <= 0f;
        }

        public void MarkShown(string key, float now)
        {
            lastGlobalShownAt = now;
            lastShownByKey[key] = now;
        }

        public void Clear()
        {
            lastShownByKey.Clear();
            lastGlobalShownAt = float.NegativeInfinity;
        }
    }
}
