using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Adds a settings page without rebuilding the authored catalog UI.</summary>
public sealed class LTCGameSettingsPanel : MonoBehaviour
{
    private GameObject page;
    private Toggle tutorialToggle;
    private TMP_Text stateLabel;
    private TMP_Text savedLabel;
    private Button entry;
    private Button back;
    private readonly Color ink = new Color(.25f, .19f, .15f);
    private readonly Color cream = new Color(1f, .975f, .91f);
    private TMP_FontAsset font;
    private Sprite skin;

    public static void Ensure(Transform canvas)
    {
        if (canvas == null) return;
        var panel = canvas.GetComponent<LTCGameSettingsPanel>();
        if (panel == null) panel = canvas.gameObject.AddComponent<LTCGameSettingsPanel>();
        panel.EnsurePage();
        if (Application.isPlaying) panel.Bind();
    }

    private void Awake()
    {
        if (Application.isPlaying && transform.Find("設定頁") != null) Bind();
    }

    private void EnsurePage()
    {
        entry = transform.Find("我的頁/個人資料內容/設定")?.GetComponent<Button>();
        if (entry == null) return;
        entry.interactable = true;
        var entryText = entry.GetComponentInChildren<TMP_Text>(true);
        if (entryText != null) { entryText.text = "設定"; font = entryText.font; }
        var entryImage = entry.targetGraphic as Image;
        if (entryImage != null) skin = entryImage.sprite;
        if (transform.Find("設定頁") != null) return;

        page = Panel(transform, "設定頁", cream, 0, 0, 1, 1);
        var background = transform.Find("我的頁")?.GetComponent<Image>();
        if (background != null)
        {
            var image = page.GetComponent<Image>();
            image.sprite = background.sprite;
            image.type = background.type;
            image.color = background.color;
        }
        var card = Panel(page.transform, "設定內容", cream, .09f, .23f, .91f, .86f);
        Text(card.transform, "標題", "設定", 42, .06f, .77f, .94f, .96f, TextAlignmentOptions.Center, true);
        Text(card.transform, "教學標題", "顯示遊戲教學", 34, .08f, .48f, .65f, .68f, TextAlignmentOptions.Left, true);
        Text(card.transform, "教學說明", "統一開啟或關閉所有遊戲的教學畫面。\n關閉後，選好關卡即可直接開始遊戲。",
            26, .08f, .15f, .92f, .43f, TextAlignmentOptions.TopLeft, false);
        var switchObject = Panel(card.transform, "遊戲教學開關", Color.white, .70f, .48f, .92f, .68f);
        tutorialToggle = switchObject.AddComponent<Toggle>();
        tutorialToggle.targetGraphic = switchObject.GetComponent<Image>();
        tutorialToggle.transition = Selectable.Transition.None;
        stateLabel = Text(switchObject.transform, "狀態", "", 32, .04f, .04f, .96f, .96f, TextAlignmentOptions.Center, true);
        savedLabel = Text(page.transform, "儲存狀態", "變更後自動儲存，下次開啟遊戲也會保留。",
            24, .12f, .16f, .88f, .23f, TextAlignmentOptions.Center, false);
        var backObject = Panel(page.transform, "返回我的頁", new Color(.85f, .92f, .73f), .35f, .045f, .65f, .14f);
        back = backObject.AddComponent<Button>();
        back.targetGraphic = backObject.GetComponent<Image>();
        Text(back.transform, "文字", "返回我的頁", 30, .04f, .04f, .96f, .96f, TextAlignmentOptions.Center, true);
        RefreshSetting();
        page.SetActive(false);
    }

    private void Bind()
    {
        entry = transform.Find("我的頁/個人資料內容/設定")?.GetComponent<Button>();
        page = transform.Find("設定頁")?.gameObject;
        if (entry == null || page == null) return;
        tutorialToggle = page.transform.Find("設定內容/遊戲教學開關").GetComponent<Toggle>();
        stateLabel = tutorialToggle.GetComponentInChildren<TMP_Text>(true);
        savedLabel = page.transform.Find("儲存狀態").GetComponent<TMP_Text>();
        back = page.transform.Find("返回我的頁").GetComponent<Button>();
        entry.onClick.RemoveListener(Open);
        entry.onClick.AddListener(Open);
        back.onClick.RemoveListener(Close);
        back.onClick.AddListener(Close);
        tutorialToggle.onValueChanged.RemoveListener(SetTutorialsEnabled);
        tutorialToggle.onValueChanged.AddListener(SetTutorialsEnabled);
        RefreshSetting();
    }

    private void Open()
    {
        RefreshSetting();
        savedLabel.text = "變更後自動儲存，下次開啟遊戲也會保留。";
        page.transform.SetAsLastSibling();
        page.SetActive(true);
    }

    private void Close() => page.SetActive(false);

    private void SetTutorialsEnabled(bool enabled)
    {
        LTCGameSettings.TutorialsEnabled = enabled;
        RefreshSetting();
        savedLabel.text = enabled ? "已儲存：遊戲教學已開啟。" : "已儲存：遊戲教學已關閉。";
    }

    private void RefreshSetting()
    {
        bool enabled = LTCGameSettings.TutorialsEnabled;
        tutorialToggle.SetIsOnWithoutNotify(enabled);
        stateLabel.text = enabled ? "開啟" : "關閉";
        tutorialToggle.GetComponent<Image>().color = enabled
            ? new Color(.71f, .85f, .57f) : new Color(.82f, .81f, .77f);
    }

    private GameObject Panel(Transform parent, string name, Color color, float x, float y, float xx, float yy)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Place(go.GetComponent<RectTransform>(), x, y, xx, yy);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.sprite = skin;
        image.type = Image.Type.Sliced;
        return go;
    }

    private TMP_Text Text(Transform parent, string name, string value, float size, float x, float y,
        float xx, float yy, TextAlignmentOptions alignment, bool bold)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var label = go.GetComponent<TMP_Text>();
        if (font != null) label.font = font;
        label.text = value;
        label.fontSize = size;
        label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        label.color = ink;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.Normal;
        Place(label.rectTransform, x, y, xx, yy);
        return label;
    }

    private static void Place(RectTransform rect, float x, float y, float xx, float yy)
    {
        rect.anchorMin = new Vector2(x, y);
        rect.anchorMax = new Vector2(xx, yy);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
