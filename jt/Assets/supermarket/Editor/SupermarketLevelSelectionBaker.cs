using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Creates editable scene UI and a build-safe, ordered level catalog.</summary>
public static class SupermarketLevelSelectionBaker
{
    public const string CatalogPath = "Assets/Resources/SupermarketLevelCatalog.asset";
    private const string ScenePath = "Assets/supermarket/Scenes/SupermarketGame.unity";
    private const string ArtRoot = "Assets/new LTC/遊戲素材/認知遊戲AI素材/暫停介面/";
    private const string RootName = "Supermarket Level Selection Canvas";
    private static readonly Color Ink = new Color(.18f, .32f, .34f);

    public static SupermarketLevelCatalog GetOrCreateCatalog()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<SupermarketLevelCatalog>(CatalogPath);
        if (catalog != null) return catalog;
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        catalog = ScriptableObject.CreateInstance<SupermarketLevelCatalog>();
        AssetDatabase.CreateAsset(catalog, CatalogPath);
        return catalog;
    }

    public static void AddLevel(SupermarketLevelData level)
    {
        if (level == null || !AssetDatabase.Contains(level)) return;
        string error;
        if (!level.ValidateProducts(SupermarketLevelEditorWindow.SceneProducts(), out error)) { Debug.LogWarning(error); return; }
        var catalog = GetOrCreateCatalog();
        if (catalog.levels.Contains(level)) return;
        Undo.RecordObject(catalog, "Add Pipe Level To Selection");
        catalog.levels.Add(level);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/LTC/Bake Supermarket Level Selection")]
    public static void Bake()
    {
        var catalog = GetOrCreateCatalog();
        // Seed only a new catalog; preserve the user's ordering on subsequent bakes.
        var scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var manager = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<SupermarketGame>(true)).First();
            var pause = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<CognitiveGamePauseMenu>(true)).First();
            if (catalog.levels.Count == 0)
            {
                if (!AssetDatabase.IsValidFolder("Assets/supermarket/Levels")) AssetDatabase.CreateFolder("Assets/supermarket", "Levels");
                var level = ScriptableObject.CreateInstance<SupermarketLevelData>();
                level.displayName = "日常採買";
                level.products = manager.requiredProducts.Select(x => new ShoppingRequirement { productImage = x.productImage, quantity = x.quantity }).ToList();
                AssetDatabase.CreateAsset(level, AssetDatabase.GenerateUniqueAssetPath("Assets/supermarket/Levels/SupermarketLevel1.asset"));
                AddLevel(level);
            }
            foreach (var old in scene.GetRootGameObjects().Where(x => x.name == RootName).ToArray())
                Object.DestroyImmediate(old);
            var root = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(root, scene);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1366, 768);
            scaler.matchWidthOrHeight = .5f;
            var controller = root.AddComponent<SupermarketLevelSelection>();
            controller.manager = manager;
            controller.pauseMenu = pause;
            controller.catalog = catalog;

            manager.levelSelection = controller;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/new LTC/Unity中文/KAIU_Dynamic.asset");
            var frameSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "pause_modal.png");
            var creamSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "home_button.png");
            var greenSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "resume_button.png");

            var panel = ImageObject("選關畫面", root.transform, null, Vector2.zero, Vector2.zero);
            Stretch(panel.rectTransform);
            panel.color = new Color(1f, .965f, .89f);
            controller.panel = panel.gameObject;
            var frame = ImageObject("選關金框", panel.transform, frameSprite, new Vector2(1120, 690), new Vector2(0, -4));
            TextObject("標題", frame.transform, "超市遊戲", font, 48, new Vector2(650, 68), new Vector2(0, 266), new Color(1f, .96f, .8f));
            TextObject("說明", frame.transform, "選擇關卡", font, 28, new Vector2(650, 42), new Vector2(0, 211), new Color(1f, .97f, .85f));
            controller.levelButtons = new Button[8];
            controller.numberLabels = new TMP_Text[8];
            controller.sizeLabels = new TMP_Text[8];
            controller.selectionFrames = new Image[8];
            for (int i = 0; i < 8; i++)
            {
                Vector2 position = new Vector2(-330 + (i % 4) * 220, 111 - (i / 4) * 164);
                var slot = new GameObject("關卡 " + (i + 1), typeof(RectTransform));
                slot.transform.SetParent(frame.transform, false);
                var slotRect = slot.GetComponent<RectTransform>();
                slotRect.sizeDelta = new Vector2(196, 166);
                slotRect.anchoredPosition = position;
                var glow = ImageObject("選取外框", slot.transform, greenSprite, new Vector2(208, 178), Vector2.zero);
                glow.raycastTarget = false;
                glow.enabled = i == 0;
                controller.selectionFrames[i] = glow;
                var buttonImage = ImageObject("關卡按鈕", slot.transform, creamSprite, new Vector2(190, 160), Vector2.zero);
                var button = buttonImage.gameObject.AddComponent<Button>();
                button.targetGraphic = buttonImage;
                ConfigureButton(button);
                controller.levelButtons[i] = button;
                controller.numberLabels[i] = TextObject("編號", button.transform, (i + 1).ToString("00"), font, 48,
                    new Vector2(120, 64), new Vector2(0, 36), Ink);
                var data = i < catalog.levels.Count ? catalog.levels[i] : null;
                controller.sizeLabels[i] = TextObject("棋盤大小", button.transform, data != null ? data.displayName + "\n" + data.products.Count + "種 " + data.TotalQuantity + "件" : "", font, 30,
                    new Vector2(174, 72), new Vector2(0, -20), Ink);
                controller.sizeLabels[i].overflowMode = TextOverflowModes.Ellipsis;
                controller.sizeLabels[i].maxVisibleLines = 2;
                button.gameObject.SetActive(data != null);
            }
            controller.selectedLabel = TextObject("目前選取", frame.transform, catalog.levels.Count > 0 ? "已選擇第 1 關" : "尚未加入關卡", font, 25,
                new Vector2(600, 36), new Vector2(0, -156), new Color(1f, .97f, .85f));
            controller.playButton = ButtonObject("開始遊戲", frame.transform, "開始遊戲", greenSprite, font, new Vector2(320, 86), new Vector2(0, -224), 32);
            controller.homeButton = ButtonObject("返回主頁", frame.transform, "返回主頁", creamSprite, font, new Vector2(186, 70), new Vector2(-377, -228), 22);
            controller.previousButton = ButtonObject("上一頁", frame.transform, "‹", creamSprite, font, new Vector2(76, 66), new Vector2(265, -228), 36);
            controller.nextButton = ButtonObject("下一頁", frame.transform, "›", creamSprite, font, new Vector2(76, 66), new Vector2(457, -228), 36);
            controller.previousButton.interactable = false;
            controller.nextButton.interactable = catalog.levels.Count > 8;
            controller.pageLabel = TextObject("頁碼", frame.transform, "1 / " + Mathf.Max(1, Mathf.CeilToInt(catalog.levels.Count / 8f)), font, 23,
                new Vector2(100, 40), new Vector2(361, -228), new Color(1f, .97f, .85f));
            controller.openButton = ButtonObject("返回選關", root.transform, "選關", creamSprite, font, new Vector2(146, 68), Vector2.zero, 25);
            var openRect = controller.openButton.GetComponent<RectTransform>();
            openRect.anchorMin = openRect.anchorMax = new Vector2(0, 1);
            openRect.anchoredPosition = new Vector2(102, -62);
            controller.openButton.gameObject.SetActive(false);
            var resultOverlay = ImageObject("過關畫面", root.transform, null, Vector2.zero, Vector2.zero);
            Stretch(resultOverlay.rectTransform);
            resultOverlay.color = new Color(0f, 0f, 0f, .55f);
            controller.completionPanel = resultOverlay.gameObject;
            var resultFrame = ImageObject("過關金框", resultOverlay.transform, frameSprite, new Vector2(710, 510), Vector2.zero);
            TextObject("過關標題", resultFrame.transform, "過關！", font, 44, new Vector2(500, 64), new Vector2(0, 174), new Color(1f, .97f, .85f));
            controller.completionText = TextObject("過關統計", resultFrame.transform, "", font, 28,
                new Vector2(500, 128), new Vector2(0, 76), new Color(1f, .97f, .85f));
            controller.completionNextButton = ButtonObject("下一關", resultFrame.transform, "下一關", greenSprite, font,
                new Vector2(390, 96), new Vector2(0, -60), 32);
            controller.completionSelectionButton = ButtonObject("返回選關", resultFrame.transform, "返回選關", creamSprite, font,
                new Vector2(300, 76), new Vector2(-165, -172), 26);
            controller.completionHomeButton = ButtonObject("返回首頁", resultFrame.transform, "返回首頁", creamSprite, font,
                new Vector2(300, 76), new Vector2(165, -172), 26);

            resultOverlay.gameObject.SetActive(false);
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("超市選關介面已建立；編輯器完成關卡後可直接加入 SupermarketLevelCatalog。");
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    private static Image ImageObject(string name, Transform parent, Sprite sprite, Vector2 size, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.rectTransform.sizeDelta = size;
        image.rectTransform.anchoredPosition = position;
        return image;
    }

    private static TMP_Text TextObject(string name, Transform parent, string text, TMP_FontAsset font, float fontSize, Vector2 size, Vector2 position, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = color;
        label.raycastTarget = false;
        label.rectTransform.sizeDelta = size;
        label.rectTransform.anchoredPosition = position;
        return label;
    }

    private static Button ButtonObject(string name, Transform parent, string text, Sprite sprite, TMP_FontAsset font, Vector2 size, Vector2 position, float fontSize)
    {
        var image = ImageObject(name, parent, sprite, size, position);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ConfigureButton(button);
        TextObject("文字", image.transform, text, font, fontSize, new Vector2(size.x * .75f, size.y * .65f), Vector2.zero, Ink);
        return button;
    }

    private static void ConfigureButton(Button button)
    {
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, .97f, .78f);
        colors.selectedColor = Color.white;
        colors.pressedColor = new Color(.78f, .88f, .85f);
        colors.disabledColor = new Color(.65f, .72f, .70f, .6f);
        button.colors = colors;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
