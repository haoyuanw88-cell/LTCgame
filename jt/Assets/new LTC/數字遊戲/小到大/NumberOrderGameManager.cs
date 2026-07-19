using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LTCCognitiveAssessment;

public class NumberOrderPoolGameManager : MonoBehaviour
{
    [Header("預先排好的數字按鈕")]
    public List<Button> numberButtons = new List<Button>();

    [Header("按鈕隨機圖片")]
    public List<Sprite> buttonSprites = new List<Sprite>();

    [Header("UI")]
    public TMP_Text scoreText;
    public TMP_Text timerText;
    public GameObject resultPanel;
    public TMP_Text resultTitleText;
    public TMP_Text resultSummaryText;
    public TMP_Text resultText;
    public TMP_Text resultNoteText;

    [Header("錯誤提示")]
    public GameObject wrongImage;
    public float wrongImageShowTime = 0.5f;

    [Header("Game Settings")]
    public float gameTime = 60f;
    public int scorePerCorrect = 10;
    public int wrongPenalty = 5;

    [Header("金幣獎勵")]
    public int coinPerCorrectClick = 1;
    public int coinPerCompletedRound = 2;

    [Header("Number Settings")]
    public int startNumberCount = 3;
    public int maxNumberCount = 8;
    public int positiveMin = 1;
    public int positiveMax = 30;
    public int negativeMin = -20;
    public int negativeStartRound = 4;

    private float timeLeft;
    private int score = 0;
    private int round = 1;
    private int correctClickCount = 0;
    private int wrongClickCount = 0;
    private int currentTargetIndex = 0;
    private int earnedCoins = 0;

    private readonly List<Button> activeRoundButtons = new List<Button>();
    private readonly List<int> currentNumbers = new List<int>();
    private readonly List<int> sortedNumbers = new List<int>();

    private bool isGameRunning = false;
    private bool isShowingWrong = false;
    private string assessmentSessionId;
    private int randomSeed;
    private float trialStartTime;
    private float roundStartTime;
    private int trialIndex;

    void Start()
    {
        if (wrongImage != null)
        {
            wrongImage.SetActive(false);
        }

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
        randomSeed = Random.Range(int.MinValue, int.MaxValue);
        Random.InitState(randomSeed);
        assessmentSessionId = CognitiveAssessmentService.BeginGame("number_order", "2.0.0");
        trialIndex = 0;
        score = 0;
        round = 1;
        correctClickCount = 0;
        wrongClickCount = 0;
        currentTargetIndex = 0;
        earnedCoins = 0;
        timeLeft = gameTime;
        isGameRunning = true;
        isShowingWrong = false;

        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }

        if (wrongImage != null)
        {
            wrongImage.SetActive(false);
        }

        SpawnRound();
        UpdateUI();
    }

    void SpawnRound()
    {
        HideAllButtons();

        activeRoundButtons.Clear();
        currentNumbers.Clear();
        sortedNumbers.Clear();
        currentTargetIndex = 0;

        int numberCount = Mathf.Min(startNumberCount + (round - 1), maxNumberCount);
        numberCount = Mathf.Min(numberCount, numberButtons.Count);

        List<Button> availableButtons = new List<Button>(numberButtons);

        for (int i = 0; i < numberCount; i++)
        {
            int randomIndex = Random.Range(0, availableButtons.Count);
            Button selectedButton = availableButtons[randomIndex];
            availableButtons.RemoveAt(randomIndex);

            int number = GenerateUniqueNumber();

            currentNumbers.Add(number);
            activeRoundButtons.Add(selectedButton);

            SetupButton(selectedButton, number);
        }

        sortedNumbers.AddRange(currentNumbers);
        sortedNumbers.Sort();
        trialStartTime = Time.time;
        roundStartTime = Time.time;
    }

    int GenerateUniqueNumber()
    {
        int number;
        int safety = 0;

        do
        {
            number = GenerateNumber();
            safety++;
        }
        while (currentNumbers.Contains(number) && safety < 100);

        return number;
    }

    int GenerateNumber()
    {
        bool allowNegative = round >= negativeStartRound;

        if (allowNegative && Random.value < 0.4f)
        {
            return Random.Range(negativeMin, 0);
        }

        return Random.Range(positiveMin, positiveMax + 1);
    }

    void SetupButton(Button button, int number)
    {
        if (button == null) return;

        button.gameObject.SetActive(true);
        button.interactable = true;

        Image image = button.GetComponent<Image>();
        if (image != null && buttonSprites != null && buttonSprites.Count > 0)
        {
            image.sprite = buttonSprites[Random.Range(0, buttonSprites.Count)];
        }

        TMP_Text text = button.GetComponentInChildren<TMP_Text>();
        if (text != null)
        {
            text.text = number.ToString();
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            OnNumberClicked(number, button);
        });
    }

    void OnNumberClicked(int number, Button button)
    {
        if (!isGameRunning) return;
        if (isShowingWrong) return;
        if (currentTargetIndex >= sortedNumbers.Count) return;

        int targetNumber = sortedNumbers[currentTargetIndex];
        bool isCorrect = number == targetNumber;
        trialIndex++;
        CognitiveAssessmentService.RecordTrial(assessmentSessionId, new CognitiveTrialRecord
        {
            trialIndex = trialIndex,
            roundIndex = round,
            stepIndex = currentTargetIndex + 1,
            eventKind = "response",
            stimulusCount = activeRoundButtons.Count,
            randomSeed = randomSeed,
            difficulty = activeRoundButtons.Count,
            condition = round >= negativeStartRound ? "positive_and_negative" : "positive_only",
            stimulus = string.Join(",", currentNumbers),
            expectedAnswer = targetNumber.ToString(),
            userAnswer = number.ToString(),
            outcome = isCorrect ? TrialOutcome.Correct : TrialOutcome.Incorrect,
            reactionTimeMs = Mathf.RoundToInt((Time.time - trialStartTime) * 1000f),
            errorType = isCorrect ? "" : "sequence_error"
        });
        trialStartTime = Time.time;

        if (isCorrect)
        {
            score += scorePerCorrect;
            correctClickCount++;
            currentTargetIndex++;

            button.gameObject.SetActive(false);

            if (currentTargetIndex >= sortedNumbers.Count)
            {
                RecordRoundSummary(TrialOutcome.Correct, "");
                round++;
                SpawnRound();
            }
        }
        else
        {
            RecordRoundSummary(TrialOutcome.Incorrect, "sequence_error");
            score -= wrongPenalty;
            wrongClickCount++;

            if (score < 0)
            {
                score = 0;
            }

            StartCoroutine(ShowWrongThenNextRound());
        }

        UpdateUI();
    }

    void RecordRoundSummary(TrialOutcome outcome, string error)
    {
        trialIndex++;
        CognitiveAssessmentService.RecordTrial(assessmentSessionId, new CognitiveTrialRecord {
            trialIndex=trialIndex, roundIndex=round, stepIndex=currentTargetIndex, eventKind="round_summary",
            randomSeed=randomSeed, difficulty=activeRoundButtons.Count, stimulusCount=activeRoundButtons.Count,
            condition=round>=negativeStartRound?"positive_and_negative":"positive_only", stimulus=string.Join(",",currentNumbers),
            expectedAnswer=string.Join(",",sortedNumbers), userAnswer=currentTargetIndex.ToString(), outcome=outcome,
            reactionTimeMs=Mathf.RoundToInt((Time.time-roundStartTime)*1000f), roundElapsedMs=Mathf.RoundToInt((Time.time-roundStartTime)*1000f),
            timedOut=outcome==TrialOutcome.Omitted, errorType=error
        });
    }

    IEnumerator ShowWrongThenNextRound()
    {
        isShowingWrong = true;

        SetRoundButtonsInteractable(false);

        if (wrongImage != null)
        {
            wrongImage.SetActive(true);
        }

        yield return new WaitForSeconds(wrongImageShowTime);

        if (wrongImage != null)
        {
            wrongImage.SetActive(false);
        }

        round++;
        SpawnRound();

        isShowingWrong = false;
    }

    void SetRoundButtonsInteractable(bool interactable)
    {
        for (int i = 0; i < activeRoundButtons.Count; i++)
        {
            if (activeRoundButtons[i] != null)
            {
                activeRoundButtons[i].interactable = interactable;
            }
        }
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
    }

    void EndGame()
    {
        if (!isShowingWrong && currentTargetIndex < sortedNumbers.Count) RecordRoundSummary(TrialOutcome.Omitted, "timeout");
        isGameRunning = false;
        HideAllButtons();

        if (wrongImage != null)
        {
            wrongImage.SetActive(false);
        }

        int completedRounds = Mathf.Max(0, round - 1);
        CognitiveGameResult cognitiveResult = CognitiveAssessmentService.CompleteGame(
            assessmentSessionId,
            CognitiveDomain.ProcessingSpeedVisualSearch,
            0f,
            Mathf.Min(startNumberCount + completedRounds, maxNumberCount));
        earnedCoins = correctClickCount * coinPerCorrectClick + completedRounds * coinPerCompletedRound;

        CoinData.AddCoins(earnedCoins);

        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
        }

        if (resultTitleText != null) resultTitleText.text = "本次測驗完成";
        if (resultSummaryText != null) resultSummaryText.text = "分數  " + score + "     金幣  +" + earnedCoins;
        if (resultText != null) resultText.text =
            "搜尋與排序表現\n完成關卡  " + completedRounds + " 關\n正確點擊  " + correctClickCount + " 次\n錯誤點擊  " + wrongClickCount + " 次";
        if (resultNoteText != null) resultNoteText.text =
            "處理速度與視覺搜尋｜本次表現指數 " + cognitiveResult.performanceScore.ToString("F0") + "/100\n" + cognitiveResult.dataQualityNote;
    }
}
