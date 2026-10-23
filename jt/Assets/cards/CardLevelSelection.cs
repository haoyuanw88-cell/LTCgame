using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Eight levels per page, using the existing pause menu artwork.</summary>
public sealed class CardLevelSelection : MonoBehaviour
{
    public GameController manager;
    public CardLevelCatalog catalog;
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
    public TMP_Text resultLabel;
    public TMP_Text completionTitle;
    public Button retryButton;
    public Button completionSelectionButton;
    private bool lastWin;
    public GameObject completionPanel;
    public Button completionNextButton;
    public Button completionHomeButton;
    public bool IsOpen => panel != null && panel.activeSelf;
    public int SelectedIndex { get; private set; } = -1;
    private int page;
    private readonly List<CardLevelData> available = new List<CardLevelData>();
    private const int PageSize = 8;

    private void Awake()
    {
        if (catalog == null) catalog = Resources.Load<CardLevelCatalog>(CardLevelCatalog.ResourceName);
        for (int i = 0; i < levelButtons.Length; i++)
        {
            int slot = i;
            levelButtons[i].onClick.AddListener(() => SelectLevel(page * PageSize + slot));
        }
        if (retryButton != null) retryButton.onClick.AddListener(() => StartLevel(SelectedIndex));
        if (completionSelectionButton != null) completionSelectionButton.onClick.AddListener(Open);
        playButton.onClick.AddListener(PlaySelected);
        previousButton.onClick.AddListener(() => ChangePage(-1));
        nextButton.onClick.AddListener(() => ChangePage(1));
        homeButton.onClick.AddListener(ReturnHome);
        openButton.onClick.AddListener(Open);
        if (completionNextButton != null) completionNextButton.onClick.AddListener(PlayNextLevel);
        if (completionHomeButton != null) completionHomeButton.onClick.AddListener(ReturnHome);
        if (completionPanel != null) completionPanel.SetActive(false);
    }

    public void Open()
    {
        if (pauseMenu != null) pauseMenu.ResumeGame();
        manager.CancelCurrentAssessment();
        manager.CloseTutorialForSelection();

        if (completionPanel != null) completionPanel.SetActive(false);
        LTCReturnHomeButton.Hide();
        available.Clear();
        if (catalog != null)
            foreach (CardLevelData level in catalog.levels)
                if (level != null && !available.Contains(level)) available.Add(level);
        if (SelectedIndex < 0 || SelectedIndex >= available.Count)
            SelectedIndex = available.Count > 0 ? 0 : -1;
        page = SelectedIndex >= 0 ? SelectedIndex / PageSize : 0;
        panel.SetActive(true);
        openButton.gameObject.SetActive(false);
        if (pauseMenu != null && pauseMenu.pauseButton != null) pauseMenu.pauseButton.gameObject.SetActive(false);
        Refresh();
    }

    public void SelectLevel(int index)
    {
        if (index < 0 || index >= available.Count) return;
        SelectedIndex = index;
        Refresh();
    }

    public void ChangePage(int direction)
    {
        page = Mathf.Clamp(page + direction, 0, Mathf.Max(0, (available.Count - 1) / PageSize));
        Refresh();
    }

    public void PlaySelected()
    {
        if (!IsOpen || SelectedIndex < 0 || SelectedIndex >= available.Count) return;
        StartLevel(SelectedIndex);
    }

    public void ShowCompletion(bool win, string result)
    {
        if (completionPanel == null) return;
        LTCReturnHomeButton.Hide();
        int currentIndex = available.IndexOf(manager.levelToLoad);
        lastWin = win;
        if (completionTitle != null) completionTitle.text = win ? "過關！" : "再試一次";
        resultLabel.text = result;
        completionNextButton.interactable = win && currentIndex >= 0 && currentIndex + 1 < available.Count;
        completionPanel.SetActive(true);
        openButton.gameObject.SetActive(false);
        if (pauseMenu != null && pauseMenu.pauseButton != null) pauseMenu.pauseButton.gameObject.SetActive(false);
    }

    public void PlayNextLevel()
    {
        if (!lastWin || completionPanel == null || !completionPanel.activeSelf || !manager.IsBattleEnded) return;
        int currentIndex = available.IndexOf(manager.levelToLoad);
        if (currentIndex < 0 || currentIndex + 1 >= available.Count) return;
        StartLevel(currentIndex + 1);
    }

    private void StartLevel(int index)
    {
        SelectedIndex = index;
        if (completionPanel != null) completionPanel.SetActive(false);
        panel.SetActive(false);
        openButton.gameObject.SetActive(true);
        if (pauseMenu != null && pauseMenu.pauseButton != null) pauseMenu.pauseButton.gameObject.SetActive(true);
        manager.LoadLevel(available[SelectedIndex]);


    }

    private void Refresh()
    {
        for (int slot = 0; slot < levelButtons.Length; slot++)
        {
            int index = page * PageSize + slot;
            bool exists = index < available.Count;
            levelButtons[slot].gameObject.SetActive(exists);
            // The selection frame is a sibling of the button; hide it even for an empty slot.
            selectionFrames[slot].enabled = exists && index == SelectedIndex;
            if (!exists) continue;
            numberLabels[slot].text = (index + 1).ToString("00");
            if (sizeLabels[slot] != null) sizeLabels[slot].gameObject.SetActive(false);

        }
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(available.Count / (float)PageSize));
        pageLabel.text = (page + 1) + " / " + pageCount;
        previousButton.interactable = page > 0;
        nextButton.interactable = page + 1 < pageCount;
        playButton.interactable = SelectedIndex >= 0;
        selectedLabel.text = SelectedIndex >= 0
            ? "已選擇第 " + (SelectedIndex + 1) + " 關" : "尚未加入關卡";
    }

    private void ReturnHome()
    {
        manager.CancelCurrentAssessment();
        Time.timeScale = 1f;
        SceneManager.LoadScene(pauseMenu != null ? pauseMenu.gameHomeScene : "GameScene");
    }
}

