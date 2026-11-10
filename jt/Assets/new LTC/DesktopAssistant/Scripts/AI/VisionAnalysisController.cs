using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using LTC.DesktopAssistant.Capture;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LTC.DesktopAssistant.AI
{
    /// <summary>
    /// 接收有變化的擷取影格、限制分析頻率、編碼 JPEG、呼叫可替換服務並顯示結果。
    /// AI 只產生建議，不會控制任何遊戲。
    /// </summary>
    public sealed class VisionAnalysisController : MonoBehaviour
    {
        /// <summary>完成一次有效分析時通知提醒模組；失敗或取消時不觸發。</summary>
        public event Action<VisionAnalysisResult> AnalysisCompleted;

        /// <summary>最近一次成功且已正規化的分析結果。</summary>
        public VisionAnalysisResult LatestResult { get; private set; }

        [SerializeField] VisionAnalysisSettings settings;
        [SerializeField] DesktopCaptureController captureController;
        [SerializeField] TMP_Text serviceModeLabel;
        [SerializeField] TMP_Text statusLabel;
        [SerializeField] TMP_Text summaryLabel;
        [SerializeField] TMP_Text detailsLabel;
        [SerializeField] TMP_Text rawJsonLabel;
        [SerializeField] Button analyzeButton;
        [SerializeField] Button automaticButton;
        [SerializeField] TMP_Text automaticButtonLabel;
        [SerializeField] Button serviceModeButton;

        ScreenCaptureFrame latestFrame;
        CancellationTokenSource lifetimeCancellation;
        IGameVisionAnalysisService analysisService;
        Coroutine pendingAnalysisRoutine;
        float lastAnalysisTime = float.NegativeInfinity;
        bool automaticAnalysis;
        bool analysisInProgress;

        public bool AutomaticAnalysisEnabled => automaticAnalysis;
        public bool IsUsingMockService => settings != null && settings.useMockService;

        void Awake()
        {
            lifetimeCancellation = new CancellationTokenSource();
            analyzeButton.onClick.AddListener(AnalyzeLatest);
            automaticButton.onClick.AddListener(ToggleAutomaticAnalysis);
            serviceModeButton.onClick.AddListener(ToggleServiceMode);
        }

        void Start()
        {
            captureController.FrameChanged += HandleFrameChanged;
            RebuildService();
            statusLabel.text = "請先擷取一張有變化的畫面。";
            summaryLabel.text = "尚無分析結果。";
            detailsLabel.text = string.Empty;
            rawJsonLabel.text = string.Empty;
        }

        void OnDestroy()
        {
            if (captureController != null) captureController.FrameChanged -= HandleFrameChanged;
            if (analyzeButton != null) analyzeButton.onClick.RemoveListener(AnalyzeLatest);
            if (automaticButton != null) automaticButton.onClick.RemoveListener(ToggleAutomaticAnalysis);
            if (serviceModeButton != null) serviceModeButton.onClick.RemoveListener(ToggleServiceMode);
            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
        }

        public void Configure(VisionAnalysisSettings value, DesktopCaptureController capture,
            TMP_Text serviceMode, TMP_Text status, TMP_Text summary, TMP_Text details,
            TMP_Text rawJson, Button analyze, Button automatic, TMP_Text automaticLabel,
            Button serviceButton)
        {
            settings = value;
            captureController = capture;
            serviceModeLabel = serviceMode;
            statusLabel = status;
            summaryLabel = summary;
            detailsLabel = details;
            rawJsonLabel = rawJson;
            analyzeButton = analyze;
            automaticButton = automatic;
            automaticButtonLabel = automaticLabel;
            serviceModeButton = serviceButton;
        }

        public void AnalyzeLatest()
        {
            if (latestFrame == null)
            {
                statusLabel.text = "尚未收到可分析的影格，請先按立即擷取。";
                return;
            }
            if (analysisInProgress) return;
            float remaining = settings.minimumAnalysisIntervalSeconds - (Time.unscaledTime - lastAnalysisTime);
            if (remaining > 0f)
            {
                statusLabel.text = "分析冷卻中，約 " + Mathf.CeilToInt(remaining) + " 秒後可再次分析。";
                SchedulePendingAnalysis(remaining);
                return;
            }
            _ = AnalyzeFrameAsync(latestFrame, lifetimeCancellation.Token);
        }

        public void ToggleAutomaticAnalysis()
        {
            automaticAnalysis = !automaticAnalysis;
            automaticButtonLabel.text = automaticAnalysis ? "停止自動分析" : "啟用自動分析";
            statusLabel.text = automaticAnalysis
                ? "自動分析已啟用，只處理有變化且通過冷卻時間的畫面。"
                : "自動分析已停止。";
            if (automaticAnalysis && latestFrame != null) AnalyzeLatest();
        }

        /// <summary>由最終設定頁明確啟用或停止自動分析。</summary>
        public void SetAutomaticAnalysis(bool enabled)
        {
            if (enabled != automaticAnalysis) ToggleAutomaticAnalysis();
        }

        public void ToggleServiceMode()
        {
            settings.useMockService = !settings.useMockService;
            RebuildService();
            statusLabel.text = settings.useMockService
                ? "已切換為本機 Mock，不會上傳圖片。"
                : "已切換為安全後端代理；Unity 內沒有 OpenAI API Key。";
        }

        /// <summary>設定是否使用本機 Mock；真實模式仍只會呼叫安全後端代理。</summary>
        public void SetUseMockService(bool useMock)
        {
            if (settings.useMockService == useMock) return;
            settings.useMockService = useMock;
            RebuildService();
            statusLabel.text = useMock
                ? "已切換為本機 Mock，不會上傳圖片。"
                : "已切換為安全後端代理；Unity 內沒有 OpenAI API Key。";
        }

        void HandleFrameChanged(ScreenCaptureFrame frame)
        {
            latestFrame = frame;
            statusLabel.text = "已收到新影格：" + frame.SourceName;
            if (automaticAnalysis) AnalyzeLatest();
        }

        async Task AnalyzeFrameAsync(ScreenCaptureFrame frame, CancellationToken cancellationToken)
        {
            analysisInProgress = true;
            analyzeButton.interactable = false;
            statusLabel.text = "正在壓縮畫面…";
            try
            {
                byte[] jpeg = VisionFrameEncoder.EncodeJpeg(frame,
                    settings.maximumImageDimension, settings.jpegQuality);
                statusLabel.text = "正在分析 JPEG（" + (jpeg.Length / 1024f).ToString("0") + " KB）…";
                VisionAnalysisResult result = await analysisService.AnalyzeAsync(jpeg,
                    frame.SourceName, frame.CapturedAtUtc.ToString("O"), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                result.Normalize(settings.minimumConfidence);
                LatestResult = result;
                lastAnalysisTime = Time.unscaledTime;
                summaryLabel.text = VisionAnalysisPresenter.FormatSummary(result);
                detailsLabel.text = VisionAnalysisPresenter.FormatDetails(result, settings.minimumConfidence);
                rawJsonLabel.text = JsonUtility.ToJson(result, true);
                statusLabel.text = result.uncertain
                    ? "分析完成，但結果含不確定資訊，請勿當成事實。"
                    : "分析完成。AI 僅提供建議。";
                AnalysisCompleted?.Invoke(result);
            }
            catch (OperationCanceledException)
            {
                statusLabel.text = "分析已取消。";
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                statusLabel.text = "分析失敗：" + exception.Message;
            }
            finally
            {
                analysisInProgress = false;
                analyzeButton.interactable = true;
            }
        }

        void SchedulePendingAnalysis(float delay)
        {
            if (!automaticAnalysis) return;
            if (pendingAnalysisRoutine != null) StopCoroutine(pendingAnalysisRoutine);
            pendingAnalysisRoutine = StartCoroutine(PendingAnalysis(delay));
        }

        IEnumerator PendingAnalysis(float delay)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(.1f, delay));
            pendingAnalysisRoutine = null;
            if (automaticAnalysis) AnalyzeLatest();
        }

        void RebuildService()
        {
            analysisService = settings.useMockService
                ? new MockVisionAnalysisService()
                : new BackendVisionAnalysisService(settings);
            serviceModeLabel.text = settings.useMockService
                ? "服務：本機 Mock JSON"
                : "服務：安全後端代理";
        }
    }
}
