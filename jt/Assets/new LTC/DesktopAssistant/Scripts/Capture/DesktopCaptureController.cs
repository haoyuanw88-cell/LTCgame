using System;
using System.Collections.Generic;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using LTC.DesktopAssistant.Capture.Windows;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LTC.DesktopAssistant.Capture
{
    /// <summary>
    /// 第二階段擷取協調器。只有使用者按下啟用或立即擷取時才讀取畫面，
    /// 並在本機預覽；本階段不會把圖片傳送到網路。
    /// </summary>
    public sealed class DesktopCaptureController : MonoBehaviour
    {
        [Header("Preview")]
        [SerializeField] RawImage previewImage;
        [SerializeField] AspectRatioFitter previewAspect;
        [SerializeField] TMP_Text sourceLabel;
        [SerializeField] TMP_Text statusLabel;
        [SerializeField] TMP_Text metricsLabel;

        [Header("Controls")]
        [SerializeField] Button sourceButton;
        [SerializeField] Button refreshButton;
        [SerializeField] Button captureButton;
        [SerializeField] Button autoCaptureButton;
        [SerializeField] TMP_Text autoCaptureButtonLabel;

        [Header("Capture")]
        [SerializeField, Min(1f)] float captureIntervalSeconds = 5f;
        [SerializeField, Range(0.001f, 0.25f)] float changeThreshold = 0.025f;

        readonly FrameChangeDetector changeDetector = new FrameChangeDetector();
        readonly List<CaptureTarget> targets = new List<CaptureTarget>();
        IScreenCaptureService captureService;
        CancellationTokenSource lifetimeCancellation;
        Coroutine automaticCaptureRoutine;
        Texture2D previewTexture;
        int selectedTargetIndex;
        int capturedCount;
        int changedCount;
        int skippedCount;
        bool captureInProgress;

        public bool AutomaticCaptureEnabled => automaticCaptureRoutine != null;

        /// <summary>
        /// 只有通過畫面變化檢查的影格才會觸發，第三階段由此接收分析素材。
        /// </summary>
        public event Action<ScreenCaptureFrame> FrameChanged;

        void Awake()
        {
            captureService = new WindowsGdiScreenCaptureService();
            lifetimeCancellation = new CancellationTokenSource();
            sourceButton.onClick.AddListener(SelectNextSource);
            refreshButton.onClick.AddListener(RefreshTargets);
            captureButton.onClick.AddListener(CaptureNow);
            autoCaptureButton.onClick.AddListener(ToggleAutomaticCapture);
        }

        void Start()
        {
            if (!captureService.IsSupported)
            {
                SetStatus("此平台不支援 Windows 畫面擷取。", true);
                SetControlsInteractable(false);
                return;
            }
            RefreshTargets();
            SetStatus("尚未擷取。圖片只會保留在本機。", false);
            UpdateMetrics();
        }

        void OnDestroy()
        {
            if (sourceButton != null) sourceButton.onClick.RemoveListener(SelectNextSource);
            if (refreshButton != null) refreshButton.onClick.RemoveListener(RefreshTargets);
            if (captureButton != null) captureButton.onClick.RemoveListener(CaptureNow);
            if (autoCaptureButton != null) autoCaptureButton.onClick.RemoveListener(ToggleAutomaticCapture);
            lifetimeCancellation?.Cancel();
            lifetimeCancellation?.Dispose();
            if (previewTexture != null) Destroy(previewTexture);
        }

        public void Configure(RawImage preview, AspectRatioFitter aspect, TMP_Text source,
            TMP_Text status, TMP_Text metrics, Button sourceControl, Button refreshControl,
            Button captureControl, Button automaticControl, TMP_Text automaticLabel)
        {
            previewImage = preview;
            previewAspect = aspect;
            sourceLabel = source;
            statusLabel = status;
            metricsLabel = metrics;
            sourceButton = sourceControl;
            refreshButton = refreshControl;
            captureButton = captureControl;
            autoCaptureButton = automaticControl;
            autoCaptureButtonLabel = automaticLabel;
        }

        public void RefreshTargets()
        {
            long selectedHandle = targets.Count == 0 ? 0 : targets[selectedTargetIndex].NativeHandle;
            CaptureTargetKind selectedKind = targets.Count == 0
                ? CaptureTargetKind.EntireDesktop
                : targets[selectedTargetIndex].Kind;
            targets.Clear();
            targets.AddRange(captureService.GetAvailableTargets());
            selectedTargetIndex = 0;
            for (int index = 0; index < targets.Count; index++)
                if (targets[index].Kind == selectedKind && targets[index].NativeHandle == selectedHandle)
                {
                    selectedTargetIndex = index;
                    break;
                }
            changeDetector.Reset();
            UpdateSourceLabel();
            SetStatus("已找到 " + targets.Count + " 個可擷取來源。", false);
        }

        public void SelectNextSource()
        {
            if (targets.Count == 0) return;
            selectedTargetIndex = (selectedTargetIndex + 1) % targets.Count;
            changeDetector.Reset();
            UpdateSourceLabel();
        }

        public void CaptureNow()
        {
            _ = CaptureSelectedAsync(lifetimeCancellation.Token);
        }

        public void ToggleAutomaticCapture()
        {
            if (automaticCaptureRoutine == null)
            {
                automaticCaptureRoutine = StartCoroutine(AutomaticCaptureLoop());
                autoCaptureButtonLabel.text = "停止定時擷取";
                SetStatus("已啟用定時擷取，每 " + captureIntervalSeconds.ToString("0.#") + " 秒檢查一次。", false);
            }
            else
            {
                StopCoroutine(automaticCaptureRoutine);
                automaticCaptureRoutine = null;
                autoCaptureButtonLabel.text = "啟用定時擷取";
                SetStatus("定時擷取已停止。", false);
            }
        }

        /// <summary>以明確狀態控制定時擷取，避免設定頁重複切換。</summary>
        public void SetAutomaticCapture(bool enabled)
        {
            if (enabled != AutomaticCaptureEnabled) ToggleAutomaticCapture();
        }

        IEnumerator AutomaticCaptureLoop()
        {
            while (true)
            {
                CaptureNow();
                yield return new WaitForSecondsRealtime(captureIntervalSeconds);
            }
        }

        async Task CaptureSelectedAsync(CancellationToken cancellationToken)
        {
            if (captureInProgress || targets.Count == 0) return;
            captureInProgress = true;
            SetStatus("正在擷取…", false);
            try
            {
                CaptureTarget target = targets[selectedTargetIndex];
                ScreenCaptureFrame frame = await captureService.CaptureAsync(target, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                capturedCount++;
                bool changed = changeDetector.HasChanged(frame, changeThreshold);
                if (!changed)
                {
                    skippedCount++;
                    SetStatus("畫面幾乎沒有變化，已略過重複影格。差異 " +
                              (changeDetector.LastDifference * 100f).ToString("0.00") + "%", false);
                    UpdateMetrics();
                    return;
                }

                changedCount++;
                UpdatePreview(frame);
                FrameChanged?.Invoke(frame);
                SetStatus("已更新本機預覽：" + frame.Width + " × " + frame.Height +
                          "，差異 " + (changeDetector.LastDifference * 100f).ToString("0.00") + "%", false);
                UpdateMetrics();
            }
            catch (OperationCanceledException)
            {
                SetStatus("擷取已取消。", false);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                SetStatus("擷取失敗：" + exception.Message, true);
            }
            finally
            {
                captureInProgress = false;
            }
        }

        void UpdatePreview(ScreenCaptureFrame frame)
        {
            if (previewTexture == null || previewTexture.width != frame.Width || previewTexture.height != frame.Height)
            {
                if (previewTexture != null) Destroy(previewTexture);
                previewTexture = new Texture2D(frame.Width, frame.Height, TextureFormat.RGBA32, false, false)
                {
                    name = "Desktop Capture Preview",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
            }
            previewTexture.LoadRawTextureData(frame.Pixels);
            previewTexture.Apply(false, false);
            previewImage.texture = previewTexture;
            previewImage.uvRect = new Rect(0f, 1f, 1f, -1f);
            previewImage.color = Color.white;
            previewAspect.aspectRatio = frame.Width / (float)frame.Height;
        }

        void UpdateSourceLabel()
        {
            sourceLabel.text = targets.Count == 0
                ? "來源：找不到可擷取畫面"
                : "來源：" + targets[selectedTargetIndex].DisplayName + "（點擊切換）";
        }

        void UpdateMetrics()
        {
            metricsLabel.text = "擷取 " + capturedCount + "｜更新 " + changedCount + "｜略過 " + skippedCount;
        }

        void SetStatus(string message, bool isError)
        {
            statusLabel.text = message;
            statusLabel.color = isError ? new Color32(190, 65, 55, 255) : new Color32(54, 94, 75, 255);
        }

        void SetControlsInteractable(bool value)
        {
            sourceButton.interactable = value;
            refreshButton.interactable = value;
            captureButton.interactable = value;
            autoCaptureButton.interactable = value;
        }
    }
}
