using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LTC.Mobile
{
    /// <summary>
    /// Central scene loader with a warm, animated loading overlay and real async progress.
    /// Call LTCSceneLoader.LoadScene instead of SceneManager.LoadScene for user navigation.
    /// </summary>
    public sealed class LTCSceneLoader : MonoBehaviour
    {
        private static LTCSceneLoader instance;
        private CanvasGroup group;
        private Image progressFill;
        private Text progressLabel;
        private RectTransform mascot;
        private RectTransform[] dots;
        private bool busy;

        public static bool IsBusy => instance != null && instance.busy;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureCreated()
        {
            if (instance != null) return;
            GameObject root = new GameObject("LTC Scene Loader");
            instance = root.AddComponent<LTCSceneLoader>();
            DontDestroyOnLoad(root);
        }

        public static void LoadScene(string sceneName)
        {
            EnsureCreated();
            if (!instance.busy)
                instance.StartCoroutine(instance.LoadRoutine(sceneName));
        }

        public static void LoadScene(int buildIndex)
        {
            EnsureCreated();
            if (!instance.busy)
                instance.StartCoroutine(instance.LoadRoutine(buildIndex));
        }

        public static void ShowStartupPreview()
        {
            EnsureCreated();
            if (!instance.busy)
                instance.StartCoroutine(instance.StartupRoutine());
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
            BuildOverlay();
            HideImmediate();
        }

        private IEnumerator StartupRoutine()
        {
            busy = true;
            Show();
            float elapsed = 0f;
            const float duration = 0.9f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                SetProgress(Mathf.Clamp01(elapsed / duration));
                Animate(elapsed);
                yield return null;
            }
            yield return Fade(false, 0.22f);
            busy = false;
        }

        private IEnumerator LoadRoutine(string sceneName)
        {
            busy = true;
            Show();
            yield return Fade(true, 0.18f);
            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            yield return Track(operation);
            yield return Fade(false, 0.24f);
            busy = false;
        }

        private IEnumerator LoadRoutine(int buildIndex)
        {
            busy = true;
            Show();
            yield return Fade(true, 0.18f);
            AsyncOperation operation = SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
            yield return Track(operation);
            yield return Fade(false, 0.24f);
            busy = false;
        }

        private IEnumerator Track(AsyncOperation operation)
        {
            float shown = 0f;
            float elapsed = 0f;
            while (!operation.isDone)
            {
                elapsed += Time.unscaledDeltaTime;
                float target = Mathf.Clamp01(operation.progress / 0.9f);
                shown = Mathf.MoveTowards(shown, target, Time.unscaledDeltaTime * 1.4f);
                SetProgress(shown);
                Animate(elapsed);
                yield return null;
            }
            SetProgress(1f);
            yield return null;
        }

        private IEnumerator Fade(bool visible, float duration)
        {
            float start = group.alpha;
            float target = visible ? 1f : 0f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            group.alpha = target;
            group.blocksRaycasts = visible;
            group.interactable = visible;
        }

        private void Show()
        {
            gameObject.SetActive(true);
            group.alpha = 1f;
            group.blocksRaycasts = true;
            group.interactable = true;
            SetProgress(0f);
        }

        private void HideImmediate()
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        private void SetProgress(float value)
        {
            value = Mathf.Clamp01(value);
            progressFill.fillAmount = value;
            progressLabel.text = string.Format("載入中  {0:0}%", value * 100f);
        }

        private void Animate(float elapsed)
        {
            float bob = Mathf.Sin(elapsed * 3.4f) * 8f;
            mascot.anchoredPosition = new Vector2(0f, 54f + bob);
            mascot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(elapsed * 2.2f) * 2.5f);
            for (int i = 0; i < dots.Length; i++)
            {
                float scale = 0.8f + Mathf.Max(0f, Mathf.Sin(elapsed * 5f - i * 0.75f)) * 0.45f;
                dots[i].localScale = Vector3.one * scale;
            }
        }

        private void BuildOverlay()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1366f, 768f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            group = gameObject.AddComponent<CanvasGroup>();

            RectTransform background = CreateImage("Loading Background", transform, new Color32(250, 239, 207, 255));
            Stretch(background);

            RectTransform glow = CreateImage("Warm Glow", background, new Color32(255, 249, 229, 255));
            glow.anchorMin = glow.anchorMax = new Vector2(0.5f, 0.5f);
            glow.sizeDelta = new Vector2(720f, 500f);

            mascot = CreateImage("Mascot", glow, new Color32(236, 174, 137, 255));
            mascot.anchorMin = mascot.anchorMax = new Vector2(0.5f, 0.5f);
            mascot.sizeDelta = new Vector2(170f, 170f);
            mascot.anchoredPosition = new Vector2(0f, 54f);
            mascot.GetComponent<Image>().sprite = CreateRoundedSprite();

            Text face = CreateText("Mascot Face", mascot, "• ᴗ •", 42, new Color32(88, 54, 43, 255));
            Stretch(face.rectTransform);
            face.alignment = TextAnchor.MiddleCenter;

            Text title = CreateText("Loading Title", glow, "一起準備今天的遊戲", 39, new Color32(82, 63, 48, 255));
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            title.rectTransform.sizeDelta = new Vector2(720f, 70f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -78f);
            title.alignment = TextAnchor.MiddleCenter;

            RectTransform track = CreateImage("Progress Track", glow, new Color32(222, 205, 170, 255));
            track.anchorMin = track.anchorMax = new Vector2(0.5f, 0.5f);
            track.sizeDelta = new Vector2(550f, 38f);
            track.anchoredPosition = new Vector2(0f, -146f);
            track.GetComponent<Image>().sprite = CreateRoundedSprite();

            RectTransform fill = CreateImage("Progress Fill", track, new Color32(102, 157, 94, 255));
            fill.anchorMin = new Vector2(0f, 0f);
            fill.anchorMax = new Vector2(1f, 1f);
            fill.offsetMin = new Vector2(5f, 5f);
            fill.offsetMax = new Vector2(-5f, -5f);
            Image fillImage = fill.GetComponent<Image>();
            fillImage.sprite = CreateRoundedSprite();
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = 0;
            progressFill = fillImage;

            progressLabel = CreateText("Progress Label", glow, "載入中  0%", 27, new Color32(104, 82, 61, 255));
            progressLabel.rectTransform.anchorMin = progressLabel.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            progressLabel.rectTransform.sizeDelta = new Vector2(360f, 50f);
            progressLabel.rectTransform.anchoredPosition = new Vector2(0f, -198f);
            progressLabel.alignment = TextAnchor.MiddleCenter;

            dots = new RectTransform[3];
            for (int i = 0; i < dots.Length; i++)
            {
                RectTransform dot = CreateImage("Loading Dot " + (i + 1), glow, new Color32(217, 132, 106, 255));
                dot.anchorMin = dot.anchorMax = new Vector2(0.5f, 0.5f);
                dot.sizeDelta = new Vector2(18f, 18f);
                dot.anchoredPosition = new Vector2(-28f + i * 28f, -238f);
                dot.GetComponent<Image>().sprite = CreateRoundedSprite();
                dots[i] = dot;
            }
        }

        private static RectTransform CreateImage(string name, Transform parent, Color color)
        {
            GameObject item = new GameObject(name, typeof(RectTransform), typeof(Image));
            item.transform.SetParent(parent, false);
            item.GetComponent<Image>().color = color;
            return item.GetComponent<RectTransform>();
        }

        private static Text CreateText(string name, Transform parent, string value, int size, Color color)
        {
            GameObject item = new GameObject(name, typeof(RectTransform), typeof(Text));
            item.transform.SetParent(parent, false);
            Text text = item.GetComponent<Text>();
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.fontStyle = FontStyle.Bold;
            text.font = CreateFont(size);
            return text;
        }

        private static Font CreateFont(int size)
        {
            string[] candidates = { "Microsoft JhengHei", "Noto Sans CJK TC", "Noto Sans CJK SC", "Arial" };
            Font font = Font.CreateDynamicFontFromOSFont(candidates, size);
            return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Sprite CreateRoundedSprite()
        {
            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "LTC Runtime Rounded UI";
            Color clear = new Color(1f, 1f, 1f, 0f);
            Color solid = Color.white;
            float radius = 15f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(radius - x, 0f) + Mathf.Max(x - (size - 1 - radius), 0f);
                    float dy = Mathf.Max(radius - y, 0f) + Mathf.Max(y - (size - 1 - radius), 0f);
                    texture.SetPixel(x, y, dx * dx + dy * dy <= radius * radius ? solid : clear);
                }
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(18f, 18f, 18f, 18f));
        }
    }
}
