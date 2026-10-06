using System.Collections;
using System.Collections.Generic;
using LTCCognitiveAssessment;
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

    private Text headerText;
    private Text scoreText;
    private Text questionText;
    private Text feedbackText;
    private Button trueButton;
    private Button falseButton;
    private Button restartButton;

    private Font quizFont;
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
        quizFont = Font.CreateDynamicFontFromOSFont(
            new[]
            {
                "Microsoft JhengHei UI",
                "Microsoft JhengHei",
                "Noto Sans CJK TC",
                "PingFang TC",
                "Arial Unicode MS",
                "SimHei"
            },
            72);

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
        mainCamera.backgroundColor = new Color(0.05f, 0.52f, 0.14f);
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
        scaler.referenceResolution = new Vector2(1080, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();

        RectTransform root = canvasObject.GetComponent<RectTransform>();

        CreatePanel(root, "Background", Anchor.Stretch, Vector2.zero, Vector2.zero, new Color(0.05f, 0.6f, 0.16f), 0, false);
        CreateDecorativeCircle(root, "Circle Top", new Vector2(0, 170), new Vector2(560, 270), new Color(0.32f, 0.86f, 0.16f, 0.9f));
        CreateDecorativeCircle(root, "Circle Left", new Vector2(-225, 0), new Vector2(450, 760), new Color(0.13f, 0.72f, 0.2f, 0.75f));
        CreateDecorativeCircle(root, "Circle Bottom", new Vector2(0, -200), new Vector2(540, 280), new Color(0.03f, 0.32f, 0.13f, 0.7f));

        RectTransform headerPanel = CreatePanel(root, "Header Panel", Anchor.TopCenter, new Vector2(100, -75), new Vector2(760, 125), new Color(0.82f, 1f, 0.56f), 34, true);
        headerText = CreateText(headerPanel, "Header Text", "問題 1", 72, FontStyle.Bold, TextAnchor.MiddleCenter, Color.black);

        RectTransform badge = CreatePanel(root, "Badge", Anchor.TopLeft, new Vector2(165, -75), new Vector2(330, 150), new Color(0.9f, 0.1f, 0.05f), 26, true);
        CreateText(badge, "Badge Text", "生活", 64, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.93f, 0.05f));

        RectTransform questionPanel = CreatePanel(root, "Question Panel", Anchor.MiddleCenter, new Vector2(0, 45), new Vector2(980, 430), new Color(0.83f, 1f, 0.58f), 28, true);
        questionText = CreateText(questionPanel, "Question Text", string.Empty, 78, FontStyle.Bold, TextAnchor.MiddleCenter, Color.black);
        questionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        questionText.verticalOverflow = VerticalWrapMode.Truncate;

        // 【優化】Feedback Text 預設為黑色
        feedbackText = CreateText(root, "Feedback Text", string.Empty, 46, FontStyle.Bold, TextAnchor.MiddleCenter, Color.black);
        SetRect(feedbackText.rectTransform, Anchor.MiddleCenter, new Vector2(0, -100), new Vector2(980, 70));

        trueButton = CreateAnswerButton(root, "True Button", "對", new Vector2(-260, 147), new Color(0.86f, 1f, 0.62f));
        falseButton = CreateAnswerButton(root, "False Button", "錯", new Vector2(260, 147), new Color(0.86f, 1f, 0.62f));
        trueButton.onClick.AddListener(() => Answer(true));
        falseButton.onClick.AddListener(() => Answer(false));

        RectTransform divider = CreatePanel(root, "Button Divider", Anchor.BottomCenter, new Vector2(0, -100), new Vector2(4, 136), Color.black, 0, false);
        divider.SetAsLastSibling();

        scoreText = CreateText(root, "Score Text", string.Empty, 38, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
        SetRect(scoreText.rectTransform, Anchor.BottomCenter, new Vector2(0, 70), new Vector2(980, 55));

        restartButton = CreateAnswerButton(root, "Restart Button", "再玩一次", new Vector2(0, 160), new Color(1f, 0.9f, 0.32f));
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

        // 【優化】答對使用深藍色，答錯使用純黑色，提高對比與長輩辨識度
        feedbackText.color = isCorrect ? new Color(0.05f, 0.2f, 0.6f) : Color.black;
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
        feedbackText.color = Color.black; // 【優化】結算文字改為黑色
        feedbackText.text = score >= questions.Count * 0.7f ? "表現很好，生活小知識都記得很清楚。" : "再玩一次，慢慢答就會更熟悉。";
        scoreText.text = "謝謝遊玩";
        trueButton.gameObject.SetActive(false);
        falseButton.gameObject.SetActive(false);
        restartButton.gameObject.SetActive(true);
        CompleteAssessment();
        LTCReturnHomeButton.Show();
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

    private Button CreateAnswerButton(RectTransform parent, string name, string label, Vector2 anchoredPosition, Color color)
    {
        RectTransform buttonRect = CreatePanel(parent, name, Anchor.BottomCenter, anchoredPosition, new Vector2(460, 145), color, 24, true);
        Button button = buttonRect.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = color;
        colors.highlightedColor = new Color(1f, 1f, 0.78f);
        colors.pressedColor = new Color(0.66f, 0.93f, 0.42f);
        colors.disabledColor = new Color(0.72f, 0.8f, 0.64f);
        button.colors = colors;

        Text text = CreateText(buttonRect, $"{name} Text", label, 70, FontStyle.Bold, TextAnchor.MiddleCenter, Color.black);
        text.raycastTarget = false;

        return button;
    }

    private Text CreateText(RectTransform parent, string name, string text, int size, FontStyle style, TextAnchor alignment, Color color)
    {
        GameObject textObject = new(name);
        textObject.transform.SetParent(parent, false);

        Text uiText = textObject.AddComponent<Text>();
        uiText.font = quizFont;
        uiText.text = text;
        uiText.fontSize = size;
        uiText.fontStyle = style;
        uiText.alignment = alignment;
        uiText.color = color;
        uiText.resizeTextForBestFit = true;
        uiText.resizeTextMinSize = Mathf.Max(24, size / 2);
        uiText.resizeTextMaxSize = size;
        uiText.horizontalOverflow = HorizontalWrapMode.Wrap;
        uiText.verticalOverflow = VerticalWrapMode.Overflow;

        SetRect(uiText.rectTransform, Anchor.Stretch, Vector2.zero, Vector2.zero);
        return uiText;
    }

    private RectTransform CreatePanel(RectTransform parent, string name, Anchor anchor, Vector2 anchoredPosition, Vector2 size, Color color, int radius, bool shadow)
    {
        GameObject panelObject = new(name);
        panelObject.transform.SetParent(parent, false);

        Image image = panelObject.AddComponent<Image>();
        image.color = color;
        image.sprite = radius > 0 ? CreateRoundedSprite(radius) : null;
        image.type = radius > 0 ? Image.Type.Sliced : Image.Type.Simple;

        if (shadow)
        {
            Shadow uiShadow = panelObject.AddComponent<Shadow>();
            uiShadow.effectColor = new Color(0f, 0f, 0f, 0.32f);
            uiShadow.effectDistance = new Vector2(8, -8);
        }

        RectTransform rect = panelObject.GetComponent<RectTransform>();
        SetRect(rect, anchor, anchoredPosition, size);
        return rect;
    }

    private void CreateDecorativeCircle(RectTransform parent, string name, Vector2 anchorPoint, Vector2 size, Color color)
    {
        RectTransform circle = CreatePanel(parent, name, Anchor.Custom, Vector2.zero, size, color, 128, false);
        circle.anchorMin = anchorPoint;
        circle.anchorMax = anchorPoint;
        circle.pivot = new Vector2(0.5f, 0.5f);
    }

    private Sprite CreateRoundedSprite(int radius)
    {
        const int size = 128;
        int safeRadius = Mathf.Clamp(radius, 0, (size / 2) - 1);
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false);
        Color clear = new(1f, 1f, 1f, 0f);
        Color fill = Color.white;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool inside = IsInsideRoundedRect(x, y, size, safeRadius);
                texture.SetPixel(x, y, inside ? fill : clear);
            }
        }

        texture.Apply();
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.name = $"Rounded {radius}";

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(safeRadius, safeRadius, safeRadius, safeRadius));
    }

    private bool IsInsideRoundedRect(int x, int y, int size, int radius)
    {
        int left = radius;
        int right = size - radius - 1;
        int bottom = radius;
        int top = size - radius - 1;

        int closestX = Mathf.Clamp(x, left, right);
        int closestY = Mathf.Clamp(y, bottom, top);
        int dx = x - closestX;
        int dy = y - closestY;

        return dx * dx + dy * dy <= radius * radius;
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

    private void SetRect(RectTransform rect, Anchor anchor, Vector2 anchoredPosition, Vector2 size)
    {
        switch (anchor)
        {
            case Anchor.Stretch:
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                break;
            case Anchor.TopLeft:
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = anchoredPosition;
                rect.sizeDelta = size;
                break;
            case Anchor.TopCenter:
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = anchoredPosition;
                rect.sizeDelta = size;
                break;
            case Anchor.MiddleCenter:
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = anchoredPosition;
                rect.sizeDelta = size;
                break;
            case Anchor.BottomCenter:
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = anchoredPosition;
                rect.sizeDelta = size;
                break;
            case Anchor.Custom:
                rect.sizeDelta = size;
                break;
        }
    }

    private enum Anchor
    {
        Stretch,
        TopLeft,
        TopCenter,
        MiddleCenter,
        BottomCenter,
        Custom
    }
}
