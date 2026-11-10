using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LTC.Identity;
using UnityEngine;
using UnityEngine.Networking;

namespace LTC.DesktopAssistant.AI
{
    /// <summary>
    /// 將圖片送往 LTC 安全後端代理。Unity 只持有玩家 Access Token，
    /// 絕不持有或傳入 OpenAI API Key。
    /// </summary>
    public sealed class BackendVisionAnalysisService : IGameVisionAnalysisService
    {
        const string DefaultApiBaseUrl = "https://staging-hello-8shi.encr.app";
        readonly VisionAnalysisSettings settings;

        public BackendVisionAnalysisService(VisionAnalysisSettings settings)
        {
            this.settings = settings;
        }

        public async Task<VisionAnalysisResult> AnalyzeAsync(byte[] jpegImage, string sourceName,
            string capturedAtUtc, CancellationToken cancellationToken)
        {
            if (jpegImage == null || jpegImage.Length == 0)
                throw new ArgumentException("沒有可分析的 JPEG 圖片。");

            var payload = new VisionAnalysisRequest
            {
                requestId = Guid.NewGuid().ToString("N"),
                sourceName = sourceName ?? string.Empty,
                capturedAtUtc = capturedAtUtc ?? string.Empty,
                imageBase64 = Convert.ToBase64String(jpegImage)
            };
            string baseUrl = PlayerPrefs.GetString(
                PlayerIdentityService.ApiBaseUrlPlayerPrefsKey,
                DefaultApiBaseUrl).Trim().TrimEnd('/');
            string path = string.IsNullOrWhiteSpace(settings.backendPath)
                ? "/api/v1/assistant/analyze"
                : "/" + settings.backendPath.Trim().TrimStart('/');
            byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));

            using var request = new UnityWebRequest(baseUrl + path, UnityWebRequest.kHttpVerbPOST);
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = settings.requestTimeoutSeconds;
            request.SetRequestHeader("Content-Type", "application/json");
            string accessToken = PlayerIdentityService.Current.AccessToken;
            if (!string.IsNullOrWhiteSpace(accessToken))
                request.SetRequestHeader("Authorization", "Bearer " + accessToken);

            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    request.Abort();
                    cancellationToken.ThrowIfCancellationRequested();
                }
                await Task.Yield();
            }

            if (request.result != UnityWebRequest.Result.Success)
                throw new InvalidOperationException("後端分析失敗（" + request.responseCode + "）：" +
                                                    SafeError(request.downloadHandler.text));

            string json = request.downloadHandler.text;
            VisionAnalysisEnvelope envelope = null;
            try { envelope = JsonUtility.FromJson<VisionAnalysisEnvelope>(json); }
            catch (Exception) { }
            if (envelope != null && envelope.analysis != null)
            {
                if (!envelope.success && !string.IsNullOrWhiteSpace(envelope.error))
                    throw new InvalidOperationException(envelope.error);
                return envelope.analysis;
            }

            VisionAnalysisResult direct = JsonUtility.FromJson<VisionAnalysisResult>(json);
            if (direct == null || string.IsNullOrWhiteSpace(direct.schemaVersion))
                throw new InvalidOperationException("後端沒有回傳可解析的結構化 JSON。");
            return direct;
        }

        static string SafeError(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return "沒有錯誤內容";
            string trimmed = message.Trim();
            return trimmed.Length <= 240 ? trimmed : trimmed.Substring(0, 240) + "…";
        }
    }
}
