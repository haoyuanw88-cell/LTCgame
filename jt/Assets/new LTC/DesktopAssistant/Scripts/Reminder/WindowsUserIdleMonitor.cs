using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace LTC.DesktopAssistant.Reminders
{
    /// <summary>
    /// 讀取 Windows 全域最後輸入時間。即使遊戲視窗沒有焦點，也能判斷玩家是否長時間未操作。
    /// 非 Windows 平台會退回 Unity 內部輸入偵測。
    /// </summary>
    public sealed class WindowsUserIdleMonitor : MonoBehaviour
    {
        public event Action<bool, float> IdleStateChanged;

        public float IdleSeconds { get; private set; }
        public bool IsIdle { get; private set; }

        float thresholdSeconds = 300f;
        float checkIntervalSeconds = 1f;
        float nextCheckAt;
        float fallbackLastInputAt;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [StructLayout(LayoutKind.Sequential)]
        struct LastInputInfo
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        static extern bool GetLastInputInfo(ref LastInputInfo info);

#endif

        public void Configure(float threshold, float interval)
        {
            thresholdSeconds = Mathf.Max(1f, threshold);
            checkIntervalSeconds = Mathf.Clamp(interval, .1f, 5f);
        }

        void Awake()
        {
            fallbackLastInputAt = Time.unscaledTime;
        }

        void Update()
        {
#if !(UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN)
            TrackFallbackInput();
#endif
            if (Time.unscaledTime < nextCheckAt) return;
            nextCheckAt = Time.unscaledTime + checkIntervalSeconds;
            IdleSeconds = ReadIdleSeconds();
            bool newState = IdleSeconds >= thresholdSeconds;
            if (newState == IsIdle) return;
            IsIdle = newState;
            IdleStateChanged?.Invoke(IsIdle, IdleSeconds);
        }

        void TrackFallbackInput()
        {
            if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) ||
                Mathf.Abs(Input.mouseScrollDelta.y) > .01f)
                fallbackLastInputAt = Time.unscaledTime;
        }

        float ReadIdleSeconds()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            LastInputInfo info = new LastInputInfo { cbSize = (uint)Marshal.SizeOf<LastInputInfo>() };
            if (GetLastInputInfo(ref info))
            {
                // LASTINPUTINFO 使用 32 位 tick；unchecked 減法可正確處理約 49.7 天一次的回繞。
                uint elapsedMilliseconds = unchecked((uint)Environment.TickCount - info.dwTime);
                return elapsedMilliseconds / 1000f;
            }
#endif
            return Mathf.Max(0f, Time.unscaledTime - fallbackLastInputAt);
        }

        /// <summary>供測試介面立即觸發掛機狀態，不會修改 Windows 系統狀態。</summary>
        public void SimulateIdleForDemo()
        {
            IdleSeconds = thresholdSeconds;
            IsIdle = true;
            IdleStateChanged?.Invoke(true, IdleSeconds);
        }
    }
}
