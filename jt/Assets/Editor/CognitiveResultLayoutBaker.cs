using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CognitiveResultLayoutBaker
{
    const string FontPath = "Assets/new LTC/Unity中文/KAIU_Dynamic.asset";

    [InitializeOnLoadMethod]
    static void UpgradeOpenCatalogScene()
    {
        EditorApplication.update -= TryUpgradeCatalogSceneAsset;
        EditorApplication.update += TryUpgradeCatalogSceneAsset;
    }

    static void TryUpgradeCatalogSceneAsset()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorApplication.update -= TryUpgradeCatalogSceneAsset;
        UpgradeCatalogSceneAsset();
    }

    static void UpgradeCatalogSceneAsset()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        const string scenePath = "Assets/new LTC/場景轉換/GameScene.unity";
        string sceneText = File.ReadAllText(Path.GetFullPath(scenePath));
        if (sceneText.Contains("UI版本_2") || sceneText.Contains("UI\\u7248\\u672C_2")) return;
        Scene scene = OpenAdditive(scenePath, out bool closeAfter);
        try
        {
            CognitiveGameCatalogController controller = FindInScene<CognitiveGameCatalogController>(scene);
            if (controller == null) return;
            controller.RebuildInterfaceForEditor();
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("首次登入資料介面已升級為日期滾輪與下拉選單，並保存到 GameScene。");
        }
        finally
        {
            if (closeAfter) EditorSceneManager.CloseScene(scene, true);
        }
    }

    [MenuItem("Tools/LTC/整理三個遊戲結算畫面")]
    public static void BakeAll()
    {
        Bake<ColorMatchStroopGameManager>("Assets/new LTC/場景轉換/js.unity");
        Bake<NumberOrderPoolGameManager>("Assets/new LTC/場景轉換/mb.unity");
        Bake<NumberSumGameManager>("Assets/new LTC/場景轉換/mb2.unity");
        AssetDatabase.SaveAssets();
        Debug.Log("三個遊戲的結算畫面已整理完成。");
    }

    [MenuItem("Tools/LTC/重建認知首頁與首次引導 _F8")]
    public static void BakeCatalogAndOnboarding()
    {
        const string scenePath = "Assets/new LTC/場景轉換/GameScene.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        var controller = Object.FindAnyObjectByType<CognitiveGameCatalogController>(FindObjectsInactive.Include);
        if (controller == null) throw new MissingReferenceException(scenePath + " 找不到認知首頁控制器");
        controller.RebuildInterfaceForEditor();
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("認知首頁與首次登入 NPC 已重建並保存到場景。");
    }

    [MenuItem("Tools/LTC/恢復蜂巢排版並套用清晰文字")]
    [MenuItem("Tools/LTC/美化數字排序與加總遊戲")]
    [MenuItem("Tools/LTC/Bake Core Game Visuals")]
    public static void BakeCoreGames()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null) throw new MissingReferenceException("找不到中文字型：" + FontPath);

        BakeNumberOrderPlayArea("Assets/new LTC/場景轉換/mb.unity", font);
        BakeNumberSumPlayArea("Assets/new LTC/場景轉換/mb2.unity", font);
        AssetDatabase.SaveAssets();
        Debug.Log("數字排序與數字加總：保留原始蜂巢位置與素材，只套用 KAIU 清晰文字。");
    }

    [MenuItem("Tools/LTC/Open Number Sum Preview")]
    public static void OpenNumberSumPreview()
    {
        EditorSceneManager.OpenScene("Assets/new LTC/場景轉換/mb2.unity", OpenSceneMode.Single);
    }

    [MenuItem("Tools/LTC/Open Number Order Preview")]
    public static void OpenNumberOrderPreview()
    {
        EditorSceneManager.OpenScene("Assets/new LTC/場景轉換/mb.unity", OpenSceneMode.Single);
    }

    static void BakeNumberSumPlayArea(string scenePath, TMP_FontAsset font)
    {
        Scene scene = OpenAdditive(scenePath, out bool closeAfter);
        try
        {
            NumberSumGameManager manager = FindInScene<NumberSumGameManager>(scene);
            Canvas canvas = FindInScene<Canvas>(scene);
            if (manager == null || canvas == null) throw new MissingReferenceException(scenePath + " 缺少遊戲管理器或 Canvas");

            RemoveGeneratedDecoration(canvas.transform, "遊戲背景", "題目資訊卡", "難度與操作提示", "加總操作說明");
            ApplyReadableFont(canvas, font);
            foreach (Button button in manager.numberButtons)
                StyleNumberLabelOnly(button, font, 44);

            if (manager.resultPanel != null) manager.resultPanel.transform.SetAsLastSibling();
            EditorUtility.SetDirty(manager);
            SaveScene(scene);
        }
        finally { if (closeAfter) EditorSceneManager.CloseScene(scene, true); }
    }

    static void BakeNumberOrderPlayArea(string scenePath, TMP_FontAsset font)
    {
        Scene scene = OpenAdditive(scenePath, out bool closeAfter);
        try
        {
            NumberOrderPoolGameManager manager = FindInScene<NumberOrderPoolGameManager>(scene);
            Canvas canvas = FindInScene<Canvas>(scene);
            if (manager == null || canvas == null) throw new MissingReferenceException(scenePath + " 缺少遊戲管理器或 Canvas");

            manager.roundsPerNumberIncrease = 2;
            manager.negativeStartRound = 5;

            RemoveGeneratedDecoration(canvas.transform, "遊戲背景", "題目資訊卡", "排序題目提示", "排序操作說明");
            ApplyReadableFont(canvas, font);
            foreach (Button button in manager.numberButtons)
                StyleNumberLabelOnly(button, font, 39);

            if (manager.resultPanel != null) manager.resultPanel.transform.SetAsLastSibling();
            if (manager.wrongImage != null) manager.wrongImage.transform.SetAsLastSibling();
            EditorUtility.SetDirty(manager);
            SaveScene(scene);
        }
        finally { if (closeAfter) EditorSceneManager.CloseScene(scene, true); }
    }

    static void RemoveGeneratedDecoration(Transform canvas, params string[] names)
    {
        foreach (string name in names)
        {
            Transform child = canvas.Find(name);
            if (child != null) Object.DestroyImmediate(child.gameObject);
        }
    }

    static void ApplyReadableFont(Canvas canvas, TMP_FontAsset font)
    {
        foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>(true))
        {
            text.font = font;
            text.raycastTarget = false;
        }
    }

    static void StyleNumberLabelOnly(Button button, TMP_FontAsset font, float fontSize)
    {
        if (button == null) return;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label == null) return;
        label.font = font;
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.enableAutoSizing = true;
        label.fontSizeMin = 28;
        label.fontSizeMax = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.outlineColor = new Color32(25, 35, 45, 255);
        label.outlineWidth = .25f;
        label.raycastTarget = false;
    }

    static Scene OpenAdditive(string path, out bool closeAfter)
    {
        Scene loaded = SceneManager.GetSceneByPath(path);
        if (loaded.IsValid() && loaded.isLoaded) { closeAfter = false; return loaded; }
        closeAfter = true;
        return EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
    }

    static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T result = root.GetComponentInChildren<T>(true);
            if (result != null) return result;
        }
        return null;
    }

    static void SaveScene(Scene scene)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void PrepareCanvas(Canvas canvas, Color background)
    {
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1366, 768);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;
        Camera camera = FindInScene<Camera>(canvas.gameObject.scene);
        if (camera != null) camera.backgroundColor = background;
        foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>(true))
        {
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            text.raycastTarget = false;
        }
    }

    static Image EnsurePanel(Transform parent, string name, Color color, Vector2 min, Vector2 max, int siblingIndex)
    {
        Transform child = parent.Find(name);
        GameObject go = child != null ? child.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        if (child == null) go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        go.transform.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, parent.childCount - 1));
        return image;
    }

    static TMP_Text EnsureSceneText(Transform parent, string name, TMP_FontAsset font, string content, float size,
        TextAlignmentOptions alignment, Vector2 min, Vector2 max)
    {
        Transform child = parent.Find(name);
        TextMeshProUGUI text;
        if (child == null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            text = go.GetComponent<TextMeshProUGUI>();
        }
        else text = child.GetComponent<TextMeshProUGUI>();
        text.text = content;
        ConfigureHudText(text, parent, font, size, alignment, min, max);
        return text;
    }

    static void ConfigureHudText(TMP_Text text, Transform parent, TMP_FontAsset font, float size,
        TextAlignmentOptions alignment, Vector2 min, Vector2 max)
    {
        if (text == null) return;
        text.transform.SetParent(parent, false);
        text.font = font; text.fontSize = size; text.fontStyle = FontStyles.Bold;
        text.enableAutoSizing = true; text.fontSizeMin = Mathf.Max(20, size - 8); text.fontSizeMax = size;
        text.alignment = alignment; text.color = new Color32(42, 82, 88, 255);
        text.raycastTarget = false; text.textWrappingMode = TextWrappingModes.NoWrap;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
    }

    static void PlaceAtAnchor(RectTransform rect, Vector2 anchor, Vector2 size)
    {
        rect.anchorMin = anchor; rect.anchorMax = anchor; rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero; rect.sizeDelta = size; rect.localScale = Vector3.one;
    }

    static void StyleNumberButton(Button button, TMP_FontAsset font, Color background, float fontSize)
    {
        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = background;
            Shadow shadow = button.GetComponent<Shadow>();
            if (shadow == null) shadow = button.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.12f, 0.22f, 0.24f, .26f);
            shadow.effectDistance = new Vector2(0, -6);
        }

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(.94f, .98f, .98f, 1f);
        colors.pressedColor = new Color(.78f, .88f, .88f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(1f, 1f, 1f, .45f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
        button.transition = Selectable.Transition.ColorTint;
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.font = font; label.fontSize = fontSize; label.fontStyle = FontStyles.Bold;
            label.enableAutoSizing = true; label.fontSizeMin = 28; label.fontSizeMax = fontSize;
            label.alignment = TextAlignmentOptions.Center; label.color = Color.white;
            label.outlineColor = new Color32(28, 42, 54, 255); label.outlineWidth = .25f;
            label.raycastTarget = false;
            RectTransform rect = label.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(8, 6); rect.offsetMax = new Vector2(-8, -6);
        }
    }

    static void Bake<T>(string scenePath) where T : MonoBehaviour
    {
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        T manager = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
        if (manager == null) throw new MissingReferenceException(scenePath + " 找不到遊戲管理器");

        GameObject panel;
        TMP_Text oldDetail;
        if (manager is ColorMatchStroopGameManager color) { panel = color.resultPanel; oldDetail = color.resultText; }
        else if (manager is NumberOrderPoolGameManager order) { panel = order.resultPanel; oldDetail = order.resultText; }
        else { var sum = manager as NumberSumGameManager; panel = sum.resultPanel; oldDetail = sum.resultText; }
        if (panel == null || oldDetail == null) throw new MissingReferenceException(scenePath + " 結算面板引用不完整");

        panel.transform.SetAsLastSibling();
        Image panelImage = panel.GetComponent<Image>();
        if (panelImage != null) panelImage.color = new Color32(250, 247, 239, 255);

        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero; panelRect.offsetMax = Vector2.zero;

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        TMP_Text title = EnsureText(panel.transform, "結算標題", font, 42, FontStyles.Bold, TextAlignmentOptions.Center,
            new Vector2(.08f, .80f), new Vector2(.92f, .94f));
        TMP_Text summary = EnsureText(panel.transform, "結算摘要", font, 30, FontStyles.Bold, TextAlignmentOptions.Center,
            new Vector2(.08f, .68f), new Vector2(.92f, .80f));
        TMP_Text detail = ConfigureText(oldDetail, "結算詳細", font, 25, FontStyles.Normal, TextAlignmentOptions.TopLeft,
            new Vector2(.13f, .28f), new Vector2(.87f, .66f));
        TMP_Text note = EnsureText(panel.transform, "結算說明", font, 20, FontStyles.Normal, TextAlignmentOptions.Center,
            new Vector2(.08f, .14f), new Vector2(.92f, .27f));
        title.text = "本次測驗完成"; summary.text = "分數與獎勵"; detail.text = "詳細表現會顯示在這裡"; note.text = "本結果用於個人趨勢參考";

        Button[] buttons = panel.GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            RectTransform rt = buttons[i].GetComponent<RectTransform>();
            float width = Mathf.Min(.42f, .84f / Mathf.Max(1, buttons.Length));
            float center = .5f + (i - (buttons.Length - 1) * .5f) * width;
            rt.anchorMin = new Vector2(center - width * .45f, .025f);
            rt.anchorMax = new Vector2(center + width * .45f, .12f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            TMP_Text label = buttons[i].GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.font = font; label.fontSize = 25; label.enableAutoSizing = true;
                label.fontSizeMin = 18; label.fontSizeMax = 28; label.raycastTarget = false;
                if (label.text == "Button" || label.text == "New Text") label.text = "返回主選單";
            }
        }

        if (manager is ColorMatchStroopGameManager c) { c.resultTitleText=title; c.resultSummaryText=summary; c.resultText=detail; c.resultNoteText=note; }
        else if (manager is NumberOrderPoolGameManager o) { o.resultTitleText=title; o.resultSummaryText=summary; o.resultText=detail; o.resultNoteText=note; }
        else { var s=manager as NumberSumGameManager; s.resultTitleText=title; s.resultSummaryText=summary; s.resultText=detail; s.resultNoteText=note; }

        EditorUtility.SetDirty(manager); EditorUtility.SetDirty(panel);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
    }

    static TMP_Text EnsureText(Transform parent, string name, TMP_FontAsset font, float size, FontStyles style,
        TextAlignmentOptions alignment, Vector2 min, Vector2 max)
    {
        Transform child = parent.Find(name);
        TMP_Text text;
        if (child == null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false); text = go.GetComponent<TextMeshProUGUI>();
        }
        else text = child.GetComponent<TMP_Text>();
        return ConfigureText(text, name, font, size, style, alignment, min, max);
    }

    static TMP_Text ConfigureText(TMP_Text text, string name, TMP_FontAsset font, float size, FontStyles style,
        TextAlignmentOptions alignment, Vector2 min, Vector2 max)
    {
        text.gameObject.name = name; text.font = font; text.fontSize = size; text.fontStyle = style;
        text.alignment = alignment; text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Truncate;
        text.raycastTarget = false; text.color = new Color32(55, 61, 68, 255);
        RectTransform rt = text.rectTransform; rt.anchorMin=min; rt.anchorMax=max; rt.offsetMin=Vector2.zero; rt.offsetMax=Vector2.zero;
        return text;
    }
}
