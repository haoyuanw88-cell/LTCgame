using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace LTC.DesktopAssistant.AI
{
    /// <summary>
    /// 以 JSON 字串模擬後端回應，驗證解析、可信度與 UI，不會上傳圖片。
    /// </summary>
    public sealed class MockVisionAnalysisService : IGameVisionAnalysisService
    {
        const string MockJson = "{\"schemaVersion\":\"1.0\",\"summary\":\"畫面看起來是遊戲進行中的介面\",\"overallConfidence\":0.78,\"uncertain\":true,\"uncertaintyReason\":\"目前為本機 Mock，尚未由視覺模型分析\",\"observations\":[{\"label\":\"正在進行關卡\",\"evidence\":\"畫面包含遊戲 UI 區域\",\"confidence\":0.72,\"uncertain\":true}],\"ocrTexts\":[{\"text\":\"範例任務文字\",\"confidence\":0.58,\"region\":\"畫面上方\"}],\"events\":[{\"type\":\"possible_gameplay\",\"description\":\"可能正在遊戲中\",\"confidence\":0.66,\"evidence\":\"Mock 測試事件\"}],\"suggestedAdvice\":[\"這是測試建議，不代表真實畫面判定。\"]}";

        public async Task<VisionAnalysisResult> AnalyzeAsync(byte[] jpegImage, string sourceName,
            string capturedAtUtc, CancellationToken cancellationToken)
        {
            await Task.Delay(650, cancellationToken);
            if (jpegImage == null || jpegImage.Length == 0)
                throw new System.ArgumentException("沒有可分析的 JPEG 圖片。");
            VisionAnalysisResult result = JsonUtility.FromJson<VisionAnalysisResult>(MockJson);
            if (result == null) throw new System.InvalidOperationException("Mock JSON 解析失敗。");
            return result;
        }
    }
}
