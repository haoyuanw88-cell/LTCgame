using System.Threading;
using System.Threading.Tasks;

namespace LTC.DesktopAssistant
{
    /// <summary>
    /// AI 回答的標準格式。Confidence 會讓 UI 與提醒系統知道結果是否可靠。
    /// </summary>
    public sealed class AssistantReply
    {
        public string Text { get; }
        public float Confidence { get; }
        public bool IsUncertain { get; }

        public AssistantReply(string text, float confidence, bool isUncertain)
        {
            Text = text ?? string.Empty;
            Confidence = confidence;
            IsUncertain = isUncertain;
        }
    }

    /// <summary>
    /// 所有 AI 供應商都必須實作的介面。Unity UI 不直接依賴 OpenAI 或特定模型。
    /// </summary>
    public interface IAIService
    {
        Task<AssistantReply> SendMessageAsync(string message, CancellationToken cancellationToken);
    }
}
