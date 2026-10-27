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
    private Slider musicSlider;
    private Slider effectsSlider;
    private TMP_Text musicPercentage;
    private TMP_Text effectsPercentage;
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
        page = transform.Find("設定頁")?.gameObject;
        if (page != null) { UpgradePage(); return; }

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
        UpgradePage();
        RefreshSetting();
        page.SetActive(false);
    }

    private void Bind()
    {
        EnsurePage();
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
        musicSlider.onValueChanged.RemoveListener(SetMusicVolume);
        musicSlider.onValueChanged.AddListener(SetMusicVolume);
        effectsSlider.onValueChanged.RemoveListener(SetEffectsVolume);
        effectsSlider.onValueChanged.AddListener(SetEffectsVolume);
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
        RefreshVolumes();
        bool enabled = LTCGameSettings.TutorialsEnabled;
        tutorialToggle.SetIsOnWithoutNotify(enabled);
        stateLabel.text = enabled ? "開啟" : "關閉";
        tutorialToggle.GetComponent<Image>().color = enabled
            ? new Color(.71f, .85f, .57f) : new Color(.82f, .81f, .77f);
    }

    private void UpgradePage()
    {
        var card = page.transform.Find("設定內容");
        var description = card.Find("教學說明");
        if (description != null)
        {
            description.gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(description.gameObject);
            else DestroyImmediate(description.gameObject);
        }
        Place((RectTransform)card.Find("標題"), .06f, .80f, .94f, .97f);
        Place((RectTransform)card.Find("教學標題"), .08f, .62f, .65f, .77f);
        Place((RectTransform)card.Find("遊戲教學開關"), .70f, .62f, .92f, .77f);
        CreateVolumeRow(card, "背景音樂", .38f);
        CreateVolumeRow(card, "音效", .14f);
        musicSlider = card.Find("背景音樂/滑桿").GetComponent<Slider>();
        effectsSlider = card.Find("音效/滑桿").GetComponent<Slider>();
        musicPercentage = card.Find("背景音樂/百分比").GetComponent<TMP_Text>();
        effectsPercentage = card.Find("音效/百分比").GetComponent<TMP_Text>();
        RefreshVolumes();
    }

    private void CreateVolumeRow(Transform card, string name, float bottom)
    {
        if (card.Find(name) != null) return;
        var rowObject = new GameObject(name, typeof(RectTransform));
        rowObject.transform.SetParent(card, false);
        var row = rowObject.GetComponent<RectTransform>();
        Place(row, .08f, bottom, .94f, bottom + .17f);
        Text(row, "標籤", name, 32, 0, 0, .26f, 1, TextAlignmentOptions.Left, true);
        Text(row, "百分比", "", 30, .85f, 0, 1, 1, TextAlignmentOptions.Right, true);
        var track = Panel(row, "滑桿", Color.clear, .28f, 0, .82f, 1);
        var slider = track.AddComponent<Slider>();
        slider.minValue = 0; slider.maxValue = 100; slider.wholeNumbers = true;
        slider.direction = Slider.Direction.LeftToRight;
        var outline = Panel(track.transform, "軌道外框", ink, 0, .34f, 1, .66f);
        outline.GetComponent<Image>().raycastTarget = false;
        var background = Panel(track.transform, "軌道底色", new Color(.88f, .85f, .77f), .008f, .39f, .992f, .61f);
        background.GetComponent<Image>().raycastTarget = false;
        var fillArea = new GameObject("填滿範圍", typeof(RectTransform)).GetComponent<RectTransform>();
        fillArea.SetParent(track.transform, false); Place(fillArea, .008f, .39f, .992f, .61f);
        var fill = Panel(fillArea, "填滿", new Color(.57f, .72f, .38f), 0, 0, 1, 1);
        fill.GetComponent<Image>().raycastTarget = false;
        slider.fillRect = fill.GetComponent<RectTransform>();
        var handleArea = new GameObject("拖曳範圍", typeof(RectTransform)).GetComponent<RectTransform>();
        handleArea.SetParent(track.transform, false); Place(handleArea, .02f, .14f, .98f, .86f);
        var handle = Panel(handleArea, "把手", new Color(.99f, .96f, .85f), 0, 0, 0, 1).GetComponent<RectTransform>();
        handle.sizeDelta = new Vector2(32, 0);
        slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<Image>();
    }

    private void RefreshVolumes()
    {
        if (musicSlider == null || effectsSlider == null) return;
        float music = PlayerPrefs.GetFloat("LTC.MusicVolume", .18f);
        float effects = PlayerPrefs.GetFloat("LTC.EffectsVolume", .45f);
        musicSlider.SetValueWithoutNotify(Mathf.RoundToInt(Mathf.Clamp01(music) * 100));
        effectsSlider.SetValueWithoutNotify(Mathf.RoundToInt(Mathf.Clamp01(effects) * 100));
        musicPercentage.text = musicSlider.value.ToString("0") + "%";
        effectsPercentage.text = effectsSlider.value.ToString("0") + "%";
    }

    private void SetMusicVolume(float percent)
    {
        LTC.Audio.LTCGameAudio.EnsureInstance().SetMusicVolume(percent / 100f);
        RefreshVolumes(); savedLabel.text = "已儲存：背景音樂 " + musicPercentage.text;
    }

    private void SetEffectsVolume(float percent)
    {
        var audio = LTC.Audio.LTCGameAudio.EnsureInstance();
        audio.SetEffectsVolume(percent / 100f);
        RefreshVolumes(); savedLabel.text = "已儲存：音效 " + effectsPercentage.text;
        audio.PlayTap();
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
