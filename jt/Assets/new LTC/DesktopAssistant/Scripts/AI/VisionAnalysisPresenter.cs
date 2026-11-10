using System.Text;

namespace LTC.DesktopAssistant.AI
{
    /// <summary>把結構化結果轉成可讀文字，同時保留可信度與不確定語氣。</summary>
    public static class VisionAnalysisPresenter
    {
        public static string FormatSummary(VisionAnalysisResult result)
        {
            if (result == null) return "尚無分析結果。";
            string prefix = result.uncertain ? "可能：" : string.Empty;
            return prefix + result.summary + "\n可信度：" +
                   (result.overallConfidence * 100f).ToString("0") + "%" +
                   (string.IsNullOrWhiteSpace(result.uncertaintyReason)
                       ? string.Empty
                       : "\n不確定原因：" + result.uncertaintyReason);
        }

        public static string FormatDetails(VisionAnalysisResult result, float minimumConfidence)
        {
            if (result == null) return string.Empty;
            var builder = new StringBuilder(1024);
            builder.AppendLine("【觀察】");
            if (result.observations.Length == 0) builder.AppendLine("無");
            foreach (VisionObservation observation in result.observations)
            {
                if (observation == null) continue;
                bool uncertain = observation.uncertain || observation.confidence < minimumConfidence;
                builder.Append("• ").Append(uncertain ? "可能 " : string.Empty)
                    .Append(observation.label).Append("（")
                    .Append((observation.confidence * 100f).ToString("0")).AppendLine("%）");
                if (!string.IsNullOrWhiteSpace(observation.evidence))
                    builder.Append("  依據：").AppendLine(observation.evidence);
            }

            builder.AppendLine("\n【OCR 文字】");
            if (result.ocrTexts.Length == 0) builder.AppendLine("無");
            foreach (RecognizedText text in result.ocrTexts)
            {
                if (text == null) continue;
                builder.Append("• ").Append(text.confidence < minimumConfidence ? "可能辨識為「" : "「")
                    .Append(text.text).Append("」 （")
                    .Append((text.confidence * 100f).ToString("0")).Append("%）");
                if (!string.IsNullOrWhiteSpace(text.region))
                    builder.Append("，位置：").Append(text.region);
                builder.AppendLine();
            }

            builder.AppendLine("\n【事件】");
            if (result.events.Length == 0) builder.AppendLine("無");
            foreach (DetectedGameEvent gameEvent in result.events)
            {
                if (gameEvent == null) continue;
                builder.Append("• ").Append(gameEvent.confidence < minimumConfidence ? "可能 " : string.Empty)
                    .Append(gameEvent.description).Append("（")
                    .Append((gameEvent.confidence * 100f).ToString("0")).AppendLine("%）");
            }

            builder.AppendLine("\n【建議】");
            if (result.suggestedAdvice.Length == 0) builder.AppendLine("目前沒有建議。");
            foreach (string advice in result.suggestedAdvice)
                if (!string.IsNullOrWhiteSpace(advice)) builder.Append("• ").AppendLine(advice);
            return builder.ToString().Trim();
        }
    }
}
