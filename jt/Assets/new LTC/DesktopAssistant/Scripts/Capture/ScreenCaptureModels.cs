using System;

namespace LTC.DesktopAssistant.Capture
{
    /// <summary>可擷取的來源類型。</summary>
    public enum CaptureTargetKind
    {
        EntireDesktop,
        Window
    }

    /// <summary>使用者可選擇的桌面或視窗來源。</summary>
    public sealed class CaptureTarget
    {
        public CaptureTargetKind Kind { get; }
        public long NativeHandle { get; }
        public string DisplayName { get; }

        public CaptureTarget(CaptureTargetKind kind, long nativeHandle, string displayName)
        {
            Kind = kind;
            NativeHandle = nativeHandle;
            DisplayName = displayName ?? string.Empty;
        }
    }

    /// <summary>
    /// 不含 Unity 物件的擷取結果，可安全地由背景執行緒建立。
    /// Pixels 使用 RGBA32，由 Unity 主執行緒建立 Texture2D。
    /// </summary>
    public sealed class ScreenCaptureFrame
    {
        public int Width { get; }
        public int Height { get; }
        public byte[] Pixels { get; }
        public string SourceName { get; }
        public DateTime CapturedAtUtc { get; }

        public ScreenCaptureFrame(int width, int height, byte[] pixels, string sourceName)
        {
            Width = width;
            Height = height;
            Pixels = pixels;
            SourceName = sourceName ?? string.Empty;
            CapturedAtUtc = DateTime.UtcNow;
        }
    }
}
