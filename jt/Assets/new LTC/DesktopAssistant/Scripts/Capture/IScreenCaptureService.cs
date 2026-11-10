using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LTC.DesktopAssistant.Capture
{
    /// <summary>
    /// 螢幕擷取服務介面。UI 與後續 AI 分析不依賴特定 Windows API。
    /// </summary>
    public interface IScreenCaptureService
    {
        bool IsSupported { get; }
        IReadOnlyList<CaptureTarget> GetAvailableTargets();
        Task<ScreenCaptureFrame> CaptureAsync(CaptureTarget target, CancellationToken cancellationToken);
    }
}
