using System.IO;
using System.Linq;
using LTC.DesktopAssistant;
using LTC.DesktopAssistant.AI;
using LTC.DesktopAssistant.Capture;
using LTC.DesktopAssistant.Reminders;
using LTC.DesktopAssistant.Windows;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>建立第五階段透明置頂視窗、第六階段設定介面，以及獨立 Windows Build。</summary>
public static class DesktopOverlayPhase5And6SetupUtility
{
    const string Root5 = "Assets/new LTC/DesktopAssistant/Phase5";
    const string Root6 = "Assets/new LTC/DesktopAssistant/Phase6";
    const string SourceScene = "Assets/new LTC/DesktopAssistant/Phase4/Scenes/ProactiveReminderPhase4Demo.unity";
    const string Phase5Scene = Root5 + "/Scenes/DesktopOverlayPhase5Demo.unity";
    const string Phase6Scene = Root6 + "/Scenes/DesktopAssistantFinal.unity";
    const string OverlaySettingsPath = Root5 + "/Config/DesktopOverlaySettings.asset";
    const string AssistantPrefabPath = "Assets/new LTC/DesktopAssistant/Resources/DesktopAssistant.prefab";
    const string FontPath = "Assets/new LTC/Unity中文/KAIU_Dynamic.asset";
    const string BuildPath = "Builds/DesktopAssistant/LTCDesktopAssistant.exe";

    static readonly Color Cream = new Color32(255, 250, 238, 250);
    static readonly Color Mint = new Color32(195, 229, 207, 250);
    static readonly Color Green = new Color32(72, 160, 128, 255);
    static readonly Color Orange = new Color32(246, 149, 61, 255);
    static readonly Color Ink = new Color32(54, 58, 51, 255);

    [MenuItem("Tools/LTC/桌面 AI 助理/建立第五階段透明桌面 Demo")]
    public static void BuildPhaseFive()
    {
        EnsureFolder(Root5);
        EnsureFolder(Root5 + "/Config");
        EnsureFolder(Root5 + "/Scenes");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScene) == null)
            throw new FileNotFoundException("找不到第四階段場景。", SourceScene);

        DesktopOverlaySettings settings = AssetDatabase.LoadAssetAtPath<DesktopOverlaySettings>(OverlaySettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<DesktopOverlaySettings>();
            AssetDatabase.CreateAsset(settings, OverlaySettingsPath);
        }

        Scene scene = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
        Camera camera = Object.FindFirstObjectByType<Camera>();
        if (camera == null) throw new MissingReferenceException("找不到主攝影機。");
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.clear;

        HideDevelopmentPanel("CapturePanel");
        HideDevelopmentPanel("VisionAnalysisPanel");
        HideDevelopmentPanel("ReminderPolicyPanel");
        HideDevelopmentPanel("ActiveReminderPanel");

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssistantPrefabPath);
        if (prefab == null) throw new FileNotFoundException("找不到桌面助理 Prefab。", AssistantPrefabPath);
        GameObject assistantRoot = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        assistantRoot.name = "DesktopAssistantFinal";
        DesktopAssistantController assistant = assistantRoot.GetComponent<DesktopAssistantController>();
        RectTransform assistantAnchor = assistantRoot.GetComponentsInChildren<RectTransform>(true)
            .First(rect => rect.name == "AssistantAnchor");

        DesktopCaptureController capture = Object.FindFirstObjectByType<DesktopCaptureController>();
        VisionAnalysisController vision = Object.FindFirstObjectByType<VisionAnalysisController>();
        ProactiveReminderController reminder = Object.FindFirstObjectByType<ProactiveReminderController>();
        if (capture == null || vision == null || reminder == null)
            throw new MissingReferenceException("第四階段模組不完整。");
        reminder.SetLargePopupEnabled(false);

        GameObject host = new GameObject("DesktopOverlayRuntime");
        WindowsDesktopOverlayController overlay = host.AddComponent<WindowsDesktopOverlayController>();
        overlay.Configure(settings, camera, assistantAnchor);
        AssistantReminderBridge bridge = host.AddComponent<AssistantReminderBridge>();
        bridge.Configure(reminder, assistant);

        EditorSceneManager.SaveScene(scene, Phase5Scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(Phase5Scene);
        Debug.Log("已建立第五階段透明桌面 Demo：" + Phase5Scene);
    }

    [MenuItem("Tools/LTC/桌面 AI 助理/建立第六階段最終桌面版")]
    public static void BuildPhaseSix()
    {
        EnsureFolder(Root6);
        EnsureFolder(Root6 + "/Scenes");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Phase5Scene) == null) BuildPhaseFive();
        Scene scene = EditorSceneManager.OpenScene(Phase5Scene, OpenSceneMode.Single);
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        DesktopOverlaySettings settings = AssetDatabase.LoadAssetAtPath<DesktopOverlaySettings>(OverlaySettingsPath);
        if (font == null || settings == null) throw new MissingReferenceException("最終版缺少字型或設定檔。");

        WindowsDesktopOverlayController overlay = Object.FindFirstObjectByType<WindowsDesktopOverlayController>();
        DesktopCaptureController capture = Object.FindFirstObjectByType<DesktopCaptureController>();
        VisionAnalysisController vision = Object.FindFirstObjectByType<VisionAnalysisController>();
        RectTransform assistantAnchor = Object.FindFirstObjectByType<DesktopAssistantController>()
            .GetComponentsInChildren<RectTransform>(true).First(rect => rect.name == "AssistantAnchor");

        GameObject canvasObject = new GameObject("FinalSettingsCanvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = .5f;

        Button openButton = CreateButton(canvasObject.transform, "SettingsOpenButton", "設定", font, Green);
        SetRect(openButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(1f, 1f), new Vector2(150f, 54f), new Vector2(-24f, -24f));

        GameObject panel = CreatePanel(canvasObject.transform, "SettingsPanel", Cream);
        SetRect(panel.GetComponent<RectTransform>(), new Vector2(1f, .5f), new Vector2(1f, .5f),
            new Vector2(1f, .5f), new Vector2(510f, 900f), new Vector2(-24f, 0f));
        TMP_Text title = CreateText(panel.transform, "Title", "智慧寵物助理設定", font, 29f,
            FontStyles.Bold, Ink, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
            new Vector2(.5f, 1f), new Vector2(460f, 56f), new Vector2(0f, -24f));

        Toggle topMost = CreateToggle(panel.transform, "TopMost", "永遠顯示在最上層", font, -100f);
        Toggle clickThrough = CreateToggle(panel.transform, "ClickThrough", "透明區域允許點擊穿透", font, -160f);
        Toggle automatic = CreateToggle(panel.transform, "AutomaticAnalysis", "每 5 秒檢查變化並自動分析", font, -220f);
        Toggle mock = CreateToggle(panel.transform, "MockService", "使用本機 Mock（不傳送圖片）", font, -280f);

        TMP_Text fpsTitle = CreateText(panel.transform, "FpsTitle", "目標幀率", font, 19f,
            FontStyles.Bold, Ink, TextAlignmentOptions.Left);
        SetRect(fpsTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(220f, 38f), new Vector2(28f, -344f));
        Slider fpsSlider = CreateSlider(panel.transform, "FrameRate", 15f, 60f, true, -392f);
        TMP_Text fpsValue = CreateText(panel.transform, "FrameRateValue", "30 FPS", font, 18f,
            FontStyles.Bold, Green, TextAlignmentOptions.Right);
        SetRect(fpsValue.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(1f, 1f), new Vector2(130f, 38f), new Vector2(-28f, -344f));

        TMP_Text scaleTitle = CreateText(panel.transform, "ScaleTitle", "角色大小", font, 19f,
            FontStyles.Bold, Ink, TextAlignmentOptions.Left);
        SetRect(scaleTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(220f, 38f), new Vector2(28f, -450f));
        Slider scaleSlider = CreateSlider(panel.transform, "AssistantScale", .6f, 1.5f, false, -498f);
        TMP_Text scaleValue = CreateText(panel.transform, "ScaleValue", "100%", font, 18f,
            FontStyles.Bold, Green, TextAlignmentOptions.Right);
        SetRect(scaleValue.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(1f, 1f), new Vector2(130f, 38f), new Vector2(-28f, -450f));

        GameObject healthBox = CreatePanel(panel.transform, "HealthBox", Mint);
        SetRect(healthBox.GetComponent<RectTransform>(), new Vector2(.5f, 1f), new Vector2(.5f, 1f),
            new Vector2(.5f, 1f), new Vector2(454f, 110f), new Vector2(0f, -570f));
        TMP_Text healthText = CreateText(healthBox.transform, "HealthText", "正在讀取執行狀態…", font,
            16f, FontStyles.Normal, Ink, TextAlignmentOptions.Center);
        Stretch(healthText.rectTransform, 12f, 10f);

        Button resetButton = CreateButton(panel.transform, "ResetPosition", "重設角色位置", font, Orange);
        SetRect(resetButton.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(0f, 0f), new Vector2(210f, 54f), new Vector2(28f, 96f));
        Button quitButton = CreateButton(panel.transform, "Quit", "結束助理", font, new Color32(170, 82, 72, 255));
        SetRect(quitButton.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(1f, 0f), new Vector2(210f, 54f), new Vector2(-28f, 96f));
        Button closeButton = CreateButton(panel.transform, "Close", "儲存並關閉", font, Green);
        SetRect(closeButton.GetComponent<RectTransform>(), new Vector2(.5f, 0f), new Vector2(.5f, 0f),
            new Vector2(.5f, 0f), new Vector2(454f, 56f), new Vector2(0f, 26f));

        GameObject host = overlay.gameObject;
        RuntimeHealthMonitor health = host.AddComponent<RuntimeHealthMonitor>();
        health.Configure(healthText);
        DesktopAssistantSettingsPanel controller = host.AddComponent<DesktopAssistantSettingsPanel>();
        controller.Configure(settings, overlay, capture, vision, assistantAnchor, panel, openButton,
            closeButton, resetButton, quitButton, topMost, clickThrough, automatic, mock,
            fpsSlider, fpsValue, scaleSlider, scaleValue);
        UnityEventTools.AddPersistentListener(openButton.onClick, controller.Open);
        UnityEventTools.AddPersistentListener(closeButton.onClick, controller.Close);
        UnityEventTools.AddPersistentListener(resetButton.onClick, controller.ResetPosition);
        UnityEventTools.AddPersistentListener(quitButton.onClick, controller.QuitApplication);
        UnityEventTools.AddPersistentListener(topMost.onValueChanged, overlay.SetAlwaysOnTop);
        UnityEventTools.AddPersistentListener(clickThrough.onValueChanged, overlay.SetClickThroughOutside);
        UnityEventTools.AddPersistentListener(automatic.onValueChanged, controller.SetAutomaticAnalysis);
        UnityEventTools.AddPersistentListener(mock.onValueChanged, vision.SetUseMockService);
        UnityEventTools.AddPersistentListener(fpsSlider.onValueChanged, controller.SetFrameRate);
        UnityEventTools.AddPersistentListener(scaleSlider.onValueChanged, controller.SetScale);
        overlay.SetInteractiveRegions(assistantAnchor, openButton.GetComponent<RectTransform>(),
            panel.GetComponent<RectTransform>());
        panel.SetActive(false);

        EditorSceneManager.SaveScene(scene, Phase6Scene);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(Phase6Scene);
        Debug.Log("已建立第六階段最終桌面版：" + Phase6Scene);
    }

    [MenuItem("Tools/LTC/桌面 AI 助理/建置 Windows 最終版")]
    public static void BuildWindowsFinal()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Phase6Scene) == null) BuildPhaseSix();
        string absoluteOutput = Path.GetFullPath(BuildPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absoluteOutput));
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { Phase6Scene },
            locationPathName = absoluteOutput,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new BuildFailedException("桌面 AI 助理 Windows Build 失敗：" + report.summary.result);
        Debug.Log("Windows 最終版建置完成：" + absoluteOutput + "（" +
                  (report.summary.totalSize / 1048576f).ToString("0.0") + " MB）");
    }

    static void HideDevelopmentPanel(string name)
    {
        GameObject value = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(item => item.name == name);
        if (value != null) value.SetActive(false);
    }

    static Toggle CreateToggle(Transform parent, string name, string label, TMP_FontAsset font, float y)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Toggle));
        root.transform.SetParent(parent, false);
        SetRect(root.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(.5f, 1f), new Vector2(-56f, 48f), new Vector2(0f, y));
        Image background = CreatePanel(root.transform, "Background", Color.white).GetComponent<Image>();
        SetRect(background.rectTransform, new Vector2(0f, .5f), new Vector2(0f, .5f),
            new Vector2(0f, .5f), new Vector2(34f, 34f), Vector2.zero);
        Image check = CreatePanel(background.transform, "Checkmark", Green).GetComponent<Image>();
        Stretch(check.rectTransform, 7f, 7f);
        TMP_Text text = CreateText(root.transform, "Label", label, font, 18f,
            FontStyles.Normal, Ink, TextAlignmentOptions.MidlineLeft);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(50f, 0f);
        text.rectTransform.offsetMax = Vector2.zero;
        Toggle toggle = root.GetComponent<Toggle>();
        toggle.targetGraphic = background;
        toggle.graphic = check;
        return toggle;
    }

    static Slider CreateSlider(Transform parent, string name, float min, float max, bool whole, float y)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Slider));
        root.transform.SetParent(parent, false);
        SetRect(root.GetComponent<RectTransform>(), new Vector2(.5f, 1f), new Vector2(.5f, 1f),
            new Vector2(.5f, 1f), new Vector2(454f, 32f), new Vector2(0f, y));
        Image background = CreatePanel(root.transform, "Background", new Color32(200, 205, 195, 255)).GetComponent<Image>();
        SetRect(background.rectTransform, new Vector2(0f, .5f), new Vector2(1f, .5f),
            new Vector2(.5f, .5f), new Vector2(0f, 8f), Vector2.zero);
        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(root.transform, false);
        Stretch(fillArea.GetComponent<RectTransform>(), 8f, 10f);
        Image fill = CreatePanel(fillArea.transform, "Fill", Green).GetComponent<Image>();
        Stretch(fill.rectTransform, 0f, 0f);
        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(root.transform, false);
        Stretch(handleArea.GetComponent<RectTransform>(), 8f, 0f);
        Image handle = CreatePanel(handleArea.transform, "Handle", Orange).GetComponent<Image>();
        SetRect(handle.rectTransform, new Vector2(0f, .5f), new Vector2(0f, .5f),
            new Vector2(.5f, .5f), new Vector2(26f, 26f), Vector2.zero);
        Slider slider = root.GetComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = whole;
        return slider;
    }

    static Button CreateButton(Transform parent, string name, string label, TMP_FontAsset font, Color color)
    {
        GameObject root = CreatePanel(parent, name, color);
        Button button = root.AddComponent<Button>();
        button.targetGraphic = root.GetComponent<Image>();
        TMP_Text text = CreateText(root.transform, "Label", label, font, 18f,
            FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        Stretch(text.rectTransform, 5f, 3f);
        return button;
    }

    static GameObject CreatePanel(Transform parent, string name, Color color)
    {
        GameObject value = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        value.transform.SetParent(parent, false);
        value.GetComponent<Image>().color = color;
        return value;
    }

    static TMP_Text CreateText(Transform parent, string name, string value, TMP_FontAsset font,
        float size, FontStyles style, Color color, TextAlignmentOptions alignment)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        root.transform.SetParent(parent, false);
        TextMeshProUGUI text = root.GetComponent<TextMeshProUGUI>();
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

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
