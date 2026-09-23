using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Full-screen pet-garden themed storefront. It replaces the legacy shop canvas at runtime
/// while preserving the existing wallet, cloud purchase, inventory, and navigation systems.
/// </summary>
public sealed class LTCGardenShopController : MonoBehaviour
{
    [Serializable]
    sealed class ShopItem
    {
        public string id;
        public string name;
        public string category;
        public string description;
        public int price;
        public string resourceIcon;
        public string symbol;
        public bool available;
        public bool consumable;
    }

    const string ShopScene = "shop";
    const string FoodCategory = "點心";
    const string SupplyCategory = "用品";

    readonly Color leaf = new Color(0.32f, 0.48f, 0.18f, 1f);
    readonly Color leafLight = new Color(0.70f, 0.78f, 0.34f, 1f);
    readonly Color wood = new Color(0.48f, 0.27f, 0.12f, 1f);
    readonly Color woodLight = new Color(0.78f, 0.54f, 0.29f, 1f);
    readonly Color parchment = new Color(1f, 0.94f, 0.76f, 1f);
    readonly Color cream = new Color(1f, 0.98f, 0.88f, 1f);
    readonly Color ink = new Color(0.25f, 0.18f, 0.10f, 1f);

    readonly List<ShopItem> items = new List<ShopItem>
    {
        new ShopItem { id = "F_APPLE", name = "元氣蘋果", category = FoodCategory,
            description = "清脆香甜的每日點心，適合陪伴寵物散步後一起享用。",
            price = 1, resourceIcon = "Shop/apple", symbol = "蘋", available = true, consumable = true },
        new ShopItem { id = "F_BANANA", name = "開心香蕉", category = FoodCategory,
            description = "柔軟又好入口的水果點心，為寵物補充好心情。",
            price = 1, resourceIcon = "Shop/banana", symbol = "蕉", available = true, consumable = true },
        new ShopItem { id = "F_PINE", name = "陽光鳳梨", category = FoodCategory,
            description = "充滿熱帶氣息的特別點心，讓寵物花園多一點陽光。",
            price = 1, resourceIcon = "Shop/pineapple", symbol = "鳳", available = true, consumable = true },
        new ShopItem { id = "P_FOREST_BALL", name = "快樂球", category = SupplyCategory,
            description = "會在草地上滾動的耐咬玩具，規劃用來提升寵物的玩樂心情。",
            price = 25, symbol = "球", available = false, consumable = false },
        new ShopItem { id = "P_CLOUD_BED", name = "雲朵睡墊", category = SupplyCategory,
            description = "柔軟蓬鬆的休息小床，規劃用來加速寵物恢復精神。",
            price = 40, symbol = "床", available = false, consumable = false },
        new ShopItem { id = "P_WOOD_BRUSH", name = "木柄梳子", category = SupplyCategory,
            description = "溫和整理毛髮的木梳，規劃用來提升親密度與整潔度。",
            price = 30, symbol = "梳", available = false, consumable = false }
    };

    TMP_FontAsset font;
    Transform productContent;
    TMP_Text coinText;
    TMP_Text detailName;
    TMP_Text detailCategory;
    TMP_Text detailDescription;
    TMP_Text detailOwned;
    TMP_Text detailPrice;
    TMP_Text detailSymbol;
    Image detailIcon;
    Button buyButton;
    TMP_Text buyButtonLabel;
    TMP_Text statusText;
    string activeCategory = "推薦";
    ShopItem selected;
    readonly List<Button> categoryButtons = new List<Button>();

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
        if (scene.name != ShopScene || FindFirstObjectByType<LTCGardenShopController>() != null) return;
        new GameObject("LTC Garden Shop").AddComponent<LTCGardenShopController>();
    }

    void Start()
    {
        TMP_Text sample = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault();
        font = Resources.Load<TMP_FontAsset>("Shop/KAIU_Dynamic");
        if (font == null) font = sample == null ? null : sample.font;

        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            canvas.gameObject.SetActive(false);

        EnsureEventSystem();
        BuildInterface();
        CoinData.BalanceChanged += OnBalanceChanged;
        CoinCloudService.RefreshWallet();
        SelectItem(items[0]);
    }

    void OnDestroy() { CoinData.BalanceChanged -= OnBalanceChanged; }

    void BuildInterface()
    {
        GameObject canvasObject = new GameObject("Garden Shop Canvas", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600f, 900f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject background = Panel(canvasObject.transform, "草地背景", new Color(0.86f, 0.88f, 0.42f));
        Image backgroundImage = background.GetComponent<Image>();
        Sprite grass = Resources.Load<Sprite>("Shop/pet_grass_background_16x9");
        if (grass != null)
        {
            backgroundImage.sprite = grass;
            backgroundImage.color = Color.white;
            backgroundImage.preserveAspect = false;
        }

        AddDecoration(background.transform, "左上葉片", "花  葉", 48, new Vector2(0.015f, 0.88f), new Vector2(0.18f, 0.98f));
        AddDecoration(background.transform, "右下葉片", "  ", 48, new Vector2(0.82f, 0.02f), new Vector2(0.985f, 0.12f));

        GameObject header = Panel(canvasObject.transform, "木牌標題", parchment);
        SetRect(header.GetComponent<RectTransform>(), new Vector2(0.035f, 0.84f), new Vector2(0.965f, 0.965f));
        OutlinePanel(header, wood, 5f);

        TMP_Text title = Text(header.transform, "標題", "小樂寵物商店", 43, FontStyles.Bold, TextAlignmentOptions.Left);
        title.color = wood;
        SetRect(title.rectTransform, new Vector2(0.055f, 0.39f), new Vector2(0.48f, 0.88f));
        TMP_Text subtitle = Text(header.transform, "副標題", "替毛茸茸的朋友挑選今天的小驚喜", 22,
            FontStyles.Normal, TextAlignmentOptions.Left);
        subtitle.color = new Color(0.43f, 0.34f, 0.20f);
        SetRect(subtitle.rectTransform, new Vector2(0.057f, 0.10f), new Vector2(0.55f, 0.44f));

        GameObject coin = Panel(header.transform, "金幣木牌", new Color(0.97f, 0.79f, 0.35f));
        SetRect(coin.GetComponent<RectTransform>(), new Vector2(0.62f, 0.20f), new Vector2(0.77f, 0.80f));
        OutlinePanel(coin, woodLight, 3f);
        coinText = Text(coin.transform, "金幣", "金幣 0", 25, FontStyles.Bold, TextAlignmentOptions.Center);
        coinText.color = wood;
        Stretch(coinText.rectTransform, 8f);
        OnBalanceChanged(CoinData.TotalCoins);

        Button back = MakeButton(header.transform, "返回", "返回主選單", woodLight);
        SetRect(back.GetComponent<RectTransform>(), new Vector2(0.80f, 0.19f), new Vector2(0.95f, 0.81f));
        back.onClick.AddListener(() => SceneManager.LoadScene("GameScene"));

        GameObject sidebar = Panel(canvasObject.transform, "分類木牌", parchment);
        SetRect(sidebar.GetComponent<RectTransform>(), new Vector2(0.035f, 0.09f), new Vector2(0.19f, 0.81f));
        OutlinePanel(sidebar, wood, 5f);
        TMP_Text categoryTitle = Text(sidebar.transform, "分類標題", "商品分類", 29, FontStyles.Bold,
            TextAlignmentOptions.Center);
        categoryTitle.color = wood;
        SetRect(categoryTitle.rectTransform, new Vector2(0.08f, 0.86f), new Vector2(0.92f, 0.96f));
        CreateCategoryButton(sidebar.transform, "推薦", 0.69f, leaf);
        CreateCategoryButton(sidebar.transform, FoodCategory, 0.52f, woodLight);
        CreateCategoryButton(sidebar.transform, SupplyCategory, 0.35f, woodLight);
        TMP_Text hint = Text(sidebar.transform, "提示", "點選商品卡片\n查看詳細介紹", 20,
            FontStyles.Normal, TextAlignmentOptions.Center);
        hint.color = new Color(0.43f, 0.34f, 0.20f);
        SetRect(hint.rectTransform, new Vector2(0.09f, 0.08f), new Vector2(0.91f, 0.24f));

        GameObject products = Panel(canvasObject.transform, "商品陳列", new Color(1f, 0.98f, 0.85f, 0.94f));
        SetRect(products.GetComponent<RectTransform>(), new Vector2(0.21f, 0.09f), new Vector2(0.69f, 0.81f));
        OutlinePanel(products, new Color(0.61f, 0.43f, 0.21f), 4f);

        TMP_Text shelfTitle = Text(products.transform, "陳列標題", "本日精選", 31, FontStyles.Bold,
            TextAlignmentOptions.Left);
        shelfTitle.color = leaf;
        SetRect(shelfTitle.rectTransform, new Vector2(0.05f, 0.88f), new Vector2(0.68f, 0.97f));
        TMP_Text shelfHint = Text(products.transform, "陳列提示", "點心可重複購買", 18, FontStyles.Normal,
            TextAlignmentOptions.Right);
        shelfHint.color = wood;
        SetRect(shelfHint.rectTransform, new Vector2(0.64f, 0.89f), new Vector2(0.95f, 0.96f));

        GameObject viewport = Panel(products.transform, "商品視窗", new Color(0, 0, 0, 0));
        SetRect(viewport.GetComponent<RectTransform>(), new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.86f));
        viewport.AddComponent<RectMask2D>();

        GameObject contentObject = new GameObject("商品格", typeof(RectTransform), typeof(GridLayoutGroup),
            typeof(ContentSizeFitter));
        contentObject.transform.SetParent(viewport.transform, false);
        productContent = contentObject.transform;
        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.offsetMin = new Vector2(8f, 0f);
        contentRect.offsetMax = new Vector2(-8f, 0f);
        GridLayoutGroup grid = contentObject.GetComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(310f, 230f);
        grid.spacing = new Vector2(24f, 22f);
        grid.padding = new RectOffset(8, 8, 8, 8);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        contentObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollRect scroll = viewport.AddComponent<ScrollRect>();
        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 32f;

        BuildDetailPanel(canvasObject.transform);
        RefreshProducts();
    }

    void BuildDetailPanel(Transform parent)
    {
        GameObject detail = Panel(parent, "商品詳情", cream);
        SetRect(detail.GetComponent<RectTransform>(), new Vector2(0.71f, 0.09f), new Vector2(0.965f, 0.81f));
        OutlinePanel(detail, wood, 5f);

        GameObject iconPlate = Panel(detail.transform, "圖示底座", new Color(0.88f, 0.92f, 0.60f));
        SetRect(iconPlate.GetComponent<RectTransform>(), new Vector2(0.16f, 0.58f), new Vector2(0.84f, 0.92f));
        OutlinePanel(iconPlate, leafLight, 3f);
        detailIcon = new GameObject("商品圖片", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        detailIcon.transform.SetParent(iconPlate.transform, false);
        detailIcon.preserveAspect = true;
        SetRect(detailIcon.rectTransform, new Vector2(0.18f, 0.12f), new Vector2(0.82f, 0.88f));
        detailSymbol = Text(iconPlate.transform, "替代圖示", "", 64, FontStyles.Bold, TextAlignmentOptions.Center);
        detailSymbol.color = leaf;
        Stretch(detailSymbol.rectTransform, 8f);

        detailCategory = Text(detail.transform, "分類", "", 19, FontStyles.Bold, TextAlignmentOptions.Center);
        detailCategory.color = leaf;
        SetRect(detailCategory.rectTransform, new Vector2(0.12f, 0.51f), new Vector2(0.88f, 0.58f));
        detailName = Text(detail.transform, "名稱", "", 31, FontStyles.Bold, TextAlignmentOptions.Center);
        detailName.color = wood;
        SetRect(detailName.rectTransform, new Vector2(0.08f, 0.43f), new Vector2(0.92f, 0.52f));
        detailDescription = Text(detail.transform, "說明", "", 20, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        detailDescription.color = ink;
        SetRect(detailDescription.rectTransform, new Vector2(0.10f, 0.27f), new Vector2(0.90f, 0.42f));
        detailOwned = Text(detail.transform, "持有數量", "", 18, FontStyles.Bold, TextAlignmentOptions.Left);
        detailOwned.color = leaf;
        SetRect(detailOwned.rectTransform, new Vector2(0.10f, 0.20f), new Vector2(0.62f, 0.27f));
        detailPrice = Text(detail.transform, "價格", "", 24, FontStyles.Bold, TextAlignmentOptions.Right);
        detailPrice.color = wood;
        SetRect(detailPrice.rectTransform, new Vector2(0.54f, 0.19f), new Vector2(0.90f, 0.28f));

        buyButton = MakeButton(detail.transform, "購買", "購買", leaf);
        SetRect(buyButton.GetComponent<RectTransform>(), new Vector2(0.10f, 0.08f), new Vector2(0.90f, 0.18f));
        buyButtonLabel = buyButton.GetComponentInChildren<TMP_Text>();
        buyButton.onClick.AddListener(BuySelected);

        statusText = Text(detail.transform, "狀態", "", 17, FontStyles.Bold, TextAlignmentOptions.Center);
        statusText.color = new Color(0.65f, 0.24f, 0.16f);
        SetRect(statusText.rectTransform, new Vector2(0.08f, 0.01f), new Vector2(0.92f, 0.075f));
    }

    void CreateCategoryButton(Transform parent, string category, float y, Color color)
    {
        Button button = MakeButton(parent, "分類_" + category, category == "推薦" ? "今日推薦" :
            category == FoodCategory ? "寵物點心" : "生活用品", color);
        SetRect(button.GetComponent<RectTransform>(), new Vector2(0.10f, y), new Vector2(0.90f, y + 0.12f));
        button.onClick.AddListener(() =>
        {
            activeCategory = category;
            RefreshProducts();
        });
        categoryButtons.Add(button);
    }

    void RefreshProducts()
    {
        if (productContent == null) return;
        foreach (Transform child in productContent) Destroy(child.gameObject);
        List<ShopItem> visible = (activeCategory == "推薦"
            ? items
            : items.Where(item => item.category == activeCategory)).ToList();
        foreach (ShopItem item in visible) CreateProductCard(item);
        if (visible.Count > 0 && (selected == null || !visible.Contains(selected)))
            SelectItem(visible[0]);
    }

    void CreateProductCard(ShopItem item)
    {
        GameObject card = Panel(productContent, "商品_" + item.id, Color.white);
        OutlinePanel(card, item.available ? leafLight : new Color(0.72f, 0.69f, 0.58f), 3f);
        Button button = card.AddComponent<Button>();
        button.onClick.AddListener(() => SelectItem(item));

        GameObject art = Panel(card.transform, "圖片底色", item.available
            ? new Color(0.91f, 0.94f, 0.69f)
            : new Color(0.90f, 0.88f, 0.80f));
        SetRect(art.GetComponent<RectTransform>(), new Vector2(0.08f, 0.37f), new Vector2(0.92f, 0.92f));

        Sprite sprite = string.IsNullOrEmpty(item.resourceIcon) ? null : Resources.Load<Sprite>(item.resourceIcon);
        if (sprite != null)
        {
            Image image = new GameObject("商品圖片", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            image.transform.SetParent(art.transform, false);
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            SetRect(image.rectTransform, new Vector2(0.23f, 0.08f), new Vector2(0.77f, 0.92f));
        }
        else
        {
            TMP_Text symbol = Text(art.transform, "商品符號", item.symbol, 54, FontStyles.Bold,
                TextAlignmentOptions.Center);
            symbol.color = item.available ? leaf : new Color(0.48f, 0.45f, 0.38f);
            Stretch(symbol.rectTransform, 4f);
        }

        TMP_Text name = Text(card.transform, "名稱", item.name, 23, FontStyles.Bold, TextAlignmentOptions.Left);
        name.color = wood;
        SetRect(name.rectTransform, new Vector2(0.08f, 0.21f), new Vector2(0.70f, 0.36f));
        TMP_Text category = Text(card.transform, "分類", item.category, 16, FontStyles.Normal, TextAlignmentOptions.Left);
        category.color = leaf;
        SetRect(category.rectTransform, new Vector2(0.08f, 0.08f), new Vector2(0.45f, 0.21f));
        TMP_Text price = Text(card.transform, "價格", item.available ? "金幣 " + item.price : "即將推出",
            18, FontStyles.Bold, TextAlignmentOptions.Right);
        price.color = item.available ? wood : new Color(0.52f, 0.48f, 0.40f);
        SetRect(price.rectTransform, new Vector2(0.46f, 0.07f), new Vector2(0.92f, 0.22f));
    }

    void SelectItem(ShopItem item)
    {
        selected = item;
        statusText.text = "";
        detailName.text = item.name;
        detailCategory.text = item.category == FoodCategory ? "寵物點心" : "生活用品";
        detailDescription.text = item.description;
        detailPrice.text = item.available ? item.price + " 金幣" : "尚未開放";
        detailOwned.text = item.consumable
            ? "目前持有：" + InventoryData.GetItemCount(item.id)
            : InventoryData.HasItem(item.id) ? "已擁有" : "尚未擁有";

        Sprite sprite = string.IsNullOrEmpty(item.resourceIcon) ? null : Resources.Load<Sprite>(item.resourceIcon);
        detailIcon.sprite = sprite;
        detailIcon.enabled = sprite != null;
        detailSymbol.text = sprite == null ? item.symbol : "";
        buyButton.interactable = item.available;
        buyButtonLabel.text = item.available ? "用 " + item.price + " 金幣購買" : "即將推出";
    }

    void BuySelected()
    {
        if (selected == null || !selected.available) return;
        if (CoinData.TotalCoins < selected.price)
        {
            statusText.text = "金幣不足，完成遊戲或每日任務可獲得金幣";
            return;
        }

        buyButton.interactable = false;
        buyButtonLabel.text = "購買中…";
        statusText.text = "正在連線確認商品";
        ShopItem purchasing = selected;
        CoinCloudService.Purchase(purchasing.id, 1, result =>
        {
            if (this == null) return;
            if (!result.success)
            {
                statusText.text = result.message;
                buyButton.interactable = true;
                buyButtonLabel.text = "再試一次";
                return;
            }

            if (purchasing.consumable)
                InventoryData.SetItemCount(purchasing.id, result.itemQuantity);
            else
                InventoryData.AddItem(purchasing.id);
            statusText.text = "購買成功，已放入寵物背包";
            SelectItem(purchasing);
            statusText.text = "購買成功，已放入寵物背包";
        });
    }

    void OnBalanceChanged(int balance)
    {
        if (coinText != null) coinText.text = "金幣  " + balance;
    }

    Button MakeButton(Transform parent, string name, string label, Color color)
    {
        GameObject obj = Panel(parent, name, color);
        Button button = obj.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.92f, 0.72f);
        colors.pressedColor = new Color(0.86f, 0.77f, 0.58f);
        colors.disabledColor = new Color(0.68f, 0.67f, 0.61f);
        button.colors = colors;
        TMP_Text text = Text(obj.transform, "文字", label, 22, FontStyles.Bold, TextAlignmentOptions.Center);
        text.color = Color.white;
        Stretch(text.rectTransform, 8f);
        return button;
    }

    GameObject Panel(Transform parent, string name, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        obj.GetComponent<Image>().color = color;
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return obj;
    }

    void OutlinePanel(GameObject panel, Color color, float distance)
    {
        Outline outline = panel.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(distance, -distance);
        outline.useGraphicAlpha = true;
    }

    TMP_Text Text(Transform parent, string name, string value, float size, FontStyles style,
        TextAlignmentOptions alignment)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        TMP_Text text = obj.GetComponent<TMP_Text>();
        if (font != null) text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        return text;
    }

    void AddDecoration(Transform parent, string name, string value, float size, Vector2 min, Vector2 max)
    {
        TMP_Text decoration = Text(parent, name, value, size, FontStyles.Bold, TextAlignmentOptions.Center);
        decoration.color = new Color(0.36f, 0.52f, 0.17f, 0.65f);
        SetRect(decoration.rectTransform, min, max);
    }

    static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    static void Stretch(RectTransform rect, float padding)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(padding, padding);
        rect.offsetMax = new Vector2(-padding, -padding);
    }

    static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
