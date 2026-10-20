using System.IO;
using LTC.DesktopAssistant;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 建立第一階段測試用 Prefab、設定檔與獨立場景。
/// 只寫入 DesktopAssistant 資料夾，不會修改現有遊戲場景或 Build Settings。
/// </summary>
public static class DesktopAssistantSetupUtility
{
    const string Root = "Assets/new LTC/DesktopAssistant";
    const string SettingsPath = Root + "/Config/DesktopAssistantSettings.asset";
    const string PrefabPath = Root + "/Resources/DesktopAssistant.prefab";
    const string ScenePath = Root + "/Scenes/DesktopAssistantDemo.unity";
    const string FontPath = "Assets/new LTC/Unity中文/KAIU_Dynamic.asset";

    static readonly Color Cream = new Color32(255, 250, 238, 255);
    static readonly Color Mint = new Color32(195, 229, 207, 255);
    static readonly Color Green = new Color32(72, 160, 128, 255);
    static readonly Color Orange = new Color32(246, 149, 61, 255);
    static readonly Color Ink = new Color32(54, 58, 51, 255);

    [MenuItem("Tools/LTC/桌面 AI 助理/建立第一階段 Demo")]
    public static void BuildPhaseOneDemo()
    {
        EnsureFolders();
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null)
            throw new FileNotFoundException("找不到中文 TMP 字型。", FontPath);

        AssistantSettings settings = AssetDatabase.LoadAssetAtPath<AssistantSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<AssistantSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }

        GameObject prefabRoot = BuildPrefabContents(settings, font);
        PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
        Object.DestroyImmediate(prefabRoot);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "DesktopAssistantDemo";

        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(230, 239, 222, 255);
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
        Debug.Log("已建立桌面 AI 助理第一階段 Demo：" + ScenePath);
    }

    static GameObject BuildPrefabContents(AssistantSettings settings, TMP_FontAsset font)
    {
        GameObject root = new GameObject("DesktopAssistantRoot");
        DesktopAssistantController controller = root.AddComponent<DesktopAssistantController>();
        PetCatalogAdapter catalog = root.AddComponent<PetCatalogAdapter>();

        GameObject canvasObject = new GameObject("AssistantCanvas", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(root.transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();

        GameObject anchor = CreatePanel(canvasObject.transform, "AssistantAnchor", Color.clear);
        RectTransform anchorRect = anchor.GetComponent<RectTransform>();
        SetRect(anchorRect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(390f, 450f), new Vector2(-40f, 40f));
        anchor.GetComponent<Image>().raycastTarget = true;
        AssistantDragHandler drag = anchor.AddComponent<AssistantDragHandler>();

        GameObject bubble = CreatePanel(anchor.transform, "DialogueBubble", Color.white);
        SetRect(bubble.GetComponent<RectTransform>(), new Vector2(.5f, 0f), new Vector2(.5f, 0f),
            new Vector2(.5f, 0f), new Vector2(340f, 96f), new Vector2(0f, 340f));
        TMP_Text bubbleText = CreateText(bubble.transform, "BubbleText", "", font, 21f,
            FontStyles.Normal, Ink, TextAlignmentOptions.Center);
        Stretch(bubbleText.rectTransform, 16f, 12f);

        GameObject characterObject = new GameObject("CharacterImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        characterObject.transform.SetParent(anchor.transform, false);
        Image characterImage = characterObject.GetComponent<Image>();
        characterImage.preserveAspect = true;
        characterImage.raycastTarget = true;
        Button characterButton = characterObject.AddComponent<Button>();
        characterButton.targetGraphic = characterImage;
        SetRect(characterObject.GetComponent<RectTransform>(), new Vector2(.5f, 0f), new Vector2(.5f, 0f),
            new Vector2(.5f, 0f), new Vector2(250f, 250f), new Vector2(65f, 0f));

        TMP_Text effect = CreateText(anchor.transform, "StateEffect", "", font, 58f,
            FontStyles.Bold, Orange, TextAlignmentOptions.Center);
        SetRect(effect.rectTransform, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(.5f, 0f),
            new Vector2(90f, 90f), new Vector2(140f, 180f));
        effect.raycastTarget = false;

        AssistantCharacterView character = anchor.AddComponent<AssistantCharacterView>();
        character.Configure(characterImage, bubble, bubbleText, effect);
        drag.Configure(anchorRect, canvasRect, canvas);

        GameObject statePanel = CreatePanel(anchor.transform, "StateMenuPanel", new Color32(195, 229, 207, 245));
        SetRect(statePanel.GetComponent<RectTransform>(), new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(0f, 0f), new Vector2(150f, 158f), new Vector2(0f, 58f));
        CreateStateButton(statePanel.transform, "Idle", "待機", font, 108f, controller.SetIdleState);
        CreateStateButton(statePanel.transform, "Talking", "說話", font, 58f, controller.SetTalkingState);
        CreateStateButton(statePanel.transform, "Reminder", "提醒", font, 8f, controller.SetReminderState);

        AssistantStateMenu stateMenu = anchor.AddComponent<AssistantStateMenu>();
        stateMenu.Configure(statePanel);
        UnityEventTools.AddPersistentListener(characterButton.onClick, stateMenu.Toggle);

        controller.Configure(settings, catalog, character, drag, stateMenu);
        return root;
    }

    static void CreateStateButton(Transform parent, string name, string label, TMP_FontAsset font,
        float y, UnityEngine.Events.UnityAction callback)
    {
        Button button = CreateButton(parent, name, label, font, Green);
        SetRect(button.GetComponent<RectTransform>(), new Vector2(.5f, 0f), new Vector2(.5f, 0f),
            new Vector2(.5f, 0f), new Vector2(134f, 42f), new Vector2(0f, y));
        UnityEventTools.AddPersistentListener(button.onClick, callback);
    }

    static TMP_Dropdown CreateDropdown(Transform parent, TMP_FontAsset font)
    {
        GameObject root = CreatePanel(parent, "PetSelector", Mint);
        TMP_Dropdown dropdown = root.AddComponent<TMP_Dropdown>();

        TMP_Text label = CreateText(root.transform, "Label", "選擇寵物", font, 20f,
            FontStyles.Normal, Ink, TextAlignmentOptions.Center);
        SetRect(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(.5f, .5f),
            Vector2.zero, new Vector2(-12f, 0f));
        label.rectTransform.offsetMin = new Vector2(12f, 4f);
        label.rectTransform.offsetMax = new Vector2(-42f, -4f);

        TMP_Text arrow = CreateText(root.transform, "Arrow", "▼", font, 18f,
            FontStyles.Bold, Ink, TextAlignmentOptions.Center);
        SetRect(arrow.rectTransform, new Vector2(1f, .5f), new Vector2(1f, .5f), new Vector2(1f, .5f),
            new Vector2(38f, 38f), new Vector2(-2f, 0f));

        GameObject template = CreatePanel(root.transform, "Template", Cream);
        RectTransform templateRect = template.GetComponent<RectTransform>();
        SetRect(templateRect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(.5f, 1f),
            new Vector2(0f, 150f), new Vector2(0f, 2f));
        template.AddComponent<CanvasGroup>();
        ScrollRect scroll = template.AddComponent<ScrollRect>();
        scroll.horizontal = false;

        GameObject viewport = CreatePanel(template.transform, "Viewport", new Color32(255, 250, 238, 255));
        Stretch(viewport.GetComponent<RectTransform>(), 2f, 2f);
        viewport.AddComponent<RectMask2D>();

        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(.5f, 1f);
        contentRect.sizeDelta = new Vector2(0f, 34f);

        GameObject item = new GameObject("Item", typeof(RectTransform), typeof(Toggle));
        item.transform.SetParent(content.transform, false);
        RectTransform itemRect = item.GetComponent<RectTransform>();
        itemRect.anchorMin = new Vector2(0f, .5f);
        itemRect.anchorMax = new Vector2(1f, .5f);
        itemRect.sizeDelta = new Vector2(0f, 34f);

        Image itemBackground = CreatePanel(item.transform, "Item Background", Color.clear).GetComponent<Image>();
        Stretch(itemBackground.rectTransform, 0f, 0f);
        TMP_Text checkmark = CreateText(item.transform, "Item Checkmark", "✓", font, 18f,
            FontStyles.Bold, Green, TextAlignmentOptions.Center);
        SetRect(checkmark.rectTransform, new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(0f, .5f),
            new Vector2(30f, 30f), new Vector2(4f, 0f));
        TMP_Text itemLabel = CreateText(item.transform, "Item Label", "寵物", font, 19f,
            FontStyles.Normal, Ink, TextAlignmentOptions.Center);
        itemLabel.rectTransform.anchorMin = Vector2.zero;
        itemLabel.rectTransform.anchorMax = Vector2.one;
        itemLabel.rectTransform.offsetMin = new Vector2(34f, 0f);
        itemLabel.rectTransform.offsetMax = new Vector2(-8f, 0f);

        Toggle toggle = item.GetComponent<Toggle>();
        toggle.targetGraphic = itemBackground;
        toggle.graphic = checkmark;
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = contentRect;
        dropdown.targetGraphic = root.GetComponent<Image>();
        dropdown.template = templateRect;
        dropdown.captionText = label;
        dropdown.itemText = itemLabel;
        template.SetActive(false);
        return dropdown;
    }

    static ScrollRect CreateTranscript(Transform parent, TMP_FontAsset font, out TMP_Text transcript)
    {
        GameObject root = CreatePanel(parent, "Transcript", new Color32(255, 255, 255, 210));
        ScrollRect scroll = root.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewport = CreatePanel(root.transform, "Viewport", Color.clear);
        Stretch(viewport.GetComponent<RectTransform>(), 6f, 5f);
        viewport.AddComponent<RectMask2D>();

        transcript = CreateText(viewport.transform, "Content", "", font, 17f,
            FontStyles.Normal, Ink, TextAlignmentOptions.TopLeft);
        RectTransform content = transcript.rectTransform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 100f);
        transcript.textWrappingMode = TextWrappingModes.Normal;
        ContentSizeFitter fitter = transcript.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = content;
        return scroll;
    }

    static TMP_InputField CreateInputField(Transform parent, TMP_FontAsset font)
    {
        GameObject root = CreatePanel(parent, "MessageInput", Color.white);
        TMP_InputField input = root.AddComponent<TMP_InputField>();

        GameObject area = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
        area.transform.SetParent(root.transform, false);
        RectTransform areaRect = area.GetComponent<RectTransform>();
        Stretch(areaRect, 12f, 8f);

        TMP_Text placeholder = CreateText(area.transform, "Placeholder", "輸入想問的問題…", font, 18f,
            FontStyles.Italic, new Color32(120, 125, 117, 190), TextAlignmentOptions.MidlineLeft);
        Stretch(placeholder.rectTransform, 0f, 0f);
        TMP_Text text = CreateText(area.transform, "Text", "", font, 18f,
            FontStyles.Normal, Ink, TextAlignmentOptions.MidlineLeft);
        Stretch(text.rectTransform, 0f, 0f);

        input.textViewport = areaRect;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.lineType = TMP_InputField.LineType.SingleLine;
        return input;
    }

    static Button CreateButton(Transform parent, string name, string label, TMP_FontAsset font, Color color)
    {
        GameObject root = CreatePanel(parent, name, color);
        Button button = root.AddComponent<Button>();
        button.targetGraphic = root.GetComponent<Image>();
        TMP_Text text = CreateText(root.transform, "Label", label, font, 19f,
            FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        Stretch(text.rectTransform, 4f, 2f);
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
        float fontSize, FontStyles style, Color color, TextAlignmentOptions alignment)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        gameObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = gameObject.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = font;
        text.fontSize = fontSize;
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
        string[] folders = { Root, Root + "/Config", Root + "/Resources", Root + "/Scenes" };
        foreach (string folder in folders)
            if (!AssetDatabase.IsValidFolder(folder))
            {
                string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
                string name = Path.GetFileName(folder);
                AssetDatabase.CreateFolder(parent, name);
            }
    }
}
