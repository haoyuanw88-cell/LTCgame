using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Eight levels per page, using the existing pause menu artwork.</summary>
public sealed class PipeLevelSelection : MonoBehaviour
{
    public PipeManager manager;
    public PipeLevelCatalog catalog;
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
    public SpriteRenderer boardBackground;
    public GameObject completionPanel;
    public Button completionNextButton;
    public Button completionHomeButton;
    public bool IsOpen => panel != null && panel.activeSelf;
    public int SelectedIndex { get; private set; } = -1;
    private int page;
    private readonly List<PipeLevelData> available = new List<PipeLevelData>();
    private const int PageSize = 8;

    private void Awake()
    {
        if (catalog == null) catalog = Resources.Load<PipeLevelCatalog>(PipeLevelCatalog.ResourceName);
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
        if (completionHomeButton != null) completionHomeButton.onClick.AddListener(ReturnHome);
        if (completionPanel != null) completionPanel.SetActive(false);
    }

    public void Open()
    {
        if (pauseMenu != null) pauseMenu.ResumeGame();
        manager.CancelCurrentAssessment();
        manager.CloseTutorial();
        if (manager.winUI != null) manager.winUI.SetActive(false);
        if (completionPanel != null) completionPanel.SetActive(false);
        LTCReturnHomeButton.Hide();
        available.Clear();
        if (catalog != null)
            foreach (PipeLevelData level in catalog.levels)
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

    public void ShowCompletion()
    {
        if (completionPanel == null) return;
        LTCReturnHomeButton.Hide();
        int currentIndex = available.IndexOf(manager.levelToLoad);
        completionNextButton.interactable = currentIndex >= 0 && currentIndex + 1 < available.Count;
        completionPanel.SetActive(true);
        openButton.gameObject.SetActive(false);
        if (pauseMenu != null && pauseMenu.pauseButton != null) pauseMenu.pauseButton.gameObject.SetActive(false);
    }

    public void PlayNextLevel()
    {
        if (completionPanel == null || !completionPanel.activeSelf || !manager.isGameOver) return;
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
        FitCamera(available[SelectedIndex]);
        manager.CenterTutorial();
    }

    private void Refresh()
    {
        for (int slot = 0; slot < levelButtons.Length; slot++)
        {
            int index = page * PageSize + slot;
            bool exists = index < available.Count;
            levelButtons[slot].gameObject.SetActive(exists);
            selectionFrames[slot].enabled = exists && index == SelectedIndex;
            if (!exists) continue;
            numberLabels[slot].text = (index + 1).ToString("00");
            sizeLabels[slot].text = available[index].width + " × " + available[index].height;
        }
        int pageCount = Mathf.Max(1, Mathf.CeilToInt(available.Count / (float)PageSize));
        pageLabel.text = (page + 1) + " / " + pageCount;
        previousButton.interactable = page > 0;
        nextButton.interactable = page + 1 < pageCount;
        playButton.interactable = SelectedIndex >= 0;
        selectedLabel.text = SelectedIndex >= 0
            ? "已選擇第 " + (SelectedIndex + 1) + " 關" : "尚未加入關卡";
    }

    private void FitCamera(PipeLevelData level)
    {
        Camera camera = Camera.main;
        if (camera == null || !camera.orthographic) return;
        Bounds bounds = new Bounds(new Vector3((level.width - 1) * level.cellSize * .5f,
            (level.height - 1) * level.cellSize * .5f, 0f), new Vector3(level.width * level.cellSize, level.height * level.cellSize));
        var renderers = manager.levelRoot != null ? manager.levelRoot.GetComponentsInChildren<SpriteRenderer>() : new SpriteRenderer[0];
        if (renderers.Length > 0)
        {
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        }
        float margin = level.cellSize * .7f;
        camera.transform.position = new Vector3(bounds.center.x, bounds.center.y + margin * .2f, camera.transform.position.z);
        camera.orthographicSize = Mathf.Max(bounds.extents.y + margin,
            (bounds.extents.x + margin) / Mathf.Max(.1f, camera.aspect));
        if (boardBackground != null)
        {
            Vector3 size = boardBackground.bounds.size;
            float scale = Mathf.Max(camera.orthographicSize * 2f * camera.aspect / Mathf.Max(.01f, size.x),
                camera.orthographicSize * 2f / Mathf.Max(.01f, size.y)) * 1.02f;
            boardBackground.transform.localScale *= scale;
            Vector3 position = boardBackground.transform.position;
            position.x += camera.transform.position.x - boardBackground.bounds.center.x;
            position.y += camera.transform.position.y - boardBackground.bounds.center.y;
            boardBackground.transform.position = position;
        }
    }

    private void ReturnHome()
    {
        manager.CancelCurrentAssessment();
        Time.timeScale = 1f;
        SceneManager.LoadScene(pauseMenu != null ? pauseMenu.gameHomeScene : "GameScene");
    }
}
