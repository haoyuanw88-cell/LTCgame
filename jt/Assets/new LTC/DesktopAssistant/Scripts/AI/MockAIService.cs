using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace LTC.DesktopAssistant
{
    /// <summary>
    /// 第一階段使用的本機假 AI。它以非同步方式回覆，方便先驗證 UI 與狀態機，
    /// 且不需要任何 API Key 或網路連線。
    /// </summary>
    public sealed class MockAIService : MonoBehaviour, IAIService
    {
        [SerializeField, Range(0.1f, 5f)] float simulatedDelaySeconds = 0.8f;

        public async Task<AssistantReply> SendMessageAsync(
            string message,
            CancellationToken cancellationToken)
        {
            int delayMilliseconds = Mathf.RoundToInt(simulatedDelaySeconds * 1000f);
            await Task.Delay(delayMilliseconds, cancellationToken);

            string trimmed = string.IsNullOrWhiteSpace(message) ? "你的問題" : message.Trim();
            string reply = "我收到「" + trimmed + "」了！目前是第一階段測試模式，" +
                           "之後接上安全後端，我就能參考最近的遊戲畫面提供建議。";
            return new AssistantReply(reply, 1f, false);
        }
    }
}
