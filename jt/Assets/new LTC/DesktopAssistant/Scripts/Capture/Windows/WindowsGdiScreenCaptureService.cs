using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LTC.DesktopAssistant.Capture.Windows
{
    /// <summary>
    /// Windows GDI 擷取實作。PrintWindow 在背景執行緒執行，失敗時改用 BitBlt。
    /// 某些硬體加速、受保護或反作弊遊戲可能回傳黑畫面，這時需在第五階段改用
    /// Windows Graphics Capture；本服務不會繞過任何保護機制。
    /// </summary>
    public sealed class WindowsGdiScreenCaptureService : IScreenCaptureService
    {
        const int SmXVirtualScreen = 76;
        const int SmYVirtualScreen = 77;
        const int SmCxVirtualScreen = 78;
        const int SmCyVirtualScreen = 79;
        const uint Srccopy = 0x00CC0020;
        const uint CaptureBlt = 0x40000000;
        const uint DibRgbColors = 0;
        const uint PwRenderFullContent = 2;
        const int MaxDimension = 8192;

        public bool IsSupported
        {
            get
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                return true;
#else
                return false;
#endif
            }
        }

        public IReadOnlyList<CaptureTarget> GetAvailableTargets()
        {
            var targets = new List<CaptureTarget>
            {
                new CaptureTarget(CaptureTargetKind.EntireDesktop, 0, "整個桌面")
            };

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            IntPtr shellWindow = GetShellWindow();
            EnumWindows((window, _) =>
            {
                if (window == shellWindow || !IsWindowVisible(window) || IsIconic(window)) return true;
                int length = GetWindowTextLength(window);
                if (length <= 0) return true;
                var title = new StringBuilder(length + 1);
                GetWindowText(window, title, title.Capacity);
                string value = title.ToString().Trim();
                if (string.IsNullOrEmpty(value) || !TryGetCaptureRect(window, out Rect rect)) return true;
                if (rect.Width < 64 || rect.Height < 64) return true;
                targets.Add(new CaptureTarget(CaptureTargetKind.Window, window.ToInt64(), value));
                return true;
            }, IntPtr.Zero);
#endif
            return targets;
        }

        public Task<ScreenCaptureFrame> CaptureAsync(
            CaptureTarget target,
            CancellationToken cancellationToken)
        {
            if (!IsSupported) throw new PlatformNotSupportedException("此擷取服務只支援 Windows。");
            if (target == null) throw new ArgumentNullException(nameof(target));
            return Task.Run(() => CaptureInternal(target, cancellationToken), cancellationToken);
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        static ScreenCaptureFrame CaptureInternal(CaptureTarget target, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IntPtr window = target.Kind == CaptureTargetKind.Window
                ? new IntPtr(target.NativeHandle)
                : IntPtr.Zero;
            int left;
            int top;
            int width;
            int height;

            if (target.Kind == CaptureTargetKind.EntireDesktop)
            {
                left = GetSystemMetrics(SmXVirtualScreen);
                top = GetSystemMetrics(SmYVirtualScreen);
                width = GetSystemMetrics(SmCxVirtualScreen);
                height = GetSystemMetrics(SmCyVirtualScreen);
            }
            else
            {
                if (!IsWindow(window) || !TryGetCaptureRect(window, out Rect rect))
                    throw new InvalidOperationException("選取的視窗已關閉或無法取得尺寸。");
                left = rect.Left;
                top = rect.Top;
                width = rect.Width;
                height = rect.Height;
            }

            if (width <= 0 || height <= 0 || width > MaxDimension || height > MaxDimension)
                throw new InvalidOperationException("擷取尺寸無效或超過安全上限：" + width + "x" + height);

            IntPtr sourceDc = GetDC(IntPtr.Zero);
            if (sourceDc == IntPtr.Zero) throw new InvalidOperationException("無法取得桌面裝置內容。");
            IntPtr memoryDc = IntPtr.Zero;
            IntPtr bitmap = IntPtr.Zero;
            IntPtr previousObject = IntPtr.Zero;
            IntPtr bitmapPixels = IntPtr.Zero;

            try
            {
                memoryDc = CreateCompatibleDC(sourceDc);
                var info = new BitmapInfo
                {
                    Header = new BitmapInfoHeader
                    {
                        Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                        Width = width,
                        Height = -height,
                        Planes = 1,
                        BitCount = 32,
                        Compression = 0,
                        SizeImage = (uint)(width * height * 4)
                    }
                };
                bitmap = CreateDIBSection(sourceDc, ref info, DibRgbColors,
                    out bitmapPixels, IntPtr.Zero, 0);
                if (memoryDc == IntPtr.Zero || bitmap == IntPtr.Zero)
                    throw new InvalidOperationException("無法建立擷取緩衝區。");

                previousObject = SelectObject(memoryDc, bitmap);
                bool copied = target.Kind == CaptureTargetKind.Window &&
                              PrintWindow(window, memoryDc, PwRenderFullContent);
                if (!copied)
                    copied = BitBlt(memoryDc, 0, 0, width, height, sourceDc, left, top,
                        Srccopy | CaptureBlt);
                if (!copied) throw new InvalidOperationException("Windows 無法複製指定畫面。");

                cancellationToken.ThrowIfCancellationRequested();
                if (bitmapPixels == IntPtr.Zero)
                    throw new InvalidOperationException("無法取得擷取緩衝區記憶體。");
                byte[] pixels = new byte[width * height * 4];
                Marshal.Copy(bitmapPixels, pixels, 0, pixels.Length);

                // GetDIBits 回傳 BGRA；轉為 Unity Texture2D 使用的 RGBA32。
                for (int index = 0; index < pixels.Length; index += 4)
                {
                    byte blue = pixels[index];
                    pixels[index] = pixels[index + 2];
                    pixels[index + 2] = blue;
                    pixels[index + 3] = 255;
                }
                return new ScreenCaptureFrame(width, height, pixels, target.DisplayName);
            }
            finally
            {
                if (previousObject != IntPtr.Zero && memoryDc != IntPtr.Zero)
                    SelectObject(memoryDc, previousObject);
                if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
                if (memoryDc != IntPtr.Zero) DeleteDC(memoryDc);
                ReleaseDC(IntPtr.Zero, sourceDc);
            }
        }

        static bool TryGetCaptureRect(IntPtr window, out Rect rect)
        {
            if (!GetWindowRect(window, out rect)) return false;
            return rect.Right > rect.Left && rect.Bottom > rect.Top;
        }

        delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);

        [StructLayout(LayoutKind.Sequential)]
        struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
            public int Width => Right - Left;
            public int Height => Bottom - Top;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct BitmapInfoHeader
        {
            public uint Size;
            public int Width;
            public int Height;
            public ushort Planes;
            public ushort BitCount;
            public uint Compression;
            public uint SizeImage;
            public int XPelsPerMeter;
            public int YPelsPerMeter;
            public uint ColorsUsed;
            public uint ColorsImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct BitmapInfo
        {
            public BitmapInfoHeader Header;
            public uint Colors;
        }

        [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")] static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")] static extern IntPtr GetShellWindow();
        [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr window);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetWindowText(IntPtr window, StringBuilder text, int maximumCount);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out Rect rect);
        [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr window, IntPtr dc);
        [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr window, IntPtr targetDc, uint flags);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern IntPtr CreateDIBSection(IntPtr dc,
            ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr gdiObject);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr gdiObject);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr targetDc, int x, int y, int width,
            int height, IntPtr sourceDc, int sourceX, int sourceY, uint operation);
#else
        static ScreenCaptureFrame CaptureInternal(CaptureTarget target, CancellationToken cancellationToken)
        {
            throw new PlatformNotSupportedException("此擷取服務只支援 Windows。");
        }
#endif
    }
}
