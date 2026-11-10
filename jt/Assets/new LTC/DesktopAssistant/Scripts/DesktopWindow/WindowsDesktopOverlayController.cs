using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

namespace LTC.DesktopAssistant.Windows
{
    /// <summary>
    /// 將 Windows Player 設成無邊框、透明色鍵與最上層視窗。
    /// 透明區域可自動穿透點擊；游標移到助理或設定介面時恢復互動。
    /// Editor 只模擬設定，不會修改 Unity Editor 視窗。
    /// </summary>
    public sealed class WindowsDesktopOverlayController : MonoBehaviour
    {
        [SerializeField] DesktopOverlaySettings settings;
        [SerializeField] Camera overlayCamera;
        [SerializeField] RectTransform[] interactiveRegions = Array.Empty<RectTransform>();

        public bool AlwaysOnTop { get; private set; }
        public bool ClickThroughOutside { get; private set; }
        public bool NativeOverlayApplied { get; private set; }

        IntPtr windowHandle;
        IntPtr originalStyle;
        IntPtr originalExtendedStyle;
        bool clickThroughActive;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        const int GwlStyle = -16;
        const int GwlExStyle = -20;
        const long WsPopup = unchecked((long)0x80000000);
        const long WsVisible = 0x10000000;
        const long WsExLayered = 0x00080000;
        const long WsExTransparent = 0x00000020;
        const uint LwaColorKey = 0x00000001;
        const uint SwpFrameChanged = 0x0020;
        const uint SwpShowWindow = 0x0040;
        const uint SwpNoMove = 0x0002;
        const uint SwpNoSize = 0x0001;
        const int SmCxScreen = 0;
        const int SmCyScreen = 1;
        static readonly IntPtr HwndTopmost = new IntPtr(-1);
        static readonly IntPtr HwndNotTopmost = new IntPtr(-2);

        [StructLayout(LayoutKind.Sequential)]
        struct Point { public int x; public int y; }

        [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        static extern IntPtr GetWindowLongPtr64(IntPtr handle, int index);
        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        static extern IntPtr GetWindowLongPtr32(IntPtr handle, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        static extern IntPtr SetWindowLongPtr64(IntPtr handle, int index, IntPtr value);
        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        static extern IntPtr SetWindowLongPtr32(IntPtr handle, int index, IntPtr value);
        [DllImport("user32.dll")] static extern bool SetLayeredWindowAttributes(
            IntPtr handle, uint colorKey, byte alpha, uint flags);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr handle, IntPtr insertAfter,
            int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] static extern bool GetCursorPos(out Point point);

        static IntPtr GetWindowLongPtr(IntPtr handle, int index) => IntPtr.Size == 8
            ? GetWindowLongPtr64(handle, index) : GetWindowLongPtr32(handle, index);
        static IntPtr SetWindowLongPtr(IntPtr handle, int index, IntPtr value) => IntPtr.Size == 8
            ? SetWindowLongPtr64(handle, index, value) : SetWindowLongPtr32(handle, index, value);
#endif

        public void Configure(DesktopOverlaySettings value, Camera camera, params RectTransform[] regions)
        {
            settings = value;
            overlayCamera = camera;
            interactiveRegions = regions ?? Array.Empty<RectTransform>();
        }

        IEnumerator Start()
        {
            Application.runInBackground = true;
            AlwaysOnTop = DesktopOverlayPreferences.GetBool("TopMost", settings.alwaysOnTop);
            ClickThroughOutside = DesktopOverlayPreferences.GetBool("ClickThrough",
                settings.clickThroughOutsideInteractiveAreas);
            Application.targetFrameRate = Mathf.Clamp(
                Mathf.RoundToInt(DesktopOverlayPreferences.GetFloat("TargetFps", settings.targetFrameRate)), 15, 60);
            ConfigureCamera();
            yield return new WaitForEndOfFrame();
            ApplyNativeOverlay();
        }

        void Update()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!NativeOverlayApplied || !ClickThroughOutside) return;
            SetClickThrough(!IsCursorOverInteractiveRegion());
#endif
        }

        void OnApplicationFocus(bool focused)
        {
            if (focused && NativeOverlayApplied) ApplyTopMost();
        }

        void OnDestroy()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (windowHandle != IntPtr.Zero)
            {
                SetWindowLongPtr(windowHandle, GwlStyle, originalStyle);
                SetWindowLongPtr(windowHandle, GwlExStyle, originalExtendedStyle);
            }
#endif
        }

        public void SetAlwaysOnTop(bool enabled)
        {
            AlwaysOnTop = enabled;
            DesktopOverlayPreferences.SetBool("TopMost", enabled);
            ApplyTopMost();
        }

        public void SetClickThroughOutside(bool enabled)
        {
            ClickThroughOutside = enabled;
            DesktopOverlayPreferences.SetBool("ClickThrough", enabled);
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (!enabled) SetClickThrough(false);
#endif
        }

        public void SetInteractiveRegions(params RectTransform[] regions)
        {
            interactiveRegions = regions ?? Array.Empty<RectTransform>();
        }

        void ConfigureCamera()
        {
            if (overlayCamera == null) return;
            overlayCamera.clearFlags = CameraClearFlags.SolidColor;
            overlayCamera.backgroundColor = Color.clear;
        }

        void ApplyNativeOverlay()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            windowHandle = GetActiveWindow();
            if (windowHandle == IntPtr.Zero) return;
            originalStyle = GetWindowLongPtr(windowHandle, GwlStyle);
            originalExtendedStyle = GetWindowLongPtr(windowHandle, GwlExStyle);
            SetWindowLongPtr(windowHandle, GwlStyle, new IntPtr(WsPopup | WsVisible));
            SetWindowLongPtr(windowHandle, GwlExStyle,
                new IntPtr(originalExtendedStyle.ToInt64() | WsExLayered));
            if (settings.transparentBackground)
                SetLayeredWindowAttributes(windowHandle, 0u, 255, LwaColorKey);
            SetWindowPos(windowHandle, AlwaysOnTop ? HwndTopmost : HwndNotTopmost,
                0, 0, GetSystemMetrics(SmCxScreen), GetSystemMetrics(SmCyScreen),
                SwpFrameChanged | SwpShowWindow);
            NativeOverlayApplied = true;
#endif
        }

        void ApplyTopMost()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (windowHandle == IntPtr.Zero) return;
            SetWindowPos(windowHandle, AlwaysOnTop ? HwndTopmost : HwndNotTopmost,
                0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpShowWindow);
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        bool IsCursorOverInteractiveRegion()
        {
            if (!GetCursorPos(out Point cursor)) return false;
            Vector2 screenPoint = new Vector2(cursor.x, Screen.height - cursor.y);
            foreach (RectTransform region in interactiveRegions)
                if (region != null && region.gameObject.activeInHierarchy &&
                    RectTransformUtility.RectangleContainsScreenPoint(region, screenPoint, null))
                    return true;
            return false;
        }

        void SetClickThrough(bool enabled)
        {
            if (windowHandle == IntPtr.Zero || clickThroughActive == enabled) return;
            long style = GetWindowLongPtr(windowHandle, GwlExStyle).ToInt64();
            style = enabled ? style | WsExTransparent : style & ~WsExTransparent;
            SetWindowLongPtr(windowHandle, GwlExStyle, new IntPtr(style));
            clickThroughActive = enabled;
        }
#endif
    }
}
