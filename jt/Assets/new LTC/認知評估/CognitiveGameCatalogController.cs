using System;
using System.Collections.Generic;
using System.Linq;
using LTCCognitiveAssessment;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[ExecuteAlways]
public class CognitiveGameCatalogController : MonoBehaviour
{
    private const string CanvasName = "Cognitive Catalog Canvas";

    [Header("Chinese UI")]
    [SerializeField] private TMP_FontAsset chineseFont;
    [SerializeField] private Sprite avatarSprite;
    [SerializeField] private Sprite coinPanelSprite;
    [SerializeField] private Sprite pageBackgroundSprite;

    [Serializable]
    private class GameDefinition
    {
        public string title;
        public string domainTitle;
        public string domainSummary;
        public string summary;
        public string instructions;
        public string sceneName;
        public CognitiveDomain domain;
        public bool available = true;
        public bool experimental;
    }

    private readonly List<GameDefinition> games = new List<GameDefinition>
    {
        new GameDefinition
        {
            title = "顏色文字判斷", domainTitle = "注意力與抑制控制",
            domainSummary = "專注目標，排除不相關資訊的干擾",
            summary = "觀察面對干擾資訊時，是否能維持專注並做出正確判斷。",
            instructions = "比較畫面中的文字與顏色，選擇相同或不同。系統會記錄正確率、反應時間與干擾差值。",
            sceneName = "js", domain = CognitiveDomain.AttentionInhibitoryControl
        },
        new GameDefinition
        {
            title = "圖形專注挑戰", domainTitle = "注意力與抑制控制",
            domainSummary = "專注目標，排除不相關資訊的干擾",
            summary = "尋找指定圖形並忽略干擾物。", instructions = "此遊戲正在開發中。",
            domain = CognitiveDomain.AttentionInhibitoryControl, available = false
        },
        new GameDefinition
        {
            title = "數字由小到大", domainTitle = "處理速度與視覺搜尋",
            domainSummary = "快速搜尋、辨認並依照規則完成操作",
            summary = "觀察搜尋數字、辨認順序與持續作答的速度和穩定性。",
            instructions = "依照由小到大的順序點選所有數字。系統會記錄每次點擊時間、順序錯誤與完成難度。",
            sceneName = "mb", domain = CognitiveDomain.ProcessingSpeedVisualSearch
        },
        new GameDefinition
        {
            title = "符號配對", domainTitle = "處理速度與視覺搜尋",
            domainSummary = "快速搜尋、辨認並依照規則完成操作",
            summary = "依規則快速配對符號。", instructions = "此遊戲正在開發中。",
            domain = CognitiveDomain.ProcessingSpeedVisualSearch, available = false
        },
        new GameDefinition
        {
            title = "數字組合加總", domainTitle = "執行功能與數字操作",
            domainSummary = "規劃步驟、暫存資訊並修正策略",
            summary = "觀察暫存數字、選擇策略及修正操作的能力。",
            instructions = "選擇數字，使加總結果等於目標。系統會記錄完成率、操作步數、反應時間與錯誤嘗試。",
            sceneName = "mb2", domain = CognitiveDomain.ExecutiveFunctionNumericalReasoning
        },
        new GameDefinition
        {
            title = "順序規劃", domainTitle = "執行功能與數字操作",
            domainSummary = "規劃步驟、暫存資訊並修正策略",
            summary = "依限制安排正確的操作順序。", instructions = "此遊戲正在開發中。",
            domain = CognitiveDomain.ExecutiveFunctionNumericalReasoning, available = false
        },
        new GameDefinition
        {
            title = "手勢打地鼠", domainTitle = "認知與動作互動",
            domainSummary = "結合視覺注意、反應速度與手眼協調",
            summary = "結合視覺注意、反應速度、手眼協調與手勢操作。",
            instructions = "使用攝影機辨識手勢抓取出現的地鼠。此結果暫不納入核心認知總結。",
            sceneName = "gopher", domain = CognitiveDomain.ProcessingSpeedVisualSearch, experimental = true
        },
        new GameDefinition
        {
            title = "動作追蹤", domainTitle = "認知與動作互動",
            domainSummary = "結合視覺注意、反應速度與手眼協調",
            summary = "追蹤目標並完成指定動作。", instructions = "此遊戲正在開發中。",
            domain = CognitiveDomain.ProcessingSpeedVisualSearch, available = false, experimental = true
        }
    };

    private readonly Color backgroundColor = new Color(0.965f, 0.945f, 0.90f, 1f);
    private readonly Color surfaceColor = new Color(1f, 0.995f, 0.975f, 1f);
    private readonly Color cardColor = new Color(1f, 0.995f, 0.975f, 1f);
    private readonly Color accentColor = new Color(0.29f, 0.63f, 0.49f, 1f);
    private readonly Color orangeColor = new Color(0.94f, 0.57f, 0.25f, 1f);
    private readonly Color mutedColor = new Color(0.66f, 0.66f, 0.61f, 1f);
    private readonly Color textColor = new Color(0.20f, 0.24f, 0.21f, 1f);

    private GameObject catalogPage;
    private GameObject statisticsPage;
    private GameObject profilePage;
    private GameObject detailPage;
    private GameObject bottomNavigation;
    private TMP_Text headerName;
    private TMP_Text headerCoins;
    private TMP_Text statisticsChartLabel;
    private TMP_Text statisticsAttentionScore;
    private TMP_Text statisticsSpeedScore;
    private TMP_Text statisticsExecutiveScore;
    private CognitiveTrendChartGraphic trendChart;
    private TMP_Text profileText;
    private TMP_Text detailDomain;
    private TMP_Text detailTitle;
    private TMP_Text detailSummary;
    private TMP_Text detailInstructions;
    private TMP_Text detailRecord;
    private GameDefinition selectedGame;
    private ChartSelection chartSelection = ChartSelection.Composite;

    private enum ChartSelection
    {
        Composite,
        Attention,
        ProcessingSpeed,
        Executive
    }

    private void Awake()
    {
        EnsureInterfaceExists();
        if (Application.isPlaying) BindExistingInterface();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) EnsureInterfaceExists();
    }

    private void EnsureInterfaceExists()
    {
        if (transform.Find(CanvasName) == null) BuildInterface();
    }

    private void BuildInterface()
    {
        EnsureEventSystem();
        var canvasObject = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1366f, 768f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        catalogPage = CreatePanel(canvasObject.transform, "遊戲首頁", backgroundColor, Vector2.zero, Vector2.one);
        ApplyPageBackground(catalogPage);
        BuildCatalogPage(catalogPage.transform);
        statisticsPage = CreatePanel(canvasObject.transform, "統計頁", backgroundColor, Vector2.zero, Vector2.one);
        ApplyPageBackground(statisticsPage);
        BuildStatisticsPage(statisticsPage.transform);
        profilePage = CreatePanel(canvasObject.transform, "我的頁", backgroundColor, Vector2.zero, Vector2.one);
        ApplyPageBackground(profilePage);
        BuildProfilePage(profilePage.transform);
        detailPage = CreatePanel(canvasObject.transform, "遊戲詳情", backgroundColor, Vector2.zero, Vector2.one);
        ApplyPageBackground(detailPage);
        BuildDetailPage(detailPage.transform);
        bottomNavigation = BuildBottomNavigation(canvasObject.transform);

        statisticsPage.SetActive(false);
        profilePage.SetActive(false);
        detailPage.SetActive(false);
    }

    private void BuildCatalogPage(Transform parent)
    {
        BuildProfileHeader(parent);

        TMP_Text title = CreateText(parent, "主標題", "選擇今天想了解的認知能力", 38, FontStyles.Bold,
            TextAlignmentOptions.Left);
        title.color = textColor;
        SetRect(title.rectTransform, new Vector2(0.055f, 0.75f), new Vector2(0.95f, 0.84f), Vector2.zero, Vector2.zero);

        TMP_Text subtitle = CreateText(parent, "副標題", "每項能力包含不同遊戲，左右滑動查看更多", 22,
            FontStyles.Normal, TextAlignmentOptions.Left);
        subtitle.color = new Color(0.39f, 0.43f, 0.39f);
        SetRect(subtitle.rectTransform, new Vector2(0.055f, 0.705f), new Vector2(0.95f, 0.765f), Vector2.zero,
            Vector2.zero);

        GameObject scrollObject = new GameObject("能力分類滑動區", typeof(RectTransform), typeof(Image),
            typeof(ScrollRect));
        scrollObject.transform.SetParent(parent, false);
        scrollObject.GetComponent<Image>().color = new Color(0.94f, 0.92f, 0.86f, 1f);
        SetRect(scrollObject.GetComponent<RectTransform>(), new Vector2(0.035f, 0.17f), new Vector2(0.965f, 0.70f),
            Vector2.zero, Vector2.zero);

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollObject.transform, false);
        viewport.GetComponent<Image>().color = Color.white;
        viewport.GetComponent<Mask>().showMaskGraphic = false;
        Stretch(viewport.GetComponent<RectTransform>(), 12f);

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(HorizontalLayoutGroup),
            typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 0f);
        contentRect.anchorMax = new Vector2(0f, 1f);
        contentRect.pivot = new Vector2(0f, 0.5f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;
        HorizontalLayoutGroup layout = content.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 22f;
        layout.padding = new RectOffset(18, 18, 16, 16);
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = contentRect;
        scroll.horizontal = true;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 40f;

        foreach (IGrouping<string, GameDefinition> domainGroup in games.GroupBy(game => game.domainTitle))
            CreateDomainCard(content.transform, domainGroup.Key, domainGroup.ToList());
    }

    private void BuildProfileHeader(Transform parent)
    {
        GameObject header = CreatePanel(parent, "玩家資訊列", surfaceColor, new Vector2(0.035f, 0.855f),
            new Vector2(0.965f, 0.98f));
        AddSoftShadow(header);

        GameObject avatar = new GameObject("頭像", typeof(RectTransform), typeof(Image));
        avatar.transform.SetParent(header.transform, false);
        Image avatarImage = avatar.GetComponent<Image>();
        avatarImage.sprite = avatarSprite;
        avatarImage.color = avatarSprite == null ? accentColor : Color.white;
        avatarImage.preserveAspect = true;
        SetRect(avatar.GetComponent<RectTransform>(), new Vector2(0.025f, 0.10f), new Vector2(0.095f, 0.90f),
            Vector2.zero, Vector2.zero);

        headerName = CreateText(header.transform, "玩家名稱", GetPlayerName(), 28, FontStyles.Bold,
            TextAlignmentOptions.Left);
        headerName.color = textColor;
        SetRect(headerName.rectTransform, new Vector2(0.115f, 0.15f), new Vector2(0.55f, 0.85f), Vector2.zero,
            Vector2.zero);

        GameObject coinPanel = CreatePanel(header.transform, "金幣區", new Color(1f, 0.91f, 0.68f, 1f),
            new Vector2(0.78f, 0.18f), new Vector2(0.965f, 0.82f));
        Image coinImage = coinPanel.GetComponent<Image>();
        if (coinPanelSprite != null)
        {
            coinImage.sprite = coinPanelSprite;
            coinImage.color = Color.white;
            coinImage.preserveAspect = false;
        }
        headerCoins = CreateText(coinPanel.transform, "金幣數量", CoinData.TotalCoins.ToString(), 26, FontStyles.Bold,
            TextAlignmentOptions.Center);
        headerCoins.color = new Color(0.48f, 0.31f, 0.08f, 1f);
        SetRect(headerCoins.rectTransform, new Vector2(0.31f, 0.08f), new Vector2(0.94f, 0.92f), Vector2.zero,
            Vector2.zero);
    }

    private void CreateDomainCard(Transform parent, string domainTitle, List<GameDefinition> domainGames)
    {
        GameObject card = CreatePanel(parent, domainTitle, cardColor, Vector2.zero, Vector2.one);
        AddSoftShadow(card);
        LayoutElement element = card.AddComponent<LayoutElement>();
        element.preferredWidth = 465f;
        element.minWidth = 465f;
        element.preferredHeight = 365f;
        element.minHeight = 350f;

        TMP_Text domain = CreateText(card.transform, "能力名稱", domainTitle, 29, FontStyles.Bold,
            TextAlignmentOptions.Left);
        domain.color = domainGames[0].experimental ? orangeColor : accentColor;
        SetRect(domain.rectTransform, new Vector2(0.06f, 0.78f), new Vector2(0.79f, 0.94f), Vector2.zero,
            Vector2.zero);

        GameObject badge = CreatePanel(card.transform, "能力徽章",
            domainGames[0].experimental ? orangeColor : accentColor, new Vector2(0.82f, 0.77f),
            new Vector2(0.94f, 0.94f));
        TMP_Text badgeText = CreateText(badge.transform, "文字", DomainBadge(domainTitle), 28, FontStyles.Bold,
            TextAlignmentOptions.Center);
        badgeText.color = Color.white;
        Stretch(badgeText.rectTransform, 2f);

        TMP_Text summary = CreateText(card.transform, "能力說明", domainGames[0].domainSummary, 20, FontStyles.Normal,
            TextAlignmentOptions.Left);
        summary.color = textColor;
        SetRect(summary.rectTransform, new Vector2(0.06f, 0.65f), new Vector2(0.94f, 0.80f), Vector2.zero,
            Vector2.zero);

        for (int i = 0; i < domainGames.Count && i < 2; i++)
        {
            GameDefinition game = domainGames[i];
            Color buttonColor = game.available ? (i == 0 ? accentColor : orangeColor) : mutedColor;
            string label = game.available ? game.title + "　查看" : game.title + "　開發中";
            Button button = CreateButton(card.transform, "遊戲_" + game.title, label, buttonColor);
            float top = 0.60f - i * 0.25f;
            SetRect(button.GetComponent<RectTransform>(), new Vector2(0.06f, top - 0.19f),
                new Vector2(0.94f, top), Vector2.zero, Vector2.zero);
            button.interactable = game.available;
        }
    }

    private void BuildStatisticsPage(Transform parent)
    {
        TMP_Text title = CreateText(parent, "統計標題", "過去 30 天認知趨勢", 40, FontStyles.Bold,
            TextAlignmentOptions.Center);
        title.color = textColor;
        SetRect(title.rectTransform, new Vector2(0.08f, 0.885f), new Vector2(0.92f, 0.97f), Vector2.zero,
            Vector2.zero);

        TMP_Text subtitle = CreateText(parent, "統計副標題", "分數用來觀察個人長期變化，不代表醫療診斷", 20,
            FontStyles.Normal, TextAlignmentOptions.Center);
        subtitle.color = new Color(0.43f, 0.46f, 0.42f);
        SetRect(subtitle.rectTransform, new Vector2(0.08f, 0.83f), new Vector2(0.92f, 0.89f), Vector2.zero,
            Vector2.zero);

        statisticsAttentionScore = CreateScoreCard(parent, "注意力摘要", "注意力與抑制", accentColor, 0.06f, 0.34f);
        statisticsSpeedScore = CreateScoreCard(parent, "速度摘要", "處理速度", orangeColor, 0.36f, 0.64f);
        statisticsExecutiveScore = CreateScoreCard(parent, "執行摘要", "執行功能", new Color(0.49f, 0.48f, 0.72f),
            0.66f, 0.94f);

        GameObject chartPanel = CreatePanel(parent, "30天趨勢圖", surfaceColor, new Vector2(0.06f, 0.285f),
            new Vector2(0.94f, 0.65f));
        AddSoftShadow(chartPanel);
        statisticsChartLabel = CreateText(chartPanel.transform, "目前能力", "綜合認知能力", 24, FontStyles.Bold,
            TextAlignmentOptions.Left);
        statisticsChartLabel.color = textColor;
        SetRect(statisticsChartLabel.rectTransform, new Vector2(0.05f, 0.84f), new Vector2(0.72f, 0.97f),
            Vector2.zero, Vector2.zero);
        TMP_Text range = CreateText(chartPanel.transform, "分數範圍", "100\n\n50\n\n0", 16, FontStyles.Normal,
            TextAlignmentOptions.Right);
        range.color = mutedColor;
        SetRect(range.rectTransform, new Vector2(0.01f, 0.12f), new Vector2(0.07f, 0.80f), Vector2.zero,
            Vector2.zero);

        Color grid = new Color(0.36f, 0.43f, 0.37f, 0.28f);
        for (int i = 0; i <= 4; i++)
        {
            float y = Mathf.Lerp(0.16f, 0.80f, i / 4f);
            GameObject line = CreatePanel(chartPanel.transform, "水平格線_" + i, grid,
                new Vector2(0.08f, y), new Vector2(0.96f, y));
            line.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, i == 0 ? 4f : 2f);
            line.GetComponent<Image>().raycastTarget = false;
        }
        for (int i = 0; i <= 6; i++)
        {
            float x = Mathf.Lerp(0.08f, 0.96f, i / 6f);
            GameObject line = CreatePanel(chartPanel.transform, "垂直格線_" + i, grid,
                new Vector2(x, 0.16f), new Vector2(x, 0.80f));
            line.GetComponent<RectTransform>().sizeDelta = new Vector2(2f, 0f);
            line.GetComponent<Image>().raycastTarget = false;
        }

        GameObject chartObject = new GameObject("折線圖", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(CognitiveTrendChartGraphic));
        chartObject.transform.SetParent(chartPanel.transform, false);
        trendChart = chartObject.GetComponent<CognitiveTrendChartGraphic>();
        SetRect(chartObject.GetComponent<RectTransform>(), new Vector2(0.08f, 0.16f), new Vector2(0.96f, 0.80f),
            Vector2.zero, Vector2.zero);
        TMP_Text days = CreateText(chartPanel.transform, "日期刻度", "30天前　　　　　　20天前　　　　　　10天前　　　　　　今天",
            15, FontStyles.Normal, TextAlignmentOptions.Center);
        days.color = mutedColor;
        SetRect(days.rectTransform, new Vector2(0.08f, 0.02f), new Vector2(0.96f, 0.15f), Vector2.zero,
            Vector2.zero);

        CreateStatisticsFilter(parent, "綜合", "綜合", accentColor, 0.06f, 0.265f);
        CreateStatisticsFilter(parent, "注意力", "注意力", new Color(0.37f, 0.69f, 0.57f), 0.275f, 0.475f);
        CreateStatisticsFilter(parent, "處理速度", "處理速度", orangeColor, 0.485f, 0.685f);
        CreateStatisticsFilter(parent, "執行功能", "執行功能", new Color(0.49f, 0.48f, 0.72f), 0.695f, 0.94f);
    }

    private void BuildProfilePage(Transform parent)
    {
        TMP_Text title = CreateText(parent, "我的標題", "我的資料", 42, FontStyles.Bold,
            TextAlignmentOptions.Center);
        title.color = textColor;
        SetRect(title.rectTransform, new Vector2(0.08f, 0.82f), new Vector2(0.92f, 0.94f), Vector2.zero,
            Vector2.zero);
        GameObject panel = CreatePanel(parent, "個人資料內容", surfaceColor, new Vector2(0.07f, 0.20f),
            new Vector2(0.93f, 0.80f));
        profileText = CreateText(panel.transform, "個人資料", BuildProfileText(), 28, FontStyles.Normal,
            TextAlignmentOptions.TopLeft);
        profileText.color = textColor;
        SetRect(profileText.rectTransform, new Vector2(0.07f, 0.10f), new Vector2(0.93f, 0.90f), Vector2.zero,
            Vector2.zero);
    }

    private void BuildDetailPage(Transform parent)
    {
        detailDomain = CreateText(parent, "分類標題", "認知能力", 30, FontStyles.Bold,
            TextAlignmentOptions.Center);
        detailDomain.color = accentColor;
        SetRect(detailDomain.rectTransform, new Vector2(0.08f, 0.85f), new Vector2(0.92f, 0.94f), Vector2.zero,
            Vector2.zero);
        detailTitle = CreateText(parent, "遊戲標題", "遊戲名稱", 44, FontStyles.Bold,
            TextAlignmentOptions.Center);
        detailTitle.color = textColor;
        SetRect(detailTitle.rectTransform, new Vector2(0.08f, 0.73f), new Vector2(0.92f, 0.86f), Vector2.zero,
            Vector2.zero);

        GameObject information = CreatePanel(parent, "遊戲資訊", surfaceColor, new Vector2(0.08f, 0.25f),
            new Vector2(0.92f, 0.72f));
        detailSummary = CreateText(information.transform, "測量內容", "", 25, FontStyles.Bold,
            TextAlignmentOptions.TopLeft);
        detailSummary.color = textColor;
        SetRect(detailSummary.rectTransform, new Vector2(0.06f, 0.66f), new Vector2(0.94f, 0.94f), Vector2.zero,
            Vector2.zero);
        detailInstructions = CreateText(information.transform, "玩法", "", 22, FontStyles.Normal,
            TextAlignmentOptions.TopLeft);
        detailInstructions.color = textColor;
        SetRect(detailInstructions.rectTransform, new Vector2(0.06f, 0.35f), new Vector2(0.94f, 0.67f),
            Vector2.zero, Vector2.zero);
        detailRecord = CreateText(information.transform, "最近紀錄", "", 22, FontStyles.Normal,
            TextAlignmentOptions.TopLeft);
        detailRecord.color = new Color(0.30f, 0.39f, 0.32f);
        SetRect(detailRecord.rectTransform, new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.35f), Vector2.zero,
            Vector2.zero);

        Button back = CreateButton(parent, "返回", "返回能力列表", mutedColor);
        SetRect(back.GetComponent<RectTransform>(), new Vector2(0.08f, 0.07f), new Vector2(0.40f, 0.19f),
            Vector2.zero, Vector2.zero);
        Button start = CreateButton(parent, "開始", "確定，開始遊戲", accentColor);
        SetRect(start.GetComponent<RectTransform>(), new Vector2(0.52f, 0.07f), new Vector2(0.92f, 0.19f),
            Vector2.zero, Vector2.zero);
    }

    private GameObject BuildBottomNavigation(Transform parent)
    {
        GameObject bar = CreatePanel(parent, "底部導覽", surfaceColor, new Vector2(0.18f, 0.018f),
            new Vector2(0.82f, 0.145f));
        Button gamesButton = CreateButton(bar.transform, "遊戲", "遊戲", accentColor);
        SetRect(gamesButton.GetComponent<RectTransform>(), new Vector2(0.02f, 0.10f), new Vector2(0.32f, 0.90f),
            Vector2.zero, Vector2.zero);
        Button statsButton = CreateButton(bar.transform, "統計", "統計", orangeColor);
        SetRect(statsButton.GetComponent<RectTransform>(), new Vector2(0.35f, 0.10f), new Vector2(0.65f, 0.90f),
            Vector2.zero, Vector2.zero);
        Button mineButton = CreateButton(bar.transform, "我的", "我的", mutedColor);
        SetRect(mineButton.GetComponent<RectTransform>(), new Vector2(0.68f, 0.10f), new Vector2(0.98f, 0.90f),
            Vector2.zero, Vector2.zero);
        return bar;
    }

    private void BindExistingInterface()
    {
        Transform root = transform.Find(CanvasName);
        if (root == null) return;
        catalogPage = root.Find("遊戲首頁")?.gameObject;
        statisticsPage = root.Find("統計頁")?.gameObject;
        profilePage = root.Find("我的頁")?.gameObject;
        detailPage = root.Find("遊戲詳情")?.gameObject;
        bottomNavigation = root.Find("底部導覽")?.gameObject;
        if (catalogPage == null || statisticsPage == null || profilePage == null || detailPage == null) return;

        Transform header = catalogPage.transform.Find("玩家資訊列");
        headerName = header?.Find("玩家名稱")?.GetComponent<TMP_Text>();
        headerCoins = header?.Find("金幣區/金幣數量")?.GetComponent<TMP_Text>();
        statisticsChartLabel = statisticsPage.transform.Find("30天趨勢圖/目前能力")?.GetComponent<TMP_Text>();
        trendChart = statisticsPage.transform.Find("30天趨勢圖/折線圖")?.GetComponent<CognitiveTrendChartGraphic>();
        statisticsAttentionScore = statisticsPage.transform.Find("注意力摘要/分數")?.GetComponent<TMP_Text>();
        statisticsSpeedScore = statisticsPage.transform.Find("速度摘要/分數")?.GetComponent<TMP_Text>();
        statisticsExecutiveScore = statisticsPage.transform.Find("執行摘要/分數")?.GetComponent<TMP_Text>();
        profileText = profilePage.transform.Find("個人資料內容/個人資料")?.GetComponent<TMP_Text>();
        detailDomain = detailPage.transform.Find("分類標題")?.GetComponent<TMP_Text>();
        detailTitle = detailPage.transform.Find("遊戲標題")?.GetComponent<TMP_Text>();
        detailSummary = detailPage.transform.Find("遊戲資訊/測量內容")?.GetComponent<TMP_Text>();
        detailInstructions = detailPage.transform.Find("遊戲資訊/玩法")?.GetComponent<TMP_Text>();
        detailRecord = detailPage.transform.Find("遊戲資訊/最近紀錄")?.GetComponent<TMP_Text>();

        Button back = detailPage.transform.Find("返回")?.GetComponent<Button>();
        Button start = detailPage.transform.Find("開始")?.GetComponent<Button>();
        if (back != null) back.onClick.AddListener(() => ShowMainPage(catalogPage));
        if (start != null) start.onClick.AddListener(StartSelectedGame);

        Transform content = catalogPage.transform.Find("能力分類滑動區/Viewport/Content");
        if (content != null)
        {
            foreach (GameDefinition game in games.Where(item => item.available))
            {
                Button button = content.Find(game.domainTitle + "/遊戲_" + game.title)?.GetComponent<Button>();
                if (button == null) continue;
                GameDefinition captured = game;
                button.onClick.AddListener(() => OpenDetails(captured));
            }
        }

        Button gamesButton = bottomNavigation?.transform.Find("遊戲")?.GetComponent<Button>();
        Button statsButton = bottomNavigation?.transform.Find("統計")?.GetComponent<Button>();
        Button mineButton = bottomNavigation?.transform.Find("我的")?.GetComponent<Button>();
        if (gamesButton != null) gamesButton.onClick.AddListener(() => ShowMainPage(catalogPage));
        if (statsButton != null) statsButton.onClick.AddListener(() => ShowMainPage(statisticsPage));
        if (mineButton != null) mineButton.onClick.AddListener(() => ShowMainPage(profilePage));
        BindStatisticsFilter(statisticsPage.transform.Find("篩選_綜合")?.GetComponent<Button>(),
            ChartSelection.Composite);
        BindStatisticsFilter(statisticsPage.transform.Find("篩選_注意力")?.GetComponent<Button>(),
            ChartSelection.Attention);
        BindStatisticsFilter(statisticsPage.transform.Find("篩選_處理速度")?.GetComponent<Button>(),
            ChartSelection.ProcessingSpeed);
        BindStatisticsFilter(statisticsPage.transform.Find("篩選_執行功能")?.GetComponent<Button>(),
            ChartSelection.Executive);
        RefreshUserData();
    }

    private void ShowMainPage(GameObject page)
    {
        catalogPage.SetActive(page == catalogPage);
        statisticsPage.SetActive(page == statisticsPage);
        profilePage.SetActive(page == profilePage);
        detailPage.SetActive(false);
        bottomNavigation.SetActive(true);
        RefreshUserData();
    }

    private void OpenDetails(GameDefinition game)
    {
        selectedGame = game;
        detailDomain.text = game.domainTitle;
        detailDomain.color = game.experimental ? orangeColor : accentColor;
        detailTitle.text = game.title;
        detailSummary.text = "主要觀察：\n" + game.summary;
        detailInstructions.text = "玩法：\n" + game.instructions;
        CognitiveProfile profile = CognitiveAssessmentService.BuildProfile();
        CognitiveDomainScore score = profile.domains.FirstOrDefault(item => item.domain == game.domain);
        detailRecord.text = score == null
            ? "最近紀錄：尚無有效紀錄。完成遊戲後會顯示趨勢。\n\n" + profile.disclaimer
            : "最近紀錄：" + score.score.ToString("F0") + "/100（最近 " + score.contributingSessions +
              " 次有效紀錄）\n" + score.interpretation + "\n\n" + profile.disclaimer;
        catalogPage.SetActive(false);
        statisticsPage.SetActive(false);
        profilePage.SetActive(false);
        detailPage.SetActive(true);
        bottomNavigation.SetActive(false);
    }

    private void StartSelectedGame()
    {
        if (selectedGame != null && selectedGame.available && !string.IsNullOrWhiteSpace(selectedGame.sceneName))
            SceneManager.LoadScene(selectedGame.sceneName);
    }

    private void RefreshUserData()
    {
        if (headerName != null) headerName.text = GetPlayerName();
        if (headerCoins != null) headerCoins.text = CoinData.TotalCoins.ToString();
        CognitiveProfile profile = CognitiveAssessmentService.BuildProfile();
        SetScoreText(statisticsAttentionScore, profile, CognitiveDomain.AttentionInhibitoryControl);
        SetScoreText(statisticsSpeedScore, profile, CognitiveDomain.ProcessingSpeedVisualSearch);
        SetScoreText(statisticsExecutiveScore, profile, CognitiveDomain.ExecutiveFunctionNumericalReasoning);
        RefreshTrendChart();
        if (profileText != null) profileText.text = BuildProfileText();
    }

    private TMP_Text CreateScoreCard(Transform parent, string name, string label, Color accent, float minX,
        float maxX)
    {
        GameObject card = CreatePanel(parent, name, surfaceColor, new Vector2(minX, 0.675f),
            new Vector2(maxX, 0.815f));
        AddSoftShadow(card);
        GameObject strip = CreatePanel(card.transform, "色帶", accent, new Vector2(0f, 0f), new Vector2(0.035f, 1f));
        strip.GetComponent<Image>().raycastTarget = false;
        TMP_Text labelText = CreateText(card.transform, "名稱", label, 19, FontStyles.Bold,
            TextAlignmentOptions.Left);
        labelText.color = textColor;
        SetRect(labelText.rectTransform, new Vector2(0.09f, 0.52f), new Vector2(0.95f, 0.90f), Vector2.zero,
            Vector2.zero);
        TMP_Text score = CreateText(card.transform, "分數", "0", 32, FontStyles.Bold, TextAlignmentOptions.Left);
        score.color = accent;
        SetRect(score.rectTransform, new Vector2(0.09f, 0.08f), new Vector2(0.95f, 0.55f), Vector2.zero,
            Vector2.zero);
        return score;
    }

    private void CreateStatisticsFilter(Transform parent, string name, string label, Color color, float minX,
        float maxX)
    {
        Button button = CreateButton(parent, "篩選_" + name, label, color);
        SetRect(button.GetComponent<RectTransform>(), new Vector2(minX, 0.175f), new Vector2(maxX, 0.255f),
            Vector2.zero, Vector2.zero);
    }

    private void BindStatisticsFilter(Button button, ChartSelection selection)
    {
        if (button == null) return;
        button.onClick.AddListener(() =>
        {
            chartSelection = selection;
            RefreshTrendChart();
        });
    }

    private void RefreshTrendChart()
    {
        if (trendChart == null || statisticsChartLabel == null) return;
        string label;
        Color color;
        switch (chartSelection)
        {
            case ChartSelection.Attention:
                label = "注意力與抑制控制";
                color = accentColor;
                break;
            case ChartSelection.ProcessingSpeed:
                label = "處理速度與視覺搜尋";
                color = orangeColor;
                break;
            case ChartSelection.Executive:
                label = "執行功能與數字操作";
                color = new Color(0.49f, 0.48f, 0.72f);
                break;
            default:
                label = "綜合認知能力";
                color = new Color(0.22f, 0.53f, 0.62f);
                break;
        }
        statisticsChartLabel.text = label + "　目前尚無 30 天資料";
        trendChart.SetValues(new float[30], color);
    }

    private static void SetScoreText(TMP_Text target, CognitiveProfile profile, CognitiveDomain domain)
    {
        if (target == null) return;
        CognitiveDomainScore score = profile.domains.FirstOrDefault(item => item.domain == domain);
        target.text = score == null ? "0" : score.score.ToString("F0") + " / 100";
    }

    private static string DomainBadge(string domainTitle)
    {
        if (domainTitle.Contains("注意")) return "專";
        if (domainTitle.Contains("速度")) return "速";
        if (domainTitle.Contains("執行")) return "策";
        return "動";
    }

    private void ApplyPageBackground(GameObject page)
    {
        if (pageBackgroundSprite == null) return;
        Image image = page.GetComponent<Image>();
        image.sprite = pageBackgroundSprite;
        image.color = Color.white;
        image.preserveAspect = false;
    }

    private static void AddSoftShadow(GameObject target)
    {
        Shadow shadow = target.AddComponent<Shadow>();
        shadow.effectColor = new Color(0.23f, 0.25f, 0.20f, 0.16f);
        shadow.effectDistance = new Vector2(0f, -4f);
        shadow.useGraphicAlpha = true;
    }

    private string BuildStatisticsText()
    {
        CognitiveProfile profile = CognitiveAssessmentService.BuildProfile();
        string[] lines =
        {
            DomainLine(profile, CognitiveDomain.AttentionInhibitoryControl, "注意力與抑制控制"),
            DomainLine(profile, CognitiveDomain.ProcessingSpeedVisualSearch, "處理速度與視覺搜尋"),
            DomainLine(profile, CognitiveDomain.ExecutiveFunctionNumericalReasoning, "執行功能與數字操作")
        };
        return "近期認知表現\n\n" + string.Join("\n\n", lines) +
               "\n\n分數用於呈現個人長期變化，不代表醫療診斷。";
    }

    private static string DomainLine(CognitiveProfile profile, CognitiveDomain domain, string label)
    {
        CognitiveDomainScore score = profile.domains.FirstOrDefault(item => item.domain == domain);
        return score == null ? label + "　尚無紀錄" : label + "　" + score.score.ToString("F0") + "/100　有效紀錄 " +
            score.contributingSessions + " 次";
    }

    private string BuildProfileText()
    {
        CognitiveProfile profile = CognitiveAssessmentService.BuildProfile();
        return "玩家姓名　" + GetPlayerName() + "\n\n持有金幣　" + CoinData.TotalCoins +
               "\n\n有效測驗紀錄　" + profile.domains.Sum(item => item.contributingSessions) +
               " 次\n\n資料用途　觀察自己的長期認知變化\n\n" + profile.disclaimer;
    }

    private static string GetPlayerName()
    {
        string name = PlayerPrefs.GetString("SavedPlayerName", PlayerPrefs.GetString("AccountName", "使用者"));
        return string.IsNullOrWhiteSpace(name) ? "使用者" : name;
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
        Type inputModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputModule != null) eventSystem.AddComponent(inputModule);
    }

    private static GameObject CreatePanel(Transform parent, string name, Color color, Vector2 anchorMin,
        Vector2 anchorMax)
    {
        var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        panel.GetComponent<Image>().color = color;
        SetRect(panel.GetComponent<RectTransform>(), anchorMin, anchorMax, Vector2.zero, Vector2.zero);
        return panel;
    }

    private TMP_Text CreateText(Transform parent, string name, string value, float size, FontStyles style,
        TextAlignmentOptions alignment)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        if (chineseFont != null) text.font = chineseFont;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = textColor;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private Button CreateButton(Transform parent, string name, string label, Color color)
    {
        var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        buttonObject.GetComponent<Image>().color = color;
        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.15f);
        button.colors = colors;
        TMP_Text text = CreateText(buttonObject.transform, "文字", label, 24, FontStyles.Bold,
            TextAlignmentOptions.Center);
        text.color = Color.white;
        Stretch(text.rectTransform, 7f);
        return button;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void Stretch(RectTransform rect, float margin)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(margin, margin);
        rect.offsetMax = new Vector2(-margin, -margin);
    }
}
