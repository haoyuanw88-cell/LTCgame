using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared warm-paper surfaces and readable controls for secondary screens.</summary>
public static class LTCWarmUi
{
    public static readonly Color Ink = new Color(.29f, .20f, .14f);
    public static readonly Color Paper = new Color(1f, .97f, .88f);
    public static readonly Color Sage = new Color(.80f, .87f, .66f);
    public static readonly Color Peach = new Color(.98f, .83f, .68f);
    static Sprite rounded;

    static Sprite Rounded
    {
        get
        {
            if (rounded != null) return rounded;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Warm UI rounded surface";
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x - 31.5f) - 15.5f, 0);
                float dy = Mathf.Max(Mathf.Abs(y - 31.5f) - 15.5f, 0);
                pixels[y * size + x] = new Color(1, 1, 1,
                    Mathf.Clamp01(16 - Mathf.Sqrt(dx * dx + dy * dy)));
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            rounded = Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(20, 20, 20, 20));
            return rounded;
        }
    }

    public static void Surface(Image image, Color color, bool border = false)
    {
        if (!image) return;
        image.sprite = Rounded;
        image.type = Image.Type.Sliced;
        image.color = color;
        if (border)
        {
            var outline = image.GetComponent<Outline>() ?? image.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.43f, .30f, .19f, .65f);
            outline.effectDistance = new Vector2(2, -2);
        }
    }

    public static void Place(Transform target, float x, float y, float right, float top)
    {
        if (!target) return;
        var rect = target as RectTransform;
        rect.anchorMin = new Vector2(x, y); rect.anchorMax = new Vector2(right, top);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    static void Text(Transform root, string path, string value, float size)
    {
        var text = root.Find(path)?.GetComponent<TMP_Text>();
        if (!text) return;
        if (value != null) text.text = value;
        text.color = Ink;
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = size * .85f;
        text.fontSizeMax = size;
        text.raycastTarget = false;
    }

    static void Controls(Transform root)
    {
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) text.color = Ink;
        foreach (var button in root.GetComponentsInChildren<Button>(true))
        {
            Surface(button.GetComponent<Image>(), Sage, true);
            var colors = button.colors;
            colors.highlightedColor = new Color(1, .98f, .90f);
            colors.pressedColor = new Color(.85f, .88f, .75f);
            button.colors = colors;
        }
    }

    public static void StyleCatalog(Transform root, Action editProfile)
    {
        var header = root.Find("遊戲首頁/玩家資訊列");
        if (header && !header.Find("編輯資料"))
        {
            var go = new GameObject("編輯資料", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(header, false);
            Place(go.transform, .29f, .18f, .42f, .82f);
            Surface(go.GetComponent<Image>(), Sage, true);
            var labelObject = new GameObject("文字", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(go.transform, false);
            Place(labelObject.transform, .05f, .08f, .95f, .92f);
            var label = labelObject.GetComponent<TMP_Text>();
            var source = header.Find("玩家名稱")?.GetComponent<TMP_Text>();
            if (source) label.font = source.font;
            label.text = "編輯資料"; label.color = Ink; label.fontSize = 20;
            label.enableAutoSizing = true; label.fontSizeMin = 16; label.fontSizeMax = 22;
            label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
            go.GetComponent<Button>().onClick.AddListener(() => editProfile());
        }
        if (header) Place(header.Find("玩家名稱"), .14f, .15f, .28f, .85f);

        var card = root.Find("首次引導/引導卡片");
        if (card)
        {
            Place(card, .10f, .07f, .90f, .93f);
            Surface(card.GetComponent<Image>(), Paper, true);
            Controls(card);
            var npc = card.Find("NPC區");
            Place(npc, .035f, .06f, .33f, .94f);
            if (npc) Surface(npc.GetComponent<Image>(), new Color(.90f, .92f, .78f));
            Text(card, "NPC區/NPC名稱", "一起開始今天的小練習", 25);
            Text(card, "NPC區/介紹", "留下基本資料，\n更了解自己的進步。\n\n填寫一次即可，之後可從\n名稱旁的「編輯資料」修改。\n\n遊戲紀錄不作為醫療診斷。", 22);
            Text(card, "標題", "我的基本資料", 36);
            Text(card, "用途說明", "用於計算年齡及整理適當的比較資料。", 21);
            Place(card.Find("標題"), .38f, .81f, .94f, .93f);
            Place(card.Find("用途說明"), .38f, .72f, .94f, .81f);
            Place(card.Find("生日標題"), .38f, .65f, .94f, .72f);
            Place(card.Find("生日滾輪"), .38f, .48f, .94f, .65f);
            Place(card.Find("性別下拉"), .38f, .36f, .94f, .46f);
            Place(card.Find("教育程度下拉"), .38f, .24f, .94f, .34f);
            Place(card.Find("狀態"), .38f, .17f, .94f, .24f);
            Place(card.Find("稍後填寫"), .38f, .055f, .58f, .16f);
            Place(card.Find("儲存並開始"), .61f, .055f, .94f, .16f);
            Text(card, "儲存並開始/文字", "儲存資料", 25);
            Surface(card.Find("稍後填寫")?.GetComponent<Image>(), new Color(.92f, .88f, .79f), true);
        }

        var detail = root.Find("遊戲詳情");
        if (detail)
        {
            Controls(detail);
            // Reuse the homepage artwork so introductions belong to the same game.
            var background = root.Find("遊戲首頁")?.GetComponent<Image>();
            var target = detail.GetComponent<Image>();
            if (background && target) { target.sprite = background.sprite; target.color = background.color; }
            var info = detail.Find("遊戲資訊");
            Place(info, .10f, .24f, .90f, .72f);
            if (info) Surface(info.GetComponent<Image>(), Paper, true);
            Place(detail.Find("分類標題"), .12f, .87f, .88f, .95f);
            Place(detail.Find("遊戲標題"), .12f, .75f, .88f, .87f);
            Text(detail, "分類標題", null, 26);
            Text(detail, "遊戲標題", null, 40);
            Text(detail, "遊戲資訊/測量內容", null, 25);
            Text(detail, "遊戲資訊/玩法", null, 23);
            Text(detail, "遊戲資訊/最近紀錄", null, 20);
            Place(detail.Find("返回"), .10f, .07f, .38f, .19f);
            Place(detail.Find("開始"), .57f, .07f, .90f, .19f);
            if (info)
            {
                Place(info.Find("測量內容"), .045f, .70f, .955f, .96f);
                Place(info.Find("玩法"), .045f, .39f, .955f, .69f);
                Place(info.Find("最近紀錄"), .045f, .035f, .955f, .38f);
            }
        }
    }
}
