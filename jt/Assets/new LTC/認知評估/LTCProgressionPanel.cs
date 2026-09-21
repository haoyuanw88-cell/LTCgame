using System.Collections;
using System.Collections.Generic;
using System.Linq;
using LTCCognitiveAssessment;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Runtime UI for achievements and daily quests on the cognitive game catalog.</summary>
public sealed class LTCProgressionPanel : MonoBehaviour
{
    const string CatalogSceneName = "GameScene";
    readonly Color green = new Color(0.29f, 0.63f, 0.49f, 1f);
    readonly Color orange = new Color(0.94f, 0.57f, 0.25f, 1f);
    readonly Color cream = new Color(1f, 0.995f, 0.975f, 1f);
    TMP_FontAsset font;
    GameObject overlay;
    RectTransform content;
    TMP_Text title;
    TMP_Text summary;
    bool showAchievements;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryCreate(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { TryCreate(scene); }

    static void TryCreate(Scene scene)
    {
        if (scene.name != CatalogSceneName || FindFirstObjectByType<LTCProgressionPanel>() != null) return;
        new GameObject("LTC Achievement & Daily Quest UI").AddComponent<LTCProgressionPanel>();
    }

    IEnumerator Start()
    {
        CognitiveGameCatalogController catalog = null;
        for (int i = 0; i < 60 && catalog == null; i++)
        {
            catalog = FindFirstObjectByType<CognitiveGameCatalogController>();
            if (catalog == null) yield return null;
        }
        if (catalog == null) { Destroy(gameObject); yield break; }

        Transform canvas = catalog.transform.Find("Cognitive Catalog Canvas");
        if (canvas == null) { Destroy(gameObject); yield break; }
        TMP_Text sample = canvas.GetComponentInChildren<TMP_Text>(true);
        font = sample == null ? null : sample.font;
        Build(canvas);
        LTCProgressionService.Changed += Refresh;
        LTCProgressionService.RefreshForToday();
    }

    void OnDestroy() { LTCProgressionService.Changed -= Refresh; }

    void Build(Transform canvas)
    {
        Transform header = canvas.Find("遊戲首頁/玩家資訊列");
        if (header == null) return;

        Button open = Button(header, "成就任務", "成就任務", green);
        SetRect(open.GetComponent<RectTransform>(), new Vector2(0.59f, 0.18f), new Vector2(0.765f, 0.82f));
        open.onClick.AddListener(Open);

        overlay = Panel(canvas, "成就與每日任務", new Color(0.08f, 0.10f, 0.08f, 0.74f));
        GameObject card = Panel(overlay.transform, "內容卡片", cream);
        SetRect(card.GetComponent<RectTransform>(), new Vector2(0.13f, 0.07f), new Vector2(0.87f, 0.93f));

        title = Text(card.transform, "標題", "每日任務", 36, FontStyles.Bold, TextAlignmentOptions.Left);
        SetRect(title.rectTransform, new Vector2(0.06f, 0.86f), new Vector2(0.55f, 0.96f));

        summary = Text(card.transform, "摘要", "", 21, FontStyles.Bold, TextAlignmentOptions.Right);
        summary.color = green;
        SetRect(summary.rectTransform, new Vector2(0.52f, 0.87f), new Vector2(0.80f, 0.95f));

        Button close = Button(card.transform, "關閉", "關閉", new Color(0.58f, 0.59f, 0.55f, 1f));
        SetRect(close.GetComponent<RectTransform>(), new Vector2(0.82f, 0.87f), new Vector2(0.95f, 0.96f));
        close.onClick.AddListener(() => overlay.SetActive(false));

        Button daily = Button(card.transform, "每日任務分頁", "每日任務", orange);
        SetRect(daily.GetComponent<RectTransform>(), new Vector2(0.06f, 0.76f), new Vector2(0.28f, 0.84f));
        daily.onClick.AddListener(() => { showAchievements = false; Refresh(); });
        Button achievement = Button(card.transform, "成就分頁", "遊戲成就", green);
        SetRect(achievement.GetComponent<RectTransform>(), new Vector2(0.30f, 0.76f), new Vector2(0.52f, 0.84f));
        achievement.onClick.AddListener(() => { showAchievements = true; Refresh(); });

        GameObject viewport = Panel(card.transform, "清單視窗", new Color(0.95f, 0.94f, 0.89f, 1f));
        SetRect(viewport.GetComponent<RectTransform>(), new Vector2(0.06f, 0.07f), new Vector2(0.95f, 0.73f));
        var mask = viewport.AddComponent<RectMask2D>();
        mask.padding = new Vector4(8, 8, 8, 8);

        GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup),
            typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewport.transform, false);
        content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = new Vector2(14f, 0f);
        content.offsetMax = new Vector2(-14f, 0f);
        var layout = contentObject.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 10, 10);
        layout.spacing = 8f;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        contentObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = viewport.AddComponent<ScrollRect>();
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 35f;
        overlay.SetActive(false);
    }

    void Open()
    {
        if (overlay == null) return;
        LTCProgressionService.RefreshForToday();
        showAchievements = false;
        Refresh();
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
    }

    void Refresh()
    {
        if (content == null) return;
        foreach (Transform child in content) Destroy(child.gameObject);

        IReadOnlyList<LTCProgressionEntry> entries = showAchievements
            ? LTCProgressionService.Achievements
            : LTCProgressionService.DailyQuests;
        title.text = showAchievements ? "遊戲成就" : "每日任務";
        int completed = entries.Count(item => item.completed);
        summary.text = "已完成 " + completed + " / " + entries.Count;

        IEnumerable<LTCProgressionEntry> ordered = entries
            .OrderByDescending(item => item.completed)
            .ThenBy(item => item.gameId)
            .ThenBy(item => item.target);
        foreach (LTCProgressionEntry entry in ordered) AddRow(entry);
    }

    void AddRow(LTCProgressionEntry entry)
    {
        GameObject row = Panel(content, "項目_" + entry.id,
            entry.completed ? new Color(0.86f, 0.95f, 0.88f, 1f) : Color.white);
        var element = row.AddComponent<LayoutElement>();
        element.preferredHeight = 88f;
        element.minHeight = 88f;

        TMP_Text heading = Text(row.transform, "名稱", (entry.completed ? "✓ " : "○ ") + entry.title,
            22, FontStyles.Bold, TextAlignmentOptions.Left);
        heading.color = entry.completed ? green : new Color(0.20f, 0.24f, 0.21f);
        SetRect(heading.rectTransform, new Vector2(0.025f, 0.52f), new Vector2(0.66f, 0.92f));

        TMP_Text detail = Text(row.transform, "說明", entry.description, 17, FontStyles.Normal,
            TextAlignmentOptions.Left);
        detail.color = new Color(0.35f, 0.38f, 0.35f);
        SetRect(detail.rectTransform, new Vector2(0.025f, 0.08f), new Vector2(0.68f, 0.50f));

        string reward = entry.rewardGranted ? "已獲得 +" + entry.rewardCoins :
            entry.ProgressLabel + "\n獎勵 +" + entry.rewardCoins;
        TMP_Text progress = Text(row.transform, "進度", reward + " 金幣", 18, FontStyles.Bold,
            TextAlignmentOptions.Center);
        progress.color = entry.completed ? green : orange;
        SetRect(progress.rectTransform, new Vector2(0.70f, 0.10f), new Vector2(0.975f, 0.90f));
    }

    GameObject Panel(Transform parent, string name, Color color)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        obj.GetComponent<Image>().color = color;
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        return obj;
    }

    Button Button(Transform parent, string name, string label, Color color)
    {
        GameObject obj = Panel(parent, name, color);
        Button button = obj.AddComponent<Button>();
        TMP_Text text = Text(obj.transform, "文字", label, 20, FontStyles.Bold, TextAlignmentOptions.Center);
        text.color = Color.white;
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(8f, 4f), new Vector2(-8f, -4f));
        return button;
    }

    TMP_Text Text(Transform parent, string name, string value, float size, FontStyles style,
        TextAlignmentOptions alignment)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        TMP_Text text = obj.GetComponent<TMP_Text>();
        if (font != null) text.font = font;
        text.text = value; text.fontSize = size; text.fontStyle = style; text.alignment = alignment;
        text.enableWordWrapping = true; text.raycastTarget = false;
        return text;
    }

    static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
    {
        SetRect(rect, min, max, Vector2.zero, Vector2.zero);
    }

    static void SetRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
    }
}
