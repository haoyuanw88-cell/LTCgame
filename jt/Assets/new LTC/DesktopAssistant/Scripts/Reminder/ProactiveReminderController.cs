using System;
using System.Collections.Generic;
using System.Text;
using LTC.DesktopAssistant.AI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LTC.DesktopAssistant.Reminders
{
    /// <summary>
    /// 將掛機狀態與可信的視覺事件轉換成建議提醒。此元件只顯示文字，不操作其他遊戲。
    /// </summary>
    public sealed class ProactiveReminderController : MonoBehaviour
    {
        public event Action<string> ReminderRaised;

        [SerializeField] ProactiveReminderSettings settings;
        [SerializeField] VisionAnalysisController visionController;
        [SerializeField] WindowsUserIdleMonitor idleMonitor;
        [SerializeField] GameObject reminderPanel;
        [SerializeField] TMP_Text reminderTitleText;
        [SerializeField] TMP_Text reminderText;
        [SerializeField] TMP_Text policyStatusText;
        [SerializeField] TMP_Text historyText;
        [SerializeField] Button dismissButton;
        [SerializeField] Button simulateTrustedEventButton;
        [SerializeField] Button simulateIdleButton;

        readonly ReminderCooldownRegistry cooldowns = new ReminderCooldownRegistry();
        readonly List<string> history = new List<string>();
        int suppressedCount;
        [SerializeField] bool showLargePopup = true;

        public void Configure(ProactiveReminderSettings value, VisionAnalysisController vision,
            WindowsUserIdleMonitor idle, GameObject panel, TMP_Text reminderTitle, TMP_Text reminder,
            TMP_Text policyStatus,
            TMP_Text historyLabel, Button dismiss, Button simulateTrusted, Button simulateIdle)
        {
            settings = value;
            visionController = vision;
            idleMonitor = idle;
            reminderPanel = panel;
            reminderTitleText = reminderTitle;
            reminderText = reminder;
            policyStatusText = policyStatus;
            historyText = historyLabel;
            dismissButton = dismiss;
            simulateTrustedEventButton = simulateTrusted;
            simulateIdleButton = simulateIdle;
        }

        void Awake()
        {
            dismissButton.onClick.AddListener(Dismiss);
            simulateTrustedEventButton.onClick.AddListener(SimulateTrustedEvent);
            simulateIdleButton.onClick.AddListener(idleMonitor.SimulateIdleForDemo);
        }

        void Start()
        {
            visionController.AnalysisCompleted += HandleAnalysisCompleted;
            idleMonitor.IdleStateChanged += HandleIdleStateChanged;
            AssistantPreferences.SelectedPetChanged += HandleSelectedPetChanged;
            idleMonitor.Configure(settings.idleThresholdSeconds, settings.idleCheckIntervalSeconds);
            RefreshPetName();
            reminderPanel.SetActive(false);
            policyStatusText.text = BuildPolicyStatus("等待事件");
            RefreshHistory();
        }

        void OnDestroy()
        {
            if (visionController != null) visionController.AnalysisCompleted -= HandleAnalysisCompleted;
            if (idleMonitor != null) idleMonitor.IdleStateChanged -= HandleIdleStateChanged;
            AssistantPreferences.SelectedPetChanged -= HandleSelectedPetChanged;
            if (dismissButton != null) dismissButton.onClick.RemoveListener(Dismiss);
            if (simulateTrustedEventButton != null)
                simulateTrustedEventButton.onClick.RemoveListener(SimulateTrustedEvent);
            if (simulateIdleButton != null)
                simulateIdleButton.onClick.RemoveListener(idleMonitor.SimulateIdleForDemo);
        }

        public void EvaluateAnalysis(VisionAnalysisResult result)
        {
            if (result == null) return;
            if (result.uncertain || result.overallConfidence < settings.minimumOverallConfidence)
            {
                Suppress("視覺結果不確定或整體可信度不足");
                return;
            }

            DetectedGameEvent bestEvent = null;
            foreach (DetectedGameEvent candidate in result.events)
                if (candidate != null && candidate.confidence >= settings.minimumEventConfidence &&
                    (bestEvent == null || candidate.confidence > bestEvent.confidence))
                    bestEvent = candidate;

            if (bestEvent == null)
            {
                Suppress("沒有達到門檻的遊戲事件");
                return;
            }

            string key = "vision:" + NormalizeKey(bestEvent.type + ":" + bestEvent.description);
            string advice = result.suggestedAdvice.Length > 0 ? "\n建議：" + result.suggestedAdvice[0] : string.Empty;
            string message = "遊戲提醒（可信度 " + Mathf.RoundToInt(bestEvent.confidence * 100f) + "%）\n" +
                             bestEvent.description + advice;
            TryShow(key, message);
        }

        public void EvaluateIdle(bool isIdle, float idleSeconds)
        {
            if (!isIdle) return;
            string minutes = Mathf.Max(1, Mathf.FloorToInt(idleSeconds / 60f)).ToString();
            TryShow("idle", "休息提醒\n已經約 " + minutes + " 分鐘沒有操作，要不要先確認遊戲狀態或休息一下？");
        }

        /// <summary>桌面寵物模式可關閉大型彈窗，改由 ReminderRaised 事件顯示角色氣泡。</summary>
        public void SetLargePopupEnabled(bool enabled)
        {
            showLargePopup = enabled;
            if (!enabled && reminderPanel != null) reminderPanel.SetActive(false);
        }

        void HandleAnalysisCompleted(VisionAnalysisResult result) => EvaluateAnalysis(result);
        void HandleIdleStateChanged(bool isIdle, float idleSeconds) => EvaluateIdle(isIdle, idleSeconds);
        void HandleSelectedPetChanged(string petId) => RefreshPetName();

        void RefreshPetName()
        {
            if (reminderTitleText != null)
                reminderTitleText.text = AssistantPreferences.GetSelectedPetDisplayName() + "主動提醒";
        }

        void TryShow(string key, string message)
        {
            float now = Time.unscaledTime;
            if (!cooldowns.CanShow(key, now, settings.globalCooldownSeconds,
                    settings.sameReminderCooldownSeconds, out float remaining))
            {
                Suppress("冷卻中，約 " + Mathf.CeilToInt(remaining) + " 秒後才能再次顯示");
                return;
            }

            cooldowns.MarkShown(key, now);
            reminderText.text = message;
            if (showLargePopup) reminderPanel.SetActive(true);
            AddHistory("已顯示｜" + message.Replace('\n', ' '));
            policyStatusText.text = BuildPolicyStatus("已顯示提醒");
            ReminderRaised?.Invoke(message);
        }

        void Suppress(string reason)
        {
            suppressedCount++;
            AddHistory("已略過｜" + reason);
            policyStatusText.text = BuildPolicyStatus(reason);
        }

        void Dismiss()
        {
            reminderPanel.SetActive(false);
            policyStatusText.text = BuildPolicyStatus("提醒已關閉");
        }

        void SimulateTrustedEvent()
        {
            VisionAnalysisResult result = new VisionAnalysisResult
            {
                summary = "測試用可信事件",
                overallConfidence = .9f,
                uncertain = false,
                events = new[]
                {
                    new DetectedGameEvent
                    {
                        type = "demo_attention",
                        description = "測試事件：畫面上出現需要注意的提示",
                        confidence = .88f,
                        evidence = "第四階段本機驗收按鍵"
                    }
                },
                suggestedAdvice = new[] { "請由玩家自行確認畫面內容。" }
            };
            EvaluateAnalysis(result);
        }

        string BuildPolicyStatus(string state)
        {
            return "狀態：" + state + "\n已抑制 " + suppressedCount + " 次｜掛機門檻 " +
                   Mathf.RoundToInt(settings.idleThresholdSeconds) + " 秒｜相同提醒冷卻 " +
                   Mathf.RoundToInt(settings.sameReminderCooldownSeconds) + " 秒";
        }

        void AddHistory(string entry)
        {
            history.Insert(0, DateTime.Now.ToString("HH:mm:ss") + "　" + entry);
            if (history.Count > settings.maximumHistoryEntries)
                history.RemoveRange(settings.maximumHistoryEntries, history.Count - settings.maximumHistoryEntries);
            RefreshHistory();
        }

        void RefreshHistory()
        {
            StringBuilder builder = new StringBuilder();
            foreach (string entry in history) builder.AppendLine(entry);
            historyText.text = builder.Length == 0 ? "尚無提醒紀錄。" : builder.ToString();
        }

        static string NormalizeKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "unknown";
            return value.Trim().ToLowerInvariant().Replace(" ", string.Empty).Replace("\n", string.Empty);
        }
    }
}
