using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using LTC.Identity;

namespace LTCCognitiveAssessment
{
    /// <summary>
    /// Keeps completed assessments locally and synchronizes them to the LTC API.
    /// Pending files remain on disk until the server confirms successful storage.
    /// </summary>
    public sealed class CognitiveApiUploader : MonoBehaviour
    {
        public const string ApiBaseUrlPlayerPrefsKey = "LTC_CognitiveApiBaseUrl";
        const string DefaultApiBaseUrl = "http://localhost:5077";
        static CognitiveApiUploader instance;
        readonly Queue<string> pendingFiles = new Queue<string>();
        bool isProcessing;
        IPlayerIdentityProvider identityProvider;
        string PendingDirectory => Path.Combine(Application.persistentDataPath, "CognitiveAssessment", "pending_uploads");
        string ApiBaseUrl => PlayerPrefs.GetString(ApiBaseUrlPlayerPrefsKey, DefaultApiBaseUrl).TrimEnd('/');

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            EnsureInstance();
        }

        static CognitiveApiUploader EnsureInstance()
        {
            if (instance != null) return instance;
            var host = new GameObject("Cognitive API Uploader");
            DontDestroyOnLoad(host);
            instance = host.AddComponent<CognitiveApiUploader>();
            return instance;
        }

        public static void Enqueue(CognitiveAssessmentSession session)
        {
            if (session == null || string.IsNullOrWhiteSpace(session.sessionId)) return;
            var uploader = EnsureInstance();
            Directory.CreateDirectory(uploader.PendingDirectory);
            string path = Path.Combine(uploader.PendingDirectory, session.sessionId + ".json");
            File.WriteAllText(path, JsonUtility.ToJson(session, true));
            uploader.QueueFile(path);
        }

        public static void SetApiBaseUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) PlayerPrefs.DeleteKey(ApiBaseUrlPlayerPrefsKey);
            else PlayerPrefs.SetString(ApiBaseUrlPlayerPrefsKey, url.TrimEnd('/'));
            PlayerPrefs.Save();
        }

void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            identityProvider = PlayerIdentityService.Current;
            identityProvider.IdentityChanged += StartProcessing;
            LoadPendingFiles();
        }

void OnDestroy()
        {
            if (identityProvider != null)
                identityProvider.IdentityChanged -= StartProcessing;
            identityProvider = null;
        }


        void LoadPendingFiles()
        {
            Directory.CreateDirectory(PendingDirectory);
            foreach (string path in Directory.GetFiles(PendingDirectory, "*.json"))
                pendingFiles.Enqueue(path);
            StartProcessing();
        }

        void QueueFile(string path)
        {
            if (!pendingFiles.Contains(path)) pendingFiles.Enqueue(path);
            StartProcessing();
        }

        void StartProcessing()
        {
            if (!isProcessing && pendingFiles.Count > 0)
                StartCoroutine(ProcessQueue());
        }

        IEnumerator ProcessQueue()
        {
            isProcessing = true;
            var identity = PlayerIdentityService.Current;
            if (!identity.IsReady)
            {
                isProcessing = false;
                yield break;
            }
            while (pendingFiles.Count > 0)
            {
                string path = pendingFiles.Peek();
                CognitiveAssessmentSession session = null;
                try { session = JsonUtility.FromJson<CognitiveAssessmentSession>(File.ReadAllText(path)); }
                catch (Exception exception)
                {
                    Debug.LogWarning("無法讀取待同步認知資料：" + exception.Message);
                    pendingFiles.Dequeue();
                    continue;
                }

                if (session == null)
                {
                    pendingFiles.Dequeue();
                    continue;
                }

                bool uploaded = false;
                yield return PostJson("/api/v1/assessments/", BuildAssessmentJson(session), ok => uploaded = ok);
                if (!uploaded) break;

                try { File.Delete(path); }
                catch (Exception exception) { Debug.LogWarning("資料已同步，但無法刪除待傳檔案：" + exception.Message); }
                pendingFiles.Dequeue();
                Debug.Log("認知測驗已同步至資料庫：" + session.sessionId);
            }
            isProcessing = false;
        }

        IEnumerator PostJson(string route, string json, Action<bool> completed)
        {
            byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
            using (var request = new UnityWebRequest(ApiBaseUrl + route, UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + PlayerIdentityService.Current.AccessToken);
                request.timeout = 10;
                yield return request.SendWebRequest();
                bool ok = request.result == UnityWebRequest.Result.Success;
                if (!ok)
                    Debug.LogWarning("認知資料暫存於本機，API 尚未同步：" + request.error);
                completed(ok);
            }
        }

        static string BuildAssessmentJson(CognitiveAssessmentSession session)
        {
            var request = new AssessmentRequest
            {
                sessionId = session.sessionId,
                gameCode = session.gameId,
                taskVersion = session.taskVersion,
                schemaVersion = session.schemaVersion,
                startedAtUtc = ToIsoUtc(session.startedAtUnixMs),
                endedAtUtc = ToIsoUtc(session.endedAtUnixMs),
                completionStatus = session.completed ? "completed" : "aborted"
            };

            foreach (var trial in session.trials ?? new List<CognitiveTrialRecord>())
            {
                request.trials.Add(new TrialRequest
                {
                    trialIndex = trial.trialIndex,
                    trialType = string.IsNullOrEmpty(trial.condition) ? trial.eventKind : trial.condition,
                    stimulusJson = JsonUtility.ToJson(new StimulusRequest
                    {
                        stimulus = trial.stimulus,
                        condition = trial.condition,
                        difficulty = trial.difficulty,
                        stimulusCount = trial.stimulusCount,
                        roundIndex = trial.roundIndex,
                        stepIndex = trial.stepIndex,
                        randomSeed = trial.randomSeed,
                        eventKind = trial.eventKind,
                        outcome = trial.outcome.ToString(),
                        errorType = trial.errorType,
                        exclusionReason = trial.exclusionReason,
                        isPractice = trial.isPractice,
                        timedOut = trial.timedOut,
                        frameRate = trial.frameRate,
                        inputMethod = trial.inputMethod,
                        initialPlanningTimeMs = ClampToInt(trial.initialPlanningTimeMs),
                        minimumActionCount = trial.minimumActionCount,
                        actionCount = trial.actionCount,
                        errorCount = trial.errorCount
                    }),
                    expectedResponse = trial.expectedAnswer,
                    actualResponse = trial.userAnswer,
                    isCorrect = trial.outcome == TrialOutcome.Correct,
                    reactionTimeMs = ClampToInt(trial.reactionTimeMs),
                    presentationDurationMs = 0
                });
            }

            if (session.result != null)
            {
                string domainCode = DomainCode(session.result.primaryDomain);
                foreach (var metric in session.result.metrics ?? new List<CognitiveMetric>())
                    request.metrics.Add(new MetricRequest
                    {
                        metricCode = metric.key,
                        value = metric.value,
                        unit = metric.unit,
                        calculationVersion = string.IsNullOrEmpty(session.result.scoringVersion)
                            ? CognitiveProtocolRegistry.ScoringVersion
                            : session.result.scoringVersion,
                        domainCode = domainCode,
                        qualityFlag = session.result.eligibleForTrend ? "valid" : "review"
                    });
            }
            return JsonUtility.ToJson(request);
        }

        static string ToIsoUtc(long unixMs)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(unixMs).UtcDateTime.ToString("O");
        }

        static int ClampToInt(long value)
        {
            return (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, value));
        }

        static string DomainCode(CognitiveDomain domain)
        {
            switch (domain)
            {
                case CognitiveDomain.AttentionInhibitoryControl: return "attention_inhibition";
                case CognitiveDomain.ProcessingSpeedVisualSearch: return "processing_speed";
                case CognitiveDomain.ExecutiveFunctionNumericalReasoning: return "executive_reasoning";
                case CognitiveDomain.WorkingMemory: return "visual_working_memory";
                case CognitiveDomain.VisuospatialAbility: return "visuospatial_planning";
                case CognitiveDomain.EpisodicMemory: return "episodic_memory";
                case CognitiveDomain.Language: return "language";
                case CognitiveDomain.Orientation: return "orientation";
                default: return null;
            }
        }

        [Serializable] sealed class StimulusRequest
        {
            public string stimulus; public string condition; public int difficulty; public int stimulusCount;
            public int roundIndex; public int stepIndex; public int randomSeed;
            public string eventKind; public string outcome; public string errorType; public string exclusionReason;
            public bool isPractice; public bool timedOut; public float frameRate; public string inputMethod;
            public int initialPlanningTimeMs; public int minimumActionCount; public int actionCount; public int errorCount;
        }
        [Serializable] sealed class TrialRequest
        {
            public int trialIndex; public string trialType; public string stimulusJson;
            public string expectedResponse; public string actualResponse; public bool isCorrect;
            public int reactionTimeMs; public int presentationDurationMs;
        }
        [Serializable] sealed class MetricRequest
        {
            public string metricCode; public float value; public string unit; public string calculationVersion;
            public string domainCode; public string qualityFlag;
        }
        [Serializable] sealed class AssessmentRequest
        {
            public string sessionId; public string gameCode;
            public string taskVersion; public string schemaVersion; public string startedAtUtc; public string endedAtUtc;
            public string completionStatus;
            public List<TrialRequest> trials = new List<TrialRequest>();
            public List<MetricRequest> metrics = new List<MetricRequest>();
        }
    }
}
