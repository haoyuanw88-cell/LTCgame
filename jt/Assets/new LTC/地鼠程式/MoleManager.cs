using System.Collections;
using LTCCognitiveAssessment;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Mediapipe.Unity.Sample.HandLandmarkDetection;

public class MoleManager : MonoBehaviour
{
    [Header("MediaPipe")]
    public HandLandmarkerRunner handDataSource;

    [Header("地鼠清單")]
    public Mole[] moles;

    [Header("遊戲設定")]
    public float spawnInterval = 1.5f;
    public float moleVisibleTime = 3f;
    public int targetScore = 100;

    [Header("炸彈設定")]
    [Range(0f, 1f)]
    public float bombSpawnChance = 0.25f;

    [Header("分數")]
    public TMP_Text ScoreTex;
    public int scorePerHit = 10;

    [Header("金幣")]
    public int coinPerHit = 1;
    public int clearBonusCoins = 5;

    [Header("倒數 UI")]
    public TMP_Text countdownText;

    [Header("結算 UI")]
    public GameObject resultPanel;
    public TMP_Text resultText;

    [Header("MediaPipe 防連擊")]
    public float mediaPipeHitCooldown = 0.25f;

    private float timer;
    private float lastMediaPipeHitTime = -999f;

    private int score = 0;
    private int hitCount = 0;
    private int missCount = 0;
    private int shotCount = 0;
    private int bombHitCount = 0;
    private int earnedCoins = 0;
    private int trialIndex = 0;
    private int randomSeed;
    private float gameStartTime;
    private string assessmentSessionId;

    private bool gameRunning = false;
    private bool gameEnded = false;

    void Start()
    {
        if (moles != null)
        {
            for (int i = 0; i < moles.Length; i++)
            {
                if (moles[i] != null)
                {
                    moles[i].manager = this;
                    moles[i].Hide();
                }
            }
        }

        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(true);
            countdownText.transform.localScale = Vector3.one * 0.9f;
            countdownText.text = "準備中";
        }

        UpdateScoreUI();
        StartCoroutine(WaitCameraThenCountdown());
    }

    void Update()
    {
        if (!gameRunning || gameEnded) return;

        timer += Time.deltaTime;

        if (timer >= spawnInterval)
        {
            SpawnRandomMole();
            timer = 0f;
        }

        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            UpdateFromMediaPipe(Mouse.current.position.ReadValue(), true);
        }
    }

    IEnumerator WaitCameraThenCountdown()
    {
        gameRunning = false;

        while (handDataSource != null && !handDataSource.HasLatestResult)
        {
            if (countdownText != null)
            {
                countdownText.text = "準備中";
                countdownText.transform.localScale = Vector3.one * 0.9f;
            }

            yield return null;
        }

        yield return StartCountdown();
    }

    IEnumerator StartCountdown()
    {
        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(true);
            countdownText.transform.localScale = Vector3.one * 2.5f;

            countdownText.text = "3";
            yield return new WaitForSeconds(1f);

            countdownText.text = "2";
            yield return new WaitForSeconds(1f);

            countdownText.text = "1";
            yield return new WaitForSeconds(1f);

            countdownText.text = "";
            countdownText.gameObject.SetActive(false);
        }
        else
        {
            yield return new WaitForSeconds(3f);
        }

        timer = 0f;
        StartAssessment();
        gameRunning = true;
    }

    void StartAssessment()
    {
        randomSeed = Random.Range(int.MinValue, int.MaxValue);
        gameStartTime = Time.time;
        trialIndex = 0;
        assessmentSessionId = CognitiveAssessmentService.BeginGame(
            "gopher_reaction",
            CognitiveProtocolRegistry.ProtocolVersion,
            inputMethod: "hand_tracking_or_mouse");
    }

    void SpawnRandomMole()
    {
        if (moles == null || moles.Length == 0) return;

        for (int i = 0; i < moles.Length; i++)
        {
            int randomIndex = Random.Range(0, moles.Length);
            Mole mole = moles[randomIndex];

            if (mole != null && !mole.isUp)
            {
                bool spawnBomb = Random.value < bombSpawnChance;
                mole.Pop(moleVisibleTime, spawnBomb);
                return;
            }
        }
    }

    public void UpdateFromMediaPipe(Vector2 screenPos, bool isGrabbing)
    {
        if (!gameRunning || gameEnded) return;
        if (!isGrabbing) return;

        if (Time.time - lastMediaPipeHitTime < mediaPipeHitCooldown)
        {
            return;
        }

        shotCount++;

        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        RaycastHit hit;

        Debug.DrawRay(ray.origin, ray.direction * 100, Color.yellow, 0.5f);

        bool recordedShot = false;
        if (Physics.Raycast(ray, out hit))
        {
            Mole m = hit.collider.GetComponent<Mole>();

            if (m == null)
            {
                m = hit.collider.GetComponentInParent<Mole>();
            }

            if (m != null && m.isUp)
            {
                if (m.IsBomb)
                {
                    m.OnHit();
                    bombHitCount++;
                    RecordGopherTrial("bomb", "gopher", TrialOutcome.Incorrect, "bomb_hit", false);
                }
                else
                {
                    m.OnHit();

                    AddScore(scorePerHit);
                    hitCount++;
                    RecordGopherTrial("gopher", "gopher", TrialOutcome.Correct, "", false);

                    if (score >= targetScore)
                    {
                        EndGame();
                    }
                }

                lastMediaPipeHitTime = Time.time;
                recordedShot = true;
            }
        }

        if (!recordedShot)
        {
            RecordGopherTrial("empty", "gopher", TrialOutcome.Incorrect, "empty_hit", false);
        }
    }

    public void OnMoleMissed(Mole mole)
    {
        if (!gameRunning || gameEnded) return;
        if (mole == null) return;

        if (!mole.IsBomb)
        {
            missCount++;
            RecordGopherTrial("gopher", "gopher", TrialOutcome.Omitted, "missed_visible_mole", true);
        }
    }

    void AddScore(int amount)
    {
        score += amount;
        UpdateScoreUI();
    }

    void UpdateScoreUI()
    {
        if (ScoreTex != null)
        {
            ScoreTex.text = score.ToString();
        }
    }

    void EndGame()
    {
        gameEnded = true;
        gameRunning = false;

        HideAllMoles();

        earnedCoins = hitCount * coinPerHit + clearBonusCoins;
        CoinData.AddCoins(earnedCoins);

        float accuracy = shotCount > 0
            ? (float)hitCount / shotCount * 100f
            : 0f;
        CognitiveAssessmentService.CompleteGame(
            assessmentSessionId,
            CognitiveDomain.ProcessingSpeedVisualSearch,
            0f,
            score);

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
    "命中地鼠：" + hitCount + "\n" +
    "漏掉地鼠：" + missCount + "\n" +
    "誤抓炸彈：" + bombHitCount + "\n" +
    "出手次數：" + shotCount + "\n" +
    "命中率：" + accuracy.ToString("F0") + "%";
        }
    }

    void HideAllMoles()
    {
        if (moles == null) return;

        for (int i = 0; i < moles.Length; i++)
        {
            if (moles[i] != null)
            {
                moles[i].Hide();
            }
        }
    }

    void RecordGopherTrial(string stimulus, string expectedAnswer, TrialOutcome outcome, string errorType, bool timedOut)
    {
        if (string.IsNullOrEmpty(assessmentSessionId)) return;

        float previousActionTime = lastMediaPipeHitTime > 0f ? lastMediaPipeHitTime : gameStartTime;
        int reactionTimeMs = timedOut
            ? Mathf.RoundToInt(moleVisibleTime * 1000f)
            : Mathf.RoundToInt(Mathf.Clamp(Time.time - previousActionTime, 0.1f, 10f) * 1000f);

        trialIndex++;
        CognitiveAssessmentService.RecordTrial(assessmentSessionId, new CognitiveTrialRecord
        {
            trialIndex = trialIndex,
            roundIndex = trialIndex,
            stepIndex = 1,
            eventKind = "response",
            randomSeed = randomSeed,
            difficulty = Mathf.Max(1, Mathf.RoundToInt(1f / Mathf.Max(0.1f, spawnInterval))),
            stimulusCount = moles != null ? moles.Length : 0,
            condition = "reaction_hit",
            stimulus = stimulus + "|score=" + score + "|shots=" + shotCount,
            expectedAnswer = expectedAnswer,
            userAnswer = timedOut ? "" : stimulus,
            outcome = outcome,
            reactionTimeMs = reactionTimeMs,
            timedOut = timedOut,
            errorType = errorType
        });
    }
}
