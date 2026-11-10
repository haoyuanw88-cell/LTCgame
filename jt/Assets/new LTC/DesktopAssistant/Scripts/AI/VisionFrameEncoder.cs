using LTC.DesktopAssistant.Capture;
using UnityEngine;

namespace LTC.DesktopAssistant.AI
{
    /// <summary>
    /// 在 Unity 主執行緒把 RGBA 影格縮小並編碼成 JPEG。
    /// 縮圖可降低傳輸量、視覺模型費用與記憶體尖峰。
    /// </summary>
    public static class VisionFrameEncoder
    {
        public static byte[] EncodeJpeg(ScreenCaptureFrame frame, int maximumDimension, int quality)
        {
            if (frame == null || frame.Pixels == null || frame.Pixels.Length == 0)
                throw new System.ArgumentException("沒有可編碼的畫面。");

            int maxDimension = Mathf.Max(256, maximumDimension);
            float scale = Mathf.Min(1f, maxDimension / (float)Mathf.Max(frame.Width, frame.Height));
            int targetWidth = Mathf.Max(1, Mathf.RoundToInt(frame.Width * scale));
            int targetHeight = Mathf.Max(1, Mathf.RoundToInt(frame.Height * scale));

            // GDI 影格為由上而下；先翻成 Unity Texture2D 的由下而上排列。
            byte[] flipped = new byte[frame.Pixels.Length];
            int rowBytes = frame.Width * 4;
            for (int y = 0; y < frame.Height; y++)
                System.Buffer.BlockCopy(frame.Pixels, y * rowBytes,
                    flipped, (frame.Height - 1 - y) * rowBytes, rowBytes);

            var source = new Texture2D(frame.Width, frame.Height, TextureFormat.RGBA32, false, false);
            RenderTexture temporary = null;
            Texture2D scaled = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                source.LoadRawTextureData(flipped);
                source.Apply(false, false);
                temporary = RenderTexture.GetTemporary(targetWidth, targetHeight, 0,
                    RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                Graphics.Blit(source, temporary);
                RenderTexture.active = temporary;
                scaled = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false, false);
                scaled.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0, false);
                scaled.Apply(false, false);
                return scaled.EncodeToJPG(Mathf.Clamp(quality, 40, 95));
            }
            finally
            {
                RenderTexture.active = previous;
                if (temporary != null) RenderTexture.ReleaseTemporary(temporary);
                if (scaled != null) Object.Destroy(scaled);
                Object.Destroy(source);
            }
        }
    }
}
