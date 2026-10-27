using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LTC.Mobile
{
    /// <summary>
    /// Applies the same mobile layout rules to every scene without replacing existing UI.
    /// The project is designed as a landscape phone/tablet application.
    /// </summary>
    public sealed class LTCMobileBootstrap : MonoBehaviour
    {
        private static LTCMobileBootstrap instance;
        private static readonly Vector2 ReferenceResolution = new Vector2(1366f, 768f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            if (instance != null) return;

            GameObject root = new GameObject("LTC Mobile Runtime");
            instance = root.AddComponent<LTCMobileBootstrap>();
            DontDestroyOnLoad(root);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

#if UNITY_ANDROID || UNITY_IOS
            Screen.orientation = ScreenOrientation.AutoRotation;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
#endif

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private IEnumerator Start()
        {
            yield return null;
            ApplyToLoadedScene();
            LTCSceneLoader.ShowStartupPreview();
        }

        private void OnDestroy()
        {
            if (instance == this)
                SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StartCoroutine(ApplyNextFrame());
        }

        private IEnumerator ApplyNextFrame()
        {
            yield return null;
            ApplyToLoadedScene();
        }

        private static void ApplyToLoadedScene()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null || canvas.transform.root.GetComponent<LTCSceneLoader>() != null) continue;

                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler == null) continue;

                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = ReferenceResolution;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;

                if (canvas.isRootCanvas && canvas.GetComponent<LTCSafeAreaAdapter>() == null)
                    canvas.gameObject.AddComponent<LTCSafeAreaAdapter>();
            }

            if (FindFirstObjectByType<EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                DontDestroyOnLoad(eventSystem);
            }
        }
    }

    /// <summary>
    /// Adds notch/rounded-corner padding to full-screen content panels while leaving decorative
    /// backgrounds full bleed. It only touches stretched top-level panels, so fixed game layouts
    /// keep their original positions and proportions.
    /// </summary>
    public sealed class LTCSafeAreaAdapter : MonoBehaviour
    {
        private struct Entry
        {
            public RectTransform rect;
            public Vector2 offsetMin;
            public Vector2 offsetMax;
        }

        private readonly List<Entry> entries = new List<Entry>();
        private Rect lastSafeArea;
        private Vector2Int lastScreen;
        private Canvas canvas;

        private void Awake()
        {
            canvas = GetComponent<Canvas>();
            CacheEligiblePanels();
            Apply(true);
        }

        private void OnEnable()
        {
            Apply(true);
        }

        private void Update()
        {
            Apply(false);
        }

        private void CacheEligiblePanels()
        {
            entries.Clear();
            for (int i = 0; i < transform.childCount; i++)
            {
                RectTransform rect = transform.GetChild(i) as RectTransform;
                if (rect == null || !IsFullStretch(rect) || IsDecorativeBackground(rect.name)) continue;
                entries.Add(new Entry { rect = rect, offsetMin = rect.offsetMin, offsetMax = rect.offsetMax });
            }
        }

        private void Apply(bool force)
        {
            if (canvas == null || Screen.width <= 0 || Screen.height <= 0) return;
            Rect safe = Screen.safeArea;
            Vector2Int size = new Vector2Int(Screen.width, Screen.height);
            if (!force && safe == lastSafeArea && size == lastScreen) return;

            lastSafeArea = safe;
            lastScreen = size;
            float scale = Mathf.Max(0.01f, canvas.scaleFactor);
            Vector2 minPadding = new Vector2(safe.xMin, safe.yMin) / scale;
            Vector2 maxPadding = new Vector2(Screen.width - safe.xMax, Screen.height - safe.yMax) / scale;

            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                if (entry.rect == null) continue;
                entry.rect.offsetMin = entry.offsetMin + minPadding;
                entry.rect.offsetMax = entry.offsetMax - maxPadding;
            }
        }

        private static bool IsFullStretch(RectTransform rect)
        {
            return Vector2.SqrMagnitude(rect.anchorMin) < 0.0001f &&
                   Vector2.SqrMagnitude(rect.anchorMax - Vector2.one) < 0.0001f;
        }

        private static bool IsDecorativeBackground(string objectName)
        {
            string value = objectName.ToLowerInvariant();
            return value.Contains("background") || value == "bg" || value.EndsWith(" bg") ||
                   value.Contains("背景") || value.Contains("loading overlay");
        }
    }
}
