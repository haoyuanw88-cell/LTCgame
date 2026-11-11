using System.IO;
using LTC.DesktopAssistant.AI;
using LTC.DesktopAssistant.Reminders;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>由第三階段 Demo 建立第四階段主動提醒驗收場景，不覆寫既有場景。</summary>
public static class ProactiveReminderPhase4SetupUtility
{
    const string Root = "Assets/new LTC/DesktopAssistant/Phase4";
    const string SourceScenePath = "Assets/new LTC/DesktopAssistant/Phase3/Scenes/VisionAnalysisPhase3Demo.unity";
    const string ScenePath = Root + "/Scenes/ProactiveReminderPhase4Demo.unity";
    const string SettingsPath = Root + "/Config/ProactiveReminderSettings.asset";
    const string FontPath = "Assets/new LTC/Unity中文/KAIU_Dynamic.asset";

    static readonly Color Cream = new Color32(255, 250, 238, 250);
    static readonly Color Mint = new Color32(195, 229, 207, 255);
    static readonly Color Green = new Color32(72, 160, 128, 255);
    static readonly Color Orange = new Color32(246, 149, 61, 255);
    static readonly Color Ink = new Color32(54, 58, 51, 255);

    [MenuItem("Tools/LTC/桌面 AI 助理/建立第四階段主動提醒 Demo")]
    public static void BuildPhaseFourDemo()
    {
        EnsureFolders();
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null) throw new FileNotFoundException("找不到中文 TMP 字型。", FontPath);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath) == null)
            throw new FileNotFoundException("請先建立第三階段 Demo。", SourceScenePath);

        ProactiveReminderSettings settings =
            AssetDatabase.LoadAssetAtPath<ProactiveReminderSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<ProactiveReminderSettings>();
            settings.idleThresholdSeconds = 60f;
            settings.globalCooldownSeconds = 10f;
            settings.sameReminderCooldownSeconds = 60f;
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }

        Scene scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        VisionAnalysisController vision = Object.FindFirstObjectByType<VisionAnalysisController>();
        if (canvas == null || vision == null)
            throw new MissingReferenceException("第三階段場景缺少 Canvas 或視覺分析控制器。");

        Transform capturePanel = canvas.transform.Find("CapturePanel");
        Transform analysisPanel = canvas.transform.Find("VisionAnalysisPanel");
        if (capturePanel != null)
        {
            GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(capturePanel.gameObject);
            if (prefabRoot != null)
                PrefabUtility.UnpackPrefabInstance(prefabRoot,
                    PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }
        RectTransform scrollContent = CreateHorizontalTestScroll(canvas.transform);
        if (capturePanel != null) capturePanel.SetParent(scrollContent, false);
        if (analysisPanel != null) analysisPanel.SetParent(scrollContent, false);
        ScaleAndMove(capturePanel, .58f, new Vector2(-650f, 0f));
        ScaleAndMove(analysisPanel, .64f, new Vector2(-65f, 0f));

        GameObject policyPanel = CreatePanel(scrollContent, "ReminderPolicyPanel", Cream);
        SetRect(policyPanel.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
            new Vector2(.5f, .5f), new Vector2(550f, 860f), new Vector2(660f, 0f));

        TMP_Text title = CreateText(policyPanel.transform, "Title", "智慧主動提醒", font,
            30f, FontStyles.Bold, Ink, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
            new Vector2(.5f, 1f), new Vector2(500f, 52f), new Vector2(0f, -24f));

        TMP_Text description = CreateText(policyPanel.transform, "Description",
            "掛機判斷採用 Windows 全域輸入；不確定或低可信度的視覺結果不會主動打擾玩家。", font,
            18f, FontStyles.Normal, Ink, TextAlignmentOptions.TopLeft);
        SetRect(description.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
            new Vector2(.5f, 1f), new Vector2(490f, 90f), new Vector2(0f, -88f));

        GameObject statusBox = CreatePanel(policyPanel.transform, "PolicyStatusBox", Mint);
        SetRect(statusBox.GetComponent<RectTransform>(), new Vector2(.5f, 1f), new Vector2(.5f, 1f),
            new Vector2(.5f, 1f), new Vector2(490f, 112f), new Vector2(0f, -190f));
        TMP_Text policyStatus = CreateText(statusBox.transform, "PolicyStatus", "等待事件", font,
            17f, FontStyles.Bold, Ink, TextAlignmentOptions.Center);
        Stretch(policyStatus.rectTransform, 14f, 10f);

        Button trustedButton = CreateButton(policyPanel.transform, "SimulateTrustedEvent",
            "模擬可信事件", font, Orange);
        SetRect(trustedButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(232f, 54f), new Vector2(30f, -330f));
        Button idleButton = CreateButton(policyPanel.transform, "SimulateIdle",
            "模擬掛機", font, Green);
        SetRect(idleButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(1f, 1f), new Vector2(232f, 54f), new Vector2(-30f, -330f));

        TMP_Text historyTitle = CreateText(policyPanel.transform, "HistoryTitle", "提醒與抑制紀錄", font,
            21f, FontStyles.Bold, Ink, TextAlignmentOptions.Left);
        SetRect(historyTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(480f, 38f), new Vector2(30f, -410f));
        GameObject historyBox = CreatePanel(policyPanel.transform, "HistoryBox", new Color32(255, 255, 255, 215));
        SetRect(historyBox.GetComponent<RectTransform>(), new Vector2(.5f, 0f), new Vector2(.5f, 0f),
            new Vector2(.5f, 0f), new Vector2(490f, 370f), new Vector2(0f, 30f));
        TMP_Text history = CreateText(historyBox.transform, "History", "尚無提醒紀錄。", font,
            16f, FontStyles.Normal, Ink, TextAlignmentOptions.TopLeft);
        Stretch(history.rectTransform, 16f, 14f);

        GameObject reminderPanel = CreatePanel(canvas.transform, "ActiveReminderPanel",
            new Color32(40, 45, 38, 238));
        SetRect(reminderPanel.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
            new Vector2(.5f, .5f), new Vector2(700f, 330f), Vector2.zero);
        TMP_Text reminderTitle = CreateText(reminderPanel.transform, "ReminderTitle", "寵物主動提醒", font,
            28f, FontStyles.Bold, new Color32(255, 224, 142, 255), TextAlignmentOptions.Center);
        SetRect(reminderTitle.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
            new Vector2(.5f, 1f), new Vector2(650f, 50f), new Vector2(0f, -20f));
        TMP_Text reminderText = CreateText(reminderPanel.transform, "ReminderText", string.Empty, font,
            20f, FontStyles.Normal, Color.white, TextAlignmentOptions.Center);
        SetRect(reminderText.rectTransform, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
            new Vector2(.5f, .5f), new Vector2(640f, 180f), new Vector2(0f, 12f));
        Button dismissButton = CreateButton(reminderPanel.transform, "Dismiss", "我知道了", font, Green);
        SetRect(dismissButton.GetComponent<RectTransform>(), new Vector2(.5f, 0f), new Vector2(.5f, 0f),
            new Vector2(.5f, 0f), new Vector2(220f, 52f), new Vector2(0f, 20f));

        GameObject host = vision.gameObject;
        WindowsUserIdleMonitor idleMonitor = host.GetComponent<WindowsUserIdleMonitor>();
        if (idleMonitor == null) idleMonitor = host.AddComponent<WindowsUserIdleMonitor>();
        ProactiveReminderController reminderController = host.GetComponent<ProactiveReminderController>();
        if (reminderController == null) reminderController = host.AddComponent<ProactiveReminderController>();
        reminderController.Configure(settings, vision, idleMonitor, reminderPanel, reminderTitle, reminderText,
            policyStatus, history, dismissButton, trustedButton, idleButton);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        Debug.Log("已建立第四階段主動提醒 Demo：" + ScenePath);
    }

    static void ScaleAndMove(Transform target, float scale, Vector2 position)
    {
        if (target == null) return;
        RectTransform rect = target.GetComponent<RectTransform>();
        rect.localScale = Vector3.one * scale;
        rect.anchoredPosition = position;
    }

    /// <summary>
    /// 建立橫向測試工作區。ScrollRect 在只啟用 horizontal 時，會把一般滑鼠滾輪的 Y 軸
    /// 自動轉成水平位移；底部 Scrollbar 也能直接拖曳，方便小解析度操作所有按鈕。
    /// </summary>
    static RectTransform CreateHorizontalTestScroll(Transform canvas)
    {
        GameObject root = CreatePanel(canvas, "HorizontalTestScroll", new Color32(0, 0, 0, 0));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        Stretch(rootRect, 12f, 12f);
        Image rootImage = root.GetComponent<Image>();
        rootImage.raycastTarget = true;

        ScrollRect scroll = root.AddComponent<ScrollRect>();
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = true;
        scroll.decelerationRate = .12f;
        scroll.scrollSensitivity = 85f;

        GameObject viewport = CreatePanel(root.transform, "Viewport", new Color32(0, 0, 0, 0));
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(0f, 46f);
        viewportRect.offsetMax = Vector2.zero;
        viewport.AddComponent<RectMask2D>();
        viewport.GetComponent<Image>().raycastTarget = true;

        GameObject contentObject = new GameObject("Content", typeof(RectTransform));
        contentObject.transform.SetParent(viewport.transform, false);
        RectTransform content = contentObject.GetComponent<RectTransform>();
        SetRect(content, new Vector2(.5f, .5f), new Vector2(.5f, .5f),
            new Vector2(.5f, .5f), new Vector2(2100f, 960f), Vector2.zero);

        GameObject scrollbarObject = CreatePanel(root.transform, "HorizontalScrollbar",
            new Color32(90, 105, 82, 115));
        RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
        scrollbarRect.anchorMin = new Vector2(0f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 0f);
        scrollbarRect.pivot = new Vector2(.5f, 0f);
        scrollbarRect.sizeDelta = new Vector2(-190f, 24f);
        scrollbarRect.anchoredPosition = new Vector2(0f, 6f);
        Scrollbar scrollbar = scrollbarObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.LeftToRight;

        GameObject slidingArea = new GameObject("Sliding Area", typeof(RectTransform));
        slidingArea.transform.SetParent(scrollbarObject.transform, false);
        Stretch(slidingArea.GetComponent<RectTransform>(), 8f, 3f);
        Image handle = CreatePanel(slidingArea.transform, "Handle", Green).GetComponent<Image>();
        RectTransform handleRect = handle.rectTransform;
        SetRect(handleRect, new Vector2(0f, 0f), new Vector2(.35f, 1f),
            new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        scrollbar.handleRect = handleRect;
        scrollbar.targetGraphic = handle;

        scroll.viewport = viewportRect;
        scroll.content = content;
        scroll.horizontalScrollbar = scrollbar;
        scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
        scroll.horizontalScrollbarSpacing = 4f;
        return content;
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
        string[] folders = { Root, Root + "/Config", Root + "/Scenes" };
        foreach (string folder in folders)
            if (!AssetDatabase.IsValidFolder(folder))
            {
                string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            }
    }
}
