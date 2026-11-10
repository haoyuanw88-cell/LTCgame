using System.Collections;
using System.Collections.Generic;
using LTCCognitiveAssessment;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SeniorTrueFalseQuiz : MonoBehaviour, ICognitiveGamePauseTarget
{
    public bool IsAssessmentRunning => !assessmentCompleted;
    public void RestartCurrentItemAfterPause() { }
    public void CancelCurrentAssessment() { acceptingAnswer = false; StopAllCoroutines(); CognitiveAssessmentService.CancelGame(assessmentSessionId); assessmentSessionId = null; }

    private struct Question
    {
        public readonly string Text;
        public readonly bool Answer;
        public readonly string Tip;

        public Question(string text, bool answer, string tip)
        {
            Text = text;
            Answer = answer;
            Tip = tip;
        }
    }

    private const int QuestionsPerGame = 10;
    private readonly List<Question> questionBank = new()
    {
        // 安全
        new Question("出門前先確認瓦斯爐有關好。", true, "出門前多看一眼，家裡更安心。"),
        new Question("雨天路滑，走路可以慢一點。", true, "慢慢走、扶好扶手，減少滑倒風險。"),
        new Question("家中地板有水，先擦乾比較安全。", true, "地板乾爽，比較不會滑倒。"),
        new Question("紅燈時，沒有車就可以過馬路。", false, "遵守號誌，綠燈時也要注意來車。"),
        new Question("插座不夠時，可以一直加接延長線。", false, "不要過度加接，以免用電過載。"),
        new Question("火災逃生時，搭電梯比較安全。", false, "火災時不要搭電梯，循逃生路線離開。"),

        // 醫療
        new Question("看診時，應告訴醫師自己對哪些藥過敏。", true, "主動說明過敏紀錄，幫助安全用藥。"),
        new Question("看不懂藥袋說明，可以先詢問藥師。", true, "問清楚用法，再依指示服藥。"),
        new Question("看診時，可以帶上目前使用的藥物清單。", true, "讓醫師知道所有用藥與保健品。"),
        new Question("忘記吃藥，下次可以自己吃兩倍。", false, "漏服時依藥袋指示，或詢問醫師、藥師。"),
        new Question("症狀相似，就可以吃朋友的處方藥。", false, "不要共用處方藥，每個人的用藥不同。"),
        new Question("覺得好轉，就可以自行停掉處方藥。", false, "改藥或停藥前，先與醫師、藥師確認。"),

        // 財務
        new Question("買東西時，應確認價格與找零。", true, "看清價格、核對找零，避免付錯錢。"),
        new Question("付款前，可以先想想是否真的需要。", true, "先想需求與預算，再決定是否購買。"),
        new Question("收到催繳訊息，可以自行查官方電話確認。", true, "透過官方管道查證，不照陌生訊息操作。"),
        new Question("陌生人說要退款，可以告訴他提款卡密碼。", false, "提款卡密碼要保密，不提供給他人。"),
        new Question("買東西時，店家多找的錢可以自己留下。", false, "發現多找錢，應告知店家並歸還。"),
        new Question("陌生電話說中獎，要先匯款才能領獎。", false, "先匯款領獎可能是詐騙，應停止並查證。"),

        // 社會／倫理
        new Question("拍朋友照片要公開前，先問對方是否同意。", true, "尊重對方意願，保護個人隱私。"),
        new Question("搭電梯時，先讓裡面的人出來再進去。", true, "先出後進，大家都方便。"),
        new Question("和朋友意見不同，也可以好好聽對方說。", true, "尊重差異，輪流表達自己的想法。"),
        new Question("撿到錢包，可以把裡面的錢當成自己的。", false, "交給警方或失物招領，協助物歸原主。"),
        new Question("朋友告訴你的私事，可以隨便傳給別人。", false, "未經同意，不轉傳他人的私事。"),
        new Question("看到別人行動比較慢，就可以嘲笑他。", false, "尊重每個人，耐心等待並適時協助。"),

        // 日常生活
        new Question("冰箱門沒關緊，食物比較容易壞。", true, "冰箱門關好，才能維持保鮮溫度。"),
        new Question("備餐前先洗手，可以減少污染食物。", true, "先洗手，再處理食物。"),
        new Question("手機收到不明連結，最好不要隨便點開。", true, "不明連結可能有詐騙或資料外洩風險。"),
        new Question("食物長霉了，聞起來正常就一定能吃。", false, "長霉食物不要勉強食用。"),
        new Question("洗澡水很燙，也可以直接沖到身上。", false, "先確認水溫，避免燙傷。"),
        new Question("網路消息只要很多人轉傳，就一定是真的。", false, "先查證來源與內容，再決定是否轉傳。"),
    };
    private readonly List<Question> questions = new(QuestionsPerGame);
    private void SelectQuestionsForGame()
    {
        var shuffled = new List<Question>(questionBank);
        var random = new System.Random(randomSeed);
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            Question temporary = shuffled[i];
            shuffled[i] = shuffled[j]; shuffled[j] = temporary;
        }
        questions.Clear();
        questions.AddRange(shuffled.GetRange(0, Mathf.Min(QuestionsPerGame, shuffled.Count)));
    }

    // ===== 背景音樂相關設定 =====
    [Header("音效設定")]
    [Tooltip("請將背景音樂音樂檔 (MP3/WAV) 拖到這裡")]
    public AudioClip bgmClip;
    private AudioSource bgmSource;
    // ============================

    private TMP_Text headerText;
    private TMP_Text scoreText;
    private TMP_Text questionText;
    private TMP_Text feedbackText;
    private Button trueButton;
    private Button falseButton;
    private Button restartButton;

    [SerializeField] private TMP_FontAsset quizFont;
    [SerializeField] private Sprite goldFrame;
    [SerializeField] private Sprite trueButtonSprite;
    [SerializeField] private Sprite falseButtonSprite;

    private static readonly Color PageColor = new Color32(255, 246, 222, 255);
    private static readonly Color InkColor = new Color32(43, 78, 82, 255);
    private static readonly Color CorrectColor = new Color32(31, 104, 81, 255);
    private static readonly Color IncorrectColor = new Color32(139, 51, 39, 255);
    private int currentQuestion;
    private int score;
    private int trialIndex;
    private int randomSeed;
    private float questionStartTime;
    private bool acceptingAnswer;
    private bool assessmentCompleted;
    private string assessmentSessionId;

    private void Start()
    {
        ConfigureCamera();
        BuildInterface();
        SetupBackgroundMusic(); // 初始化背景音樂
        RestartGame();
    }

    private void ConfigureCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject cameraObject = new("Main Camera");
            mainCamera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }

        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = PageColor;
        mainCamera.orthographic = true;
    }

    // 初始化背景音樂組件並播放
    private void SetupBackgroundMusic()
    {
        if (bgmClip == null)
        {
            Debug.LogWarning("【提示】尚未裝載背景音樂 (BGM)！請將音效檔案拖入 Inspector 中掛載。");
            return;
        }

        // 動態掛載 AudioSource 組件
        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.clip = bgmClip;
        bgmSource.loop = true;          // 設定循環播放
        bgmSource.playOnAwake = false;
        bgmSource.volume = 0.25f;        // 預設音量 25%，避免太大聲嚇到長輩
        
        bgmSource.Play();               // 開始播放音樂
    }

    private void BuildInterface()
    {
        EnsureEventSystem();

        GameObject canvasObject = new("Quiz Canvas");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1366, 768);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        RectTransform root = canvasObject.GetComponent<RectTransform>();
        CreatePanel(root, "Background", Vector2.zero, Vector2.one, null, PageColor);

        Image header = CreatePanel(root, "Header Panel", new Vector2(.34f, .79f),
            new Vector2(.66f, .98f), goldFrame, Color.white);
        CreateText(header.rectTransform, "Title", "生活常識問答", 34,
            new Vector2(.13f, .48f), new Vector2(.87f, .79f));
        headerText = CreateText(header.rectTransform, "Header Text", string.Empty, 27,
            new Vector2(.13f, .22f), new Vector2(.87f, .49f));

        Image questionPanel = CreatePanel(root, "Question Panel", new Vector2(.18f, .31f),
            new Vector2(.82f, .77f), goldFrame, Color.white);
        questionText = CreateText(questionPanel.rectTransform, "Question Text", string.Empty, 50,
            new Vector2(.12f, .34f), new Vector2(.88f, .83f));
        feedbackText = CreateText(questionPanel.rectTransform, "Feedback Text", string.Empty, 30,
            new Vector2(.12f, .16f), new Vector2(.88f, .34f));

        trueButton = CreateAnswerButton(root, "True Button", "對", .36f, trueButtonSprite);
        falseButton = CreateAnswerButton(root, "False Button", "錯", .64f, falseButtonSprite);
        trueButton.onClick.AddListener(() => Answer(true));
        falseButton.onClick.AddListener(() => Answer(false));

        scoreText = CreateText(root, "Score Text", string.Empty, 30,
            new Vector2(.25f, .055f), new Vector2(.75f, .12f));

        restartButton = CreateAnswerButton(root, "Restart Button", "再玩一次", .36f, trueButtonSprite);
        restartButton.onClick.AddListener(RestartGame);
        restartButton.gameObject.SetActive(false);
    }

    private void RestartGame()
    {
        if (CognitiveGamePauseMenu.IsGamePaused) return;
        LTCReturnHomeButton.Hide();
        currentQuestion = 0;
        score = 0;
        trialIndex = 0;
        randomSeed = Random.Range(int.MinValue, int.MaxValue);
        SelectQuestionsForGame();
        assessmentCompleted = false;
        assessmentSessionId = CognitiveAssessmentService.BeginGame(
            "true_false_life_quiz",
            CognitiveProtocolRegistry.ProtocolVersion);
        trueButton.gameObject.SetActive(true);
        falseButton.gameObject.SetActive(true);
        restartButton.gameObject.SetActive(false);
        ShowQuestion();
    }

    private void ShowQuestion()
    {
        acceptingAnswer = true;
        Question question = questions[currentQuestion];
        headerText.text = $"問題 {currentQuestion + 1} / {questions.Count}";
        questionText.text = question.Text;
        feedbackText.text = string.Empty;
        scoreText.text = $"答對 {score} 題 / 共 {questions.Count} 題";
        trueButton.interactable = true;
        falseButton.interactable = true;
        questionStartTime = Time.time;
    }

    private void Answer(bool playerAnswer)
    {
        if (CognitiveGamePauseMenu.IsGamePaused) return;
        if (!acceptingAnswer)
        {
            return;
        }

        acceptingAnswer = false;
        trueButton.interactable = false;
        falseButton.interactable = false;

        Question question = questions[currentQuestion];
        bool isCorrect = playerAnswer == question.Answer;
        if (isCorrect)
        {
            score++;
        }
        RecordAnswerTrial(question, playerAnswer, isCorrect);

        feedbackText.color = isCorrect ? CorrectColor : IncorrectColor;
        feedbackText.text = isCorrect ? $"答對了！{question.Tip}" : $"答錯了。{question.Tip}";
        scoreText.text = $"答對 {score} 題 / 共 {questions.Count} 題";

        StartCoroutine(GoNextQuestion());
    }

    private IEnumerator GoNextQuestion()
    {
        yield return new WaitForSeconds(1.25f);

        currentQuestion++;
        if (currentQuestion >= questions.Count)
        {
            ShowResult();
        }
        else
        {
            ShowQuestion();
        }
    }

    private void ShowResult()
    {
        headerText.text = "完成";
        questionText.text = $"完成！\n答對 {score} / {questions.Count} 題";
        feedbackText.color = InkColor;
        feedbackText.text = score >= questions.Count * 0.7f ? "表現很好，生活小知識都記得很清楚。" : "再玩一次，慢慢答就會更熟悉。";
        scoreText.text = "謝謝遊玩";
        trueButton.gameObject.SetActive(false);
        falseButton.gameObject.SetActive(false);
        restartButton.gameObject.SetActive(true);
        CompleteAssessment();
        Button homeButton = LTCReturnHomeButton.Show();
        SetAnswerButtonBounds((RectTransform)homeButton.transform, .64f);
        ApplyAnswerButtonStyle(homeButton, falseButtonSprite);
        Transform originalLabel = homeButton.transform.Find("Label");
        originalLabel.gameObject.SetActive(false);
        if (homeButton.transform.Find("Quiz Home Label") == null)
        {
            CreateText((RectTransform)homeButton.transform, "Quiz Home Label", "返回首頁", 44,
                new Vector2(.12f, .17f), new Vector2(.88f, .83f));
        }
    }

    private void RecordAnswerTrial(Question question, bool playerAnswer, bool isCorrect)
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
            stepIndex = currentQuestion + 1,
            eventKind = "response",
            randomSeed = randomSeed,
            difficulty = 1,
            stimulusCount = questions.Count,
            condition = "true_false_life_knowledge",
            stimulus = question.Text,
            expectedAnswer = question.Answer ? "true" : "false",
            userAnswer = playerAnswer ? "true" : "false",
            outcome = isCorrect ? TrialOutcome.Correct : TrialOutcome.Incorrect,
            reactionTimeMs = Mathf.RoundToInt(Mathf.Max(0f, Time.time - questionStartTime) * 1000f),
            errorType = isCorrect ? "" : "knowledge_or_comprehension_error"
        });
    }

    private void CompleteAssessment()
    {
        if (assessmentCompleted || string.IsNullOrEmpty(assessmentSessionId))
        {
            return;
        }

        assessmentCompleted = true;
        CognitiveAssessmentService.CompleteGame(
            assessmentSessionId,
            CognitiveDomain.Language,
            0f,
            questions.Count);
    }

    private Button CreateAnswerButton(RectTransform parent, string name, string label, float centerX, Sprite sprite)
    {
        Image face = CreatePanel(parent, name, Vector2.zero, Vector2.one, sprite, Color.white);
        SetAnswerButtonBounds(face.rectTransform, centerX);
        Button button = face.gameObject.AddComponent<Button>();
        button.targetGraphic = face;
        ApplyAnswerButtonStyle(button, sprite);

        CreateText(face.rectTransform, $"{name} Text", label, 44,
            new Vector2(.12f, .17f), new Vector2(.88f, .83f));
        return button;
    }

    private static void SetAnswerButtonBounds(RectTransform rect, float centerX)
    {
        SetBounds(rect, new Vector2(centerX - .11f, .13f), new Vector2(centerX + .11f, .30f));
    }

    private static void ApplyAnswerButtonStyle(Button button, Sprite sprite)
    {
        Image face = button.image;
        face.sprite = sprite;
        face.color = Color.white;
        face.preserveAspect = true;
        face.raycastTarget = true;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, .97f, .87f);
        colors.pressedColor = new Color(.88f, .91f, .86f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(1f, 1f, 1f, .45f);
        button.colors = colors;

    }

    private TMP_Text CreateText(RectTransform parent, string name, string text, float size,
        Vector2 min, Vector2 max)
    {
        var textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        var label = textObject.AddComponent<TextMeshProUGUI>();
        label.font = quizFont;
        label.text = text;
        label.fontSize = size;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = InkColor;
        label.enableAutoSizing = true;
        label.fontSizeMin = 18;
        label.fontSizeMax = size;
        label.overflowMode = TextOverflowModes.Truncate;
        label.raycastTarget = false;
        SetBounds(label.rectTransform, min, max);
        return label;
    }

    private static Image CreatePanel(RectTransform parent, string name, Vector2 min, Vector2 max,
        Sprite sprite, Color color)
    {
        var panelObject = new GameObject(name, typeof(RectTransform));
        panelObject.transform.SetParent(parent, false);
        Image image = panelObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        SetBounds(image.rectTransform, min, max);
        return image;
    }

    private void EnsureEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
        eventSystemObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        eventSystemObject.AddComponent<StandaloneInputModule>();
#endif
    }

    private static void SetBounds(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
