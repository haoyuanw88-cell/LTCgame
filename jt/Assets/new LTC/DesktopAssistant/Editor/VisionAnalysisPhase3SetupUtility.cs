using System.IO;
using LTC.DesktopAssistant.AI;
using LTC.DesktopAssistant.Capture;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 以第二階段擷取 Demo 為基礎建立第三階段結構化分析介面。
/// 原始第二階段場景不會被覆寫。
/// </summary>
public static class VisionAnalysisPhase3SetupUtility
{
    const string Root = "Assets/new LTC/DesktopAssistant/Phase3";
    const string SourceScenePath = "Assets/new LTC/DesktopAssistant/Phase2/Scenes/DesktopCapturePhase2Demo.unity";
    const string ScenePath = Root + "/Scenes/VisionAnalysisPhase3Demo.unity";
    const string SettingsPath = Root + "/Config/VisionAnalysisSettings.asset";
    const string FontPath = "Assets/new LTC/Unity中文/KAIU_Dynamic.asset";

    static readonly Color Cream = new Color32(255, 250, 238, 255);
    static readonly Color Mint = new Color32(195, 229, 207, 255);
    static readonly Color Green = new Color32(72, 160, 128, 255);
    static readonly Color Orange = new Color32(246, 149, 61, 255);
    static readonly Color Ink = new Color32(54, 58, 51, 255);

    [MenuItem("Tools/LTC/桌面 AI 助理/建立第三階段視覺分析 Demo")]
    public static void BuildPhaseThreeDemo()
    {
        EnsureFolders();
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null) throw new FileNotFoundException("找不到中文 TMP 字型。", FontPath);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath) == null)
            throw new FileNotFoundException("請先建立第二階段擷取 Demo。", SourceScenePath);

        VisionAnalysisSettings settings = AssetDatabase.LoadAssetAtPath<VisionAnalysisSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<VisionAnalysisSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }

        Scene scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
        DesktopCaptureController capture = Object.FindFirstObjectByType<DesktopCaptureController>();
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (capture == null || canvas == null)
            throw new MissingReferenceException("第二階段場景缺少擷取控制器或 Canvas。");

        Transform capturePanel = canvas.transform.Find("CapturePanel");
        if (capturePanel != null)
        {
            RectTransform rect = capturePanel.GetComponent<RectTransform>();
            rect.localScale = Vector3.one * .74f;
            rect.anchoredPosition = new Vector2(-430f, 0f);
        }

        GameObject analysisPanel = CreatePanel(canvas.transform, "VisionAnalysisPanel",
            new Color32(255, 250, 238, 250));
        SetRect(analysisPanel.GetComponent<RectTransform>(), new Vector2(.5f, .5f), new Vector2(.5f, .5f),
            new Vector2(.5f, .5f), new Vector2(760f, 860f), new Vector2(540f, 0f));

        TMP_Text title = CreateText(analysisPanel.transform, "AnalysisTitle", "第三階段・AI 結構化分析",
            font, 31f, FontStyles.Bold, Ink, TextAlignmentOptions.Center);
        SetRect(title.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
            new Vector2(.5f, 1f), new Vector2(700f, 50f), new Vector2(0f, -20f));

        TMP_Text serviceMode = CreateText(analysisPanel.transform, "ServiceMode", "服務：本機 Mock JSON",
            font, 20f, FontStyles.Bold, Green, TextAlignmentOptions.Left);
        SetRect(serviceMode.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(430f, 40f), new Vector2(30f, -76f));
        Button serviceButton = CreateButton(analysisPanel.transform, "ServiceModeButton", "切換 Mock／後端", font, Green);
        SetRect(serviceButton.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(1f, 1f), new Vector2(230f, 44f), new Vector2(-28f, -72f));

        Button analyzeButton = CreateButton(analysisPanel.transform, "AnalyzeButton", "分析最新畫面", font, Orange);
        SetRect(analyzeButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(220f, 50f), new Vector2(30f, -124f));
        Button automaticButton = CreateButton(analysisPanel.transform, "AutomaticAnalysisButton", "啟用自動分析", font, Green);
        SetRect(automaticButton.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(240f, 50f), new Vector2(264f, -124f));
        TMP_Text automaticLabel = automaticButton.GetComponentInChildren<TMP_Text>(true);

        TMP_Text status = CreateText(analysisPanel.transform, "AnalysisStatus", "請先擷取畫面。",
            font, 18f, FontStyles.Normal, Green, TextAlignmentOptions.Center);
        SetRect(status.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
            new Vector2(.5f, 1f), new Vector2(700f, 54f), new Vector2(0f, -184f));

        GameObject summaryPanel = CreatePanel(analysisPanel.transform, "SummaryPanel", Mint);
        SetRect(summaryPanel.GetComponent<RectTransform>(), new Vector2(.5f, 1f), new Vector2(.5f, 1f),
            new Vector2(.5f, 1f), new Vector2(700f, 125f), new Vector2(0f, -244f));
        TMP_Text summary = CreateText(summaryPanel.transform, "Summary", "尚無分析結果。",
            font, 20f, FontStyles.Bold, Ink, TextAlignmentOptions.Center);
        Stretch(summary.rectTransform, 18f, 12f);

        TMP_Text detailsTitle = CreateText(analysisPanel.transform, "DetailsTitle", "結構化結果",
            font, 21f, FontStyles.Bold, Ink, TextAlignmentOptions.Left);
        SetRect(detailsTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(330f, 36f), new Vector2(30f, -380f));
        TMP_Text jsonTitle = CreateText(analysisPanel.transform, "JsonTitle", "原始 JSON",
            font, 21f, FontStyles.Bold, Ink, TextAlignmentOptions.Left);
        SetRect(jsonTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(330f, 36f), new Vector2(395f, -380f));

        ScrollRect detailsScroll = CreateScrollText(analysisPanel.transform, "DetailsScroll", font,
            17f, out TMP_Text details);
        SetRect(detailsScroll.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(0f, 0f), new Vector2(335f, 410f), new Vector2(30f, 30f));
        ScrollRect jsonScroll = CreateScrollText(analysisPanel.transform, "JsonScroll", font,
            14f, out TMP_Text rawJson);
        SetRect(jsonScroll.GetComponent<RectTransform>(), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(1f, 0f), new Vector2(335f, 410f), new Vector2(-30f, 30f));

        VisionAnalysisController controller = capture.gameObject.AddComponent<VisionAnalysisController>();
        controller.Configure(settings, capture, serviceMode, status, summary, details, rawJson,
            analyzeButton, automaticButton, automaticLabel, serviceButton);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        Debug.Log("已建立第三階段視覺分析 Demo：" + ScenePath);
    }

    static ScrollRect CreateScrollText(Transform parent, string name, TMP_FontAsset font,
        float fontSize, out TMP_Text contentText)
    {
        GameObject root = CreatePanel(parent, name, new Color32(255, 255, 255, 215));
        ScrollRect scroll = root.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        GameObject viewport = CreatePanel(root.transform, "Viewport", Color.clear);
        Stretch(viewport.GetComponent<RectTransform>(), 7f, 7f);
        viewport.AddComponent<RectMask2D>();
        contentText = CreateText(viewport.transform, "Content", string.Empty, font, fontSize,
            FontStyles.Normal, Ink, TextAlignmentOptions.TopLeft);
        RectTransform content = contentText.rectTransform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(.5f, 1f);
        content.sizeDelta = new Vector2(0f, 200f);
        ContentSizeFitter fitter = contentText.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = content;
        return scroll;
    }

    static Button CreateButton(Transform parent, string name, string label, TMP_FontAsset font, Color color)
    {
        GameObject root = CreatePanel(parent, name, color);
        Button button = root.AddComponent<Button>();
        button.targetGraphic = root.GetComponent<Image>();
        TMP_Text text = CreateText(root.transform, "Label", label, font, 18f,
            FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        Stretch(text.rectTransform, 4f, 2f);
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
