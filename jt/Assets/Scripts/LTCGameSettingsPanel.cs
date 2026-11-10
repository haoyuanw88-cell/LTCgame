using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Adds a settings page without rebuilding the authored catalog UI.</summary>
public sealed class LTCGameSettingsPanel : MonoBehaviour
{
    private GameObject page;
    private Toggle tutorialToggle;
    private TMP_Text stateLabel;
    private Toggle unlockLevelsToggle;
    private TMP_Text unlockLevelsState;
    private TMP_Text savedLabel;
    private Button entry;
    private Button back;
    private Button resetButton;
    private Button confirmResetButton;
    private Button cancelResetButton;
    private GameObject resetConfirmation;
    private Slider musicSlider;
    private Slider effectsSlider;
    private TMP_Text musicPercentage;
    private TMP_Text effectsPercentage;
    private readonly Color ink = new Color(.25f, .19f, .15f);
    private readonly Color cream = new Color(1f, .975f, .91f);
    private TMP_FontAsset font;
    private Sprite skin;
    private GameObject pauseControls;
    private string BackName => pauseControls != null ? "返回暫停選單" : "返回我的頁";

    public static void Ensure(Transform canvas)
    {
        if (canvas == null) return;
        var panel = canvas.GetComponent<LTCGameSettingsPanel>();
        if (panel == null) panel = canvas.gameObject.AddComponent<LTCGameSettingsPanel>();
        panel.EnsurePage();
        if (Application.isPlaying) panel.Bind();
    }

    public static void AttachToPause(GameObject overlay, GameObject controls, Button settingsButton)
    {
        var panel = overlay.AddComponent<LTCGameSettingsPanel>();
        panel.pauseControls = controls;
        panel.entry = settingsButton;
        panel.Bind();
    }

    private void Awake()
    {
        if (Application.isPlaying && transform.Find("設定頁") != null) Bind();
    }

    private void EnsurePage()
    {
        if (pauseControls == null)
            entry = transform.Find("我的頁/個人資料內容/設定")?.GetComponent<Button>();
        if (entry == null) return;
        entry.interactable = true;
        var entryText = entry.GetComponentInChildren<TMP_Text>(true);
        if (entryText != null) { entryText.text = "設定"; font = entryText.font; }
        var entryImage = entry.targetGraphic as Image;
        if (entryImage != null && pauseControls == null) skin = entryImage.sprite;
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
        var backObject = Panel(page.transform, BackName, new Color(.85f, .92f, .73f), .35f, .045f, .65f, .14f);
        back = backObject.AddComponent<Button>();
        back.targetGraphic = backObject.GetComponent<Image>();
        Text(back.transform, "文字", BackName, 30, .04f, .04f, .96f, .96f, TextAlignmentOptions.Center, true);
        UpgradePage();
        RefreshSetting();
        page.SetActive(false);
    }

    private void Bind()
    {
        EnsurePage();
        if (entry == null || page == null) return;
        tutorialToggle = page.transform.Find("設定內容/遊戲教學開關").GetComponent<Toggle>();
        stateLabel = tutorialToggle.GetComponentInChildren<TMP_Text>(true);
        savedLabel = page.transform.Find("儲存狀態").GetComponent<TMP_Text>();
        back = page.transform.Find(BackName).GetComponent<Button>();
        entry.onClick.RemoveListener(Open);
        entry.onClick.AddListener(Open);
        back.onClick.RemoveListener(Close);
        back.onClick.AddListener(Close);
        tutorialToggle.onValueChanged.RemoveListener(SetTutorialsEnabled);
        tutorialToggle.onValueChanged.AddListener(SetTutorialsEnabled);
        unlockLevelsToggle.onValueChanged.RemoveListener(SetUnlockAllLevels);
        unlockLevelsToggle.onValueChanged.AddListener(SetUnlockAllLevels);
        musicSlider.onValueChanged.RemoveListener(SetMusicVolume);
        musicSlider.onValueChanged.AddListener(SetMusicVolume);
        effectsSlider.onValueChanged.RemoveListener(SetEffectsVolume);
        effectsSlider.onValueChanged.AddListener(SetEffectsVolume);
        resetButton.onClick.RemoveListener(OpenResetConfirmation);
        resetButton.onClick.AddListener(OpenResetConfirmation);
        confirmResetButton.onClick.RemoveListener(RestoreDefaultSettings);
        confirmResetButton.onClick.AddListener(RestoreDefaultSettings);
        cancelResetButton.onClick.RemoveListener(CancelResetConfirmation);
        cancelResetButton.onClick.AddListener(CancelResetConfirmation);
        RefreshSetting();
    }

    private void Open()
    {
        resetConfirmation.SetActive(false);
        RefreshSetting();
        savedLabel.text = "變更後自動儲存，下次開啟遊戲也會保留。";
        page.transform.SetAsLastSibling();
        page.SetActive(true);
        if (pauseControls != null) pauseControls.SetActive(false);
    }

    private void Close()
    {
        resetConfirmation.SetActive(false);
        page.SetActive(false);
        if (pauseControls != null) pauseControls.SetActive(true);
    }

    private void OnDisable()
    {
        if (pauseControls != null && page != null && page.activeSelf) Close();
    }

    private void SetTutorialsEnabled(bool enabled)
    {
        LTCGameSettings.TutorialsEnabled = enabled;
        RefreshSetting();
        savedLabel.text = enabled ? "已儲存：遊戲教學已開啟。" : "已儲存：遊戲教學已關閉。";
    }

    private void RefreshSetting()
    {
        RefreshVolumes();
        RefreshToggle(tutorialToggle, stateLabel, LTCGameSettings.TutorialsEnabled);
        RefreshToggle(unlockLevelsToggle, unlockLevelsState, LTCGameSettings.UnlockAllLevels);
    }

    private void SetUnlockAllLevels(bool enabled)
    {
        LTCGameSettings.UnlockAllLevels = enabled;
        RefreshSetting();
        savedLabel.text = enabled ? "已儲存：所有關卡皆可遊玩。" : "已儲存：依照通關進度解鎖。";
    }

    private static void RefreshToggle(Toggle toggle, TMP_Text label, bool enabled)
    {
        toggle.SetIsOnWithoutNotify(enabled);
        label.text = enabled ? "開啟" : "關閉";
        toggle.GetComponent<Image>().color = enabled
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
        Place((RectTransform)card.Find("教學標題"), .08f, .63f, .65f, .78f);
        Place((RectTransform)card.Find("遊戲教學開關"), .70f, .63f, .92f, .78f);
        var unlockSwitch = card.Find("解鎖所有關卡開關");
        if (unlockSwitch == null)
        {
            Text(card, "解鎖關卡標題", "解鎖所有關卡", 34,
                .08f, .45f, .65f, .60f, TextAlignmentOptions.Left, true);
            var switchObject = Panel(card, "解鎖所有關卡開關", Color.white, .70f, .45f, .92f, .60f);
            unlockLevelsToggle = switchObject.AddComponent<Toggle>();
            unlockLevelsToggle.targetGraphic = switchObject.GetComponent<Image>();
            unlockLevelsToggle.transition = Selectable.Transition.None;
            unlockLevelsState = Text(switchObject.transform, "狀態", "", 32,
                .04f, .04f, .96f, .96f, TextAlignmentOptions.Center, true);
        }
        else
        {
            unlockLevelsToggle = unlockSwitch.GetComponent<Toggle>();
            unlockLevelsState = unlockSwitch.Find("狀態").GetComponent<TMP_Text>();
        }
        CreateVolumeRow(card, "背景音樂", .26f);
        CreateVolumeRow(card, "音效", .07f);
        musicSlider = card.Find("背景音樂/滑桿").GetComponent<Slider>();
        effectsSlider = card.Find("音效/滑桿").GetComponent<Slider>();
        musicPercentage = card.Find("背景音樂/百分比").GetComponent<TMP_Text>();
        effectsPercentage = card.Find("音效/百分比").GetComponent<TMP_Text>();
        EnsureResetControls();
        RefreshVolumes();
    }

    private void EnsureResetControls()
    {
        Place((RectTransform)page.transform.Find(BackName), .53f, .045f, .83f, .14f);
        var existing = page.transform.Find("恢復預設設定");
        if (existing == null)
        {
            var button = Panel(page.transform, "恢復預設設定", new Color(.94f, .87f, .74f), .17f, .045f, .47f, .14f);
            resetButton = button.AddComponent<Button>();
            resetButton.targetGraphic = button.GetComponent<Image>();
            Text(button.transform, "文字", "恢復預設設定", 28, .04f, .04f, .96f, .96f, TextAlignmentOptions.Center, true);
        }
        else resetButton = existing.GetComponent<Button>();

        var overlay = page.transform.Find("恢復預設確認");
        if (overlay == null)
        {
            resetConfirmation = Panel(page.transform, "恢復預設確認", new Color(.14f, .12f, .09f, .7f), 0, 0, 1, 1);
            var shade = resetConfirmation.GetComponent<Image>();
            shade.sprite = null; shade.type = Image.Type.Simple;
            var card = Panel(resetConfirmation.transform, "確認卡片", cream, .19f, .19f, .81f, .81f);
            Text(card.transform, "標題", "恢復預設設定？", 38, .06f, .77f, .94f, .94f, TextAlignmentOptions.Center, true);
            Text(card.transform, "說明", "",
                27, .07f, .28f, .93f, .75f, TextAlignmentOptions.Center, false);
            var cancel = Panel(card.transform, "取消", new Color(.80f, .89f, .91f), .10f, .08f, .46f, .24f);
            cancelResetButton = cancel.AddComponent<Button>();
            cancelResetButton.targetGraphic = cancel.GetComponent<Image>();
            Text(cancel.transform, "文字", "取消", 30, .04f, .04f, .96f, .96f, TextAlignmentOptions.Center, true);
            var confirm = Panel(card.transform, "確認恢復", new Color(.85f, .92f, .73f), .54f, .08f, .90f, .24f);
            confirmResetButton = confirm.AddComponent<Button>();
            confirmResetButton.targetGraphic = confirm.GetComponent<Image>();
            Text(confirm.transform, "文字", "確認恢復", 30, .04f, .04f, .96f, .96f, TextAlignmentOptions.Center, true);
            resetConfirmation.SetActive(false);
        }
        else
        {
            resetConfirmation = overlay.gameObject;
            cancelResetButton = overlay.Find("確認卡片/取消").GetComponent<Button>();
            confirmResetButton = overlay.Find("確認卡片/確認恢復").GetComponent<Button>();
        }
        resetConfirmation.transform.Find("確認卡片/說明").GetComponent<TMP_Text>().text =
            "背景音樂：18%　音效：45%\n遊戲教學：開啟　解鎖所有關卡：關閉\n\n玩家資料與遊戲進度都會保留。";
    }

    private void OpenResetConfirmation()
    {
        resetConfirmation.transform.SetAsLastSibling();
        resetConfirmation.SetActive(true);
        if (UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(cancelResetButton.gameObject);
    }

    private void CancelResetConfirmation()
    {
        resetConfirmation.SetActive(false);
        if (UnityEngine.EventSystems.EventSystem.current != null)
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(resetButton.gameObject);
    }

    private void RestoreDefaultSettings()
    {
        LTCGameSettings.RestoreDefaults();
        RefreshSetting();
        CancelResetConfirmation();
        savedLabel.text = "已恢復預設設定並儲存。";
    }

    private void CreateVolumeRow(Transform card, string name, float bottom)
    {
        var existing = card.Find(name);
        if (existing != null)
        {
            Place((RectTransform)existing, .08f, bottom, .94f, bottom + .17f);
            return;
        }
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
        float music = PlayerPrefs.GetFloat("LTC.MusicVolume", LTCGameSettings.DefaultMusicVolume);
        float effects = PlayerPrefs.GetFloat("LTC.EffectsVolume", LTCGameSettings.DefaultEffectsVolume);
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
