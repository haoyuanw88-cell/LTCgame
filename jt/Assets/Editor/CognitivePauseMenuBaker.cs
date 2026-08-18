using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 只在三個遊戲的 Canvas 上新增暫停 UI，不調整任何既有 UI。
/// </summary>
public static class CognitivePauseMenuBaker
{
    private const string SceneRoot = "Assets/new LTC/場景轉換/";
    private const string ArtRoot = "Assets/new LTC/遊戲素材/認知遊戲AI素材/暫停介面/";
    private const string FontPath = "Assets/new LTC/Unity中文/KAIU_Dynamic.asset";

    [MenuItem("Tools/LTC/Copy Stroop Pause Layout To Number Games")]
    public static void CopyStroopPauseLayoutToNumberGames()
    {
        string reopenPath = SceneManager.GetActiveScene().path;
        Scene sourceScene = EditorSceneManager.OpenScene(SceneRoot + "js.unity", OpenSceneMode.Single);
        Canvas sourceCanvas = FindInScene<Canvas>(sourceScene);
        CognitiveGamePauseMenu sourceController = sourceCanvas != null
            ? sourceCanvas.GetComponent<CognitiveGamePauseMenu>()
            : null;

        if (sourceController == null || sourceController.pauseButton == null || sourceController.pausePanel == null)
            throw new MissingReferenceException("顏色文字判斷場景缺少完整的暫停介面，無法作為複製來源。");

        CopyPauseLayoutToScene(sourceController, SceneRoot + "mb.unity");
        CopyPauseLayoutToScene(sourceController, SceneRoot + "mb2.unity");

        if (!string.IsNullOrEmpty(reopenPath))
            EditorSceneManager.OpenScene(reopenPath, OpenSceneMode.Single);

        Debug.Log("已把顏色文字判斷的暫停介面完整複製到數字排序與數字加總；其他 UI 未變更。");
    }

    private static void CopyPauseLayoutToScene(CognitiveGamePauseMenu source, string targetPath)
    {
        Scene targetScene = EditorSceneManager.OpenScene(targetPath, OpenSceneMode.Additive);
        Canvas targetCanvas = FindInScene<Canvas>(targetScene);
        if (targetCanvas == null)
            throw new MissingReferenceException(targetPath + " 找不到 Canvas");

        Transform oldPauseButton = targetCanvas.transform.Find("暫停按鈕");
        Transform oldPausePanel = targetCanvas.transform.Find("認知遊戲暫停選單");
        if (oldPauseButton != null) Object.DestroyImmediate(oldPauseButton.gameObject);
        if (oldPausePanel != null) Object.DestroyImmediate(oldPausePanel.gameObject);

        GameObject pauseButtonObject = Object.Instantiate(source.pauseButton.gameObject);
        pauseButtonObject.name = source.pauseButton.gameObject.name;
        SceneManager.MoveGameObjectToScene(pauseButtonObject, targetScene);
        pauseButtonObject.transform.SetParent(targetCanvas.transform, false);

        GameObject pausePanelObject = Object.Instantiate(source.pausePanel);
        pausePanelObject.name = source.pausePanel.name;
        SceneManager.MoveGameObjectToScene(pausePanelObject, targetScene);
        pausePanelObject.transform.SetParent(targetCanvas.transform, false);

        CognitiveGamePauseMenu targetController = targetCanvas.GetComponent<CognitiveGamePauseMenu>();
        if (targetController == null)
            targetController = targetCanvas.gameObject.AddComponent<CognitiveGamePauseMenu>();

        targetController.pauseButton = pauseButtonObject.GetComponent<Button>();
        targetController.pausePanel = pausePanelObject;
        targetController.resumeButton = FindClonedComponent(source.pausePanel.transform,
            source.resumeButton, pausePanelObject.transform);
        targetController.homeButton = FindClonedComponent(source.pausePanel.transform,
            source.homeButton, pausePanelObject.transform);
        targetController.messageText = FindClonedComponent(source.pausePanel.transform,
            source.messageText, pausePanelObject.transform);
        targetController.gameHomeScene = source.gameHomeScene;

        pauseButtonObject.transform.SetAsLastSibling();
        pausePanelObject.transform.SetAsLastSibling();
        pausePanelObject.SetActive(false);

        EditorUtility.SetDirty(targetController);
        EditorSceneManager.MarkSceneDirty(targetScene);
        EditorSceneManager.SaveScene(targetScene);
        EditorSceneManager.CloseScene(targetScene, true);
    }

    private static T FindClonedComponent<T>(Transform sourceRoot, T sourceComponent, Transform cloneRoot)
        where T : Component
    {
        if (sourceComponent == null) return null;
        string path = GetRelativePath(sourceRoot, sourceComponent.transform);
        Transform clone = string.IsNullOrEmpty(path) ? cloneRoot : cloneRoot.Find(path);
        if (clone == null)
            throw new MissingReferenceException("複製暫停介面後找不到子物件：" + path);
        return clone.GetComponent<T>();
    }

    private static string GetRelativePath(Transform root, Transform target)
    {
        if (target == root) return string.Empty;
        string path = target.name;
        Transform current = target.parent;
        while (current != null && current != root)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
        if (current != root)
            throw new MissingReferenceException(target.name + " 不在暫停表單之下。");
        return path;
    }

    [MenuItem("Tools/LTC/Add Cognitive Pause Menus")]
    public static void BakeAll()
    {
        string reopenPath = SceneManager.GetActiveScene().path;
        PrepareSprite(ArtRoot + "pause_icon.png");
        PrepareSprite(ArtRoot + "pause_modal.png");
        PrepareSprite(ArtRoot + "resume_button.png");
        PrepareSprite(ArtRoot + "home_button.png");

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null) throw new MissingReferenceException("找不到中文字型：" + FontPath);

        BakeScene(SceneRoot + "js.unity", font);
        BakeScene(SceneRoot + "mb.unity", font);
        BakeScene(SceneRoot + "mb2.unity", font);

        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(reopenPath))
            EditorSceneManager.OpenScene(reopenPath, OpenSceneMode.Single);
        Debug.Log("三款認知遊戲的暫停選單已加入 Scene。既有 UI 未調整。");
    }

    private static void BakeScene(string path, TMP_FontAsset font)
    {
        Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        Canvas canvas = FindInScene<Canvas>(scene);
        if (canvas == null) throw new MissingReferenceException(path + " 找不到 Canvas");

        Button pauseButton = EnsureButton(canvas.transform, "暫停按鈕",
            AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "pause_icon.png"), font, "",
            new Vector2(1f, 1f), new Vector2(-68f, -68f), new Vector2(92f, 92f), 1f);
        pauseButton.GetComponent<Image>().preserveAspect = true;

        GameObject panel = EnsurePanel(canvas.transform, "認知遊戲暫停選單");
        Image modal = EnsureImage(panel.transform, "暫停表單框",
            AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "pause_modal.png"), Color.white,
            new Vector2(.5f, .5f), Vector2.zero, new Vector2(710f, 510f));
        modal.preserveAspect = true;

        EnsureText(modal.transform, "標題", font, "遊戲已暫停", 43f, FontStyles.Bold,
            new Color32(255, 250, 226, 255), new Vector2(.10f, .76f), new Vector2(.90f, .92f));
        TMP_Text message = EnsureText(modal.transform, "暫停說明", font,
            "返回遊戲時，會捨棄目前這一題，\n恢復到上一題完成時的剩餘時間，\n並重新產生相同難度的新題目。",
            26f, FontStyles.Normal, new Color32(31, 76, 78, 255),
            new Vector2(.10f, .42f), new Vector2(.90f, .75f));

        Button resume = EnsureButton(modal.transform, "返回遊戲按鈕",
            AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "resume_button.png"), font, "返回遊戲",
            new Vector2(.5f, .29f), Vector2.zero, new Vector2(390f, 112f), 32f);
        Button home = EnsureButton(modal.transform, "回到主頁按鈕",
            AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "home_button.png"), font, "回到遊戲主頁",
            new Vector2(.5f, .09f), Vector2.zero, new Vector2(390f, 112f), 31f);

        CognitiveGamePauseMenu controller = canvas.GetComponent<CognitiveGamePauseMenu>();
        if (controller == null) controller = canvas.gameObject.AddComponent<CognitiveGamePauseMenu>();
        controller.pauseButton = pauseButton;
        controller.pausePanel = panel;
        controller.resumeButton = resume;
        controller.homeButton = home;
        controller.messageText = message;
        controller.gameHomeScene = "GameScene";

        pauseButton.transform.SetAsLastSibling();
        panel.transform.SetAsLastSibling();
        panel.SetActive(false);
        EditorUtility.SetDirty(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static GameObject EnsurePanel(Transform canvas, string name)
    {
        Transform existing = canvas.Find(name);
        GameObject go = existing != null ? existing.gameObject :
            new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        if (existing == null) go.transform.SetParent(canvas, false);
        SetLayerRecursively(go, LayerMask.NameToLayer("UI"));

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        Image overlay = go.GetComponent<Image>();
        overlay.sprite = null;
        overlay.color = new Color(0.035f, 0.08f, 0.09f, .72f);
        overlay.raycastTarget = true;
        return go;
    }

    private static Button EnsureButton(Transform parent, string name, Sprite sprite, TMP_FontAsset font,
        string labelText, Vector2 anchor, Vector2 position, Vector2 size, float fontSize)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject :
            new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        if (existing == null) go.transform.SetParent(parent, false);
        SetLayerRecursively(go, LayerMask.NameToLayer("UI"));

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = Color.white;
        image.preserveAspect = true;
        image.raycastTarget = true;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, .96f, .83f, 1f);
        colors.pressedColor = new Color(.84f, .91f, .88f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.colorMultiplier = 1f;
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        if (!string.IsNullOrEmpty(labelText))
        {
            TMP_Text label = EnsureText(go.transform, "文字", font, labelText, fontSize, FontStyles.Bold,
                new Color32(37, 75, 77, 255), new Vector2(.08f, .16f), new Vector2(.92f, .84f));
            label.enableAutoSizing = true;
            label.fontSizeMin = 22f;
            label.fontSizeMax = fontSize;
        }
        return button;
    }

    private static Image EnsureImage(Transform parent, string name, Sprite sprite, Color color,
        Vector2 anchor, Vector2 position, Vector2 size)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject :
            new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        if (existing == null) go.transform.SetParent(parent, false);
        SetLayerRecursively(go, LayerMask.NameToLayer("UI"));

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;

        Image image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text EnsureText(Transform parent, string name, TMP_FontAsset font, string value,
        float fontSize, FontStyles style, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        Transform existing = parent.Find(name);
        GameObject go = existing != null ? existing.gameObject :
            new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        if (existing == null) go.transform.SetParent(parent, false);
        SetLayerRecursively(go, LayerMask.NameToLayer("UI"));

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        return text;
    }

    private static void PrepareSprite(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            importer = AssetImporter.GetAtPath(path) as TextureImporter;
        }
        if (importer == null) throw new MissingReferenceException("找不到暫停 UI 素材：" + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.SaveAndReimport();
    }

    private static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T result = root.GetComponentInChildren<T>(true);
            if (result != null) return result;
        }
        return null;
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        if (layer < 0) layer = 5;
        go.layer = layer;
        foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
    }
}
