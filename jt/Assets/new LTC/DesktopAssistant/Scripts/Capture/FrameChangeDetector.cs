using System;

namespace LTC.DesktopAssistant.Capture
{
    /// <summary>
    /// 將畫面取樣成 16x9 灰階指紋，比較平均亮度差異。
    /// 目的是略過幾乎相同的畫面，避免第三階段反覆提交相同內容。
    /// </summary>
    public sealed class FrameChangeDetector
    {
        const int SampleColumns = 16;
        const int SampleRows = 9;
        byte[] previousFingerprint;

        public float LastDifference { get; private set; }

        public bool HasChanged(ScreenCaptureFrame frame, float threshold)
        {
            if (frame == null || frame.Pixels == null || frame.Width <= 0 || frame.Height <= 0)
                return false;

            byte[] current = BuildFingerprint(frame);
            if (previousFingerprint == null)
            {
                previousFingerprint = current;
                LastDifference = 1f;
                return true;
            }

            long difference = 0;
            for (int index = 0; index < current.Length; index++)
                difference += Math.Abs(current[index] - previousFingerprint[index]);

            LastDifference = difference / (255f * current.Length);
            bool changed = LastDifference >= Math.Max(0.001f, threshold);
            if (changed) previousFingerprint = current;
            return changed;
        }

        public void Reset()
        {
            previousFingerprint = null;
            LastDifference = 0f;
        }

        static byte[] BuildFingerprint(ScreenCaptureFrame frame)
        {
            byte[] fingerprint = new byte[SampleColumns * SampleRows];
            for (int row = 0; row < SampleRows; row++)
            {
                int y = Math.Min(frame.Height - 1,
                    (int)((row + 0.5f) * frame.Height / SampleRows));
                for (int column = 0; column < SampleColumns; column++)
                {
                    int x = Math.Min(frame.Width - 1,
                        (int)((column + 0.5f) * frame.Width / SampleColumns));
                    int pixel = (y * frame.Width + x) * 4;
                    int red = frame.Pixels[pixel];
                    int green = frame.Pixels[pixel + 1];
                    int blue = frame.Pixels[pixel + 2];
                    fingerprint[row * SampleColumns + column] =
                        (byte)((red * 54 + green * 183 + blue * 19) >> 8);
                }
            }
            return fingerprint;
        }
    }
}
