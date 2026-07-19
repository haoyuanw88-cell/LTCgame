using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LTCCognitiveAssessment;

public class NumberSumGameManager : MonoBehaviour
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
    private readonly HashSet<Button> selectedButtons = new HashSet<Button>();

    private bool isGameRunning = false;
    private string assessmentSessionId;
    private int randomSeed;
    private float trialStartTime;
    private float roundStartTime;
    private int roundActionCount;
    private int roundResetCount;
    private int trialIndex;

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
        randomSeed = Random.Range(int.MinValue, int.MaxValue);
        Random.InitState(randomSeed);
        assessmentSessionId = CognitiveAssessmentService.BeginGame("number_sum", "2.0.0");
        trialIndex = 0;
        score = 0;
        round = 1;
        completedRoundCount = 0;
        wrongClickCount = 0;
        earnedCoins = 0;
        timeLeft = gameTime;
        isGameRunning = true;

        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }

        SpawnRound();
        UpdateUI();
        trialStartTime = Time.time;
    }

    void SpawnRound()
    {
        HideAllButtons();

        activeButtons.Clear();
        buttonNumbers.Clear();
        originalSprites.Clear();
        selectedButtons.Clear();
        currentSum = 0;
        roundActionCount = 0;
        roundResetCount = 0;
        roundStartTime = Time.time;

        int buttonCount = Random.Range(minButtonCount, maxButtonCount + 1);
        buttonCount = Mathf.Clamp(buttonCount, 3, numberButtons.Count);

        List<int> activeNumbers = GenerateValidNumberSet(buttonCount);

        List<Button> availableButtons = new List<Button>(numberButtons);

        for (int i = 0; i < buttonCount; i++)
        {
            int randomIndex = Random.Range(0, availableButtons.Count);
            Button button = availableButtons[randomIndex];
            availableButtons.RemoveAt(randomIndex);

            SetupButton(button, activeNumbers[i]);
        }

        UpdateUI();
    }

    List<int> GenerateValidNumberSet(int buttonCount)
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

            int answerCount = Random.Range(2, buttonCount);
            List<int> shuffled = new List<int>(numbers);
            Shuffle(shuffled);

            targetNumber = 0;

            for (int i = 0; i < answerCount; i++)
            {
                targetNumber += shuffled[i];
            }

            bool targetEqualsSingleButton = numbers.Contains(targetNumber);

            if (!targetEqualsSingleButton)
            {
                return new List<int>(numbers);
            }
        }

        numbers.Clear();
        numbers.Add(2);
        numbers.Add(3);
        numbers.Add(8);
        targetNumber = 5;

        while (numbers.Count < buttonCount)
        {
            numbers.Add(Random.Range(minNumber, maxNumber + 1));
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
        int sumBeforeInput = currentSum;

        if (selectedButtons.Contains(button))
        {
            selectedButtons.Remove(button);
            currentSum -= number;
            SetButtonSelectedVisual(button, false);
        }
        else
        {
            selectedButtons.Add(button);
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
            roundResetCount++;
            score -= wrongPenalty;

            if (score < 0)
            {
                score = 0;
            }

            ResetSelection();
        }
        else
        {
            RecordSelectionTrial(number, sumBeforeInput, true, "partial_progress");
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
        currentSum = 0;
    }

    void CompleteRound()
    {
        RecordRoundSummary(TrialOutcome.Correct, "");
        score += scorePerRound;
        completedRoundCount++;
        round++;

        SpawnRound();
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
            timedOut=outcome==TrialOutcome.Omitted, errorType=error
        });
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
            targetText.text = "目標：" + targetNumber;
        }
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
