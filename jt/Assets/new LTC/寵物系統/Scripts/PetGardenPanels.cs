using System;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Pet-garden status and inventory panels. Uses the shop's actual saved inventory.</summary>
public sealed class PetGardenPanels : MonoBehaviour
{
    sealed class StatusView
    {
        public PetWander pet;
        public string id;
        public TMP_Text activity;
        public Image[] meats;
    }

    struct Item
    {
        public string id, name, icon;
        public bool consumable;
        public Item(string id, string name, string icon, bool consumable)
        { this.id = id; this.name = name; this.icon = icon; this.consumable = consumable; }
    }

    static readonly Item[] Items =
    {
        new Item("F_APPLE", "元氣蘋果", "Shop/apple", true),
        new Item("F_BANANA", "開心香蕉", "Shop/banana", true),
        new Item("F_PINE", "陽光鳳梨", "Shop/pineapple", true),
        new Item("P_FOREST_BALL", "快樂球", null, false),
        new Item("P_CLOUD_BED", "雲朵睡墊", null, false),
        new Item("P_WOOD_BRUSH", "木柄梳子", null, false)
    };

    static readonly Color Ink = new Color(.31f, .20f, .13f);
    static readonly Color Wood = new Color(.43f, .26f, .16f);
    static readonly Color Paper = new Color(1f, .97f, .87f);
    static readonly Color Sage = new Color(.83f, .89f, .68f);
    static readonly Color Peach = new Color(1f, .86f, .74f);

    TMP_FontAsset font;
    Transform canvas;
    GameObject overlay;
    Transform rows;
    bool showingBag;
    float nextStatusRefresh;
    readonly List<StatusView> statusViews = new List<StatusView>();
    readonly List<PetWander> orderedPets = new List<PetWander>();
    TMP_Text footer;
    Sprite meatSprite;
    Vector2 pointerDown;
    bool pointerStarted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryCreate(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate(scene);

    static void TryCreate(Scene scene)
    {
        if (scene.name != "PetGarden" || FindFirstObjectByType<PetGardenPanels>() != null) return;
        new GameObject("Pet Garden Panels").AddComponent<PetGardenPanels>();
    }

    void Start()
    {
        var ui = GameObject.Find("Pet Garden UI");
        if (ui == null) { Debug.LogError("Pet Garden UI was not found.", this); return; }
        canvas = ui.transform;
        var management = canvas.Find("Pet Management Button")?.GetComponent<Button>();
        if (management == null) { Debug.LogError("Pet Management Button was not found.", this); return; }
        font = management.GetComponentInChildren<TMP_Text>(true)?.font;
        meatSprite = Resources.Load<Sprite>("PetUI/meat");
        management.onClick.AddListener(OpenStatus);
        BuildBagButton(management);
        MatchNavigationText(canvas.Find("Return Main Menu Button")?.GetComponent<Button>(), management);
        BuildPopup();
        BuildResetButton(management);
    }

    void OnDestroy()
    {
        var management = canvas == null ? null : canvas.Find("Pet Management Button")?.GetComponent<Button>();
        if (management != null) management.onClick.RemoveListener(OpenStatus);
    }

    void Update()
    {
        HandlePetClick();
        if (overlay == null || !overlay.activeSelf || showingBag || Time.unscaledTime < nextStatusRefresh) return;
        nextStatusRefresh = Time.unscaledTime + .5f;
        RefreshStatusLabels();
    }

    void BuildBagButton(Button management)
    {
        var button = CreateButton(canvas, "背包按鈕", management.GetComponent<Image>().sprite);
        var rect = (RectTransform)button.transform;
        var source = (RectTransform)management.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(source.anchoredPosition.x + 355f, source.anchoredPosition.y);
        rect.sizeDelta = source.sizeDelta;
        var icon = CreateImage(button.transform, "背包圖示", Resources.Load<Sprite>("PetUI/backpack"), Color.white);
        Place(icon.rectTransform, .08f, .25f, .29f, .75f);
        icon.preserveAspect = true;

        TMP_Text sourceLabel = management.GetComponentInChildren<TMP_Text>(true);
        var label = CreateText(button.transform, "文字", "我的背包", 39, TextAlignmentOptions.Center);
        if (sourceLabel != null)
        {
            label.font = sourceLabel.font;
            label.fontStyle = sourceLabel.fontStyle;
            label.color = sourceLabel.color;
            label.enableAutoSizing = sourceLabel.enableAutoSizing;
            label.fontSizeMin = sourceLabel.fontSizeMin;
            label.fontSizeMax = sourceLabel.fontSizeMax;
        }
        Place(label.rectTransform, .30f, .18f, .94f, .82f);
        button.onClick.AddListener(OpenBag);
    }

    void MatchNavigationText(Button target, Button source)
    {
        if (target == null || source == null) return;
        TMP_Text targetLabel = target.GetComponentInChildren<TMP_Text>(true);
        TMP_Text sourceLabel = source.GetComponentInChildren<TMP_Text>(true);
        if (targetLabel == null || sourceLabel == null) return;

        targetLabel.font = sourceLabel.font;
        targetLabel.fontStyle = sourceLabel.fontStyle;
        targetLabel.fontSize = sourceLabel.fontSize;
        targetLabel.color = sourceLabel.color;
        targetLabel.alignment = TextAlignmentOptions.Center;
        targetLabel.enableAutoSizing = sourceLabel.enableAutoSizing;
        targetLabel.fontSizeMin = sourceLabel.fontSizeMin;
        targetLabel.fontSizeMax = sourceLabel.fontSizeMax;
        Place(targetLabel.rectTransform, .30f, .18f, .94f, .82f);
    }

    void BuildResetButton(Button source)
    {
        var button = CreateButton(canvas, "測試用寵物重置按鈕", source.GetComponent<Image>().sprite);
        RectTransform rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(24f, -24f);
        rect.sizeDelta = new Vector2(310f, 112f);
        button.image.color = new Color(1f, .84f, .78f);

        TMP_Text sourceLabel = source.GetComponentInChildren<TMP_Text>(true);
        var label = CreateText(button.transform, "文字", "測試：重置寵物", 30, TextAlignmentOptions.Center);
        if (sourceLabel != null)
        {
            label.font = sourceLabel.font;
            label.fontStyle = sourceLabel.fontStyle;
            label.color = sourceLabel.color;
        }
        label.fontSizeMin = 23f;
        label.fontSizeMax = 30f;
        Place(label.rectTransform, .17f, .16f, .94f, .84f);
        button.onClick.AddListener(OpenPetResetConfirmation);
    }


    void BuildPopup()
    {
        overlay = CreateImage(canvas, "寵物視窗遮罩", null, new Color(.15f, .11f, .08f, .68f)).gameObject;
        Place((RectTransform)overlay.transform, 0, 0, 1, 1);
        // Pet sprite renderers use a high depth order so they stay above the grass.
        // A nested canvas ensures modal windows remain above those sprites.
        var modalCanvas = overlay.AddComponent<Canvas>();
        modalCanvas.overrideSorting = true;
        modalCanvas.sortingOrder = 10000;
        overlay.AddComponent<GraphicRaycaster>();
        var frame = new GameObject("視窗深色外框", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(overlay.transform, false);
        var frameImage = frame.GetComponent<Image>();
        var texture = Resources.Load<Texture2D>("PetUI/panel");
        frameImage.sprite = Sprite.Create(texture, new Rect(0, texture.height * .19f, texture.width, texture.height * .62f),
            new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(180, 110, 180, 110));
        frameImage.type = Image.Type.Sliced;
        Place((RectTransform)frame.transform, .12f, .07f, .88f, .93f);
        var card = CreateImage(frame.transform, "奶油色內容", null, Color.clear);
        Place(card.rectTransform, .07f, .08f, .93f, .92f);
        var title = CreateText(card.transform, "標題", "", 52, TextAlignmentOptions.Left);
        Place(title.rectTransform, .05f, .78f, .70f, .89f);
        title.name = "視窗標題";
        var close = CreateButton(card.transform, "關閉視窗", null);
        close.GetComponent<Image>().color = Peach;
        Place((RectTransform)close.transform, .80f, .79f, .96f, .90f);
        var closeText = CreateText(close.transform, "文字", "關閉", 32, TextAlignmentOptions.Center);
        Place(closeText.rectTransform, 0, 0, 1, 1);
        close.onClick.AddListener(() => overlay.SetActive(false));

        var contentBg = CreateImage(card.transform, "狀態與背包內容", null, Color.clear);
        Place(contentBg.rectTransform, .03f, .23f, .97f, .76f);
        rows = contentBg.transform;
        footer = CreateText(card.transform, "提示", "花園裡的兔子與貓咪會自在地散步。", 32, TextAlignmentOptions.Center);
        Place(footer.rectTransform, .07f, .13f, .93f, .22f);
        overlay.SetActive(false);
    }

    void OpenStatus()
    {
        showingBag = false;
        Show("寵物狀態", "上下滑動查看；點草地上的寵物可以餵食");
        BuildStatusList();
    }

    void OpenBag()
    {
        showingBag = true;
        statusViews.Clear();
        Show("我的背包", "商店購買的物品會顯示在這裡");
        ClearRows();
        var owned = new List<(string, Sprite, string)>();
        foreach (var item in Items)
        {
            int count = item.consumable ? InventoryData.GetItemCount(item.id) : (InventoryData.HasItem(item.id) ? 1 : 0);
            if (count > 0) owned.Add((item.name, item.icon == null ? null : Resources.Load<Sprite>(item.icon), item.consumable ? "持有 " + count + " 個" : "已擁有"));
        }
        if (owned.Count == 0)
        {
            var empty = CreateText(rows, "空背包", "背包還是空的\n到商店選一件喜歡的物品吧！", 42, TextAlignmentOptions.Center);
            Place(empty.rectTransform, .08f, .22f, .92f, .80f);
            return;
        }
        for (int i = 0; i < owned.Count; i++) AddRow(i, owned[i].Item1, owned[i].Item2, owned[i].Item3, i % 2 == 0 ? Sage : Peach);
    }

    void Show(string heading, string hint)
    {
        overlay.transform.Find("視窗深色外框/奶油色內容/視窗標題").GetComponent<TMP_Text>().text = heading;
        footer.text = hint;
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
    }

    static string PetId(PetWander pet)
    {
        return pet.PetId;
    }

    void BuildStatusList()
    {
        ClearRows();
        statusViews.Clear();
        orderedPets.Clear();
        foreach (var pet in FindObjectsByType<PetWander>(FindObjectsSortMode.None))
            if (pet.isActiveAndEnabled) orderedPets.Add(pet);
        orderedPets.Sort((a, b) => string.CompareOrdinal(PetId(a), PetId(b)));

        if (orderedPets.Count == 0)
        {
            var empty = CreateText(rows, "沒有寵物", "花園裡暫時沒有寵物", 42, TextAlignmentOptions.Center);
            Place(empty.rectTransform, .08f, .20f, .92f, .80f);
            return;
        }

        var viewport = CreateImage(rows, "直式寵物清單", null, Color.clear);
        Place(viewport.rectTransform, 0, 0, 1, 1);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = new GameObject("清單內容", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        var rect = (RectTransform)content.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(.5f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        var layout = content.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 4, 4);
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport.rectTransform;
        scroll.content = rect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        for (int i = 0; i < orderedPets.Count; i++) AddStatusRow(content.transform, orderedPets[i], i);
        RefreshStatusLabels();
    }

    void AddStatusRow(Transform parent, PetWander pet, int index)
    {
        string id = PetId(pet);
        var frame = CreateImage(parent, "寵物_" + id, null, Wood);
        var element = frame.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 200f;
        element.minHeight = 200f;
        var background = CreateImage(frame.transform, "卡片底色", null, index % 2 == 0 ? Peach : Sage);
        Place(background.rectTransform, .004f, .025f, .996f, .975f);

        var portrait = CreateImage(background.transform, "頭像框", null, Paper);
        Place(portrait.rectTransform, .025f, .10f, .21f, .90f);
        portrait.gameObject.AddComponent<RectMask2D>();
        AddPetPortrait(portrait.transform, pet, id);

        var name = CreateText(background.transform, "名稱", pet.DisplayName, 38, TextAlignmentOptions.Center);
        Place(name.rectTransform, .23f, .46f, .45f, .91f);
        var activity = CreateText(background.transform, "活動", "", 27, TextAlignmentOptions.Center);
        Place(activity.rectTransform, .23f, .08f, .45f, .45f);

        var icons = new Image[3];
        for (int i = 0; i < icons.Length; i++)
        {
            icons[i] = CreateImage(background.transform, "肉_" + (i + 1), meatSprite, Color.white);
            Place(icons[i].rectTransform, .49f + i * .15f, .18f, .63f + i * .15f, .82f);
            icons[i].preserveAspect = true;
            icons[i].raycastTarget = false;
        }

        statusViews.Add(new StatusView { pet = pet, id = id, activity = activity, meats = icons });
    }

    void AddPetPortrait(Transform parent, PetWander pet, string id)
    {
        if (pet.IsCollectionPet)
        {
            var picture = CreateImage(parent, "寵物頭像", pet.Portrait, Color.white);
            Place(picture.rectTransform, 0, 0, 1, 1);
            picture.preserveAspect = true;
            picture.raycastTarget = false;
            return;
        }
        var renderer = pet.transform.Find("Visual/Artwork")?.GetComponent<SpriteRenderer>();
        if (renderer == null) renderer = pet.GetComponentInChildren<SpriteRenderer>();
        if (renderer == null || renderer.sprite == null) return;
        var sprite = renderer.sprite;
        var portrait = new GameObject("寵物頭像", typeof(RectTransform), typeof(RawImage));
        portrait.transform.SetParent(parent, false);
        var image = portrait.GetComponent<RawImage>();
        image.texture = sprite.texture;
        image.raycastTarget = false;
        Rect source = sprite.textureRect;
        float left = id == "cat" ? .01f : .08f;
        float bottom = id == "cat" ? .18f : .22f;
        float width = id == "cat" ? .52f : .84f;
        float height = id == "cat" ? .80f : .75f;
        image.uvRect = new Rect((source.x + source.width * left) / sprite.texture.width,
            (source.y + source.height * bottom) / sprite.texture.height,
            source.width * width / sprite.texture.width, source.height * height / sprite.texture.height);
        Place((RectTransform)portrait.transform, 0, 0, 1, 1);
        var fit = portrait.AddComponent<AspectRatioFitter>();
        fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fit.aspectRatio = (source.width * width) / (source.height * height);
    }

    void RefreshStatusLabels()
    {
        foreach (var view in statusViews)
        {
            if (view.pet == null) continue;
            view.activity.text = view.pet.IsWalking ? "散步中" : "休息中";
            int count = PetHungerService.MeatCount(view.id);
            for (int i = 0; i < view.meats.Length; i++)
                if (view.meats[i] != null) view.meats[i].enabled = i < count;
        }
    }

    void Feed(string id)
    {
        PetHungerService.TryFeed(id, out string message);
        footer.text = message;
        RefreshStatusLabels();
    }

    void HandlePetClick()
    {
        if(PetCollectionBook.ModalOpen){pointerStarted=false;return;}
        if (overlay == null || overlay.activeSelf) { pointerStarted = false; return; }
        bool down = false, up = false;
        Vector2 position = Vector2.zero;
        if (Touchscreen.current != null && (Touchscreen.current.primaryTouch.press.isPressed ||
            Touchscreen.current.primaryTouch.press.wasReleasedThisFrame))
        {
            var touch = Touchscreen.current.primaryTouch;
            position = touch.position.ReadValue();
            down = touch.press.wasPressedThisFrame;
            up = touch.press.wasReleasedThisFrame;
        }
        else if (Mouse.current != null)
        {
            position = Mouse.current.position.ReadValue();
            down = Mouse.current.leftButton.wasPressedThisFrame;
            up = Mouse.current.leftButton.wasReleasedThisFrame;
        }
        if (down) { pointerDown = position; pointerStarted = true; }
        if (!up || !pointerStarted) return;
        pointerStarted = false;
        if (Vector2.Distance(pointerDown, position) > 20f) return;
        if (EventSystem.current != null)
        {
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            if (hits.Count > 0) return;
        }
        var camera = Camera.main;
        if (camera == null) return;
        foreach (var pet in FindObjectsByType<PetWander>(FindObjectsSortMode.None))
        {
            var renderer = pet.transform.Find("Visual/Artwork")?.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = pet.GetComponentInChildren<SpriteRenderer>();
            if (renderer == null || !renderer.enabled) continue;
            Vector3 world = camera.ScreenToWorldPoint(new Vector3(position.x, position.y,
                Mathf.Abs(camera.transform.position.z - renderer.transform.position.z)));
            world.z = renderer.bounds.center.z;
            if (renderer.bounds.Contains(world)) { OpenFeedConfirmation(pet); break; }
        }
    }

    public void OpenFeedConfirmation(PetWander pet)
    {
        if (pet == null) return;
        showingBag = true;
        statusViews.Clear();
        ClearRows();
        string id = PetId(pet);
        string name = pet.DisplayName;
        Show("餵食" + name, "一份背包點心可以補回一塊肉");
        var question = CreateText(rows, "餵食詢問", "要餵食「" + name + "」嗎？", 50, TextAlignmentOptions.Center);
        Place(question.rectTransform, .05f, .53f, .95f, .82f);
        var yes = CreateButton(rows, "確認餵食", null);
        yes.image.color = Sage;
        Place((RectTransform)yes.transform, .12f, .17f, .46f, .40f);
        var yesText = CreateText(yes.transform, "文字", "是，餵食", 40, TextAlignmentOptions.Center);
        Place(yesText.rectTransform, 0, 0, 1, 1);
        var no = CreateButton(rows, "取消餵食", null);
        no.image.color = Peach;
        Place((RectTransform)no.transform, .54f, .17f, .88f, .40f);
        var noText = CreateText(no.transform, "文字", "先不要", 40, TextAlignmentOptions.Center);
        Place(noText.rectTransform, 0, 0, 1, 1);
        no.onClick.AddListener(() => overlay.SetActive(false));
        yes.onClick.AddListener(() =>
        {
            bool success = PetHungerService.TryFeed(id, out string message);
            question.text = message;
            if (success) { yes.interactable = false; yesText.text = "已餵食"; }
            noText.text = "返回花園";
        });
    }

    void OpenPetResetConfirmation()
    {
        showingBag = true;
        statusViews.Clear();
        ClearRows();
        Show("測試用寵物重置", "只會清除已擁有寵物，不影響金幣、背包或成就");

        var question = CreateText(rows, "重置確認",
            "確定要清空目前擁有的所有寵物嗎？", 46, TextAlignmentOptions.Center);
        Place(question.rectTransform, .05f, .53f, .95f, .82f);

        var yes = CreateButton(rows, "確認重置寵物", null);
        yes.image.color = new Color(1f, .72f, .66f);
        Place((RectTransform)yes.transform, .12f, .17f, .46f, .40f);
        var yesText = CreateText(yes.transform, "文字", "確認清空", 38, TextAlignmentOptions.Center);
        Place(yesText.rectTransform, 0, 0, 1, 1);

        var no = CreateButton(rows, "取消重置", null);
        no.image.color = Sage;
        Place((RectTransform)no.transform, .54f, .17f, .88f, .40f);
        var noText = CreateText(no.transform, "文字", "取消", 38, TextAlignmentOptions.Center);
        Place(noText.rectTransform, 0, 0, 1, 1);
        no.onClick.AddListener(() => overlay.SetActive(false));

        yes.onClick.AddListener(() =>
        {
            int removed = LTCPetCollectionService.ResetOwnedPetsForTesting();
            question.text = removed > 0
                ? "已清除 " + removed + " 隻寵物。"
                : "目前沒有可清除的寵物。";
            yes.interactable = false;
            yesText.text = "已完成";
            noText.text = "返回花園";
        });
    }

    void ClearRows()
    {
        foreach (Transform child in rows)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
    }

    void AddRow(int index, string name, Sprite icon, string detail, Color tint)
    {
        // Three columns stay legible on a tablet; extra purchases continue on following rows.
        int column = index % 3, line = index / 3;
        var frame = CreateImage(rows, name, null, Wood);
        float x0 = showingBag ? .035f + column * .32f : .17f + index * .34f;
        float x1 = x0 + (showingBag ? .29f : .31f);
        float y1 = showingBag ? .95f - line * .46f : .84f;
        float y0 = showingBag ? y1 - .40f : .20f;
        Place(frame.rectTransform, x0, y0, x1, y1);
        var inner = CreateImage(frame.transform, "卡片底色", null, tint);
        Place(inner.rectTransform, .012f, .018f, .988f, .982f);
        if (icon != null)
        {
            var picture = CreateImage(inner.transform, "物品", icon, Color.white);
            Place(picture.rectTransform, .35f, .30f, .65f, .90f);
            picture.preserveAspect = true;
        }
        var heading = CreateText(inner.transform, "名稱", name, 34, TextAlignmentOptions.Center);
        Place(heading.rectTransform, .04f, icon == null ? .43f : .16f, .96f, icon == null ? .84f : .42f);
        var state = CreateText(inner.transform, "數量或活動", detail, 32, TextAlignmentOptions.Center);
        Place(state.rectTransform, .03f, .04f, .97f, icon == null ? .42f : .18f);
    }

    Button CreateButton(Transform parent, string name, Sprite sprite)
    {
        var image = CreateImage(parent, name, sprite, Color.white);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    Image CreateImage(Transform parent, string name, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        return image;
    }

    TMP_Text CreateText(Transform parent, string name, string value, float size, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TMP_Text>();
        if (font != null) text.font = font;
        text.text = value;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.alignment = alignment;
        text.color = Ink;
        text.enableAutoSizing = true;
        text.fontSizeMin = size * .78f;
        text.fontSizeMax = size;
        text.raycastTarget = false;
        return text;
    }

    static void Place(RectTransform rect, float x0, float y0, float x1, float y1)
    {
        rect.anchorMin = new Vector2(x0, y0);
        rect.anchorMax = new Vector2(x1, y1);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
