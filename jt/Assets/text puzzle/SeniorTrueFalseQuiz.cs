using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SeniorTrueFalseQuiz : MonoBehaviour
{
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

    private readonly List<Question> questions = new()
    {
        new Question("出門前先確認瓦斯爐有關好。", true, "出門前多看一眼，家裡更安心。"),
        new Question("雨天路滑，走路可以慢一點。", true, "慢慢走、扶好扶手，比較安全。"),
        new Question("陌生電話說中獎，應先提供銀行帳號。", false, "不明來電不要提供個人資料。"),
        new Question("吃藥忘記一次，下次可以自己吃兩倍。", false, "藥量要照醫師或藥袋說明。"),
        new Question("冰箱門沒關緊，食物比較容易壞。", true, "冰箱關好，食物才容易保鮮。"),
        new Question("搭電梯時，先讓裡面的人出來再進去。", true, "先出後進，大家都方便。"),
        new Question("紅燈時，只要沒有車就可以直接過馬路。", false, "等綠燈再走，最安全。"),
        new Question("洗澡前用手試水溫，可以避免太燙。", true, "先試水溫，可以避免燙傷。"),
        new Question("家中地板有水，先擦乾比較安全。", true, "地板乾爽，比較不會滑倒。"),
        new Question("身體不舒服時，忍一忍一定會自己好。", false, "不舒服要告訴家人或看醫師。"),
        new Question("手機收到不明連結，最好不要隨便點開。", true, "不明連結可能有詐騙風險。"),
        new Question("過期食品聞起來沒壞，就一定可以吃。", false, "過期食品不要勉強食用。")
    };

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
    private bool acceptingAnswer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<SeniorTrueFalseQuiz>() != null)
        {
            return;
        }

        new GameObject("Senior True False Quiz").AddComponent<SeniorTrueFalseQuiz>();
    }

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
        currentQuestion = 0;
        score = 0;
        trueButton.gameObject.SetActive(true);
        falseButton.gameObject.SetActive(true);
        restartButton.gameObject.SetActive(false);
        ShowQuestion();
    }

    private void ShowQuestion()
    {
        acceptingAnswer = true;
        Question question = questions[currentQuestion];
        headerText.text = $"問題 {currentQuestion + 1}";
        questionText.text = question.Text;
        feedbackText.text = string.Empty;
        scoreText.text = $"答對 {score} 題 / 共 {questions.Count} 題";
        trueButton.interactable = true;
        falseButton.interactable = true;
    }

    private void Answer(bool playerAnswer)
    {
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