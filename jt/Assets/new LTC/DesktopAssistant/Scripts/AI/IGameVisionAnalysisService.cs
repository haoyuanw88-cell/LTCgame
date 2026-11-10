using System.Threading;
using System.Threading.Tasks;

namespace LTC.DesktopAssistant.AI
{
    /// <summary>可替換的遊戲畫面分析服務。</summary>
    public interface IGameVisionAnalysisService
    {
        Task<VisionAnalysisResult> AnalyzeAsync(
            byte[] jpegImage,
            string sourceName,
            string capturedAtUtc,
            CancellationToken cancellationToken);
    }
}
