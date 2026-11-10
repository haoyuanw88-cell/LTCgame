using System.IO;
using LTC.DesktopAssistant.Capture;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 建立第二階段本機畫面擷取預覽。只寫入 DesktopAssistant/Phase2，
/// 不修改現有遊戲場景與 Build Settings。
/// </summary>
public static class DesktopCapturePhase2SetupUtility
{
    const string Root = "Assets/new LTC/DesktopAssistant/Phase2";
    const string PrefabPath = Root + "/Resources/DesktopCapturePreview.prefab";
    const string ScenePath = Root + "/Scenes/DesktopCapturePhase2Demo.unity";
    const string FontPath = "Assets/new LTC/Unity中文/KAIU_Dynamic.asset";

    static readonly Color Cream = new Color32(255, 250, 238, 255);
    static readonly Color Mint = new Color32(195, 229, 207, 255);
    static readonly Color Green = new Color32(72, 160, 128, 255);
    static readonly Color Orange = new Color32(246, 149, 61, 255);
    static readonly Color Ink = new Color32(54, 58, 51, 255);

    [MenuItem("Tools/LTC/桌面 AI 助理/建立第二階段畫面擷取 Demo")]
    public static void BuildPhaseTwoDemo()
    {
        EnsureFolders();
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null) throw new FileNotFoundException("找不到中文 TMP 字型。", FontPath);

        GameObject contents = BuildPrefab(font);
        PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
        Object.DestroyImmediate(contents);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "DesktopCapturePhase2Demo";

        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(224, 237, 216, 255);
        cameraObject.tag = "MainCamera";

        GameObject lightObject = new GameObject("Directional Light", typeof(Light));
        lightObject.GetComponent<Light>().type = LightType.Directional;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        PrefabUtility.InstantiatePrefab(prefab, scene);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        Debug.Log("已建立第二階段畫面擷取 Demo：" + ScenePath);
    }

    static GameObject BuildPrefab(TMP_FontAsset font)
    {
        GameObject root = new GameObject("DesktopCapturePhase2Root");
        DesktopCaptureController controller = root.AddComponent<DesktopCaptureController>();

        GameObject canvasObject = new GameObject("CaptureCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(root.transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;

        GameObject panel = CreatePanel(canvasObject.transform, "CapturePanel", new Color32(255, 250, 238, 248));
        SetRect(panel.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
            new Vector2(.5f, .5f), new Vector2(1280f, 860f), Vector2.zero);

        TMP_Text title = CreateText(panel.transform, "Title", "第二階段・Windows 畫面擷取預覽",
            font, 38f, FontStyles.Bold, Ink, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
            new Vector2(.5f, 1f), new Vector2(1000f, 58f), new Vector2(0f, -22f));

        TMP_Text privacy = CreateText(panel.transform, "PrivacyNotice",
            "只有按下擷取後才會讀取畫面｜目前只在本機預覽，不會上傳或操作其他程式",
            font, 20f, FontStyles.Normal, Green, TextAlignmentOptions.Center);
        SetRect(privacy.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
            new Vector2(.5f, 1f), new Vector2(1100f, 42f), new Vector2(0f, -84f));

        TMP_Text sourceLabel = CreateText(panel.transform, "SourceLabel", "來源：尚未載入",
            font, 22f, FontStyles.Bold, Ink, TextAlignmentOptions.Left);
        SetRect(sourceLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(760f, 48f), new Vector2(62f, -126f));

        Button sourceButton = CreateButton(panel.transform, "SourceButton", "切換來源", font, Green);
        SetRect(sourceButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(1f, 1f), new Vector2(170f, 46f), new Vector2(-236f, -124f));
        Button refreshButton = CreateButton(panel.transform, "RefreshButton", "重新整理視窗", font, Orange);
        SetRect(refreshButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(1f, 1f), new Vector2(190f, 46f), new Vector2(-38f, -124f));

        GameObject previewFrame = CreatePanel(panel.transform, "PreviewFrame", new Color32(45, 49, 44, 255));
        SetRect(previewFrame.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
            new Vector2(.5f, .5f), new Vector2(1160f, 570f), new Vector2(0f, -18f));

        GameObject rawObject = new GameObject("DesktopPreview", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(RawImage), typeof(AspectRatioFitter));
        rawObject.transform.SetParent(previewFrame.transform, false);
        RectTransform rawRect = rawObject.GetComponent<RectTransform>();
        rawRect.anchorMin = new Vector2(.5f, .5f);
        rawRect.anchorMax = new Vector2(.5f, .5f);
        rawRect.pivot = new Vector2(.5f, .5f);
        rawRect.sizeDelta = new Vector2(1120f, 530f);
        RawImage preview = rawObject.GetComponent<RawImage>();
        preview.color = new Color32(86, 92, 84, 255);
        preview.raycastTarget = false;
        AspectRatioFitter aspect = rawObject.GetComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        aspect.aspectRatio = 16f / 9f;

        Button captureButton = CreateButton(panel.transform, "CaptureNowButton", "立即擷取", font, Orange);
        SetRect(captureButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(0f, 0f), new Vector2(200f, 54f), new Vector2(62f, 58f));
        Button autoButton = CreateButton(panel.transform, "AutoCaptureButton", "啟用定時擷取", font, Green);
        SetRect(autoButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(0f, 0f), new Vector2(240f, 54f), new Vector2(276f, 58f));
        TMP_Text autoLabel = autoButton.GetComponentInChildren<TMP_Text>(true);

        TMP_Text metrics = CreateText(panel.transform, "Metrics", "擷取 0｜更新 0｜略過 0",
            font, 20f, FontStyles.Normal, Ink, TextAlignmentOptions.Right);
        SetRect(metrics.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(1f, 0f), new Vector2(480f, 42f), new Vector2(-62f, 66f));

        TMP_Text status = CreateText(panel.transform, "Status", "尚未擷取。",
            font, 19f, FontStyles.Normal, Green, TextAlignmentOptions.Center);
        SetRect(status.rectTransform, new Vector2(.5f, 0f), new Vector2(.5f, 0f),
            new Vector2(.5f, 0f), new Vector2(1100f, 42f), new Vector2(0f, 12f));

        controller.Configure(preview, aspect, sourceLabel, status, metrics, sourceButton,
            refreshButton, captureButton, autoButton, autoLabel);
        return root;
    }

    static Button CreateButton(Transform parent, string name, string label, TMP_FontAsset font, Color color)
    {
        GameObject root = CreatePanel(parent, name, color);
        Button button = root.AddComponent<Button>();
        button.targetGraphic = root.GetComponent<Image>();
        TMP_Text text = CreateText(root.transform, "Label", label, font, 20f,
            FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        Stretch(text.rectTransform, 5f, 3f);
        return button;
    }

    static GameObject CreatePanel(Transform parent, string name, Color color)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        gameObject.transform.SetParent(parent, false);
        gameObject.GetComponent<Image>().color = color;
        return gameObject;
    }

    static TMP_Text CreateText(Transform parent, string name, string value, TMP_FontAsset font,
        float size, FontStyles style, Color color, TextAlignmentOptions alignment)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        gameObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = font;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        return text;
    }

    static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 size, Vector2 position)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    static void Stretch(RectTransform rect, float horizontal, float vertical)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(horizontal, vertical);
        rect.offsetMax = new Vector2(-horizontal, -vertical);
    }

    static void EnsureFolders()
    {
        string[] folders = { Root, Root + "/Resources", Root + "/Scenes" };
        foreach (string folder in folders)
            if (!AssetDatabase.IsValidFolder(folder))
            {
                string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            }
    }
}
