using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using LTCCognitiveAssessment;

public class ColorMatchStroopGameManager : MonoBehaviour, ICognitiveGamePauseTarget
{
    [System.Serializable]
    public class ColorWord
    {
        public string colorName;
        public string displayWord;
        public Color colorValue;
    }

    [Header("題目 UI")]
    public TMP_Text topWordText;
    public TMP_Text bottomWordText;

    [Header("答案按鈕")]
    public Button correctButton;
    public Button wrongButton;

    [Header("狀態 UI")]
    public TMP_Text scoreText;
    public TMP_Text timerText;
    public GameObject resultPanel;
    public TMP_Text resultTitleText;
    public TMP_Text resultSummaryText;
    public TMP_Text resultText;
    public TMP_Text resultNoteText;

    [Header("遊戲設定")]
    public float gameTime = 60f;
    public int scorePerCorrect = 10;
    public int wrongPenalty = 5;

    [Header("金幣獎勵")]
    public int coinPerCorrect = 1;
    public int coinPerScoreUnit = 20;

    [Header("顏色資料")]
    public List<ColorWord> colorWords = new List<ColorWord>();

    private ColorWord topMeaning;
    private ColorWord topInkColor;

    private ColorWord bottomMeaning;
    private ColorWord bottomInkColor;

    private bool currentAnswerIsCorrect;
    private bool currentHighConflict;

    private float timeLeft;
    private float questionStartTime;

    private int score = 0;
    private int correctCount = 0;
    private int wrongCount = 0;
    private int questionCount = 0;
    private int earnedCoins = 0;

    private readonly List<float> matchReactionTimes = new List<float>();
    private readonly List<float> mismatchReactionTimes = new List<float>();

    private bool isGameRunning = false;
    private string assessmentSessionId;
    private int randomSeed;
    private readonly int[] conditionBlockOrder = { 0, 1, 2, 3 };
    private float pauseCheckpointTimeLeft;
    private int pauseCheckpointTrialCount;
    private int pauseCheckpointScore;
    private int pauseCheckpointCorrectCount;
    private int pauseCheckpointWrongCount;

    public bool IsAssessmentRunning => isGameRunning;

    void Start()
    {
        SetupDefaultColorsIfEmpty();
        StartGame();
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
        assessmentSessionId = CognitiveAssessmentService.BeginGame("stroop_color_match", CognitiveProtocolRegistry.ProtocolVersion);
        score = 0;
        correctCount = 0;
        wrongCount = 0;
        questionCount = 0;
        earnedCoins = 0;
        timeLeft = gameTime;
        isGameRunning = true;
        BindResultReturnButton();

        matchReactionTimes.Clear();
        mismatchReactionTimes.Clear();

        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }

        BindButtons();
        if (correctButton != null) correctButton.gameObject.SetActive(true);
        if (wrongButton != null) wrongButton.gameObject.SetActive(true);
        SavePauseCheckpoint();
        NextQuestion();
        UpdateUI();
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

    void SetupDefaultColorsIfEmpty()
    {
        if (colorWords != null && colorWords.Count > 0) return;

        colorWords = new List<ColorWord>
        {
            new ColorWord { colorName = "紅", displayWord = "紅", colorValue = Color.red },
            new ColorWord { colorName = "黃", displayWord = "黃", colorValue = Color.yellow },
            new ColorWord { colorName = "綠", displayWord = "綠", colorValue = Color.green },
            new ColorWord { colorName = "藍", displayWord = "藍", colorValue = Color.blue },
            new ColorWord { colorName = "黑", displayWord = "黑", colorValue = Color.black }
        };
    }

    void BindButtons()
    {
        if (correctButton != null)
        {
            correctButton.onClick.RemoveAllListeners();
            correctButton.onClick.AddListener(() =>
            {
                OnAnswerSelected(true);
            });
        }

        if (wrongButton != null)
        {
            wrongButton.onClick.RemoveAllListeners();
            wrongButton.onClick.AddListener(() =>
            {
                OnAnswerSelected(false);
            });
        }
    }

    void NextQuestion()
    {
        if (!isGameRunning) return;

        questionCount++;

        // 每四題維持 2x2 條件平衡，但區塊內順序隨機，降低固定順序造成的練習與疲勞偏差。
        int positionInBlock = (questionCount - 1) % conditionBlockOrder.Length;
        if (positionInBlock == 0) ShuffleConditionBlock();
        int cell = conditionBlockOrder[positionInBlock];
        bool shouldMatch = cell < 2;
        currentHighConflict = (cell % 2) == 1;

        GenerateQuestionForCondition(shouldMatch, currentHighConflict);
    }

    void GenerateQuestionForCondition(bool shouldMatch, bool highConflict)
    {
        currentHighConflict = highConflict;

        topMeaning = GetRandomColorWord();
        topInkColor = currentHighConflict ? GetDifferentColorWord(topMeaning) : topMeaning;

        if (shouldMatch)
        {
            bottomInkColor = topMeaning;
        }

        else
        {
            do
            {
                bottomInkColor = GetRandomColorWord();
            }
            while (bottomInkColor.colorName == topMeaning.colorName);
        }

        bottomMeaning = currentHighConflict ? GetDifferentColorWord(bottomInkColor) : bottomInkColor;

        currentAnswerIsCorrect = topMeaning.colorName == bottomInkColor.colorName;

        UpdateQuestionUI();
        questionStartTime = Time.time;
    }

    void ShuffleConditionBlock()
    {
        for (int index = conditionBlockOrder.Length - 1; index > 0; index--)
        {
            int swapIndex = Random.Range(0, index + 1);
            int value = conditionBlockOrder[index];
            conditionBlockOrder[index] = conditionBlockOrder[swapIndex];
            conditionBlockOrder[swapIndex] = value;
        }
    }

    ColorWord GetRandomColorWord()
    {
        return colorWords[Random.Range(0, colorWords.Count)];
    }

    ColorWord GetDifferentColorWord(ColorWord excluded)
    {
        ColorWord value;
        do { value = GetRandomColorWord(); } while (value.colorName == excluded.colorName);
        return value;
    }

    void UpdateQuestionUI()
    {
        if (topWordText != null)
        {
            topWordText.text = topMeaning.displayWord;
            topWordText.color = topInkColor.colorValue;
        }

        if (bottomWordText != null)
        {
            bottomWordText.text = bottomMeaning.displayWord;
            bottomWordText.color = bottomInkColor.colorValue;
        }
    }

    void OnAnswerSelected(bool playerChoseCorrect)
    {
        if (!isGameRunning) return;

        float reactionTime = Time.time - questionStartTime;
        bool isAnswerCorrect = playerChoseCorrect == currentAnswerIsCorrect;

        if (currentAnswerIsCorrect)
        {
            matchReactionTimes.Add(reactionTime);
        }
        else
        {
            mismatchReactionTimes.Add(reactionTime);
        }

        CognitiveAssessmentService.RecordTrial(assessmentSessionId, new CognitiveTrialRecord
        {
            trialIndex = questionCount,
            roundIndex = questionCount,
            stepIndex = 1,
            eventKind = "response",
            randomSeed = randomSeed,
            difficulty = 1,
            condition = (currentAnswerIsCorrect ? "match_" : "mismatch_") +
                        (currentHighConflict ? "high_conflict" : "low_conflict"),
            stimulus = topMeaning.colorName + "|" + topInkColor.colorName + "|" +
                       bottomMeaning.colorName + "|" + bottomInkColor.colorName,
            expectedAnswer = currentAnswerIsCorrect ? "match" : "mismatch",
            userAnswer = playerChoseCorrect ? "match" : "mismatch",
            outcome = isAnswerCorrect ? TrialOutcome.Correct : TrialOutcome.Incorrect,
            reactionTimeMs = Mathf.RoundToInt(reactionTime * 1000f),
            errorType = isAnswerCorrect ? "" : "classification_error"
        });

        if (isAnswerCorrect)
        {
            score += scorePerCorrect;
            correctCount++;
        }
        else
        {
            score -= wrongPenalty;
            wrongCount++;

            if (score < 0)
            {
                score = 0;
            }
        }

        SavePauseCheckpoint();
        NextQuestion();
        UpdateUI();
    }

    void SavePauseCheckpoint()
    {
        pauseCheckpointTimeLeft = timeLeft;
        pauseCheckpointTrialCount = CognitiveAssessmentService.GetTrialCheckpoint(assessmentSessionId);
        pauseCheckpointScore = score;
        pauseCheckpointCorrectCount = correctCount;
        pauseCheckpointWrongCount = wrongCount;
    }

    public void RestartCurrentItemAfterPause()
    {
        if (!isGameRunning) return;
        CognitiveAssessmentService.RollbackToTrialCheckpoint(assessmentSessionId, pauseCheckpointTrialCount);
        timeLeft = pauseCheckpointTimeLeft;
        score = pauseCheckpointScore;
        correctCount = pauseCheckpointCorrectCount;
        wrongCount = pauseCheckpointWrongCount;

        // 不沿用暫停時看到的題目；保留相同／不同及干擾高低，重新抽一題。
        bool sameDifficultyMatchCondition = currentAnswerIsCorrect;
        bool sameDifficultyConflict = currentHighConflict;
        GenerateQuestionForCondition(sameDifficultyMatchCondition, sameDifficultyConflict);
        UpdateUI();
    }

    public void CancelCurrentAssessment()
    {
        isGameRunning = false;
        CognitiveAssessmentService.CancelGame(assessmentSessionId);
        assessmentSessionId = null;
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
    }

    void EndGame()
    {
        isGameRunning = false;
        if (correctButton != null) correctButton.gameObject.SetActive(false);
        if (wrongButton != null) wrongButton.gameObject.SetActive(false);

        if (questionStartTime > 0f)
        {
            CognitiveAssessmentService.RecordTrial(assessmentSessionId, new CognitiveTrialRecord {
                trialIndex=questionCount, roundIndex=questionCount, stepIndex=1, eventKind="response",
                randomSeed=randomSeed, difficulty=1,
                condition=(currentAnswerIsCorrect?"match_":"mismatch_")+(currentHighConflict?"high_conflict":"low_conflict"),
                stimulus=topMeaning.colorName+"|"+topInkColor.colorName+"|"+bottomMeaning.colorName+"|"+bottomInkColor.colorName,
                expectedAnswer=currentAnswerIsCorrect?"match":"mismatch", userAnswer="", outcome=TrialOutcome.Omitted,
                reactionTimeMs=Mathf.RoundToInt((Time.time-questionStartTime)*1000f), timedOut=true, errorType="timeout"
            });
        }

        earnedCoins = correctCount * coinPerCorrect + score / coinPerScoreUnit;
        CoinData.AddCoins(earnedCoins);

        float matchAvg = Average(matchReactionTimes);
        float mismatchAvg = Average(mismatchReactionTimes);
        float interference = mismatchAvg - matchAvg;
        CognitiveGameResult cognitiveResult = CognitiveAssessmentService.CompleteGame(
            assessmentSessionId,
            CognitiveDomain.AttentionInhibitoryControl,
            interference * 1000f,
            1f);

        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
        }

        if (resultTitleText != null) resultTitleText.text = "本次測驗完成";
        if (resultSummaryText != null) resultSummaryText.text = "分數  " + score + "     金幣  +" + earnedCoins;
        if (resultText != null) resultText.text =
            "答題表現\n正確  " + correctCount + " 次     錯誤  " + wrongCount + " 次\n\n" +
            "反應速度\n相同題  " + matchAvg.ToString("F2") + " 秒\n不同題  " + mismatchAvg.ToString("F2") + " 秒\n" +
            "干擾差值  " + interference.ToString("F2") + " 秒";
        if (resultNoteText != null) resultNoteText.text =
            "注意力與抑制控制｜本次任務指數 " + cognitiveResult.performanceScore.ToString("F0") + "/100\n" + cognitiveResult.dataQualityNote;
    }

    float Average(List<float> values)
    {
        if (values == null || values.Count == 0) return 0f;

        float sum = 0f;

        for (int i = 0; i < values.Count; i++)
        {
            sum += values[i];
        }

        return sum / values.Count;
    }
}
