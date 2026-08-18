using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using LTCCognitiveAssessment;

public class NumberSumGameManager : MonoBehaviour, ICognitiveGamePauseTarget
{
    [Header("預先排好的數字按鈕")]
    public List<Button> numberButtons = new List<Button>();

    [Header("按鈕圖片")]
    public List<Sprite> normalButtonSprites = new List<Sprite>();
    public Sprite selectedButtonSprite;

    [Header("UI")]
    public TMP_Text scoreText;
    public TMP_Text timerText;
    public TMP_Text targetText;
    public TMP_Text difficultyText;
    public GameObject resultPanel;
    public TMP_Text resultTitleText;
    public TMP_Text resultSummaryText;
    public TMP_Text resultText;
    public TMP_Text resultNoteText;

    [Header("Game Settings")]
    public float gameTime = 60f;
    public int scorePerRound = 20;
    public int wrongPenalty = 5;

    [Header("金幣獎勵")]
    public int coinPerCompletedRound = 3;
    public int coinPerScoreUnit = 10;

    [Header("Number Settings")]
    public int minButtonCount = 3;
    public int maxButtonCount = 5;
    public int minNumber = 1;
    public int maxNumber = 9;

    [Header("漸進難度（長者友善）")]
    [Tooltip("前幾關固定為 3 個按鈕、2 個加數，先讓玩家熟悉操作。")]
    public int familiarizationRounds = 2;
    [Tooltip("從此關開始提高到 5 個按鈕及 3～4 個加數。")]
    public int advancedStartRound = 6;

    private float timeLeft;
    private int score = 0;
    private int round = 1;
    private int completedRoundCount = 0;
    private int wrongClickCount = 0;
    private int targetNumber = 0;
    private int currentSum = 0;
    private int earnedCoins = 0;

    private readonly List<Button> activeButtons = new List<Button>();
    private readonly Dictionary<Button, int> buttonNumbers = new Dictionary<Button, int>();
    private readonly Dictionary<Button, Sprite> originalSprites = new Dictionary<Button, Sprite>();
    private readonly Dictionary<Button, Color> originalColors = new Dictionary<Button, Color>();
    private readonly HashSet<Button> selectedButtons = new HashSet<Button>();
    private readonly List<Button> selectionOrder = new List<Button>();

    private bool isGameRunning = false;
    private string assessmentSessionId;
    private int randomSeed;
    private float trialStartTime;
    private float roundStartTime;
    private int roundActionCount;
    private int roundResetCount;
    private int minimumActionCount;
    private long initialPlanningTimeMs;
    private int trialIndex;
    private float pauseCheckpointTimeLeft;
    private int pauseCheckpointTrialCount;
    private int pauseCheckpointTrialIndex;
    private int pauseCheckpointScore;
    private int pauseCheckpointCompletedRounds;
    private int pauseCheckpointWrongClicks;
    private int pauseCheckpointRound;

    public bool IsAssessmentRunning => isGameRunning;

    void Start()
    {
        HideAllButtons();
        StartGame();
    }

    void Update()
    {
        if (!isGameRunning) return;

        timeLeft -= Time.deltaTime;

        if (timeLeft <= 0f)
        {
            timeLeft = 0f;
            EndGame();
        }

        UpdateUI();
    }

    public void StartGame()
    {
        if (!Mathf.Approximately(gameTime, 60f))
        {
            Debug.LogWarning("評估協定 3.0 固定正式測驗為 60 秒，已覆寫 Inspector 設定。");
            gameTime = 60f;
        }
        randomSeed = Random.Range(int.MinValue, int.MaxValue);
        Random.InitState(randomSeed);
        assessmentSessionId = CognitiveAssessmentService.BeginGame("number_sum", CognitiveProtocolRegistry.ProtocolVersion);
        trialIndex = 0;
        score = 0;
        round = 1;
        completedRoundCount = 0;
        wrongClickCount = 0;
        earnedCoins = 0;
        timeLeft = gameTime;
        isGameRunning = true;
        BindResultReturnButton();

        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }

        SavePauseCheckpoint();
        SpawnRound();
        UpdateUI();
        trialStartTime = Time.time;
    }

private void BindResultReturnButton()
    {
        if (resultPanel == null) return;
        Button returnButton = resultPanel.GetComponentInChildren<Button>(true);
        if (returnButton == null)
        {
            Debug.LogWarning("結算頁找不到返回主頁按鈕。");
            return;
        }
        returnButton.onClick.RemoveAllListeners();
        returnButton.onClick.AddListener(ReturnToMainMenu);
        TMP_Text label = returnButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = "返回主頁";
    }

    public void ReturnToMainMenu()
    {
        SceneManager.LoadScene("GameScene");
    }


    void SpawnRound()
    {
        HideAllButtons();

        activeButtons.Clear();
        buttonNumbers.Clear();
        originalSprites.Clear();
        originalColors.Clear();
        selectedButtons.Clear();
        selectionOrder.Clear();
        currentSum = 0;
        roundActionCount = 0;
        roundResetCount = 0;
        initialPlanningTimeMs = 0;
        roundStartTime = Time.time;

        GetRoundDifficulty(out int buttonCount, out int requiredAddends);

        List<int> activeNumbers = GenerateValidNumberSet(buttonCount, requiredAddends);
        minimumActionCount = FindMinimumSubsetSize(activeNumbers, targetNumber);

        for (int i = 0; i < buttonCount; i++)
        {
            SetupButton(numberButtons[i], activeNumbers[i]);
        }

        UpdateUI();
    }

    void GetRoundDifficulty(out int buttonCount, out int requiredAddends)
    {
        if (round <= Mathf.Max(1, familiarizationRounds))
        {
            buttonCount = 3;
            requiredAddends = 2;
        }
        else if (round < Mathf.Max(familiarizationRounds + 1, advancedStartRound))
        {
            buttonCount = 4;
            requiredAddends = round >= familiarizationRounds + 2 ? 3 : 2;
        }
        else
        {
            buttonCount = 5;
            requiredAddends = Mathf.Min(4, 3 + (round - advancedStartRound) / 3);
        }

        buttonCount = Mathf.Clamp(buttonCount, Mathf.Max(3, minButtonCount),
            Mathf.Min(maxButtonCount, numberButtons.Count));
        requiredAddends = Mathf.Clamp(requiredAddends, 2, Mathf.Max(2, buttonCount - 1));
    }

    List<int> GenerateValidNumberSet(int buttonCount, int requiredAddends)
    {
        List<int> numbers = new List<int>();

        int safety = 0;

        while (safety < 200)
        {
            safety++;
            numbers.Clear();

            for (int i = 0; i < buttonCount; i++)
            {
                numbers.Add(Random.Range(minNumber, maxNumber + 1));
            }

            List<int> shuffled = new List<int>(numbers);
            Shuffle(shuffled);

            targetNumber = 0;

            for (int i = 0; i < requiredAddends; i++)
            {
                targetNumber += shuffled[i];
            }

            bool targetEqualsSingleButton = numbers.Contains(targetNumber);
            bool matchesPlannedDifficulty = FindMinimumSubsetSize(numbers, targetNumber) == requiredAddends;

            if (!targetEqualsSingleButton && matchesPlannedDifficulty)
            {
                return new List<int>(numbers);
            }
        }

        numbers.Clear();
        targetNumber = requiredAddends * 2;
        for (int i = 0; i < requiredAddends; i++) numbers.Add(2);

        while (numbers.Count < buttonCount)
        {
            numbers.Add(targetNumber + 1);
        }

        return numbers;
    }

    void Shuffle(List<int> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int randomIndex = Random.Range(i, list.Count);
            int temp = list[i];
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }

    void SetupButton(Button button, int number)
    {
        if (button == null) return;

        button.gameObject.SetActive(true);
        button.interactable = true;

        activeButtons.Add(button);
        buttonNumbers[button] = number;

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            originalColors[button] = image.color;
            if (normalButtonSprites != null && normalButtonSprites.Count > 0)
            {
                Sprite randomSprite = normalButtonSprites[Random.Range(0, normalButtonSprites.Count)];
                image.sprite = randomSprite;
                originalSprites[button] = randomSprite;
            }
            else
            {
                originalSprites[button] = image.sprite;
            }
        }

        TMP_Text text = button.GetComponentInChildren<TMP_Text>();
        if (text != null)
        {
            text.text = number.ToString();
            text.color = Color.white;
            text.fontStyle = FontStyles.Bold;
            text.outlineColor = new Color32(32, 51, 65, 255);
            text.outlineWidth = 0.22f;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            ToggleButtonSelection(button);
        });
    }

    void ToggleButtonSelection(Button button)
    {
        if (!isGameRunning) return;
        if (!buttonNumbers.ContainsKey(button)) return;

        int number = buttonNumbers[button];
        roundActionCount++;
        if (roundActionCount == 1)
            initialPlanningTimeMs = Mathf.RoundToInt((Time.time - roundStartTime) * 1000f);
        int sumBeforeInput = currentSum;

        bool isDeselecting = selectedButtons.Contains(button);
        if (isDeselecting)
        {
            selectedButtons.Remove(button);
            selectionOrder.Remove(button);
            currentSum -= number;
            SetButtonSelectedVisual(button, false);
        }
        else
        {
            selectedButtons.Add(button);
            selectionOrder.Add(button);
            currentSum += number;
            SetButtonSelectedVisual(button, true);
        }

        if (currentSum == targetNumber)
        {
            RecordSelectionTrial(number, sumBeforeInput, true, "");
            CompleteRound();
        }
        else if (currentSum > targetNumber)
        {
            RecordSelectionTrial(number, sumBeforeInput, false, "sum_exceeded_target");
            wrongClickCount++;
            score -= wrongPenalty;

            if (score < 0)
            {
                score = 0;
            }

            // 保留目前選取，讓玩家再次點擊同一數字自行取消，而不是整題被強制清空。
        }
        else
        {
            RecordSelectionTrial(number, sumBeforeInput, true,
                isDeselecting ? "selection_removed" : "partial_progress");
        }

        UpdateUI();
    }

    void SetButtonSelectedVisual(Button button, bool selected)
    {
        Image image = button.GetComponent<Image>();
        if (image == null) return;

        if (selected && selectedButtonSprite != null)
        {
            image.sprite = selectedButtonSprite;
        }
        else if (originalSprites.ContainsKey(button))
        {
            image.sprite = originalSprites[button];
        }

        image.color = selected
            ? new Color32(245, 158, 66, 255)
            : (originalColors.TryGetValue(button, out Color original) ? original : Color.white);

        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.color = Color.white;
            label.outlineColor = selected ? new Color32(115, 55, 18, 255) : new Color32(32, 51, 65, 255);
            label.outlineWidth = 0.22f;
        }
    }

    void ResetSelection()
    {
        foreach (Button button in selectedButtons)
        {
            if (button != null)
            {
                SetButtonSelectedVisual(button, false);
            }
        }

        selectedButtons.Clear();
        selectionOrder.Clear();
        currentSum = 0;
    }

    void CompleteRound()
    {
        RecordRoundSummary(TrialOutcome.Correct, "");
        score += scorePerRound;
        completedRoundCount++;
        round++;

        SavePauseCheckpoint();
        SpawnRound();
    }

    void SavePauseCheckpoint()
    {
        pauseCheckpointTimeLeft = timeLeft;
        pauseCheckpointTrialCount = CognitiveAssessmentService.GetTrialCheckpoint(assessmentSessionId);
        pauseCheckpointTrialIndex = trialIndex;
        pauseCheckpointScore = score;
        pauseCheckpointCompletedRounds = completedRoundCount;
        pauseCheckpointWrongClicks = wrongClickCount;
        pauseCheckpointRound = round;
    }

    public void RestartCurrentItemAfterPause()
    {
        if (!isGameRunning) return;
        CognitiveAssessmentService.RollbackToTrialCheckpoint(assessmentSessionId, pauseCheckpointTrialCount);
        timeLeft = pauseCheckpointTimeLeft;
        trialIndex = pauseCheckpointTrialIndex;
        score = pauseCheckpointScore;
        completedRoundCount = pauseCheckpointCompletedRounds;
        wrongClickCount = pauseCheckpointWrongClicks;
        round = pauseCheckpointRound;

        // 關卡不變，按鈕數量與所需加數相同；只重新抽數字與目標值。
        SpawnRound();
        trialStartTime = Time.time;
        UpdateUI();
    }

    public void CancelCurrentAssessment()
    {
        isGameRunning = false;
        CognitiveAssessmentService.CancelGame(assessmentSessionId);
        assessmentSessionId = null;
    }

    void RecordSelectionTrial(int selectedNumber, int sumBeforeInput, bool validAction, string errorType)
    {
        trialIndex++;
        CognitiveAssessmentService.RecordTrial(assessmentSessionId, new CognitiveTrialRecord
        {
            trialIndex = trialIndex,
            roundIndex = round,
            stepIndex = roundActionCount,
            eventKind = "selection",
            stimulusCount = activeButtons.Count,
            randomSeed = randomSeed,
            difficulty = activeButtons.Count,
            condition = "target_sum",
            stimulus = string.Join(",", buttonNumbers.Values) + "|target=" + targetNumber +
                       "|sumBefore=" + sumBeforeInput,
            expectedAnswer = targetNumber.ToString(),
            userAnswer = selectedNumber.ToString(),
            outcome = validAction ? TrialOutcome.ValidAction : TrialOutcome.Incorrect,
            reactionTimeMs = Mathf.RoundToInt((Time.time - trialStartTime) * 1000f),
            errorType = errorType
        });
        trialStartTime = Time.time;
    }

    void RecordRoundSummary(TrialOutcome outcome, string error)
    {
        trialIndex++;
        CognitiveAssessmentService.RecordTrial(assessmentSessionId, new CognitiveTrialRecord {
            trialIndex=trialIndex, roundIndex=round, stepIndex=roundActionCount, eventKind="round_summary",
            randomSeed=randomSeed, difficulty=activeButtons.Count, stimulusCount=activeButtons.Count, condition="target_sum",
            stimulus=string.Join(",",buttonNumbers.Values)+"|target="+targetNumber+"|actions="+roundActionCount+"|resets="+roundResetCount,
            expectedAnswer=targetNumber.ToString(), userAnswer=currentSum.ToString(), outcome=outcome,
            reactionTimeMs=Mathf.RoundToInt((Time.time-roundStartTime)*1000f), roundElapsedMs=Mathf.RoundToInt((Time.time-roundStartTime)*1000f),
            initialPlanningTimeMs=initialPlanningTimeMs, minimumActionCount=minimumActionCount,
            actionCount=roundActionCount, errorCount=roundResetCount,
            timedOut=outcome==TrialOutcome.Omitted, errorType=error
        });
    }

    static int FindMinimumSubsetSize(List<int> numbers, int target)
    {
        int best = int.MaxValue;
        int combinations = 1 << numbers.Count;
        for (int mask = 1; mask < combinations; mask++)
        {
            int sum = 0;
            int count = 0;
            for (int index = 0; index < numbers.Count; index++)
            {
                if ((mask & (1 << index)) == 0) continue;
                sum += numbers[index];
                count++;
            }
            if (sum == target && count < best) best = count;
        }
        return best == int.MaxValue ? 0 : best;
    }

    void HideAllButtons()
    {
        for (int i = 0; i < numberButtons.Count; i++)
        {
            if (numberButtons[i] != null)
            {
                numberButtons[i].gameObject.SetActive(false);
            }
        }
    }

    void UpdateUI()
    {
        if (scoreText != null)
        {
            scoreText.text = "分數：" + score;
        }

        if (timerText != null)
        {
            timerText.text = "時間：" + Mathf.CeilToInt(timeLeft);
        }

        if (targetText != null)
        {
            targetText.text = BuildEquationText();
            targetText.color = currentSum > targetNumber
                ? new Color32(190, 58, 52, 255)
                : new Color32(42, 82, 88, 255);
        }

        if (difficultyText != null)
        {
            string level = round <= familiarizationRounds ? "熟悉" :
                (round < advancedStartRound ? "進階" : "挑戰");
            difficultyText.text = "第 " + round + " 關｜" + level + "難度｜再點一次可取消";
        }
    }

    string BuildEquationText()
    {
        int slotCount = Mathf.Max(Mathf.Max(2, minimumActionCount), selectionOrder.Count);
        var slots = new List<string>(slotCount);
        for (int i = 0; i < slotCount; i++)
        {
            if (i < selectionOrder.Count && buttonNumbers.TryGetValue(selectionOrder[i], out int value))
                slots.Add(value.ToString());
            else
                slots.Add("＿＿");
        }
        return string.Join(" ＋ ", slots) + " ＝ " + targetNumber;
    }

    void EndGame()
    {
        RecordRoundSummary(TrialOutcome.Omitted, "timeout");
        isGameRunning = false;
        HideAllButtons();

        earnedCoins = completedRoundCount * coinPerCompletedRound + score / coinPerScoreUnit;
        CoinData.AddCoins(earnedCoins);
        CognitiveGameResult cognitiveResult = CognitiveAssessmentService.CompleteGame(
            assessmentSessionId,
            CognitiveDomain.ExecutiveFunctionNumericalReasoning,
            0f,
            maxButtonCount);

        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
        }

        if (resultTitleText != null) resultTitleText.text = "本次測驗完成";
        if (resultSummaryText != null) resultSummaryText.text = "分數  " + score + "     金幣  +" + earnedCoins;
        if (resultText != null) resultText.text =
            "數字規劃表現\n完成關卡  " + completedRoundCount + " 關\n超過目標  " + wrongClickCount + " 次";
        if (resultNoteText != null) resultNoteText.text =
            "執行功能與數字推理｜本次表現指數 " + cognitiveResult.performanceScore.ToString("F0") + "/100\n" + cognitiveResult.dataQualityNote;
    }
}
