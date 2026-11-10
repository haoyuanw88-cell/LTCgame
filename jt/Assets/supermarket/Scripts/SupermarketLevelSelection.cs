using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Eight levels per page, using the existing pause menu artwork.</summary>
public sealed class SupermarketLevelSelection : MonoBehaviour
{
    public SupermarketGame manager;
    public SupermarketLevelCatalog catalog;
    public CognitiveGamePauseMenu pauseMenu;
    public GameObject panel;
    public Button[] levelButtons;
    public TMP_Text[] numberLabels;
    public TMP_Text[] sizeLabels;
    public Image[] selectionFrames;
    public Button playButton;
    public Button previousButton;
    public Button nextButton;
    public Button homeButton;
    public Button openButton;
    public TMP_Text pageLabel;
    public TMP_Text selectedLabel;
    public TMP_Text completionText;
    public GameObject completionPanel;
    public Button completionNextButton;
    public Button completionHomeButton;
    public Button completionSelectionButton;
    public bool IsOpen => panel != null && panel.activeSelf;
    public int SelectedIndex { get; private set; } = -1;
    private int page;
    private readonly List<SupermarketLevelData> available = new List<SupermarketLevelData>();
    private const int PageSize = 8;

    private void Awake()
    {
        if (catalog == null) catalog = Resources.Load<SupermarketLevelCatalog>(SupermarketLevelCatalog.ResourceName);
        for (int i = 0; i < levelButtons.Length; i++)
        {
            int slot = i;
            levelButtons[i].onClick.AddListener(() => SelectLevel(page * PageSize + slot));
        }
        playButton.onClick.AddListener(PlaySelected);
        previousButton.onClick.AddListener(() => ChangePage(-1));
        nextButton.onClick.AddListener(() => ChangePage(1));
        homeButton.onClick.AddListener(ReturnHome);
        openButton.onClick.AddListener(Open);
        if (completionNextButton != null) completionNextButton.onClick.AddListener(PlayNextLevel);
        if (completionSelectionButton != null) completionSelectionButton.onClick.AddListener(Open);
        if (completionHomeButton != null) completionHomeButton.onClick.AddListener(ReturnHome);
        if (completionPanel != null) completionPanel.SetActive(false);
    }

    public void Open()
    {
        if (pauseMenu != null) pauseMenu.ResumeGame();
        manager.CancelCurrentAssessment();
        manager.CloseTutorial();
        if (completionPanel != null) completionPanel.SetActive(false);
        LTCReturnHomeButton.Hide();
        available.Clear();
        if (catalog != null)
            foreach (SupermarketLevelData level in catalog.levels)
                if (level != null && !available.Contains(level)) available.Add(level);
        if (!CanPlay(SelectedIndex))
            SelectedIndex = available.Count > 0 ? 0 : -1;
        page = SelectedIndex >= 0 ? SelectedIndex / PageSize : 0;
        panel.SetActive(true);
        openButton.gameObject.SetActive(false);
        if (pauseMenu != null && pauseMenu.pauseButton != null) pauseMenu.pauseButton.gameObject.SetActive(false);
        Refresh();
    }

    public void SelectLevel(int index)
    {
        if (!CanPlay(index)) return;
        SelectedIndex = index;
        page = index / PageSize;
        Refresh();
    }

    public void ChangePage(int direction)
    {
        page = Mathf.Clamp(page + direction, 0, Mathf.Max(0, (available.Count - 1) / PageSize));
        Refresh();
    }

    public void PlaySelected()
    {
        if (!IsOpen || SelectedIndex / PageSize != page) return;
        StartLevel(SelectedIndex);
    }

    public void ShowCompletion(float seconds = 0, int failures = 0)
    {
        int currentIndex = available.IndexOf(manager.CurrentLevel);
        if (currentIndex >= 0) LTCLevelSelection.MarkCleared(SupermarketLevelCatalog.ResourceName, manager.CurrentLevel);
        if (completionPanel == null) return;
        LTCReturnHomeButton.Hide();
        completionNextButton.interactable = currentIndex >= 0 && CanPlay(currentIndex + 1);
        completionText.text = "時間：" + seconds.ToString("0.00") + " 秒\n失敗次數：" + failures;
        completionPanel.SetActive(true);
        openButton.gameObject.SetActive(false);
        if (pauseMenu != null && pauseMenu.pauseButton != null) pauseMenu.pauseButton.gameObject.SetActive(false);
    }

    public void PlayNextLevel()
    {
        if (completionPanel == null || !completionPanel.activeSelf || !manager.IsCleared) return;
        int currentIndex = available.IndexOf(manager.CurrentLevel);
        if (currentIndex < 0) return;
        StartLevel(currentIndex + 1);
    }

    private void StartLevel(int index)
    {
        if (!CanPlay(index)) return;
        SelectedIndex = index;
        if (completionPanel != null) completionPanel.SetActive(false);
        panel.SetActive(false);
        openButton.gameObject.SetActive(true);
        if (pauseMenu != null && pauseMenu.pauseButton != null) pauseMenu.pauseButton.gameObject.SetActive(true);
        if (!manager.LoadLevel(available[SelectedIndex])) { Open(); return; }
        if (!manager.IsTutorialOpen) manager.StartGame();
    }

    private bool CanPlay(int index) => LTCLevelSelection.CanPlay(SupermarketLevelCatalog.ResourceName, available, index);

    private void Refresh()
    {
        for (int slot = 0; slot < levelButtons.Length; slot++)
        {
            int index = page * PageSize + slot;
            bool exists = index < available.Count;
            levelButtons[slot].gameObject.SetActive(exists);
            bool unlocked = CanPlay(index);
            selectionFrames[slot].enabled = unlocked && index == SelectedIndex;
            if (!exists) continue;
            LTCLevelSelection.UpdateTile(levelButtons[slot], numberLabels[slot], sizeLabels[slot],
                !unlocked, LTCLevelSelection.IsCleared(SupermarketLevelCatalog.ResourceName, available[index]));
            numberLabels[slot].text = (index + 1).ToString("00");
            sizeLabels[slot].text = available[index].displayName + "\n" + available[index].products.Count + "種 " + available[index].TotalQuantity + "件";
        }
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(available.Count / (float)PageSize));
        pageLabel.text = (page + 1) + " / " + pageCount;
        previousButton.interactable = page > 0;
        nextButton.interactable = page + 1 < pageCount;
        playButton.interactable = CanPlay(SelectedIndex) && SelectedIndex / PageSize == page;
        if (playButton.interactable) selectedLabel.text = "已選擇第 " + (SelectedIndex + 1) + " 關";
        else selectedLabel.text = available.Count == 0 ? "尚未加入關卡" : "請選擇已解鎖的關卡";
    }

    private void ReturnHome()
    {
        manager.CancelCurrentAssessment();
        Time.timeScale = 1f;
        SceneManager.LoadScene(pauseMenu != null ? pauseMenu.gameHomeScene : "GameScene");
    }
}
