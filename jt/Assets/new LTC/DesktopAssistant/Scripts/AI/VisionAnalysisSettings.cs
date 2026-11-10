using UnityEngine;

namespace LTC.DesktopAssistant.AI
{
    /// <summary>第三階段視覺分析設定，不包含任何 API Key。</summary>
    [CreateAssetMenu(fileName = "VisionAnalysisSettings", menuName = "LTC/桌面 AI 助理/視覺分析設定")]
    public sealed class VisionAnalysisSettings : ScriptableObject
    {
        [Header("Service")]
        [Tooltip("開啟時使用本機 Mock JSON，方便沒有後端時測試完整流程。")]
        public bool useMockService = true;

        [Tooltip("安全後端代理路徑。OpenAI API Key 只能放在後端。")]
        public string backendPath = "/api/v1/assistant/analyze";

        [Range(5, 120)] public int requestTimeoutSeconds = 45;

        [Header("Image")]
        [Range(512, 2048)] public int maximumImageDimension = 1280;
        [Range(40, 95)] public int jpegQuality = 72;

        [Header("Policy")]
        [Range(0.1f, 1f)] public float minimumConfidence = 0.65f;
        [Range(5f, 120f)] public float minimumAnalysisIntervalSeconds = 15f;
    }
}
