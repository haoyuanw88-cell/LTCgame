using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ColorMatchStroopGameManager : MonoBehaviour
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
    public TMP_Text resultText;

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

    void Start()
    {
        SetupDefaultColorsIfEmpty();
        StartGame();
    }

    public void StartGame()
    {
        score = 0;
        correctCount = 0;
        wrongCount = 0;
        questionCount = 0;
        earnedCoins = 0;
        timeLeft = gameTime;
        isGameRunning = true;

        matchReactionTimes.Clear();
        mismatchReactionTimes.Clear();

        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }

        BindButtons();
        NextQuestion();
        UpdateUI();
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

        bool shouldMatch = Random.value < 0.5f;

        topMeaning = GetRandomColorWord();
        topInkColor = GetRandomColorWord();

        bottomMeaning = GetRandomColorWord();

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

        currentAnswerIsCorrect = topMeaning.colorName == bottomInkColor.colorName;

        UpdateQuestionUI();
        questionStartTime = Time.time;
    }

    ColorWord GetRandomColorWord()
    {
        return colorWords[Random.Range(0, colorWords.Count)];
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

        if (currentAnswerIsCorrect)
        {
            matchReactionTimes.Add(reactionTime);
        }
        else
        {
            mismatchReactionTimes.Add(reactionTime);
        }

        if (playerChoseCorrect == currentAnswerIsCorrect)
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

        NextQuestion();
        UpdateUI();
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

        earnedCoins = correctCount * coinPerCorrect + score / coinPerScoreUnit;
        CoinData.AddCoins(earnedCoins);

        float matchAvg = Average(matchReactionTimes);
        float mismatchAvg = Average(mismatchReactionTimes);
        float interference = mismatchAvg - matchAvg;

        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
        }

        if (resultText != null)
        {
            resultText.text =
                "遊戲結束\n" +
                "分數：" + score + "\n" +
                "獲得金幣：+" + earnedCoins + "\n" +
                "正確：" + correctCount + "\n" +
                "錯誤：" + wrongCount + "\n" +
                "相同平均反應：" + matchAvg.ToString("F2") + " 秒\n" +
                "不同平均反應：" + mismatchAvg.ToString("F2") + " 秒\n" +
                "判斷干擾值：" + interference.ToString("F2") + " 秒";
        }
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