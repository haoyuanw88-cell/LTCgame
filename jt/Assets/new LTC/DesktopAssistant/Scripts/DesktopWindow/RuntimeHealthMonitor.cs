using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Profiling;

namespace LTC.DesktopAssistant.Windows
{
    /// <summary>顯示 FPS、記憶體與錯誤數，並保留最近一筆錯誤方便 Windows Build 排查。</summary>
    public sealed class RuntimeHealthMonitor : MonoBehaviour
    {
        [SerializeField] TMP_Text statusText;
        float accumulatedTime;
        int accumulatedFrames;
        float displayedFps;
        int errorCount;
        string lastError = "無";

        public void Configure(TMP_Text label) => statusText = label;

        void OnEnable() => Application.logMessageReceived += HandleLog;
        void OnDisable() => Application.logMessageReceived -= HandleLog;

        void Update()
        {
            accumulatedTime += Time.unscaledDeltaTime;
            accumulatedFrames++;
            if (accumulatedTime < .5f) return;
            displayedFps = accumulatedFrames / Mathf.Max(.001f, accumulatedTime);
            accumulatedTime = 0f;
            accumulatedFrames = 0;
            RefreshLabel();
        }

        void HandleLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            errorCount++;
            lastError = string.IsNullOrWhiteSpace(condition) ? type.ToString() : condition;
            if (lastError.Length > 120) lastError = lastError.Substring(0, 120) + "…";
        }

        void RefreshLabel()
        {
            if (statusText == null) return;
            long memoryMb = Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
            StringBuilder text = new StringBuilder();
            text.Append("版本 ").Append(Application.version)
                .Append("｜FPS ").Append(displayedFps.ToString("0"))
                .Append("｜記憶體 ").Append(memoryMb).Append(" MB")
                .Append("\n錯誤 ").Append(errorCount).Append("｜最近：").Append(lastError);
            statusText.text = text.ToString();
        }
    }
}
