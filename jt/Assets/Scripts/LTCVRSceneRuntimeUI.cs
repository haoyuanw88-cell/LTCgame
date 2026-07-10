using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public class LTCVRSceneRuntimeUI : MonoBehaviour
{
    public Camera uiCamera;
    private TMP_FontAsset kaiuFont;

    void Start()
    {
        if (uiCamera == null) uiCamera = Camera.main;
        kaiuFont = Resources.Load<TMP_FontAsset>("KAIU");
        if (kaiuFont == null) kaiuFont = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/中文/KAIU.asset");
        EnsureEventSystem();
        CreateGameSelectPanel();
    }

    void EnsureEventSystem()
    {
        EventSystem existing = FindFirstObjectByType<EventSystem>();
        GameObject eventSystemObject = existing == null ? new GameObject("EventSystem") : existing.gameObject;
        if (existing == null) eventSystemObject.AddComponent<EventSystem>();
        StandaloneInputModule oldModule = eventSystemObject.GetComponent<StandaloneInputModule>();
        if (oldModule != null) Destroy(oldModule);
        if (eventSystemObject.GetComponent<InputSystemUIInputModule>() == null) eventSystemObject.AddComponent<InputSystemUIInputModule>();
    }

    void CreateGameSelectPanel()
    {
        GameObject old = GameObject.Find("Runtime_GameSelect_WorldCanvas");
        if (old != null) Destroy(old);

        GameObject canvasObject = new GameObject("Runtime_GameSelect_WorldCanvas");
        canvasObject.transform.position = new Vector3(0f, 1.55f, 0.86f);
        canvasObject.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = uiCamera;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(360f, 260f);
        canvasObject.transform.localScale = Vector3.one * 0.005f;
        canvasObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 15f;
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject background = CreateImage("PanelBackground", canvasObject.transform, new Color(0.03f, 0.08f, 0.12f, 0.55f));
        Stretch(background.GetComponent<RectTransform>());

        CreateText("Title", "遊戲選擇", canvasObject.transform, new Vector2(0f, 98f), new Vector2(320f, 42f), 28, Color.white);
        CreateText("Hint", "Esc 顯示滑鼠 拖曳清單上下滑動", canvasObject.transform, new Vector2(0f, 70f), new Vector2(330f, 26f), 13, new Color(0.85f, 0.95f, 1f, 0.9f));

        GameObject scrollView = new GameObject("GameScrollView");
        scrollView.transform.SetParent(canvasObject.transform, false);
        RectTransform scrollRectTransform = scrollView.AddComponent<RectTransform>();
        scrollRectTransform.anchorMin = new Vector2(0.08f, 0.08f);
        scrollRectTransform.anchorMax = new Vector2(0.92f, 0.68f);
        scrollRectTransform.offsetMin = Vector2.zero;
        scrollRectTransform.offsetMax = Vector2.zero;

        ScrollRect scrollRect = scrollView.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = true;

        GameObject viewport = CreateImage("Viewport", scrollView.transform, new Color(1f, 1f, 1f, 0.04f));
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        Stretch(viewportRect);
        Mask mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        scrollRect.viewport = viewportRect;

        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 330f);
        scrollRect.content = contentRect;

        string[] games = { "打地鼠反應訓練", "數字排序訓練", "加總計算訓練", "色字判斷訓練", "記憶卡牌訓練" };
        for (int i = 0; i < games.Length; i++) CreateGameButton(games[i], content.transform, i);
    }

    GameObject CreateGameButton(string label, Transform parent, int index)
    {
        GameObject buttonObject = CreateImage("GameButton_" + label, parent, new Color(0.12f, 0.45f, 0.65f, 0.86f));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.05f, 1f);
        rect.anchorMax = new Vector2(0.95f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -index * 64f - 8f);
        rect.sizeDelta = new Vector2(0f, 52f);
        Button button = buttonObject.AddComponent<Button>();
        button.onClick.AddListener(() => Debug.Log("選擇遊戲 " + label));
        CreateText("Label", label, buttonObject.transform, Vector2.zero, new Vector2(250f, 42f), 20, Color.white);
        return buttonObject;
    }

    GameObject CreateImage(string name, Transform parent, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<Image>().color = color;
        return obj;
    }

    TMP_Text CreateText(string name, string text, Transform parent, Vector2 position, Vector2 size, int fontSize, Color color)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        if (kaiuFont != null) tmp.font = kaiuFont;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return tmp;
    }

    void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
