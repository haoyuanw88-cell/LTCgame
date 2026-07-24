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
    private const string LastDailyLoginKey = "LTC_LastDailyLoginDate";
    private const int DailyLoginRewardCoins = 20;

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
        },
        new GameDefinition
        {
            title = "翻牌記憶",
            domainTitle = "視覺工作記憶",
            domainSummary = "短時間記住卡牌位置，並找出相同配對。",
            summary = "觀察並記住卡牌位置，測量配對正確率、錯誤次數與完成時間。",
            instructions = "依序翻開兩張卡牌；圖案相同即可完成配對，直到所有卡牌都配對完成。",
            sceneName = "CardsGame",
            domain = CognitiveDomain.WorkingMemory,
            experimental = true
        },
        new GameDefinition
        {
            title = "旋轉接水管",
            domainTitle = "視空間規劃",
            domainSummary = "理解圖形方向與連接關係，規劃正確路徑。",
            summary = "旋轉水管並建立完整通路，觀察空間推理、規劃效率與錯誤操作。",
            instructions = "點擊水管進行旋轉，讓起點與終點形成一條完整且不中斷的路徑。",
            sceneName = "PipeGame",
            domain = CognitiveDomain.VisuospatialAbility,
            experimental = true
        },
        new GameDefinition
        {
            title = "超市採購",
            domainTitle = "工作記憶與執行功能",
            domainSummary = "記住購物目標，搜尋商品並依需求完成任務。",
            summary = "依購物清單尋找正確商品，觀察目標維持、視覺搜尋與錯誤選擇。",
            instructions = "先查看購物需求，再到貨架選擇指定商品並完成結帳。",
            sceneName = "SupermarketGame",
            domain = CognitiveDomain.WorkingMemory,
            experimental = true
        },
        new GameDefinition
        {
            title = "文字判斷",
            domainTitle = "語言理解",
            domainSummary = "閱讀題目、理解語意並判斷敘述是否正確。",
            summary = "透過生活化敘述題，觀察閱讀理解、語意判斷與作答正確率。",
            instructions = "閱讀畫面上的敘述後，選擇正確或錯誤；作答後會進入下一題。",
            sceneName = "TextPuzzleGame",
            domain = CognitiveDomain.Language,
            experimental = true
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
    private TMP_Text statisticsTitle;
    private TMP_Text statisticsChartLabel;
    private TMP_Text[] statisticsDateLabels = new TMP_Text[4];
    private TMP_Text statisticsAttentionScore;
    private TMP_Text statisticsSpeedScore;
    private TMP_Text statisticsExecutiveScore;
    private CognitiveTrendChartGraphic trendChart;
    private TMP_Text profileText;
    private TMP_InputField profileNameInput;
    private TMP_Text profileNameStatus;
    private TMP_Text dailyLoginButtonText;
    private GameObject dailyLoginPopup;
    private TMP_Text dailyLoginPopupMessage;
    private Button dailyLoginClaimButton;
    private TMP_Text dailyLoginClaimButtonText;
    private TMP_Text detailDomain;
    private TMP_Text detailTitle;
    private TMP_Text detailSummary;
    private TMP_Text detailInstructions;
    private TMP_Text detailRecord;
    private GameDefinition selectedGame;
    private ChartSelection chartSelection = ChartSelection.Composite;
    private int statisticsRangeDays = 30;

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
        Transform existingCanvas = transform.Find(CanvasName);
        if (existingCanvas == null)
        {
            BuildInterface();
            return;
        }

        // Keep editor-authored scenes in sync when the navigation structure evolves.
        Transform navigation = existingCanvas.Find("底部導覽");
        bool hasIntegratedGames = existingCanvas.GetComponentsInChildren<Button>(true)
            .Any(button => button.name == "遊戲_翻牌記憶");
        bool interfaceIsCurrent = navigation != null &&
                                  navigation.Find("商店") != null &&
                                  navigation.Find("寵物") != null &&
                                  existingCanvas.Find("我的頁/個人資料內容/名稱輸入") != null &&
                                  existingCanvas.Find("每日登入彈窗") != null &&
                                  hasIntegratedGames;
        if (!interfaceIsCurrent)
        {
            if (Application.isPlaying)
            {
                existingCanvas.name = CanvasName + " (舊版)";
                existingCanvas.gameObject.SetActive(false);
                Destroy(existingCanvas.gameObject);
            }
            else
            {
                DestroyImmediate(existingCanvas.gameObject);
            }

            BuildInterface();
        }
    }

[ContextMenu("Rebuild Cognitive Catalog UI")]
    public void RebuildInterfaceForEditor()
    {
        if (Application.isPlaying) return;
        Transform existing = transform.Find(CanvasName);
        if (existing != null) DestroyImmediate(existing.gameObject);
        BuildInterface();
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
        dailyLoginPopup = BuildDailyLoginPopup(canvasObject.transform);

        statisticsPage.SetActive(false);
        profilePage.SetActive(false);
        detailPage.SetActive(false);
        dailyLoginPopup.SetActive(false);
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
        statisticsTitle = CreateText(parent, "統計標題", "過去 30 天認知趨勢", 42, FontStyles.Bold,
            TextAlignmentOptions.Center);
        statisticsTitle.color = textColor;
        SetRect(statisticsTitle.rectTransform, new Vector2(0.08f, 0.885f), new Vector2(0.92f, 0.97f),
            Vector2.zero, Vector2.zero);

        TMP_Text subtitle = CreateText(parent, "統計副標題", "分數用來觀察個人長期變化，不代表醫療診斷", 22,
            FontStyles.Normal, TextAlignmentOptions.Center);
        subtitle.color = new Color(0.43f, 0.46f, 0.42f);
        SetRect(subtitle.rectTransform, new Vector2(0.08f, 0.83f), new Vector2(0.92f, 0.89f),
            Vector2.zero, Vector2.zero);

        statisticsAttentionScore = CreateScoreCard(parent, "注意力摘要", "注意力與抑制", accentColor, 0.06f, 0.34f);
        statisticsSpeedScore = CreateScoreCard(parent, "速度摘要", "處理速度", orangeColor, 0.36f, 0.64f);
        statisticsExecutiveScore = CreateScoreCard(parent, "執行摘要", "執行功能",
            new Color(0.49f, 0.48f, 0.72f), 0.66f, 0.94f);

        GameObject chartPanel = CreatePanel(parent, "認知趨勢圖", surfaceColor, new Vector2(0.06f, 0.285f),
            new Vector2(0.94f, 0.65f));
        AddSoftShadow(chartPanel);

        statisticsChartLabel = CreateText(chartPanel.transform, "目前能力", "綜合認知表現", 25,
            FontStyles.Bold, TextAlignmentOptions.Left);
        statisticsChartLabel.color = textColor;
        SetRect(statisticsChartLabel.rectTransform, new Vector2(0.05f, 0.84f), new Vector2(0.57f, 0.97f),
            Vector2.zero, Vector2.zero);

        CreateStatisticsRangeButton(chartPanel.transform, 7, 0.60f, 0.71f);
        CreateStatisticsRangeButton(chartPanel.transform, 30, 0.73f, 0.84f);
        CreateStatisticsRangeButton(chartPanel.transform, 90, 0.86f, 0.97f);

        TMP_Text range = CreateText(chartPanel.transform, "分數刻度", "100\n\n50\n\n0", 19,
            FontStyles.Normal, TextAlignmentOptions.Right);
        range.color = mutedColor;
        SetRect(range.rectTransform, new Vector2(0.01f, 0.16f), new Vector2(0.07f, 0.80f),
            Vector2.zero, Vector2.zero);

        Color grid = new Color(0.36f, 0.43f, 0.37f, 0.28f);
        for (int index = 0; index <= 4; index++)
        {
            float y = Mathf.Lerp(0.20f, 0.80f, index / 4f);
            GameObject line = CreatePanel(chartPanel.transform, "水平格線_" + index, grid,
                new Vector2(0.08f, y), new Vector2(0.96f, y));
            line.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, index == 0 ? 4f : 2f);
            line.GetComponent<Image>().raycastTarget = false;
        }
        for (int index = 0; index <= 3; index++)
        {
            float x = Mathf.Lerp(0.08f, 0.96f, index / 3f);
            GameObject line = CreatePanel(chartPanel.transform, "垂直格線_" + index, grid,
                new Vector2(x, 0.20f), new Vector2(x, 0.80f));
            line.GetComponent<RectTransform>().sizeDelta = new Vector2(2f, 0f);
            line.GetComponent<Image>().raycastTarget = false;
        }

        GameObject chartObject = new GameObject("折線圖", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(CognitiveTrendChartGraphic));
        chartObject.transform.SetParent(chartPanel.transform, false);
        trendChart = chartObject.GetComponent<CognitiveTrendChartGraphic>();
        SetRect(chartObject.GetComponent<RectTransform>(), new Vector2(0.08f, 0.20f), new Vector2(0.96f, 0.80f),
            Vector2.zero, Vector2.zero);

        statisticsDateLabels = new TMP_Text[4];
        statisticsDateLabels[0] = CreateAxisLabel(chartPanel.transform, "日期_起點", TextAlignmentOptions.Left,
            0.05f, 0.25f);
        statisticsDateLabels[1] = CreateAxisLabel(chartPanel.transform, "日期_三分之一", TextAlignmentOptions.Center,
            0.25f, 0.49f);
        statisticsDateLabels[2] = CreateAxisLabel(chartPanel.transform, "日期_三分之二", TextAlignmentOptions.Center,
            0.51f, 0.75f);
        statisticsDateLabels[3] = CreateAxisLabel(chartPanel.transform, "日期_今天", TextAlignmentOptions.Right,
            0.75f, 0.98f);
        UpdateStatisticsDateLabels();

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

        TMP_Text nameLabel = CreateText(panel.transform, "名稱標題", "使用者名稱", 25, FontStyles.Bold,
            TextAlignmentOptions.Left);
        SetRect(nameLabel.rectTransform, new Vector2(0.06f, 0.79f), new Vector2(0.58f, 0.91f), Vector2.zero,
            Vector2.zero);

        profileNameInput = CreateInputField(panel.transform, "名稱輸入", "請輸入名稱", GetPlayerName());
        SetRect(profileNameInput.GetComponent<RectTransform>(), new Vector2(0.06f, 0.61f),
            new Vector2(0.45f, 0.78f), Vector2.zero, Vector2.zero);
        Button saveName = CreateButton(panel.transform, "儲存名稱", "儲存名稱", accentColor);
        SetRect(saveName.GetComponent<RectTransform>(), new Vector2(0.47f, 0.61f), new Vector2(0.62f, 0.78f),
            Vector2.zero, Vector2.zero);
        profileNameStatus = CreateText(panel.transform, "名稱狀態", "名稱會顯示在首頁與個人資料中", 18,
            FontStyles.Normal, TextAlignmentOptions.Left);
        profileNameStatus.color = new Color(0.38f, 0.43f, 0.39f);
        SetRect(profileNameStatus.rectTransform, new Vector2(0.06f, 0.51f), new Vector2(0.62f, 0.61f),
            Vector2.zero, Vector2.zero);

        profileText = CreateText(panel.transform, "個人資料", BuildProfileText(), 23, FontStyles.Normal,
            TextAlignmentOptions.TopLeft);
        profileText.color = textColor;
        SetRect(profileText.rectTransform, new Vector2(0.06f, 0.08f), new Vector2(0.64f, 0.49f), Vector2.zero,
            Vector2.zero);

        Button settings = CreateButton(panel.transform, "設定", "設定（開發中）", mutedColor);
        SetRect(settings.GetComponent<RectTransform>(), new Vector2(0.70f, 0.76f), new Vector2(0.93f, 0.91f),
            Vector2.zero, Vector2.zero);
        settings.interactable = false;

        Button dailyLogin = CreateCircleButton(panel.transform, "每日登入", orangeColor);
        SetRect(dailyLogin.GetComponent<RectTransform>(), new Vector2(0.72f, 0.22f), new Vector2(0.91f, 0.70f),
            Vector2.zero, Vector2.zero);
        dailyLoginButtonText = CreateText(dailyLogin.transform, "文字", "每日登入\n可領取", 24, FontStyles.Bold,
            TextAlignmentOptions.Center);
        dailyLoginButtonText.color = Color.white;
        Stretch(dailyLoginButtonText.rectTransform, 18f);
    }

    private GameObject BuildDailyLoginPopup(Transform parent)
    {
        GameObject overlay = CreatePanel(parent, "每日登入彈窗", new Color(0.10f, 0.12f, 0.10f, 0.66f),
            Vector2.zero, Vector2.one);
        GameObject card = CreatePanel(overlay.transform, "簽到卡片", surfaceColor, new Vector2(0.28f, 0.21f),
            new Vector2(0.72f, 0.79f));
        AddSoftShadow(card);

        TMP_Text title = CreateText(card.transform, "標題", "每日登入獎勵", 38, FontStyles.Bold,
            TextAlignmentOptions.Center);
        title.color = textColor;
        SetRect(title.rectTransform, new Vector2(0.08f, 0.75f), new Vector2(0.92f, 0.93f), Vector2.zero,
            Vector2.zero);

        dailyLoginPopupMessage = CreateText(card.transform, "說明", string.Empty, 25, FontStyles.Normal,
            TextAlignmentOptions.Center);
        dailyLoginPopupMessage.color = textColor;
        SetRect(dailyLoginPopupMessage.rectTransform, new Vector2(0.10f, 0.40f), new Vector2(0.90f, 0.73f),
            Vector2.zero, Vector2.zero);

        dailyLoginClaimButton = CreateButton(card.transform, "領取", "領取 20 金幣", orangeColor);
        SetRect(dailyLoginClaimButton.GetComponent<RectTransform>(), new Vector2(0.13f, 0.17f),
            new Vector2(0.58f, 0.34f), Vector2.zero, Vector2.zero);
        dailyLoginClaimButtonText = dailyLoginClaimButton.GetComponentInChildren<TMP_Text>();

        Button close = CreateButton(card.transform, "稍後再說", "稍後再說", mutedColor);
        SetRect(close.GetComponent<RectTransform>(), new Vector2(0.62f, 0.17f), new Vector2(0.87f, 0.34f),
            Vector2.zero, Vector2.zero);
        return overlay;
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
        SetRect(gamesButton.GetComponent<RectTransform>(), new Vector2(0.01f, 0.10f), new Vector2(0.19f, 0.90f),
            Vector2.zero, Vector2.zero);
        Button statsButton = CreateButton(bar.transform, "統計", "統計", orangeColor);
        SetRect(statsButton.GetComponent<RectTransform>(), new Vector2(0.21f, 0.10f), new Vector2(0.39f, 0.90f),
            Vector2.zero, Vector2.zero);
        Button shopButton = CreateButton(bar.transform, "商店", "商店", new Color(0.82f, 0.60f, 0.26f, 1f));
        SetRect(shopButton.GetComponent<RectTransform>(), new Vector2(0.41f, 0.10f), new Vector2(0.59f, 0.90f),
            Vector2.zero, Vector2.zero);
        Button petButton = CreateButton(bar.transform, "寵物", "寵物", new Color(0.48f, 0.67f, 0.76f, 1f));
        SetRect(petButton.GetComponent<RectTransform>(), new Vector2(0.61f, 0.10f), new Vector2(0.79f, 0.90f),
            Vector2.zero, Vector2.zero);
        Button mineButton = CreateButton(bar.transform, "我的", "我的", mutedColor);
        SetRect(mineButton.GetComponent<RectTransform>(), new Vector2(0.81f, 0.10f), new Vector2(0.99f, 0.90f),
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
        dailyLoginPopup = root.Find("每日登入彈窗")?.gameObject;
        if (catalogPage == null || statisticsPage == null || profilePage == null || detailPage == null) return;

        Transform header = catalogPage.transform.Find("玩家資訊列");
        headerName = header?.Find("玩家名稱")?.GetComponent<TMP_Text>();
        headerCoins = header?.Find("金幣區/金幣數量")?.GetComponent<TMP_Text>();
        statisticsTitle = statisticsPage.transform.Find("統計標題")?.GetComponent<TMP_Text>();
        statisticsChartLabel = statisticsPage.transform.Find("認知趨勢圖/目前能力")?.GetComponent<TMP_Text>();
        trendChart = statisticsPage.transform.Find("認知趨勢圖/折線圖")?.GetComponent<CognitiveTrendChartGraphic>();
        statisticsDateLabels = new[]
        {
            statisticsPage.transform.Find("認知趨勢圖/日期_起點")?.GetComponent<TMP_Text>(),
            statisticsPage.transform.Find("認知趨勢圖/日期_三分之一")?.GetComponent<TMP_Text>(),
            statisticsPage.transform.Find("認知趨勢圖/日期_三分之二")?.GetComponent<TMP_Text>(),
            statisticsPage.transform.Find("認知趨勢圖/日期_今天")?.GetComponent<TMP_Text>()
        };
        statisticsAttentionScore = statisticsPage.transform.Find("注意力摘要/分數")?.GetComponent<TMP_Text>();
        statisticsSpeedScore = statisticsPage.transform.Find("速度摘要/分數")?.GetComponent<TMP_Text>();
        statisticsExecutiveScore = statisticsPage.transform.Find("執行摘要/分數")?.GetComponent<TMP_Text>();
        profileText = profilePage.transform.Find("個人資料內容/個人資料")?.GetComponent<TMP_Text>();
        profileNameInput = profilePage.transform.Find("個人資料內容/名稱輸入")?.GetComponent<TMP_InputField>();
        profileNameStatus = profilePage.transform.Find("個人資料內容/名稱狀態")?.GetComponent<TMP_Text>();
        dailyLoginButtonText = profilePage.transform.Find("個人資料內容/每日登入/文字")?.GetComponent<TMP_Text>();
        dailyLoginPopupMessage = dailyLoginPopup?.transform.Find("簽到卡片/說明")?.GetComponent<TMP_Text>();
        dailyLoginClaimButton = dailyLoginPopup?.transform.Find("簽到卡片/領取")?.GetComponent<Button>();
        dailyLoginClaimButtonText = dailyLoginClaimButton?.GetComponentInChildren<TMP_Text>();
        detailDomain = detailPage.transform.Find("分類標題")?.GetComponent<TMP_Text>();
        detailTitle = detailPage.transform.Find("遊戲標題")?.GetComponent<TMP_Text>();
        detailSummary = detailPage.transform.Find("遊戲資訊/測量內容")?.GetComponent<TMP_Text>();
        detailInstructions = detailPage.transform.Find("遊戲資訊/玩法")?.GetComponent<TMP_Text>();
        detailRecord = detailPage.transform.Find("遊戲資訊/最近紀錄")?.GetComponent<TMP_Text>();

        Button back = detailPage.transform.Find("返回")?.GetComponent<Button>();
        Button start = detailPage.transform.Find("開始")?.GetComponent<Button>();
        if (back != null) back.onClick.AddListener(() => ShowMainPage(catalogPage));
        if (start != null) start.onClick.AddListener(StartSelectedGame);

        Button saveName = profilePage.transform.Find("個人資料內容/儲存名稱")?.GetComponent<Button>();
        Button dailyLogin = profilePage.transform.Find("個人資料內容/每日登入")?.GetComponent<Button>();
        Button closeDailyLogin = dailyLoginPopup?.transform.Find("簽到卡片/稍後再說")?.GetComponent<Button>();
        if (saveName != null) saveName.onClick.AddListener(SavePlayerName);
        if (dailyLogin != null) dailyLogin.onClick.AddListener(OpenDailyLoginPopup);
        if (dailyLoginClaimButton != null) dailyLoginClaimButton.onClick.AddListener(ClaimDailyLoginReward);
        if (closeDailyLogin != null) closeDailyLogin.onClick.AddListener(CloseDailyLoginPopup);

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
        Button shopButton = bottomNavigation?.transform.Find("商店")?.GetComponent<Button>();
        Button mineButton = bottomNavigation?.transform.Find("我的")?.GetComponent<Button>();
        BindStatisticsRange(statisticsPage.transform.Find("認知趨勢圖/範圍_7天")?.GetComponent<Button>(), 7);
        BindStatisticsRange(statisticsPage.transform.Find("認知趨勢圖/範圍_30天")?.GetComponent<Button>(), 30);
        BindStatisticsRange(statisticsPage.transform.Find("認知趨勢圖/範圍_90天")?.GetComponent<Button>(), 90);
        if (gamesButton != null) gamesButton.onClick.AddListener(() => ShowMainPage(catalogPage));
        if (statsButton != null) statsButton.onClick.AddListener(() => ShowMainPage(statisticsPage));
        if (shopButton != null) shopButton.onClick.AddListener(OpenShop);
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
        if (!HasClaimedDailyLoginToday()) OpenDailyLoginPopup();
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

    private void OpenShop()
    {
        SceneManager.LoadScene("shop");
    }

    private void SavePlayerName()
    {
        if (profileNameInput == null) return;
        string newName = profileNameInput.text.Trim();
        if (string.IsNullOrEmpty(newName))
        {
            profileNameStatus.text = "名稱不能留白";
            profileNameStatus.color = new Color(0.78f, 0.27f, 0.22f);
            return;
        }

        PlayerPrefs.SetString("SavedPlayerName", newName);
        PlayerPrefs.SetString("AccountName", newName);
        PlayerPrefs.Save();
        profileNameInput.text = newName;
        profileNameStatus.text = "名稱已儲存";
        profileNameStatus.color = accentColor;
        RefreshUserData();
    }

    private void OpenDailyLoginPopup()
    {
        if (dailyLoginPopup == null) return;
        UpdateDailyLoginUI();
        dailyLoginPopup.SetActive(true);
        dailyLoginPopup.transform.SetAsLastSibling();
    }

    private void CloseDailyLoginPopup()
    {
        if (dailyLoginPopup != null) dailyLoginPopup.SetActive(false);
    }

    private void ClaimDailyLoginReward()
    {
        if (HasClaimedDailyLoginToday())
        {
            UpdateDailyLoginUI();
            return;
        }

        CoinData.AddCoins(DailyLoginRewardCoins);
        PlayerPrefs.SetString(LastDailyLoginKey, TodayKey());
        PlayerPrefs.Save();
        RefreshUserData();
        UpdateDailyLoginUI();
    }

    private void UpdateDailyLoginUI()
    {
        bool claimed = HasClaimedDailyLoginToday();
        if (dailyLoginButtonText != null)
            dailyLoginButtonText.text = claimed ? "每日登入\n今日已領取" : "每日登入\n可領取 20 金幣";
        if (dailyLoginPopupMessage != null)
            dailyLoginPopupMessage.text = claimed
                ? "今天的登入獎勵已經領取。\n明天再回來看看吧！"
                : "歡迎回來！\n今天可領取 20 金幣。";
        if (dailyLoginClaimButton != null) dailyLoginClaimButton.interactable = !claimed;
        if (dailyLoginClaimButtonText != null)
            dailyLoginClaimButtonText.text = claimed ? "今日已領取" : "領取 20 金幣";
    }

    private static bool HasClaimedDailyLoginToday()
    {
        return PlayerPrefs.GetString(LastDailyLoginKey, string.Empty) == TodayKey();
    }

    private static string TodayKey()
    {
        return DateTime.Now.ToString("yyyy-MM-dd");
    }

    private void RefreshUserData()
    {
        if (headerName != null) headerName.text = GetPlayerName();
        if (headerCoins != null) headerCoins.text = CoinData.TotalCoins.ToString();
        if (profileNameInput != null && !profileNameInput.isFocused) profileNameInput.text = GetPlayerName();
        CognitiveProfile profile = CognitiveAssessmentService.BuildProfile();
        SetScoreText(statisticsAttentionScore, profile, CognitiveDomain.AttentionInhibitoryControl);
        SetScoreText(statisticsSpeedScore, profile, CognitiveDomain.ProcessingSpeedVisualSearch);
        SetScoreText(statisticsExecutiveScore, profile, CognitiveDomain.ExecutiveFunctionNumericalReasoning);
        RefreshTrendChart();
        if (profileText != null) profileText.text = BuildProfileText();
        UpdateDailyLoginUI();
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
        CognitiveDomain? domain;
        switch (chartSelection)
        {
            case ChartSelection.Attention:
                label = "注意力與抑制控制";
                color = accentColor;
                domain = CognitiveDomain.AttentionInhibitoryControl;
                break;
            case ChartSelection.ProcessingSpeed:
                label = "處理速度與視覺搜尋";
                color = orangeColor;
                domain = CognitiveDomain.ProcessingSpeedVisualSearch;
                break;
            case ChartSelection.Executive:
                label = "執行功能與數字推理";
                color = new Color(0.49f, 0.48f, 0.72f);
                domain = CognitiveDomain.ExecutiveFunctionNumericalReasoning;
                break;
            default:
                label = "綜合認知表現";
                color = new Color(0.22f, 0.53f, 0.62f);
                domain = null;
                break;
        }

        float[] values = CognitiveAssessmentService.BuildDailyTrend(domain, statisticsRangeDays);
        int recordedDays = values.Count(value => !float.IsNaN(value));
        if (statisticsTitle != null)
            statisticsTitle.text = "過去 " + statisticsRangeDays + " 天認知趨勢";
        UpdateStatisticsDateLabels();
        statisticsChartLabel.text = recordedDays == 0
            ? label + "｜尚無有效紀錄"
            : label + "｜共 " + recordedDays + " 天有有效紀錄";
        trendChart.SetValues(values, color);
    }

private static void SetScoreText(TMP_Text target, CognitiveProfile profile, CognitiveDomain domain)
    {
        if (target == null) return;
        CognitiveDomainScore score = profile.domains.FirstOrDefault(item => item.domain == domain);
        target.text = score == null ? "尚無資料" : score.score.ToString("F0") + " / 100";
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
        return "持有金幣　" + CoinData.TotalCoins +
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

    private Button CreateCircleButton(Transform parent, string name, Color color)
    {
        var buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
            typeof(DailyLoginCircleGraphic), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        DailyLoginCircleGraphic graphic = buttonObject.GetComponent<DailyLoginCircleGraphic>();
        graphic.color = color;
        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = graphic;
        ColorBlock colors = button.colors;
        colors.highlightedColor = Color.Lerp(color, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(color, Color.black, 0.15f);
        button.colors = colors;
        return button;
    }

    private TMP_InputField CreateInputField(Transform parent, string name, string placeholder, string value)
    {
        var inputObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        inputObject.transform.SetParent(parent, false);
        Image background = inputObject.GetComponent<Image>();
        background.color = new Color(0.95f, 0.94f, 0.89f, 1f);

        TMP_Text placeholderText = CreateText(inputObject.transform, "提示文字", placeholder, 22,
            FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
        placeholderText.color = new Color(0.48f, 0.50f, 0.46f, 0.75f);
        Stretch(placeholderText.rectTransform, 14f);

        TMP_Text inputText = CreateText(inputObject.transform, "輸入文字", value, 24, FontStyles.Normal,
            TextAlignmentOptions.MidlineLeft);
        inputText.color = textColor;
        Stretch(inputText.rectTransform, 14f);

        TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
        input.textViewport = inputObject.GetComponent<RectTransform>();
        input.textComponent = inputText;
        input.placeholder = placeholderText;
        input.text = value;
        input.characterLimit = 20;
        input.lineType = TMP_InputField.LineType.SingleLine;
        return input;
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


private TMP_Text CreateAxisLabel(Transform parent, string name, TextAlignmentOptions alignment,
        float minX, float maxX)
    {
        TMP_Text label = CreateText(parent, name, string.Empty, 21, FontStyles.Bold, alignment);
        label.color = new Color(0.38f, 0.42f, 0.38f);
        SetRect(label.rectTransform, new Vector2(minX, 0.02f), new Vector2(maxX, 0.18f),
            Vector2.zero, Vector2.zero);
        return label;
    }

    private void CreateStatisticsRangeButton(Transform parent, int days, float minX, float maxX)
    {
        Button button = CreateButton(parent, "範圍_" + days + "天", days + " 天", mutedColor);
        SetRect(button.GetComponent<RectTransform>(), new Vector2(minX, 0.84f), new Vector2(maxX, 0.97f),
            Vector2.zero, Vector2.zero);
        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        if (label != null) label.fontSize = 19;
    }

    private void BindStatisticsRange(Button button, int days)
    {
        if (button == null) return;
        button.onClick.AddListener(() =>
        {
            statisticsRangeDays = days;
            RefreshTrendChart();
        });
    }

    private void UpdateStatisticsDateLabels()
    {
        if (statisticsDateLabels == null || statisticsDateLabels.Length < 4) return;
        int maximumDaysAgo = Mathf.Max(1, statisticsRangeDays - 1);
        statisticsDateLabels[0].text = maximumDaysAgo + " 天前";
        statisticsDateLabels[1].text = Mathf.RoundToInt(maximumDaysAgo * 2f / 3f) + " 天前";
        statisticsDateLabels[2].text = Mathf.RoundToInt(maximumDaysAgo / 3f) + " 天前";
        statisticsDateLabels[3].text = "今天";
    }
}
