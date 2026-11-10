using LTC.DesktopAssistant.AI;
using LTC.DesktopAssistant.Capture;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LTC.DesktopAssistant.Windows
{
    /// <summary>第六階段設定介面：集中控制視窗、分析、效能與角色大小。</summary>
    public sealed class DesktopAssistantSettingsPanel : MonoBehaviour
    {
        [SerializeField] DesktopOverlaySettings defaults;
        [SerializeField] WindowsDesktopOverlayController overlay;
        [SerializeField] DesktopCaptureController captureController;
        [SerializeField] VisionAnalysisController visionController;
        [SerializeField] RectTransform assistantAnchor;
        [SerializeField] GameObject panel;
        [SerializeField] Button openButton;
        [SerializeField] Button closeButton;
        [SerializeField] Button resetPositionButton;
        [SerializeField] Button quitButton;
        [SerializeField] Toggle topMostToggle;
        [SerializeField] Toggle clickThroughToggle;
        [SerializeField] Toggle automaticAnalysisToggle;
        [SerializeField] Toggle mockServiceToggle;
        [SerializeField] Slider frameRateSlider;
        [SerializeField] TMP_Text frameRateLabel;
        [SerializeField] Slider scaleSlider;
        [SerializeField] TMP_Text scaleLabel;

        public void Configure(DesktopOverlaySettings settings, WindowsDesktopOverlayController windowOverlay,
            DesktopCaptureController capture, VisionAnalysisController vision, RectTransform assistant,
            GameObject settingsPanel, Button open, Button close, Button reset, Button quit,
            Toggle topMost, Toggle clickThrough, Toggle automaticAnalysis, Toggle mockService,
            Slider frameRate, TMP_Text frameRateValue, Slider scale, TMP_Text scaleValue)
        {
            defaults = settings;
            overlay = windowOverlay;
            captureController = capture;
            visionController = vision;
            assistantAnchor = assistant;
            panel = settingsPanel;
            openButton = open;
            closeButton = close;
            resetPositionButton = reset;
            quitButton = quit;
            topMostToggle = topMost;
            clickThroughToggle = clickThrough;
            automaticAnalysisToggle = automaticAnalysis;
            mockServiceToggle = mockService;
            frameRateSlider = frameRate;
            frameRateLabel = frameRateValue;
            scaleSlider = scale;
            scaleLabel = scaleValue;
        }

        void Start()
        {
            panel.SetActive(false);
            topMostToggle.SetIsOnWithoutNotify(
                DesktopOverlayPreferences.GetBool("TopMost", defaults.alwaysOnTop));
            clickThroughToggle.SetIsOnWithoutNotify(
                DesktopOverlayPreferences.GetBool("ClickThrough", defaults.clickThroughOutsideInteractiveAreas));
            bool automatic = DesktopOverlayPreferences.GetBool("AutoAnalysis", defaults.startAutomaticAnalysis);
            automaticAnalysisToggle.SetIsOnWithoutNotify(automatic);
            bool mock = DesktopOverlayPreferences.GetBool("MockVision", defaults.useMockVisionService);
            mockServiceToggle.SetIsOnWithoutNotify(mock);
            visionController.SetUseMockService(mock);
            float fps = DesktopOverlayPreferences.GetFloat("TargetFps", defaults.targetFrameRate);
            frameRateSlider.SetValueWithoutNotify(fps);
            SetFrameRate(fps);
            float scale = DesktopOverlayPreferences.GetFloat("AssistantScale", defaults.assistantScale);
            scaleSlider.SetValueWithoutNotify(scale);
            SetScale(scale);
            SetAutomaticAnalysis(automatic);
        }

        public void Open() => panel.SetActive(true);
        public void Close() => panel.SetActive(false);

        public void SetAutomaticAnalysis(bool enabled)
        {
            DesktopOverlayPreferences.SetBool("AutoAnalysis", enabled);
            captureController.SetAutomaticCapture(enabled);
            visionController.SetAutomaticAnalysis(enabled);
        }

        public void SetFrameRate(float value)
        {
            int fps = Mathf.Clamp(Mathf.RoundToInt(value), 15, 60);
            Application.targetFrameRate = fps;
            frameRateLabel.text = fps + " FPS";
            DesktopOverlayPreferences.SetFloat("TargetFps", fps);
        }

        public void SetScale(float value)
        {
            float scale = Mathf.Clamp(value, .6f, 1.5f);
            assistantAnchor.localScale = Vector3.one * scale;
            scaleLabel.text = Mathf.RoundToInt(scale * 100f) + "%";
            DesktopOverlayPreferences.SetFloat("AssistantScale", scale);
        }

        public void ResetPosition()
        {
            assistantAnchor.anchorMin = new Vector2(1f, 0f);
            assistantAnchor.anchorMax = new Vector2(1f, 0f);
            assistantAnchor.pivot = new Vector2(1f, 0f);
            assistantAnchor.anchoredPosition = new Vector2(-40f, 40f);
            AssistantPreferences.SavePosition(assistantAnchor.anchoredPosition);
        }

        public void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
