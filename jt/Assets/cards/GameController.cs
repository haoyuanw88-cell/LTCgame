using System.Collections;
using System.Collections.Generic;
using LTCCognitiveAssessment;
using TMPro;
using UnityEngine.Animations;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class GameController : MonoBehaviour
{
    // --- 動畫與音效 ---
    [Header("Animations")]
    public Animator playerAnimator; 
    public Animator enemyAnimator;  

    [Header("Audio")]
    public AudioSource audioSource; 
    public AudioClip victorySound;  
    public AudioClip matchSuccessSound;
    public AudioClip matchFailSound;

    [Header("卡牌設定")]
    public GameObject cardPrefab;
    public Sprite cardBackBlue;     // 攻擊牌背面
    public Sprite cardBackRed;      // 回復牌背面
    public List<Sprite> cardFaces;
    public int columns = 3;
    public int rows = 3;
    public Vector2 cardSpacing = new Vector2(2.2f, 2.35f);
    public Vector2 boardCenter = new Vector2(0f, -1.1f);
    public Vector3 cardScale = new Vector3(0.8f, 0.8f, 1f);
    public float previewSeconds = 5f;

    [Header("戰鬥數值")]
    public int playerMaxHealth = 20;
    public int enemyMaxHealth = 20;
    public int enemyAttackPower = 3;

    [Header("UI 連結")]
    public Transform playerHealthBarFill;
    public Transform enemyHealthBarFill;
    public Transform playerHealthBarFrame;
    public Transform enemyHealthBarFrame;
    public TMP_Text resultText;      
    public TMP_FontAsset statusFont;

    [Header("Teaching UI")]
    [SerializeField] private string teachingStageName = "stage";
    [SerializeField] private string teachingStageSpriteName = "黑";
    [SerializeField] private string teachingTextName = "Text (TMP)";
    [SerializeField] private string teachingPage1Name = "teaching1";
    [SerializeField] private string teachingPage2Name = "teaching2";
    [SerializeField] private string teachingCloseName = "cross_0";
    [SerializeField] private string teachingNextName = "arrowRight_0";
    [SerializeField] private string teachingPreviousName = "arrowLeft_0";
    [SerializeField] private int teachingSortingOrder = 5000;

    private TMP_Text playerHealthText;
    private TMP_Text enemyHealthText;
    private GameObject teachingStage;
    private GameObject teachingPage1;
    private GameObject teachingPage2;
    private GameObject teachingCloseButton;
    private GameObject teachingNextButton;
    private GameObject teachingPreviousButton;
    private TMP_Text teachingText;
    private int playerHealth;
    private int enemyHealth;
    private int teachingPageIndex;
    private int roundAttackPoints;   
    private int roundHealPoints;     
    private int mismatchCount;
    private int playerFlipCount;
    private int trialIndex;
    private int roundIndex;
    private int pairAttemptCount;
    private int matchedPairCount;
    private int randomSeed;
    private float totalPlayerFlipTime;
    private float flipTimerStartTime;
    private float pairStartTime;
    private bool isProcessing;
    private bool battleEnded;
    private bool tutorialShowing;
    private bool gameFlowStarted;
    private bool isTimingPlayerFlip;
    private bool assessmentCompleted;
    private string assessmentSessionId;
    private readonly List<Card> allCards = new List<Card>();
    private readonly List<Card> revealedCards = new List<Card>();

    private Dictionary<Transform, Vector3> baseScales = new Dictionary<Transform, Vector3>();
    private Dictionary<Transform, Vector3> basePositions = new Dictionary<Transform, Vector3>();
    private Dictionary<Transform, float> spriteWidths = new Dictionary<Transform, float>();
    private readonly Dictionary<Animator, ClipPlayback> activeClipPlaybacks = new Dictionary<Animator, ClipPlayback>();

    // --- 修改後的動畫變數區 ---
    [Header("Animations (拖入動畫檔案)")]
    public AnimationClip playerAttackClip;
    public AnimationClip playerHitClip;
    public AnimationClip enemyAttackClip;
    public AnimationClip enemyHitClip;
    public AnimationClip enemyDieClip;

    [Header("Animation Timing")]
    [Min(0f)] public float playerAttackToEnemyHitDelay = 0.4f;
    [Min(0f)] public float enemyAttackToPlayerHitDelay = 0.4f;
    [Min(0f)] public float afterAnimationDelay = 0.1f;
    [Min(0f)] public float deathEndDelay = 0.2f;
    [Min(0f)] public float cardFlipAnimationSeconds = 0.3f;
    public bool keepEnemyDeadPose = true;

    private struct ClipPlayback
    {
        public PlayableGraph graph;
        public AnimationClipPlayable playable;

        public ClipPlayback(PlayableGraph graph, AnimationClipPlayable playable)
        {
            this.graph = graph;
            this.playable = playable;
        }
    }
    // --- 初始化區 ---

    private void Awake()
    {
        // 自動尋找場景中的 Animator
        if (playerAnimator == null)
        {
            GameObject p = GameObject.Find("Swordsman");
            if (p != null) playerAnimator = p.GetComponent<Animator>();
        }

        if (enemyAnimator == null)
        {
            GameObject e = GameObject.Find("Skeleton Idle");
            if (e != null) enemyAnimator = e.GetComponent<Animator>();
        }

        if (playerAnimator == null || enemyAnimator == null)
        {
            Debug.LogWarning("GameController: 未找到主角或敵人的 Animator，請確認物件名稱！");
        }
    }

    private void Start()
    {
        playerHealth = playerMaxHealth;
        enemyHealth = enemyMaxHealth;
        ResetBattleStats();
        CacheHealthBarSettings();
        SetupTeachingUI();
        UpdateBattleUI("遊戲開始！");
        if (!tutorialShowing)
        {
            StartGameFlow();
        }
    }

    private void Update()
    {
        HandleTeachingPointerInput();
    }

    private void CacheHealthBarSettings()
    {
        if (playerHealthBarFill == null) playerHealthBarFill = GameObject.Find("BarV6_ProgressBar_0")?.transform;
        if (enemyHealthBarFill == null) enemyHealthBarFill = GameObject.Find("BarV5_ProgressBar_0")?.transform;
        if (playerHealthBarFrame == null) playerHealthBarFrame = GameObject.Find("BarV6_Bar_0")?.transform;
        if (enemyHealthBarFrame == null) enemyHealthBarFrame = GameObject.Find("BarV5_Bar_0")?.transform;

        RegisterBar(playerHealthBarFill, playerHealthBarFrame);
        RegisterBar(enemyHealthBarFill, enemyHealthBarFrame);

        playerHealthText = CreateHPText("PlayerHPText", playerHealthBarFrame);
        enemyHealthText = CreateHPText("EnemyHPText", enemyHealthBarFrame);
    }

    private void RegisterBar(Transform fill, Transform frame)
    {
        if (fill == null) return;
        baseScales[fill] = fill.localScale;
        basePositions[fill] = (frame != null) ? frame.position : fill.position;
        var sr = fill.GetComponent<SpriteRenderer>();
        spriteWidths[fill] = (sr != null && sr.sprite != null) ? sr.sprite.bounds.size.x : 1f;
    }

    private TMP_Text CreateHPText(string name, Transform parent)
    {
        if (parent == null) return null;
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent);
        obj.transform.localPosition = new Vector3(0, 0, -1f); 
        var t = obj.AddComponent<TextMeshPro>();
        t.alignment = TextAlignmentOptions.Center;
        t.fontSize = 2.5f;
        t.font = statusFont;
        t.color = Color.white;
        var renderer = obj.GetComponent<MeshRenderer>();
        if (renderer != null) renderer.sortingOrder = 100;
        return t;
    }

    // --- 遊戲邏輯區 ---

    public bool CanClickCards() => !isProcessing && !battleEnded && !tutorialShowing;

    private IEnumerator StartNewRound()
    {
        isProcessing = true;
        roundAttackPoints = 0;
        roundHealPoints = 0;
        
        ClearBoard();
        BuildBoard();
        UpdateBattleUI("記住牌面位置！");
        
        yield return StartCoroutine(PreviewAllCards());
        
        isProcessing = false;
        UpdateBattleUI("請翻牌");
        StartPlayerFlipTimer();
    }

    private void BuildBoard()
    {
        int total = columns * rows;
        List<CardSetup> deck = new List<CardSetup>();
        if (cardFaces == null || cardFaces.Count == 0)
        {
            Debug.LogWarning("GameController: 沒有設定卡牌符號素材。");
            return;
        }
        
        for (int i = 0; i < total / 2; i++)
        {
            Sprite face = cardFaces[i % cardFaces.Count];
            CardRewardType type = GetRewardTypeForSymbol(face);
            int val = GetPointValueForSymbol(face);
            Sprite back = (type == CardRewardType.Heal) ? cardBackRed : cardBackBlue;

            CardSetup s = new CardSetup(GetCardIDForSymbol(face), face, back, type, val);
            deck.Add(s); deck.Add(s);
        }
        Shuffle(deck);

        for (int i = 0; i < deck.Count; i++)
        {
            float startX = boardCenter.x - (columns - 1) * cardSpacing.x * 0.5f;
            float startY = boardCenter.y + (rows - 1) * cardSpacing.y * 0.5f;
            Vector3 pos = new Vector3(startX + (i % columns) * cardSpacing.x, startY - (i / columns) * cardSpacing.y, 0f);
            GameObject go = Instantiate(cardPrefab, pos, Quaternion.identity);
            Card card = go.GetComponent<Card>();
            card.SetBaseScale(cardScale);
            card.Initialize(deck[i].id, deck[i].face, deck[i].back, deck[i].type, deck[i].val);
            allCards.Add(card);
        }
    }

    public void CardClicked(Card card)
    {
        if (!CanClickCards() || revealedCards.Contains(card)) return;
        if (revealedCards.Count == 0)
            pairStartTime = Time.time;
        RecordPlayerFlipTime();

        revealedCards.Add(card);
        if (revealedCards.Count == 2) StartCoroutine(CheckMatch());
        else StartCoroutine(StartPlayerFlipTimerAfterDelay(cardFlipAnimationSeconds));
    }

    private IEnumerator CheckMatch()
    {
        isProcessing = true;
        yield return new WaitForSeconds(0.5f);
        Card c1 = revealedCards[0]; 
        Card c2 = revealedCards[1];

        if (c1.GetCardID() == c2.GetCardID())
        {
            PlaySound(matchSuccessSound);
            RecordCardPairTrial(c1, c2, true);

            int val = c1.GetPointValue(); 
            if (c1.GetRewardType() == CardRewardType.Heal) roundHealPoints += val; 
            else roundAttackPoints += val; 
            matchedPairCount++;
            
            UpdateBattleUI("配對成功！繼續翻牌"); 
            
            StartCoroutine(c1.FlyOutAnimation());
            yield return StartCoroutine(c2.FlyOutAnimation());
            revealedCards.Clear();

            if (NoActiveCards()) yield return StartCoroutine(ResolveTurn());
            else
            {
                isProcessing = false;
                UpdateBattleUI("請翻牌");
                StartPlayerFlipTimer();
            }
        }
        else
        {
            PlaySound(matchFailSound);
            mismatchCount++;
            RecordCardPairTrial(c1, c2, false);
            yield return new WaitForSeconds(0.3f);
            c1.TurnBack(); 
            c2.TurnBack();
            revealedCards.Clear();
            UpdateBattleUI("配對失敗，結算回合");
            yield return new WaitForSeconds(0.5f);
            yield return StartCoroutine(ResolveTurn());
        }
    }

    private IEnumerator ResolveTurn()
    {
        isProcessing = true;

        // --- 1. 主角行動 ---
        if (roundAttackPoints > 0)
        {
            UpdateBattleUI("主角攻擊！");
            yield return StartCoroutine(PlayAttackAndHit(
                playerAnimator,
                playerAttackClip,
                enemyAnimator,
                enemyHitClip,
                playerAttackToEnemyHitDelay));

            enemyHealth = Mathf.Max(0, enemyHealth - roundAttackPoints);
            UpdateBattleUI("敵人受傷！");
        }
    
        if (roundHealPoints > 0)
        {
            playerHealth = Mathf.Min(playerMaxHealth, playerHealth + roundHealPoints);
            UpdateBattleUI("主角回復！");
        }

        if (afterAnimationDelay > 0f)
            yield return new WaitForSeconds(afterAnimationDelay);

        if (enemyHealth <= 0) 
        { 
            yield return StartCoroutine(HandleEnemyDeath());
            yield break; 
        }

        // --- 2. 敵人行動 ---
        UpdateBattleUI("敵人反擊！");
        yield return StartCoroutine(PlayAttackAndHit(
            enemyAnimator,
            enemyAttackClip,
            playerAnimator,
            playerHitClip,
            enemyAttackToPlayerHitDelay));

        playerHealth = Mathf.Max(0, playerHealth - enemyAttackPower);
        UpdateBattleUI("主角受傷！");

        if (afterAnimationDelay > 0f)
            yield return new WaitForSeconds(afterAnimationDelay);

        if (playerHealth <= 0) { EndBattle(false); yield break; }
        yield return StartCoroutine(StartNewRound());
    }      

    private IEnumerator HandleEnemyDeath()
    {
        float deathDuration = PlayClipFromStart(enemyAnimator, enemyDieClip);
        
        PlaySound(victorySound);
        UpdateBattleUI("勝利！");

        if (deathDuration > 0f)
            yield return new WaitForSeconds(deathDuration);

        if (keepEnemyDeadPose) FreezeClipAtEnd(enemyAnimator, enemyDieClip);
        else StopClipPlayback(enemyAnimator);

        if (deathEndDelay > 0f)
            yield return new WaitForSeconds(deathEndDelay);

        EndBattle(true);
    }

    private IEnumerator PlayAttackAndHit(
        Animator attacker,
        AnimationClip attackClip,
        Animator defender,
        AnimationClip hitClip,
        float hitDelay)
    {
        float attackDuration = PlayClipFromStart(attacker, attackClip);
        float delay = Mathf.Max(0f, hitDelay);
        if (attackDuration > 0f)
            delay = Mathf.Min(delay, attackDuration);
        else
            delay = 0f;

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        float hitDuration = PlayClipFromStart(defender, hitClip);
        float remainingAttackTime = Mathf.Max(0f, attackDuration - delay);
        float remainingHitTime = hitDuration;

        if (remainingAttackTime <= 0f)
            StopClipPlayback(attacker);

        while (remainingAttackTime > 0f || remainingHitTime > 0f)
        {
            float waitTime = remainingAttackTime > 0f && remainingHitTime > 0f
                ? Mathf.Min(remainingAttackTime, remainingHitTime)
                : Mathf.Max(remainingAttackTime, remainingHitTime);

            yield return new WaitForSeconds(waitTime);

            remainingAttackTime = Mathf.Max(0f, remainingAttackTime - waitTime);
            remainingHitTime = Mathf.Max(0f, remainingHitTime - waitTime);

            if (remainingAttackTime <= 0f)
                StopClipPlayback(attacker);

            if (remainingHitTime <= 0f)
                StopClipPlayback(defender);
        }
    }

    private float PlayClipFromStart(Animator animator, AnimationClip clip)
    {
        if (animator == null || clip == null) return 0f;

        StopClipPlayback(animator);

        animator.enabled = true;
        PlayableGraph graph = PlayableGraph.Create($"{name}_{animator.name}_{clip.name}");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        AnimationPlayableOutput output = AnimationPlayableOutput.Create(graph, "Animation", animator);
        AnimationClipPlayable playable = AnimationClipPlayable.Create(graph, clip);
        playable.SetApplyFootIK(false);
        playable.SetApplyPlayableIK(false);
        playable.SetTime(0f);
        playable.SetSpeed(1f);
        playable.SetDuration(Mathf.Max(clip.length, 0.01f));

        output.SetSourcePlayable(playable);
        activeClipPlaybacks[animator] = new ClipPlayback(graph, playable);

        graph.Play();
        graph.Evaluate(0f);
        return Mathf.Max(clip.length, 0f);
    }

    private void FreezeClipAtEnd(Animator animator, AnimationClip clip)
    {
        if (animator == null || clip == null) return;
        if (!activeClipPlaybacks.TryGetValue(animator, out ClipPlayback playback)) return;
        if (!playback.graph.IsValid() || !playback.playable.IsValid()) return;

        playback.playable.SetTime(Mathf.Max(0f, clip.length));
        playback.playable.SetSpeed(0f);
        playback.graph.Evaluate(0f);
        activeClipPlaybacks[animator] = playback;
    }

    private void StopClipPlayback(Animator animator)
    {
        if (animator == null) return;
        if (!activeClipPlaybacks.TryGetValue(animator, out ClipPlayback playback)) return;

        if (playback.graph.IsValid())
            playback.graph.Destroy();

        activeClipPlaybacks.Remove(animator);
    }

    private void OnDestroy()
    {
        foreach (ClipPlayback playback in activeClipPlaybacks.Values)
        {
            if (playback.graph.IsValid())
                playback.graph.Destroy();
        }

        activeClipPlaybacks.Clear();
    }

    // --- UI 與工具區 ---

    private void UpdateBattleUI(string message)
    {
        UpdateBar(playerHealthBarFill, playerHealth, playerMaxHealth);
        UpdateBar(enemyHealthBarFill, enemyHealth, enemyMaxHealth);

        if (playerHealthText) playerHealthText.text = $"{playerHealth}/{playerMaxHealth}";
        if (enemyHealthText) enemyHealthText.text = $"{enemyHealth}/{enemyMaxHealth}";
        
        if (resultText)
        {
            resultText.text = $"<color=red>攻擊 {roundAttackPoints}</color>  <color=green>回復 {roundHealPoints}</color>\n{BuildStatusMessage(message)}";
            resultText.gameObject.SetActive(!tutorialShowing);
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    private string BuildStatusMessage(string message)
    {
        if (message == "請翻牌")
            return $"{message}\n敵人將攻擊{enemyAttackPower}點生命";

        if (message == "勝利！")
            return $"{message}\n配對失敗次數：{mismatchCount}\n平均翻卡時間：{GetAverageFlipTime():0.00}秒";

        return message;
    }

    private void ResetBattleStats()
    {
        mismatchCount = 0;
        playerFlipCount = 0;
        trialIndex = 0;
        roundIndex = 0;
        pairAttemptCount = 0;
        matchedPairCount = 0;
        randomSeed = 0;
        totalPlayerFlipTime = 0f;
        flipTimerStartTime = 0f;
        pairStartTime = 0f;
        isTimingPlayerFlip = false;
        assessmentCompleted = false;
        assessmentSessionId = null;
    }

    private void StartPlayerFlipTimer()
    {
        if (!CanClickCards())
        {
            isTimingPlayerFlip = false;
            return;
        }

        isTimingPlayerFlip = true;
        flipTimerStartTime = Time.time;
    }

    private IEnumerator StartPlayerFlipTimerAfterDelay(float delay)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        StartPlayerFlipTimer();
    }

    private void RecordPlayerFlipTime()
    {
        if (!isTimingPlayerFlip) return;

        totalPlayerFlipTime += Mathf.Max(0f, Time.time - flipTimerStartTime);
        playerFlipCount++;
        isTimingPlayerFlip = false;
    }

    private float GetAverageFlipTime()
    {
        return playerFlipCount > 0 ? totalPlayerFlipTime / playerFlipCount : 0f;
    }

    private void UpdateBar(Transform fill, int current, int max)
    {
        if (fill == null || !baseScales.ContainsKey(fill)) return;
        float ratio = Mathf.Clamp01((float)current / max);
        fill.localScale = new Vector3(baseScales[fill].x * ratio, baseScales[fill].y, baseScales[fill].z);
        
        // 根據縮放調整位置，讓血條看起來是從一側減少
        float widthDelta = (baseScales[fill].x - fill.localScale.x) * spriteWidths[fill];
        fill.position = basePositions[fill] - fill.right * widthDelta * 0.5f;
    }

    private int GetCardIDForSymbol(Sprite symbol)
    {
        return symbol != null ? symbol.name.GetHashCode() : 0;
    }

    private CardRewardType GetRewardTypeForSymbol(Sprite symbol)
    {
        string spriteName = symbol != null ? symbol.name.ToLower() : string.Empty;
        return spriteName.Contains("pink") || spriteName.Contains("heart")
            ? CardRewardType.Heal
            : CardRewardType.Attack;
    }

    private int GetPointValueForSymbol(Sprite symbol)
    {
        string spriteName = symbol != null ? symbol.name.ToLower() : string.Empty;

        if (spriteName.Contains("pink") || spriteName.Contains("heart")) return 5;
        if (spriteName.Contains("green")) return 4;
        if (spriteName.Contains("yellow")) return 3;
        if (spriteName.Contains("red")) return 2;

        return 1;
    }

    private void ClearBoard()
    {
        foreach (var c in allCards) if (c != null) Destroy(c.gameObject);
        allCards.Clear();
    }

    private IEnumerator PreviewAllCards()
    {
        foreach (var c in allCards) if(c != null) c.FlipCard();
        yield return new WaitForSeconds(previewSeconds);
        foreach (var c in allCards) if(c != null) c.TurnBack();
        yield return new WaitForSeconds(0.5f);
    }

    private bool NoActiveCards() => allCards.FindAll(c => c != null && c.gameObject.activeSelf).Count == 0;

    private void Shuffle<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = Random.Range(i, list.Count);
            T t = list[i]; list[i] = list[r]; list[r] = t;
        }
    }

    private void SetupTeachingUI()
    {
        teachingStage = FindTeachingStage();
        if (teachingStage == null)
        {
            return;
        }

        teachingPage1 = FindChildByName(teachingStage.transform, teachingPage1Name);
        teachingPage2 = FindChildByName(teachingStage.transform, teachingPage2Name);
        teachingCloseButton = FindChildByName(teachingStage.transform, teachingCloseName);
        teachingNextButton = FindChildByName(teachingStage.transform, teachingNextName);
        teachingPreviousButton = FindChildByName(teachingStage.transform, teachingPreviousName);

        GameObject textObject = FindChildByName(teachingStage.transform, teachingTextName);
        if (textObject != null)
        {
            teachingText = textObject.GetComponent<TMP_Text>();
        }

        ApplyTeachingSortingOrder();
        ConnectTeachingButton(teachingCloseButton, CloseTeachingUI);
        ConnectTeachingButton(teachingNextButton, ShowNextTeachingPage);
        ConnectTeachingButton(teachingPreviousButton, ShowPreviousTeachingPage);

        tutorialShowing = teachingStage.activeInHierarchy;
        ShowBattleStatusText(!tutorialShowing);
        teachingPageIndex = 0;
        UpdateTeachingPage();
    }

    private void ShowNextTeachingPage()
    {
        teachingPageIndex = Mathf.Min(teachingPageIndex + 1, 1);
        UpdateTeachingPage();
    }

    private void ShowPreviousTeachingPage()
    {
        teachingPageIndex = Mathf.Max(teachingPageIndex - 1, 0);
        UpdateTeachingPage();
    }

    private void CloseTeachingUI()
    {
        if (teachingStage != null)
        {
            teachingStage.SetActive(false);
        }

        tutorialShowing = false;
        ShowBattleStatusText(true);

        StartGameFlow();
    }

    private void StartGameFlow()
    {
        if (gameFlowStarted)
        {
            return;
        }

        gameFlowStarted = true;
        StartCardAssessment();
        StartCoroutine(StartNewRound());
    }

    private void StartCardAssessment()
    {
        randomSeed = Random.Range(int.MinValue, int.MaxValue);
        roundIndex = 1;
        assessmentSessionId = CognitiveAssessmentService.BeginGame(
            "card_memory_battle",
            CognitiveProtocolRegistry.ProtocolVersion);
    }

    private void RecordCardPairTrial(Card firstCard, Card secondCard, bool matched)
    {
        if (string.IsNullOrEmpty(assessmentSessionId)) return;

        trialIndex++;
        pairAttemptCount++;
        CognitiveAssessmentService.RecordTrial(assessmentSessionId, new CognitiveTrialRecord
        {
            trialIndex = trialIndex,
            roundIndex = roundIndex,
            stepIndex = pairAttemptCount,
            eventKind = "response",
            randomSeed = randomSeed,
            difficulty = rows * columns,
            stimulusCount = allCards.Count,
            condition = "card_pair_match",
            stimulus = firstCard.GetCardID() + "|" + secondCard.GetCardID(),
            expectedAnswer = "same_card_id",
            userAnswer = matched ? "same_card_id" : "different_card_id",
            outcome = matched ? TrialOutcome.Correct : TrialOutcome.Incorrect,
            reactionTimeMs = Mathf.RoundToInt(Mathf.Max(0f, Time.time - pairStartTime) * 1000f),
            errorType = matched ? "" : "memory_mismatch"
        });
    }

    private void UpdateTeachingPage()
    {
        ApplyTeachingSortingOrder();

        if (teachingPage1 != null)
        {
            teachingPage1.SetActive(teachingPageIndex == 0);
        }

        if (teachingPage2 != null)
        {
            teachingPage2.SetActive(teachingPageIndex == 1);
        }

        if (teachingText != null)
        {
            teachingText.text = teachingPageIndex == 0
                ? "每回合開始卡片會翻面5秒"
                : "配對成功卡牌可以繼續直到失敗，藍色是攻擊，紅色是回血";
        }
    }

    private void HandleTeachingPointerInput()
    {
        if (!tutorialShowing || teachingStage == null || !teachingStage.activeInHierarchy)
        {
            return;
        }

        if (!TryGetTeachingPointerDown(out Vector2 screenPosition))
        {
            return;
        }

        if (IsTeachingTargetHit(teachingCloseButton, screenPosition))
        {
            CloseTeachingUI();
            return;
        }

        if (IsTeachingTargetHit(teachingNextButton, screenPosition))
        {
            ShowNextTeachingPage();
            return;
        }

        if (IsTeachingTargetHit(teachingPreviousButton, screenPosition))
        {
            ShowPreviousTeachingPage();
        }
    }

    private bool TryGetTeachingPointerDown(out Vector2 screenPosition)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }

        if (Touchscreen.current != null)
        {
            foreach (UnityEngine.InputSystem.Controls.TouchControl touch in Touchscreen.current.touches)
            {
                if (touch.press.wasPressedThisFrame)
                {
                    screenPosition = touch.position.ReadValue();
                    return true;
                }
            }
        }
#endif
        screenPosition = Vector2.zero;
        return false;
    }

    private bool IsTeachingTargetHit(GameObject target, Vector2 screenPosition)
    {
        if (target == null || !target.activeInHierarchy)
        {
            return false;
        }

        RectTransform rectTransform = target.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            Canvas canvas = target.GetComponentInParent<Canvas>();
            Camera canvasCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            return RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, canvasCamera);
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            return false;
        }

        Vector3 worldPosition = camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -camera.transform.position.z));
        Collider2D collider = target.GetComponent<Collider2D>();
        if (collider != null)
        {
            return collider.OverlapPoint(worldPosition);
        }

        SpriteRenderer spriteRenderer = target.GetComponent<SpriteRenderer>();
        return spriteRenderer != null && spriteRenderer.bounds.Contains(worldPosition);
    }

    private void ShowBattleStatusText(bool show)
    {
        if (resultText != null)
        {
            resultText.gameObject.SetActive(show);
        }
    }

    private void ApplyTeachingSortingOrder()
    {
        if (teachingStage == null)
        {
            return;
        }

        SpriteRenderer[] sprites = teachingStage.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < sprites.Length; i++)
        {
            sprites[i].sortingOrder = teachingSortingOrder + i;
        }

        Canvas[] canvases = teachingStage.GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            canvases[i].overrideSorting = true;
            canvases[i].sortingOrder = teachingSortingOrder + 200 + i;
        }

        TMP_Text[] texts = teachingStage.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            Renderer textRenderer = texts[i].GetComponent<Renderer>();
            if (textRenderer != null)
            {
                textRenderer.sortingOrder = teachingSortingOrder + 300 + i;
            }
        }
    }

    private void ConnectTeachingButton(GameObject target, System.Action action)
    {
        if (target == null || action == null)
        {
            return;
        }

        Button button = target.GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(() => action());
            return;
        }

        SpriteRenderer spriteRenderer = target.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && target.GetComponent<Collider2D>() == null)
        {
            BoxCollider2D box = target.AddComponent<BoxCollider2D>();
            if (spriteRenderer.sprite != null)
            {
                box.size = spriteRenderer.sprite.bounds.size;
                box.offset = spriteRenderer.sprite.bounds.center;
            }
        }

        TeachingClickTarget clickTarget = target.GetComponent<TeachingClickTarget>();
        if (clickTarget == null)
        {
            clickTarget = target.AddComponent<TeachingClickTarget>();
        }

        clickTarget.Initialize(action);
    }

    private GameObject FindChildByName(Transform root, string objectName)
    {
        if (root == null || string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child.name == objectName)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private GameObject FindTeachingStage()
    {
        GameObject stageObject = FindSceneObjectByName(teachingStageName);
        if (stageObject != null)
        {
            return stageObject;
        }

        if (string.IsNullOrEmpty(teachingStageSpriteName))
        {
            return null;
        }

        SpriteRenderer[] spriteRenderers = Resources.FindObjectsOfTypeAll<SpriteRenderer>();
        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            if (spriteRenderer == null || spriteRenderer.gameObject == null || !spriteRenderer.gameObject.scene.IsValid())
            {
                continue;
            }

            if (spriteRenderer.sprite != null && spriteRenderer.sprite.name == teachingStageSpriteName)
            {
                return spriteRenderer.gameObject;
            }
        }

        return null;
    }

    private GameObject FindSceneObjectByName(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
        {
            return null;
        }

        GameObject activeObject = GameObject.Find(objectName);
        if (activeObject != null)
        {
            return activeObject;
        }

        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            if (obj.name == objectName && obj.scene.IsValid())
            {
                return obj;
            }
        }

        return null;
    }

    private void EndBattle(bool win)
    {
        if (battleEnded) return;

        battleEnded = true;
        UpdateBattleUI(win ? "勝利！" : "戰敗...");
        CompleteCardAssessment(win);
    }

    private void CompleteCardAssessment(bool win)
    {
        if (assessmentCompleted || string.IsNullOrEmpty(assessmentSessionId)) return;

        assessmentCompleted = true;
        CognitiveAssessmentService.RecordTrial(assessmentSessionId, new CognitiveTrialRecord
        {
            trialIndex = ++trialIndex,
            roundIndex = roundIndex,
            stepIndex = pairAttemptCount,
            eventKind = "summary",
            randomSeed = randomSeed,
            difficulty = rows * columns,
            stimulusCount = allCards.Count,
            condition = "battle_result",
            stimulus = "matchedPairs=" + matchedPairCount + "|mismatches=" + mismatchCount,
            expectedAnswer = "defeat_enemy",
            userAnswer = win ? "defeat_enemy" : "player_defeated",
            outcome = win ? TrialOutcome.Correct : TrialOutcome.Incorrect,
            reactionTimeMs = Mathf.RoundToInt(GetAverageFlipTime() * 1000f),
            errorCount = mismatchCount,
            actionCount = pairAttemptCount,
            errorType = win ? "" : "battle_lost"
        });
        CognitiveAssessmentService.CompleteGame(
            assessmentSessionId,
            CognitiveDomain.WorkingMemory,
            0f,
            rows * columns);
    }

    private struct CardSetup {
        public int id; public Sprite face; public Sprite back; public CardRewardType type; public int val;
        public CardSetup(int i, Sprite f, Sprite b, CardRewardType t, int v) { id = i; face = f; back = b; type = t; val = v; }
    }
}

public class TeachingClickTarget : MonoBehaviour, IPointerClickHandler
{
    private System.Action onClick;

    public void Initialize(System.Action clickAction)
    {
        onClick = clickAction;
    }

    private void OnMouseDown()
    {
        onClick?.Invoke();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        onClick?.Invoke();
    }
}
