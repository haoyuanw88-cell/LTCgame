using System;
using System.Collections;
using System.Collections.Generic;
using LTCCognitiveAssessment;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro; // ⭐ 新增：使用 TextMesh Pro 必備

public class PipeManager : MonoBehaviour
{
    public int width = 12;  
    public int height = 12; 
    public Blockin[,] allPipes;
    public GameObject winUI; 
    public bool isGameOver = false;

    [Header("關卡資料設定")]
    public PipeLevelData levelToLoad;
    public bool loadLevelOnStart = false;
    public bool clearScenePipesWhenLoading = true;
    public Transform levelRoot;
    public AudioClip defaultPipeRotateSound;

    [Header("終點提示設定")]
    public bool showEndpointHintOnStart = true;
    public string endpointHintObjectName = "GUI_2";

    [Header("Tutorial")]
    public bool showTutorialOnStart = true;
    public string tutorialBackgroundObjectName = "black_0";
    public string tutorialPreviousButtonObjectName = "arrowLeft_0";
    public string tutorialNextButtonObjectName = "arrowRight_0";
    public string tutorialCloseButtonObjectName = "cross_0";
    public string tutorialFirstPageObjectName = "10_0";
    public string tutorialSecondPageObjectName = "11_0";

    [Header("音效設定")]
    public AudioSource myAudioSource; 
    public AudioClip winSound;

    [Header("⭐ 遊戲數據 UI 設定")]
    public TextMeshProUGUI moveCountText;  // 拖入畫面上顯示步數的 TMPro
    public TextMeshProUGUI timerText;      // 拖入畫面上顯示時間的 TMPro
    public TextMeshProUGUI winResultText;  // 可選：直接顯示在過關視窗裡的綜合統計文字

    // 內部數據變數
    private int moveCount = 0;
    private float elapsedTime = 0f;
    private GameObject tutorialBackground;
    private GameObject tutorialPreviousButton;
    private GameObject tutorialNextButton;
    private GameObject tutorialCloseButton;
    private GameObject tutorialFirstPage;
    private GameObject tutorialSecondPage;
    private int tutorialPageIndex;
    private int trialIndex;
    private int randomSeed;
    private bool tutorialActive;
    private bool endpointHintPendingAfterTutorial;
    private bool assessmentCompleted;
    private string assessmentSessionId;

    public bool IsTutorialActive => tutorialActive;

    void Start()
    {
        ResetRunState();

        if (loadLevelOnStart && levelToLoad != null)
        {
            BuildLevelObjects(levelToLoad);
        }

        BuildPipeCache();

        // 初始化 UI 顯示
        UpdateUI();
        UpdateTimerUI();

        SetupTutorial();
        if (showTutorialOnStart)
        {
            ShowTutorial();
        }
        else
        {
            CloseTutorial();
        }

        if (showEndpointHintOnStart)
        {
            ShowEndpointHintWhenReady();
        }

        CheckConnections();
    }

    public void LoadLevel(PipeLevelData newLevel)
    {
        if (newLevel == null)
        {
            Debug.LogWarning("LoadLevel failed: level data is null.");
            return;
        }

        levelToLoad = newLevel;
        ResetRunState();
        BuildLevelObjects(newLevel);
        BuildPipeCache();
        UpdateUI();
        UpdateTimerUI();
        SetupTutorial();
        if (showTutorialOnStart)
        {
            ShowTutorial();
        }
        else
        {
            CloseTutorial();
        }

        if (showEndpointHintOnStart)
        {
            ShowEndpointHintWhenReady();
        }
        CheckConnections();
    }

    private void ResetRunState()
    {
        isGameOver = false;
        moveCount = 0;
        elapsedTime = 0f;
        trialIndex = 0;
        randomSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
        assessmentCompleted = false;
        assessmentSessionId = null;

        if (winUI != null)
        {
            winUI.SetActive(false);
        }
    }

    private void BuildLevelObjects(PipeLevelData level)
    {
        levelRoot = PipeLevelSceneBuilder.BuildLevel(level, levelRoot, clearScenePipesWhenLoading, null, defaultPipeRotateSound);
        width = level.width;
        height = level.height;
    }

    private void BuildPipeCache()
    {
        allPipes = new Blockin[width, height];
        
        Blockin[] pipes = UnityEngine.Object.FindObjectsByType<Blockin>(FindObjectsInactive.Include);
        
        foreach (var p in pipes)
        {
            if (p.x >= 0 && p.x < width && p.y >= 0 && p.y < height)
            {
                allPipes[p.x, p.y] = p;
            }
            else
            {
                Debug.LogError($"[座標錯誤] 水管 {p.name} 在 ({p.x}, {p.y})，超出範圍或為負數！請移動它。");
            }
        }
    }

    private void ShowEndpointHint()
    {
        GameObject hintObject = FindHintObject(endpointHintObjectName);
        List<Blockin> endpointPipes = FindEndpointPipes();

        if (hintObject != null && endpointPipes.Count > 0)
        {
            EndpointHint.PlayAll(hintObject, endpointPipes);
        }
    }

    private void ShowEndpointHintWhenReady()
    {
        if (tutorialActive)
        {
            endpointHintPendingAfterTutorial = true;
            return;
        }

        endpointHintPendingAfterTutorial = false;
        ShowEndpointHint();
    }

    private GameObject FindHintObject(string hintName)
    {
        if (string.IsNullOrWhiteSpace(hintName))
        {
            return null;
        }

        GameObject activeObject = GameObject.Find(hintName);
        if (activeObject != null)
        {
            return activeObject;
        }

        Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate != null && candidate.name == hintName)
            {
                return candidate.gameObject;
            }
        }

        return null;
    }

    private List<Blockin> FindEndpointPipes()
    {
        List<Blockin> endpointPipes = new List<Blockin>();
        if (allPipes == null)
        {
            return endpointPipes;
        }

        foreach (var pipe in allPipes)
        {
            if (pipe != null && pipe.isEndingPipe)
            {
                endpointPipes.Add(pipe);
            }
        }

        return endpointPipes;
    }

    void Update()
    {
        HandleTutorialInput();

        // 如果遊戲還沒結束，就持續累加時間
        if (!isGameOver && !tutorialActive)
        {
            elapsedTime += Time.deltaTime;
            UpdateTimerUI();
        }
    }

    // ⭐ 新增：提供給 Blockin 呼叫的方法
    public void AddMoveCount()
    {
        if (isGameOver) return;
        moveCount++;
        RecordPipeMove();
        UpdateUI();
    }

    private void StartAssessmentIfNeeded()
    {
        if (!string.IsNullOrEmpty(assessmentSessionId))
        {
            return;
        }

        assessmentSessionId = CognitiveAssessmentService.BeginGame(
            "pipe_connection",
            CognitiveProtocolRegistry.ProtocolVersion);
    }

    private void RecordPipeMove()
    {
        if (string.IsNullOrEmpty(assessmentSessionId))
        {
            return;
        }

        trialIndex++;
        CognitiveAssessmentService.RecordTrial(assessmentSessionId, new CognitiveTrialRecord
        {
            trialIndex = trialIndex,
            roundIndex = 1,
            stepIndex = moveCount,
            eventKind = "selection",
            randomSeed = randomSeed,
            difficulty = Mathf.Max(1, width * height),
            stimulusCount = width * height,
            condition = "pipe_rotation",
            stimulus = "moves=" + moveCount,
            expectedAnswer = "connect_start_to_end",
            userAnswer = "rotate_pipe",
            outcome = TrialOutcome.ValidAction,
            reactionTimeMs = Mathf.RoundToInt(elapsedTime * 1000f),
            actionCount = moveCount
        });
    }

    // ⭐ 新增：即時更新步數 UI
    private void UpdateUI()
    {
        if (moveCountText != null)
        {
            moveCountText.text = $"Moves: {moveCount}";
        }
    }

    // ⭐ 新增：即時更新時間 UI (將秒數轉為 00:00 格式)
    private void UpdateTimerUI()
    {
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(elapsedTime / 60f);
            int seconds = Mathf.FloorToInt(elapsedTime % 60f);
            timerText.text = string.Format("Time：{0:00}:{1:00}", minutes, seconds);
        }
    }

    public void CheckConnections()
    {
        if (isGameOver) return;

        foreach (var p in allPipes)
        {
            if (p == null)
            {
                continue;
            }

            p.ClearFlow();
            if (p.isStartingPipe)
            {
                p.SetFlowColor(p.pipeColor);
            }
        }

        Queue<Blockin> checkQueue = new Queue<Blockin>();
        foreach (var p in allPipes)
        {
            if (p != null && p.isStartingPipe) checkQueue.Enqueue(p);
        }

        while (checkQueue.Count > 0)
        {
            Blockin current = checkQueue.Dequeue();
            CheckNeighbor(current, 0, 0, 1, checkQueue);  // 上
            CheckNeighbor(current, 1, 1, 0, checkQueue);  // 右
            CheckNeighbor(current, 2, 0, -1, checkQueue); // 下
            CheckNeighbor(current, 3, -1, 0, checkQueue); // 左
        }

        bool hasEndingPipe = false;
        bool gameWin = true;
        foreach (var p in allPipes)
        {
            if (p != null)
            {
                p.UpdateVisual();
                if (p.isEndingPipe)
                {
                    hasEndingPipe = true;
                    PipeFlowColor requiredColor = PipeLevelUtility.NormalizeFlowColor(p.pipeColor);
                    if (!p.hasWater || p.currentFlowColor != requiredColor)
                    {
                        gameWin = false;
                    }
                }
            }
        }

        if (hasEndingPipe && gameWin) { WinGame(); }
    }

    void CheckNeighbor(Blockin curr, int dir, int dx, int dy, Queue<Blockin> queue)
    {
        PipeFlowColor flowColor = curr.currentFlowColor;
        if (flowColor == PipeFlowColor.None)
        {
            return;
        }

        int nx = curr.x + dx;
        int ny = curr.y + dy;

        if (nx >= 0 && nx < width && ny >= 0 && ny < height)
        {
            Blockin neighbor = allPipes[nx, ny];
            if (neighbor != null) 
            {
                int oppositeDir = (dir + 2) % 4; 

                if (curr.openings[dir] && neighbor.openings[oppositeDir] && neighbor.CanAcceptFlow(flowColor))
                {
                    if (!neighbor.hasWater)
                    {
                        neighbor.SetFlowColor(flowColor);
                        queue.Enqueue(neighbor);
                        Debug.DrawLine(curr.transform.position, neighbor.transform.position, flowColor == PipeFlowColor.Red ? Color.red : Color.cyan, 1f);
                    }
                }
            }
        }
    }

    void WinGame()
    {
        StartAssessmentIfNeeded();
        isGameOver = true;
        CompleteAssessment();
        if (myAudioSource != null && winSound != null) myAudioSource.PlayOneShot(winSound);
        
        // ⭐ 新增：過關時計算最終時間並顯示在結算畫面上
        if (winResultText != null)
        {
            int minutes = Mathf.FloorToInt(elapsedTime / 60f);
            int seconds = Mathf.FloorToInt(elapsedTime % 60f);
            string timeStr = string.Format("{0:00}:{1:00}", minutes, seconds);
            
            winResultText.text = $"過關！\nMOVES: {moveCount}\nTIME: {timeStr}";
        }

        if (winUI != null) {
            winUI.SetActive(true);
            StartCoroutine(PopUpEffect(winUI.transform));
        }
    }

    private void CompleteAssessment()
    {
        if (assessmentCompleted || string.IsNullOrEmpty(assessmentSessionId))
        {
            return;
        }

        assessmentCompleted = true;
        CognitiveAssessmentService.RecordTrial(assessmentSessionId, new CognitiveTrialRecord
        {
            trialIndex = ++trialIndex,
            roundIndex = 1,
            stepIndex = moveCount,
            eventKind = "round_summary",
            randomSeed = randomSeed,
            difficulty = Mathf.Max(1, width * height),
            stimulusCount = width * height,
            condition = "pipe_completion",
            stimulus = "width=" + width + "|height=" + height,
            expectedAnswer = "connected",
            userAnswer = "connected",
            outcome = TrialOutcome.Correct,
            reactionTimeMs = Mathf.RoundToInt(elapsedTime * 1000f),
            roundElapsedMs = Mathf.RoundToInt(elapsedTime * 1000f),
            actionCount = moveCount
        });
        CognitiveAssessmentService.CompleteGame(
            assessmentSessionId,
            CognitiveDomain.VisuospatialAbility,
            0f,
            moveCount);
    }

    IEnumerator PopUpEffect(Transform target)
    {
        float duration = 0.3f;
        float elapsed = 0f;
        target.localScale = Vector3.zero;
        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            target.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, elapsed / duration);
            yield return null;
        }
        target.localScale = Vector3.one;
    }

    private void SetupTutorial()
    {
        tutorialBackground = FindTutorialObject(tutorialBackgroundObjectName, "\u9ED1_0", "black_0");
        tutorialPreviousButton = FindTutorialObject(tutorialPreviousButtonObjectName, "arrowLeft_0");
        tutorialNextButton = FindTutorialObject(tutorialNextButtonObjectName, "arrowRight_0");
        tutorialCloseButton = FindTutorialObject(tutorialCloseButtonObjectName, "cross_0");
        tutorialFirstPage = FindTutorialObject(tutorialFirstPageObjectName, "10_0");
        tutorialSecondPage = FindTutorialObject(tutorialSecondPageObjectName, "11_0");

        AddTutorialButtonListener(tutorialPreviousButton, ShowPreviousTutorialPage);
        AddTutorialButtonListener(tutorialNextButton, ShowNextTutorialPage);
        AddTutorialButtonListener(tutorialCloseButton, CloseTutorial);
    }

    private GameObject FindTutorialObject(params string[] names)
    {
        Transform[] transforms = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);

        for (int i = 0; i < names.Length; i++)
        {
            string targetName = names[i];
            if (string.IsNullOrWhiteSpace(targetName))
            {
                continue;
            }

            for (int j = 0; j < transforms.Length; j++)
            {
                Transform candidate = transforms[j];
                if (candidate != null && candidate.name == targetName)
                {
                    return candidate.gameObject;
                }
            }
        }

        for (int i = 0; i < names.Length; i++)
        {
            string targetName = names[i];
            if (string.IsNullOrWhiteSpace(targetName))
            {
                continue;
            }

            for (int j = 0; j < transforms.Length; j++)
            {
                Transform candidate = transforms[j];
                if (candidate == null)
                {
                    continue;
                }

                SpriteRenderer spriteRenderer = candidate.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null && spriteRenderer.sprite != null && spriteRenderer.sprite.name == targetName)
                {
                    return candidate.gameObject;
                }

                Image image = candidate.GetComponent<Image>();
                if (image != null && image.sprite != null && image.sprite.name == targetName)
                {
                    return candidate.gameObject;
                }
            }
        }

        return null;
    }

    private void AddTutorialButtonListener(GameObject target, UnityEngine.Events.UnityAction action)
    {
        if (target == null || action == null)
        {
            return;
        }

        Button button = target.GetComponent<Button>();
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private void ShowTutorial()
    {
        if (tutorialBackground == null && tutorialFirstPage == null && tutorialSecondPage == null)
        {
            tutorialActive = false;
            StartAssessmentIfNeeded();
            return;
        }

        tutorialActive = true;
        SetTutorialPage(0);
    }

    public void ShowPreviousTutorialPage()
    {
        if (!tutorialActive)
        {
            return;
        }

        SetTutorialPage(tutorialPageIndex - 1);
    }

    public void ShowNextTutorialPage()
    {
        if (!tutorialActive)
        {
            return;
        }

        SetTutorialPage(tutorialPageIndex + 1);
    }

    public void CloseTutorial()
    {
        tutorialActive = false;
        SetTutorialElementActive(tutorialFirstPage, false);
        SetTutorialElementActive(tutorialSecondPage, false);
        SetTutorialElementActive(tutorialPreviousButton, false);
        SetTutorialElementActive(tutorialNextButton, false);
        SetTutorialElementActive(tutorialCloseButton, false);
        SetTutorialElementActive(tutorialBackground, false);

        if (endpointHintPendingAfterTutorial)
        {
            ShowEndpointHintWhenReady();
        }

        if (!isGameOver)
        {
            StartAssessmentIfNeeded();
        }
    }

    private void SetTutorialPage(int pageIndex)
    {
        tutorialPageIndex = Mathf.Clamp(pageIndex, 0, 1);

        SetTutorialElementActive(tutorialBackground, true);
        SetTutorialElementActive(tutorialFirstPage, tutorialPageIndex == 0);
        SetTutorialElementActive(tutorialSecondPage, tutorialPageIndex == 1);
        SetTutorialElementActive(tutorialPreviousButton, tutorialPageIndex > 0);
        SetTutorialElementActive(tutorialNextButton, tutorialPageIndex < 1);
        SetTutorialElementActive(tutorialCloseButton, true);
    }

    private void SetTutorialElementActive(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }

    private void HandleTutorialInput()
    {
        if (!tutorialActive || !TryGetPointerDownPosition(out Vector2 screenPosition))
        {
            return;
        }

        if (IsTutorialElementHit(tutorialCloseButton, screenPosition))
        {
            CloseTutorial();
            return;
        }

        if (IsTutorialElementHit(tutorialPreviousButton, screenPosition))
        {
            ShowPreviousTutorialPage();
            return;
        }

        if (IsTutorialElementHit(tutorialNextButton, screenPosition))
        {
            ShowNextTutorialPage();
        }
    }

    private bool TryGetPointerDownPosition(out Vector2 screenPosition)
    {
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            screenPosition = mouse.position.ReadValue();
            return true;
        }

        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
        {
            screenPosition = touchscreen.primaryTouch.position.ReadValue();
            return true;
        }

        screenPosition = Vector2.zero;
        return false;
    }

    private bool IsTutorialElementHit(GameObject target, Vector2 screenPosition)
    {
        if (target == null || !target.activeInHierarchy || target.GetComponent<Button>() != null)
        {
            return false;
        }

        RectTransform rectTransform = target.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            Canvas canvas = target.GetComponentInParent<Canvas>();
            Camera uiCamera = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                uiCamera = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            }

            return RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, uiCamera);
        }

        SpriteRenderer spriteRenderer = target.GetComponent<SpriteRenderer>();
        Camera mainCamera = Camera.main;
        if (spriteRenderer == null || mainCamera == null)
        {
            return false;
        }

        Vector3 worldPosition = mainCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -mainCamera.transform.position.z));
        worldPosition.z = spriteRenderer.bounds.center.z;
        return spriteRenderer.bounds.Contains(worldPosition);
    }
}
