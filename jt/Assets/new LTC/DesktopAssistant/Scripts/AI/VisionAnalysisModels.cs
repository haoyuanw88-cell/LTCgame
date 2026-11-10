using System;
using UnityEngine;

namespace LTC.DesktopAssistant.AI
{
    /// <summary>AI 從畫面辨識出的單一觀察。</summary>
    [Serializable]
    public sealed class VisionObservation
    {
        public string label;
        public string evidence;
        [Range(0f, 1f)] public float confidence;
        public bool uncertain;
    }

    /// <summary>OCR 辨識文字與其大約區域。</summary>
    [Serializable]
    public sealed class RecognizedText
    {
        public string text;
        [Range(0f, 1f)] public float confidence;
        public string region;
    }

    /// <summary>畫面中可能發生的遊戲事件。</summary>
    [Serializable]
    public sealed class DetectedGameEvent
    {
        public string type;
        public string description;
        [Range(0f, 1f)] public float confidence;
        public string evidence;
    }

    /// <summary>
    /// 後端代理必須回傳的結構化結果。所有陣列都必須存在，方便 Unity 安全顯示。
    /// </summary>
    [Serializable]
    public sealed class VisionAnalysisResult
    {
        public string schemaVersion = "1.0";
        public string summary;
        [Range(0f, 1f)] public float overallConfidence;
        public bool uncertain;
        public string uncertaintyReason;
        public VisionObservation[] observations = Array.Empty<VisionObservation>();
        public RecognizedText[] ocrTexts = Array.Empty<RecognizedText>();
        public DetectedGameEvent[] events = Array.Empty<DetectedGameEvent>();
        public string[] suggestedAdvice = Array.Empty<string>();

        /// <summary>
        /// 對網路資料進行防禦性整理。低於門檻的內容強制標示為不確定。
        /// </summary>
        public void Normalize(float minimumConfidence)
        {
            summary ??= string.Empty;
            uncertaintyReason ??= string.Empty;
            observations ??= Array.Empty<VisionObservation>();
            ocrTexts ??= Array.Empty<RecognizedText>();
            events ??= Array.Empty<DetectedGameEvent>();
            suggestedAdvice ??= Array.Empty<string>();
            overallConfidence = Mathf.Clamp01(overallConfidence);
            if (overallConfidence < minimumConfidence) uncertain = true;

            foreach (VisionObservation observation in observations)
            {
                if (observation == null) continue;
                observation.label ??= string.Empty;
                observation.evidence ??= string.Empty;
                observation.confidence = Mathf.Clamp01(observation.confidence);
                if (observation.confidence < minimumConfidence) observation.uncertain = true;
            }
            foreach (RecognizedText text in ocrTexts)
            {
                if (text == null) continue;
                text.text ??= string.Empty;
                text.region ??= string.Empty;
                text.confidence = Mathf.Clamp01(text.confidence);
            }
            foreach (DetectedGameEvent gameEvent in events)
            {
                if (gameEvent == null) continue;
                gameEvent.type ??= string.Empty;
                gameEvent.description ??= string.Empty;
                gameEvent.evidence ??= string.Empty;
                gameEvent.confidence = Mathf.Clamp01(gameEvent.confidence);
            }
        }
    }

    [Serializable]
    public sealed class VisionAnalysisRequest
    {
        public string requestId;
        public string schemaVersion = "1.0";
        public string sourceName;
        public string capturedAtUtc;
        public string imageMediaType = "image/jpeg";
        public string imageBase64;
        public string language = "zh-TW";
    }

    [Serializable]
    public sealed class VisionAnalysisEnvelope
    {
        public bool success;
        public VisionAnalysisResult analysis;
        public string error;
    }
}
