using UnityEngine;

namespace LTC.DesktopAssistant.Windows
{
    /// <summary>第五、六階段 Windows 桌面視窗與效能預設值。</summary>
    [CreateAssetMenu(fileName = "DesktopOverlaySettings", menuName = "LTC/桌面 AI 助理/Windows 覆蓋設定")]
    public sealed class DesktopOverlaySettings : ScriptableObject
    {
        [Header("Windows 視窗")]
        public bool transparentBackground = true;
        public bool alwaysOnTop = true;
        [Tooltip("游標不在助理或設定面板上時，點擊會穿透到下方程式。")]
        public bool clickThroughOutsideInteractiveAreas = true;

        [Header("效能")]
        [Range(15, 60)] public int targetFrameRate = 30;
        [Range(.6f, 1.5f)] public float assistantScale = 1f;
        public bool startAutomaticAnalysis;
        public bool useMockVisionService = true;
    }
}
