// Copyright (c) 2023 homuler
//
// Use of this source code is governed by an MIT-style
// license that can be found in the LICENSE file or at
// https://opensource.org/licenses/MIT.
using System.Collections;
using Mediapipe.Tasks.Vision.HandLandmarker;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mediapipe.Unity.Sample.HandLandmarkDetection
{
    public class HandLandmarkerRunner : VisionTaskApiRunner<HandLandmarker>
    {
        [SerializeField] private HandLandmarkerResultAnnotationController _handLandmarkerResultAnnotationController;

        private Experimental.TextureFramePool _textureFramePool;

        public readonly HandLandmarkDetectionConfig config = new HandLandmarkDetectionConfig();

        public HandLandmarkerResult LatestResult { get; private set; }
        public bool HasLatestResult { get; private set; }

        private bool _isLiveStreamProcessing = false;
        private float _lastLiveStreamStartTime = -999f;

        public override void Stop()
        {
            base.Stop();

            _textureFramePool?.Dispose();
            _textureFramePool = null;

            HasLatestResult = false;
            _isLiveStreamProcessing = false;
            _lastLiveStreamStartTime = -999f;
        }

        protected override IEnumerator Run()
        {
            Debug.Log($"Delegate = {config.Delegate}");
            Debug.Log($"Image Read Mode = {config.ImageReadMode}");
            Debug.Log($"Running Mode = {config.RunningMode}");
            Debug.Log($"NumHands = {config.NumHands}");
            Debug.Log($"MinHandDetectionConfidence = {config.MinHandDetectionConfidence}");
            Debug.Log($"MinHandPresenceConfidence = {config.MinHandPresenceConfidence}");
            Debug.Log($"MinTrackingConfidence = {config.MinTrackingConfidence}");

            yield return AssetLoader.PrepareAssetAsync(config.ModelPath);

            var options = config.GetHandLandmarkerOptions(
                config.RunningMode == Tasks.Vision.Core.RunningMode.LIVE_STREAM
                    ? OnHandLandmarkDetectionOutput
                    : null
            );

            taskApi = HandLandmarker.CreateFromOptions(options, GpuManager.GpuResources);
            var imageSource = ImageSourceProvider.ImageSource;

            yield return imageSource.Play();

            if (!imageSource.isPrepared)
            {
                Debug.LogError("Failed to start ImageSource, exiting...");
                yield break;
            }

            _textureFramePool = new Experimental.TextureFramePool(
                imageSource.textureWidth,
                imageSource.textureHeight,
                TextureFormat.RGBA32,
                10
            );

            screen.Initialize(imageSource);
            SetupAnnotationController(_handLandmarkerResultAnnotationController, imageSource);

            var transformationOptions = imageSource.GetTransformationOptions();
            var flipHorizontally = transformationOptions.flipHorizontally;
            var flipVertically = transformationOptions.flipVertically;

            var imageProcessingOptions = new Tasks.Vision.Core.ImageProcessingOptions(
                rotationDegrees: (int)transformationOptions.rotationAngle
            );

            AsyncGPUReadbackRequest req = default;
            var waitUntilReqDone = new WaitUntil(() => req.done);
            var waitForEndOfFrame = new WaitForEndOfFrame();

            var result = HandLandmarkerResult.Alloc(options.numHands);

            var canUseGpuImage =
                SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3 &&
                GpuManager.GpuResources != null;

            using var glContext = canUseGpuImage ? GpuManager.GetGlContext() : null;

            while (true)
            {
                if (isPaused)
                {
                    yield return new WaitWhile(() => isPaused);
                }

                if (!_textureFramePool.TryGetTextureFrame(out var textureFrame))
                {
                    yield return new WaitForEndOfFrame();
                    continue;
                }

                Image image;

                switch (config.ImageReadMode)
                {
                    case ImageReadMode.GPU:
                        if (!canUseGpuImage)
                        {
                            throw new System.Exception("ImageReadMode.GPU is not supported");
                        }

                        textureFrame.ReadTextureOnGPU(
                            imageSource.GetCurrentTexture(),
                            flipHorizontally,
                            flipVertically
                        );

                        image = textureFrame.BuildGPUImage(glContext);
                        yield return waitForEndOfFrame;
                        break;

                    case ImageReadMode.CPU:
                        yield return waitForEndOfFrame;

                        textureFrame.ReadTextureOnCPU(
                            imageSource.GetCurrentTexture(),
                            flipHorizontally,
                            flipVertically
                        );

                        image = textureFrame.BuildCPUImage();
                        textureFrame.Release();
                        break;

                    case ImageReadMode.CPUAsync:
                    default:
                        req = textureFrame.ReadTextureAsync(
                            imageSource.GetCurrentTexture(),
                            flipHorizontally,
                            flipVertically
                        );

                        yield return waitUntilReqDone;

                        if (req.hasError)
                        {
                            Debug.LogWarning("Failed to read texture from the image source");
                            textureFrame.Release();
                            continue;
                        }

                        image = textureFrame.BuildCPUImage();
                        textureFrame.Release();
                        break;
                }

                switch (taskApi.runningMode)
                {
                    case Tasks.Vision.Core.RunningMode.IMAGE:
                        if (taskApi.TryDetect(image, imageProcessingOptions, ref result))
                        {
                            LatestResult = result;
                            HasLatestResult = result.handLandmarks != null && result.handLandmarks.Count > 0;

                            if (_handLandmarkerResultAnnotationController != null)
                            {
                                _handLandmarkerResultAnnotationController.DrawNow(result);
                            }
                        }
                        else
                        {
                            HasLatestResult = false;

                            if (_handLandmarkerResultAnnotationController != null)
                            {
                                _handLandmarkerResultAnnotationController.DrawNow(default);
                            }
                        }
                        break;

                    case Tasks.Vision.Core.RunningMode.VIDEO:
                        if (taskApi.TryDetectForVideo(
                            image,
                            GetCurrentTimestampMillisec(),
                            imageProcessingOptions,
                            ref result
                        ))
                        {
                            LatestResult = result;
                            HasLatestResult = result.handLandmarks != null && result.handLandmarks.Count > 0;

                            if (_handLandmarkerResultAnnotationController != null)
                            {
                                _handLandmarkerResultAnnotationController.DrawNow(result);
                            }
                        }
                        else
                        {
                            HasLatestResult = false;

                            if (_handLandmarkerResultAnnotationController != null)
                            {
                                _handLandmarkerResultAnnotationController.DrawNow(default);
                            }
                        }
                        break;

                    case Tasks.Vision.Core.RunningMode.LIVE_STREAM:
                        if (_isLiveStreamProcessing && Time.time - _lastLiveStreamStartTime > 1f)
                        {
                            _isLiveStreamProcessing = false;
                        }

                        if (!_isLiveStreamProcessing)
                        {
                            _isLiveStreamProcessing = true;
                            _lastLiveStreamStartTime = Time.time;

                            taskApi.DetectAsync(
                                image,
                                GetCurrentTimestampMillisec(),
                                imageProcessingOptions
                            );
                        }
                        break;
                }
            }
        }

        private void OnHandLandmarkDetectionOutput(HandLandmarkerResult result, Image image, long timestamp)
        {
            LatestResult = result;
            HasLatestResult = result.handLandmarks != null && result.handLandmarks.Count > 0;

            if (_handLandmarkerResultAnnotationController != null)
            {
                _handLandmarkerResultAnnotationController.DrawLater(result);
            }

            _isLiveStreamProcessing = false;
        }
    }
}

