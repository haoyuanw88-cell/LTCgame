using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public static class LTCReturnHomeButton
{
    private const string CanvasName = "Return Home Button Canvas";
    private const string ButtonName = "Return Home Button";
    private const string HomeSceneName = "GameScene";

    public static Button Show()
    {
        EnsureEventSystem();

        Canvas canvas = FindOrCreateCanvas();
        Transform existing = canvas.transform.Find(ButtonName);
        if (existing != null && existing.TryGetComponent(out Button existingButton))
        {
            existing.gameObject.SetActive(true);
            existing.transform.SetAsLastSibling();
            return existingButton;
        }

        GameObject buttonObject = new GameObject(ButtonName);
        buttonObject.transform.SetParent(canvas.transform, false);

        RectTransform rect = buttonObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 86f);
        rect.sizeDelta = new Vector2(360f, 96f);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(1f, 0.88f, 0.24f, 0.98f);

        Button button = buttonObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(1f, 0.96f, 0.48f, 1f);
        colors.pressedColor = new Color(0.92f, 0.72f, 0.12f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.75f, 0.75f, 0.75f, 0.8f);
        button.colors = colors;
        button.onClick.AddListener(ReturnHome);

        GameObject textObject = new GameObject("Label");
        textObject.transform.SetParent(buttonObject.transform, false);

        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text label = textObject.AddComponent<Text>();
        label.font = CreateChineseFont();
        label.text = "返回首頁";
        label.fontSize = 42;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.black;
        label.raycastTarget = false;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 28;
        label.resizeTextMaxSize = 42;

        buttonObject.transform.SetAsLastSibling();
        return button;
    }

    public static void Hide()
    {
        GameObject canvasObject = GameObject.Find(CanvasName);
        if (canvasObject == null) return;

        Transform button = canvasObject.transform.Find(ButtonName);
        if (button != null) button.gameObject.SetActive(false);
    }

    private static Canvas FindOrCreateCanvas()
    {
        GameObject canvasObject = GameObject.Find(CanvasName);
        if (canvasObject != null && canvasObject.TryGetComponent(out Canvas existingCanvas))
            return existingCanvas;

        canvasObject = new GameObject(CanvasName);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32000;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<EventSystem>() != null) return;

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
    }

    private static void ReturnHome()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(HomeSceneName);
    }

    private static Font CreateChineseFont()
    {
        return Font.CreateDynamicFontFromOSFont(
            new[]
            {
                "Microsoft JhengHei UI",
                "Microsoft JhengHei",
                "Noto Sans CJK TC",
                "PingFang TC",
                "Arial Unicode MS",
                "SimHei"
            },
            42);
    }
}
