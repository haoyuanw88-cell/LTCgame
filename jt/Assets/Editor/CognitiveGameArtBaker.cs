using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 將三款核心認知遊戲的 UI 固定寫入 Scene，讓編輯模式就能看到並調整。
/// 執行後不會在遊戲開始時動態建立版面。
/// </summary>
public static class CognitiveGameArtBaker
{
    private const string SceneRoot = "Assets/new LTC/場景轉換/";
    private const string ArtRoot = "Assets/new LTC/遊戲素材/認知遊戲AI素材/";
    private const string FontPath = "Assets/new LTC/Unity中文/KAIU_Dynamic.asset";

    private static readonly Color PageColor = new Color32(255, 246, 222, 255);
    private static readonly Color InkColor = new Color32(43, 78, 82, 255);
    private static readonly Color HintColor = new Color32(92, 111, 103, 255);

    [MenuItem("Tools/LTC/套用三款認知遊戲美術")]
    [MenuItem("Tools/LTC/Bake Cognitive Game Art")]
    public static void BakeAll()
    {
        string reopenPath = SceneManager.GetActiveScene().path;
        if (string.IsNullOrEmpty(reopenPath)) reopenPath = SceneRoot + "GameScene.unity";

        TMP_FontAsset font = Load<TMP_FontAsset>(FontPath);
        if (font == null) throw new MissingReferenceException("找不到中文字型：" + FontPath);

        BakeStroop(SceneRoot + "js.unity", font);
        BakeNumberOrder(SceneRoot + "mb.unity", font);
        BakeNumberSum(SceneRoot + "mb2.unity", font);

        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(reopenPath))
            EditorSceneManager.OpenScene(reopenPath, OpenSceneMode.Single);

        Debug.Log("三款認知遊戲美術與全螢幕版面已寫入 Scene。");
    }

    public static void FixStretchableFrames()
    {
        string reopenPath = SceneManager.GetActiveScene().path;
        ConfigureStretchableSprites();

        string[] paths =
        {
            SceneRoot + "js.unity",
            SceneRoot + "mb.unity",
            SceneRoot + "mb2.unity"
        };

        foreach (string path in paths)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Canvas canvas = FindInScene<Canvas>(scene);
            if (canvas == null) continue;

            foreach (Image image in canvas.GetComponentsInChildren<Image>(true))
            {
                if (image.sprite == null) continue;
                string spritePath = AssetDatabase.GetAssetPath(image.sprite);
                if (!IsStretchableFrame(spritePath)) continue;
                image.type = Image.Type.Sliced;
                image.preserveAspect = false;
                EditorUtility.SetDirty(image);
            }

            foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>(true))
            {
                text.enableAutoSizing = true;
                text.fontSizeMin = Mathf.Min(text.fontSize, 18);
                text.raycastTarget = false;
                EditorUtility.SetDirty(text);
            }

            NumberSumGameManager sum = FindInScene<NumberSumGameManager>(scene);
            if (sum != null && sum.targetText != null)
            {
                Transform oldFrame = canvas.transform.Find("算式題目框");
                if (oldFrame != null)
                {
                    sum.targetText.transform.SetParent(canvas.transform, true);
                    Object.DestroyImmediate(oldFrame.gameObject);
                }

                TMP_FontAsset font = Load<TMP_FontAsset>(FontPath);
                PlaceCanvasText(sum.targetText, canvas.transform, font, 52, InkColor,
                    new Vector2(.20f, .59f), new Vector2(.80f, .75f));
                sum.targetText.outlineColor = new Color32(255, 251, 235, 255);
                sum.targetText.outlineWidth = .12f;
                EditorUtility.SetDirty(sum);
            }

            Save(scene);
        }

        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(reopenPath))
            EditorSceneManager.OpenScene(reopenPath, OpenSceneMode.Single);
        Debug.Log("可拉伸金框與數字加總無框算式已更新。");
    }

    [MenuItem("Tools/LTC/Restore Pre Stretch Version")]
    public static void RestorePreStretchVersion()
    {
        string reopenPath = SceneManager.GetActiveScene().path;
        SetSpriteBorder(ArtRoot + "數字加總/equation_panel.png", Vector4.zero);
        SetSpriteBorder(ArtRoot + "顏色文字判斷/response_button_left.png", Vector4.zero);
        SetSpriteBorder(ArtRoot + "顏色文字判斷/response_button_right.png", Vector4.zero);

        string[] paths =
        {
            SceneRoot + "js.unity",
            SceneRoot + "mb.unity",
            SceneRoot + "mb2.unity"
        };

        foreach (string path in paths)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Canvas canvas = FindInScene<Canvas>(scene);
            if (canvas == null) continue;

            foreach (Image image in canvas.GetComponentsInChildren<Image>(true))
            {
                if (image.sprite == null) continue;
                string spritePath = AssetDatabase.GetAssetPath(image.sprite);
                if (!IsStretchableFrame(spritePath)) continue;
                image.type = Image.Type.Simple;
                image.preserveAspect = true;
                EditorUtility.SetDirty(image);
            }

            NumberSumGameManager sum = FindInScene<NumberSumGameManager>(scene);
            if (sum != null && sum.targetText != null)
            {
                TMP_FontAsset font = Load<TMP_FontAsset>(FontPath);
                Image frame = EnsureImage(canvas.transform, "算式題目框",
                    Load<Sprite>(ArtRoot + "數字加總/equation_panel.png"), Color.white,
                    new Vector2(.5f, .67f), new Vector2(520, 170));
                frame.type = Image.Type.Simple;
                frame.preserveAspect = true;
                PlaceTextInside(sum.targetText, frame.transform, font, 48, InkColor);
                sum.targetText.outlineWidth = 0f;
                EditorUtility.SetDirty(sum);
                BringResultToFront(sum.resultPanel);
            }

            Save(scene);
        }

        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(reopenPath))
            EditorSceneManager.OpenScene(reopenPath, OpenSceneMode.Single);
        Debug.Log("已還原成不可拉伸金框版本，保留既有按鈕與手動排列位置。");
    }

    private static void BakeStroop(string scenePath, TMP_FontAsset font)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        Canvas canvas = FindInScene<Canvas>(scene);
        ColorMatchStroopGameManager manager = FindInScene<ColorMatchStroopGameManager>(scene);
        if (canvas == null || manager == null) throw new MissingReferenceException(scenePath + " 缺少 Canvas 或遊戲管理器");

        PrepareCanvas(canvas, font);
        EnsureStatusHeader(canvas.transform, font, "顏色文字判斷", manager.timerText, manager.scoreText);

        Sprite cardSprite = Load<Sprite>(ArtRoot + "顏色文字判斷/stimulus_card.png");
        Image topCard = EnsureImage(canvas.transform, "題目卡_上", cardSprite, Color.white,
            new Vector2(.5f, .63f), new Vector2(330, 205));
        Image bottomCard = EnsureImage(canvas.transform, "題目卡_下", cardSprite, Color.white,
            new Vector2(.5f, .40f), new Vector2(330, 205));
        topCard.preserveAspect = true;
        bottomCard.preserveAspect = true;

        PlaceTextInside(manager.topWordText, topCard.transform, font, 62, InkColor);
        PlaceTextInside(manager.bottomWordText, bottomCard.transform, font, 62, InkColor);

        StyleWideButton(manager.wrongButton, Load<Sprite>(ArtRoot + "顏色文字判斷/response_button_left.png"),
            font, new Vector2(.36f, .16f), "不同");
        StyleWideButton(manager.correctButton, Load<Sprite>(ArtRoot + "顏色文字判斷/response_button_right.png"),
            font, new Vector2(.64f, .16f), "相同");

        EnsureText(canvas.transform, "玩法提示", font, "判斷上下兩個字的『字義』與『顏色』是否相同",
            26, HintColor, new Vector2(.16f, .045f), new Vector2(.84f, .105f));

        BringResultToFront(manager.resultPanel);
        Save(scene);
    }

    private static void BakeNumberOrder(string scenePath, TMP_FontAsset font)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        Canvas canvas = FindInScene<Canvas>(scene);
        NumberOrderPoolGameManager manager = FindInScene<NumberOrderPoolGameManager>(scene);
        if (canvas == null || manager == null) throw new MissingReferenceException(scenePath + " 缺少 Canvas 或遊戲管理器");

        PrepareCanvas(canvas, font);
        EnsureStatusHeader(canvas.transform, font, "數字由小到大", manager.timerText, manager.scoreText);

        Sprite[] tiles =
        {
            Load<Sprite>(ArtRoot + "數字排序/tile_teal.png"),
            Load<Sprite>(ArtRoot + "數字排序/tile_orange.png"),
            Load<Sprite>(ArtRoot + "數字排序/tile_lavender.png"),
            Load<Sprite>(ArtRoot + "數字排序/tile_blue.png"),
            Load<Sprite>(ArtRoot + "數字排序/tile_yellow.png"),
            Load<Sprite>(ArtRoot + "數字排序/tile_coral.png")
        };
        manager.buttonSprites.Clear();
        foreach (Sprite tile in tiles) if (tile != null) manager.buttonSprites.Add(tile);

        RectTransform group = GetCommonParent(manager.numberButtons);
        if (group != null)
        {
            group.anchorMin = group.anchorMax = new Vector2(.5f, .5f);
            group.pivot = new Vector2(.5f, .5f);
            group.anchoredPosition = new Vector2(0, -38);
            group.localScale = new Vector3(1.55f, 1.55f, 1f);
        }

        for (int i = 0; i < manager.numberButtons.Count; i++)
            StyleHexButton(manager.numberButtons[i], tiles.Length == 0 ? null : tiles[i % tiles.Length], font, 40, new Vector2(108, 108));

        if (manager.difficultyText != null)
            PlaceCanvasText(manager.difficultyText, canvas.transform, font, 24, HintColor,
                new Vector2(.17f, .08f), new Vector2(.83f, .145f));
        else
            EnsureText(canvas.transform, "玩法提示", font, "請依序點選：從最小的數字開始",
                26, HintColor, new Vector2(.18f, .045f), new Vector2(.82f, .105f));

        BringResultToFront(manager.resultPanel);
        if (manager.wrongImage != null) manager.wrongImage.transform.SetAsLastSibling();
        EditorUtility.SetDirty(manager);
        Save(scene);
    }

    private static void BakeNumberSum(string scenePath, TMP_FontAsset font)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        Canvas canvas = FindInScene<Canvas>(scene);
        NumberSumGameManager manager = FindInScene<NumberSumGameManager>(scene);
        if (canvas == null || manager == null) throw new MissingReferenceException(scenePath + " 缺少 Canvas 或遊戲管理器");

        PrepareCanvas(canvas, font);
        EnsureStatusHeader(canvas.transform, font, "數字加總", manager.timerText, manager.scoreText);

        Image equationPanel = EnsureImage(canvas.transform, "算式題目框",
            Load<Sprite>(ArtRoot + "數字加總/equation_panel.png"), Color.white,
            new Vector2(.5f, .67f), new Vector2(520, 170));
        equationPanel.type = Image.Type.Simple;
        equationPanel.preserveAspect = true;
        PlaceTextInside(manager.targetText, equationPanel.transform, font, 48, InkColor);

        Sprite[] tiles =
        {
            Load<Sprite>(ArtRoot + "數字加總/tile_teal.png"),
            Load<Sprite>(ArtRoot + "數字加總/tile_orange.png"),
            Load<Sprite>(ArtRoot + "數字加總/tile_blue.png"),
            Load<Sprite>(ArtRoot + "數字加總/tile_yellow.png")
        };
        manager.normalButtonSprites.Clear();
        foreach (Sprite tile in tiles) if (tile != null) manager.normalButtonSprites.Add(tile);
        manager.selectedButtonSprite = Load<Sprite>(ArtRoot + "數字加總/selection_frame.png");

        Vector2[] honeycomb =
        {
            new Vector2(-180, 70), new Vector2(0, 70), new Vector2(180, 70),
            new Vector2(-90, -90), new Vector2(90, -90), new Vector2(0, -250)
        };
        RectTransform group = GetCommonParent(manager.numberButtons);
        if (group != null)
        {
            group.anchorMin = group.anchorMax = new Vector2(.5f, .5f);
            group.pivot = new Vector2(.5f, .5f);
            group.anchoredPosition = new Vector2(0, -72);
            group.localScale = Vector3.one;
        }

        for (int i = 0; i < manager.numberButtons.Count; i++)
        {
            Button button = manager.numberButtons[i];
            StyleHexButton(button, tiles.Length == 0 ? null : tiles[i % tiles.Length], font, 44, new Vector2(155, 155));
            if (button != null && i < honeycomb.Length)
            {
                RectTransform rect = button.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.anchoredPosition = honeycomb[i];
            }
        }

        if (manager.difficultyText != null)
            PlaceCanvasText(manager.difficultyText, canvas.transform, font, 23, HintColor,
                new Vector2(.14f, .035f), new Vector2(.86f, .095f));
        else
            EnsureText(canvas.transform, "玩法提示", font, "可重複點選來取消；讓所選數字加總等於題目",
                25, HintColor, new Vector2(.14f, .025f), new Vector2(.86f, .09f));

        BringResultToFront(manager.resultPanel);
        EditorUtility.SetDirty(manager);
        Save(scene);
    }

    private static void PrepareCanvas(Canvas canvas, TMP_FontAsset font)
    {
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1366, 768);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = .5f;

        Image background = null;
        foreach (Transform child in canvas.transform)
        {
            if (child.name == "Image" && child.TryGetComponent(out Image image))
            {
                background = image;
                break;
            }
        }
        if (background == null)
            background = EnsureImage(canvas.transform, "Image", null, PageColor, new Vector2(.5f, .5f), Vector2.zero);

        RectTransform backgroundRect = background.rectTransform;
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;
        backgroundRect.localScale = Vector3.one;
        background.sprite = null;
        background.color = PageColor;
        background.raycastTarget = false;
        background.transform.SetAsFirstSibling();

        Camera camera = FindInScene<Camera>(canvas.gameObject.scene);
        if (camera != null)
        {
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = PageColor;
        }

        foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>(true))
        {
            text.font = font;
            text.raycastTarget = false;
        }
    }

    private static void EnsureStatusHeader(Transform canvas, TMP_FontAsset font, string title,
        TMP_Text timer, TMP_Text score)
    {
        Image frame = EnsureImage(canvas, "狀態資訊框",
            Load<Sprite>(ArtRoot + "數字加總/equation_panel.png"), Color.white,
            new Vector2(.5f, .89f), new Vector2(760, 150));
        frame.type = Image.Type.Simple;
        frame.preserveAspect = true;
        frame.transform.SetSiblingIndex(Mathf.Min(1, canvas.childCount - 1));

        EnsureText(canvas, "遊戲標題", font, title, 34, InkColor,
            new Vector2(.38f, .895f), new Vector2(.62f, .955f));
        PlaceCanvasText(timer, canvas, font, 30, InkColor,
            new Vector2(.25f, .825f), new Vector2(.47f, .895f));
        PlaceCanvasText(score, canvas, font, 30, InkColor,
            new Vector2(.53f, .825f), new Vector2(.75f, .895f));
    }

    private static void StyleWideButton(Button button, Sprite sprite, TMP_FontAsset font, Vector2 anchor, string fallbackText)
    {
        if (button == null) return;
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.SetParent(button.transform.parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(300, 125);
        rect.localScale = Vector3.one;

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = sprite;
            image.color = Color.white;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
        }
        ConfigureButton(button);

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            if (string.IsNullOrWhiteSpace(label.text) || label.text == "Button" || label.text == "New Text")
                label.text = fallbackText;
            ConfigureButtonLabel(label, font, 34, InkColor);
        }
    }

    private static void StyleHexButton(Button button, Sprite sprite, TMP_FontAsset font, float fontSize, Vector2 size)
    {
        if (button == null) return;
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = sprite;
            image.color = Color.white;
            image.preserveAspect = true;
        }
        ConfigureButton(button);

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null) ConfigureButtonLabel(label, font, fontSize, Color.white);
    }

    private static void ConfigureButton(Button button)
    {
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, .97f, .87f, 1f);
        colors.pressedColor = new Color(.88f, .91f, .86f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(1f, 1f, 1f, .45f);
        colors.colorMultiplier = 1f;
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
    }

    private static void ConfigureButtonLabel(TMP_Text label, TMP_FontAsset font, float size, Color color)
    {
        label.font = font;
        label.fontSize = size;
        label.fontStyle = FontStyles.Bold;
        label.enableAutoSizing = true;
        label.fontSizeMin = Mathf.Max(24, size - 12);
        label.fontSizeMax = size;
        label.alignment = TextAlignmentOptions.Center;
        label.color = color;
        label.outlineColor = color == Color.white ? new Color32(35, 55, 58, 255) : new Color32(255, 249, 232, 255);
        label.outlineWidth = color == Color.white ? .22f : .08f;
        label.raycastTarget = false;
        RectTransform rect = label.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(12, 10);
        rect.offsetMax = new Vector2(-12, -10);
        rect.localScale = Vector3.one;
    }

    private static void PlaceTextInside(TMP_Text text, Transform parent, TMP_FontAsset font, float size, Color color)
    {
        if (text == null) return;
        text.transform.SetParent(parent, false);
        text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.enableAutoSizing = true;
        text.fontSizeMin = 34;
        text.fontSizeMax = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.raycastTarget = false;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(38, 30);
        rect.offsetMax = new Vector2(-38, -30);
        rect.localScale = Vector3.one;
    }

    private static void PlaceCanvasText(TMP_Text text, Transform canvas, TMP_FontAsset font, float size,
        Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        if (text == null) return;
        text.transform.SetParent(canvas, false);
        text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(20, size - 10);
        text.fontSizeMax = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color;
        text.raycastTarget = false;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    private static TMP_Text EnsureText(Transform parent, string name, TMP_FontAsset font, string content,
        float size, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        Transform child = parent.Find(name);
        TextMeshProUGUI text;
        if (child == null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            text = go.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            text = child.GetComponent<TextMeshProUGUI>();
            if (text == null) text = child.gameObject.AddComponent<TextMeshProUGUI>();
        }
        text.text = content;
        PlaceCanvasText(text, parent, font, size, color, anchorMin, anchorMax);
        return text;
    }

    private static Image EnsureImage(Transform parent, string name, Sprite sprite, Color color,
        Vector2 anchor, Vector2 size)
    {
        Transform child = parent.Find(name);
        GameObject go;
        if (child == null)
        {
            go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
        }
        else go = child.gameObject;

        Image image = go.GetComponent<Image>();
        if (image == null) image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        return image;
    }

    private static RectTransform GetCommonParent(List<Button> buttons)
    {
        foreach (Button button in buttons)
            if (button != null && button.transform.parent is RectTransform parent) return parent;
        return null;
    }

    private static void ConfigureStretchableSprites()
    {
        SetSpriteBorder(ArtRoot + "數字加總/equation_panel.png", new Vector4(82, 66, 82, 66));
        SetSpriteBorder(ArtRoot + "顏色文字判斷/response_button_left.png", new Vector4(76, 62, 76, 62));
        SetSpriteBorder(ArtRoot + "顏色文字判斷/response_button_right.png", new Vector4(76, 62, 76, 62));
    }

    private static void SetSpriteBorder(string path, Vector4 border)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        if (importer.spriteBorder == border) return;
        importer.spriteBorder = border;
        importer.SaveAndReimport();
    }

    private static bool IsStretchableFrame(string path)
    {
        return path.EndsWith("equation_panel.png") ||
               path.EndsWith("response_button_left.png") ||
               path.EndsWith("response_button_right.png");
    }

    private static void BringResultToFront(GameObject resultPanel)
    {
        if (resultPanel == null) return;
        RectTransform rect = resultPanel.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }
        resultPanel.transform.SetAsLastSibling();
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T value = root.GetComponentInChildren<T>(true);
            if (value != null) return value;
        }
        return null;
    }

    private static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

    private static void Save(Scene scene)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
