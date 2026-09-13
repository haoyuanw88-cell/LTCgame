using UnityEngine;
using UnityEngine.UI;

namespace LTC.Identity
{
    public sealed class LTCPlayerIdOverlay : MonoBehaviour
    {
        const string HostName = "LTC Player ID Overlay";
        const string CanvasName = "LTC Player ID Canvas";

        static LTCPlayerIdOverlay instance;

        IPlayerIdentityProvider identity;
        Text idText;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            if (instance != null) return;

            GameObject host = new GameObject(HostName);
            DontDestroyOnLoad(host);
            instance = host.AddComponent<LTCPlayerIdOverlay>();
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            BuildOverlay();
            BindIdentity();
            RefreshText();
        }

        void OnDestroy()
        {
            if (identity != null) identity.IdentityChanged -= RefreshText;
            if (instance == this) instance = null;
        }

        void BindIdentity()
        {
            identity = PlayerIdentityService.Current;
            if (identity != null) identity.IdentityChanged += RefreshText;
        }

        void BuildOverlay()
        {
            GameObject canvasObject = new GameObject(CanvasName);
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31000;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject textObject = new GameObject("Player ID Text");
            textObject.transform.SetParent(canvasObject.transform, false);

            RectTransform rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-14f, 10f);
            rect.sizeDelta = new Vector2(520f, 32f);

            idText = textObject.AddComponent<Text>();
            idText.font = CreateChineseFont();
            idText.fontSize = 18;
            idText.fontStyle = FontStyle.Bold;
            idText.alignment = TextAnchor.LowerRight;
            idText.color = new Color(0f, 0f, 0f, 0.82f);
            idText.raycastTarget = false;
            idText.resizeTextForBestFit = true;
            idText.resizeTextMinSize = 12;
            idText.resizeTextMaxSize = 18;

            Shadow shadow = textObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(1f, 1f, 1f, 0.72f);
            shadow.effectDistance = new Vector2(1f, -1f);
        }

        void RefreshText()
        {
            if (idText == null) return;
            if (identity == null) BindIdentity();

            idText.text = "玩家ID：" + ResolveDisplayId();
        }

        string ResolveDisplayId()
        {
            if (identity == null) return "連線中";
            if (!string.IsNullOrWhiteSpace(identity.PlayerCode)) return identity.PlayerCode.Trim();
            if (!string.IsNullOrWhiteSpace(identity.PlayerId)) return identity.PlayerId.Trim();
            if (!string.IsNullOrWhiteSpace(identity.StableLocalPlayerKey)) return identity.StableLocalPlayerKey.Trim();
            return "連線中";
        }

        static Font CreateChineseFont()
        {
            return Font.CreateDynamicFontFromOSFont(
                new[]
                {
                    "Microsoft JhengHei UI",
                    "Microsoft JhengHei",
                    "Noto Sans CJK TC",
                    "PingFang TC",
                    "Arial Unicode MS",
                    "SimHei"
                },
                18);
        }
    }
}
