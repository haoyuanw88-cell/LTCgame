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
        const string DefaultApiBaseUrl = "https://staging-hello-8shi.encr.app";
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

                if (!HasValidReservation(session))
                {
                    StartAssessmentResponse reservation = null;
                    yield return ReserveAssessmentSession(session, value => reservation = value);
                    if (reservation == null) break;

                    session.uploadSessionId = reservation.sessionId;
                    session.uploadSessionToken = reservation.sessionToken;
                    session.uploadSessionExpiresAtUtc = reservation.expiresAtUtc;
                    try { File.WriteAllText(path, JsonUtility.ToJson(session, true)); }
                    catch (Exception exception)
                    {
                        Debug.LogWarning("無法保存雲端測驗編號，稍後會重試：" + exception.Message);
                        break;
                    }
                }

                bool uploaded = false;
                yield return PostJson("/api/v1/assessments", BuildAssessmentJson(session), ok => uploaded = ok);
                if (!uploaded) break;

                try { File.Delete(path); }
                catch (Exception exception) { Debug.LogWarning("資料已同步，但無法刪除待傳檔案：" + exception.Message); }
                pendingFiles.Dequeue();
                Debug.Log("認知測驗已同步至資料庫：" + session.uploadSessionId);
            }
            isProcessing = false;
        }

        IEnumerator ReserveAssessmentSession(CognitiveAssessmentSession session, Action<StartAssessmentResponse> completed)
        {
            string json = JsonUtility.ToJson(new StartAssessmentRequest { gameCode = GameCode(session.gameId) });
            byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
            using (var request = new UnityWebRequest(ApiBaseUrl + "/api/v1/assessments/start", UnityWebRequest.kHttpVerbPOST))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + PlayerIdentityService.Current.AccessToken);
                request.timeout = 10;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("無法取得雲端測驗編號，資料仍保留在本機：" + request.error);
                    completed(null);
                    yield break;
                }

                StartAssessmentResponse response = null;
                try { response = JsonUtility.FromJson<StartAssessmentResponse>(request.downloadHandler.text); }
                catch (Exception exception) { Debug.LogWarning("雲端測驗編號格式錯誤：" + exception.Message); }
                if (response == null || !IsShortSessionId(response.sessionId) || string.IsNullOrWhiteSpace(response.sessionToken))
                    response = null;
                completed(response);
            }
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
                sessionId = session.uploadSessionId,
                sessionToken = session.uploadSessionToken,
                gameCode = GameCode(session.gameId),
                startedAtUtc = ToIsoUtc(session.startedAtUnixMs),
                endedAtUtc = ToIsoUtc(session.endedAtUnixMs),
                completionStatus = session.completed ? "completed" : "aborted"
            };

            foreach (var trial in session.trials ?? new List<CognitiveTrialRecord>())
            {
                request.trials.Add(new TrialRequest
                {
                    trialIndex = trial.trialIndex,
                    trialType = ConditionCode(string.IsNullOrEmpty(trial.condition) ? trial.eventKind : trial.condition),
                    expectedResponse = trial.expectedAnswer,
                    actualResponse = trial.userAnswer,
                    reactionTimeMs = ClampToInt(trial.reactionTimeMs)
                });
            }

            if (session.result != null)
            {
                string domainCode = DomainCode(session.result.primaryDomain);
                foreach (var metric in session.result.metrics ?? new List<CognitiveMetric>())
                {
                    string metricCode = MetricCode(metric.key);
                    if (string.IsNullOrEmpty(metricCode)) continue;
                    request.metrics.Add(new MetricRequest
                    {
                        metricCode = metricCode,
                        value = metric.value,
                        domainCode = domainCode,
                        qualityFlag = session.result.eligibleForTrend ? "valid" : "review"
                    });
                }
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
                case CognitiveDomain.AttentionInhibitoryControl: return "ATT";
                case CognitiveDomain.ProcessingSpeedVisualSearch: return "SPD";
                case CognitiveDomain.ExecutiveFunctionNumericalReasoning: return "EXE";
                case CognitiveDomain.WorkingMemory: return "VWM";
                case CognitiveDomain.VisuospatialAbility: return "VSP";
                case CognitiveDomain.EpisodicMemory: return "EPM";
                case CognitiveDomain.Language: return "LNG";
                case CognitiveDomain.Orientation: return "ORI";
                default: return "UNK";
            }
        }

        static string GameCode(string value)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "stroop_color_match": case "stroop_color": case "stp": return "STP";
                case "number_order": case "trail_making": case "ord": return "ORD";
                case "number_sum": case "sum": return "SUM";
                case "pipe_connection": case "pipe_puzzle": case "pip": return "PIP";
                case "card_memory_battle": case "memory_cards": case "crd": return "CRD";
                case "gopher_reaction": case "body_whack_a_mole": case "gop": return "GOP";
                case "supermarket_shopping": case "supermarket": case "sup": return "SUP";
                case "true_false_life_quiz": case "life_quiz": case "qiz": return "QIZ";
                default: return string.Empty;
            }
        }

        static string ConditionCode(string value)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "match_low_conflict": case "mlc": return "MLC";
                case "mismatch_low_conflict": case "xlc": return "XLC";
                case "match_high_conflict": case "mhc": return "MHC";
                case "mismatch_high_conflict": case "xhc": return "XHC";
                case "positive_only": case "pos": return "POS";
                case "positive_and_negative": case "pan": return "PAN";
                case "target_sum": case "tsm": return "TSM";
                case "response": case "rsp": return "RSP";
                case "round_summary": case "rnd": return "RND";
                case "selection": case "sel": return "SEL";
                default: return "UNK";
            }
        }

        static string MetricCode(string value)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "valid_trial_count": case "vtc": return "VTC";
                case "excluded_trial_rate": case "exr": return "EXR";
                case "completion_rate": case "cpr": return "CPR";
                case "accuracy": case "acc": return "ACC";
                case "omission_rate": case "omr": return "OMR";
                case "median_correct_rt": case "mrt": return "MRT";
                case "rt_mad": case "mad": return "MAD";
                case "robust_rt_variability": case "rtv": return "RTV";
                case "inverse_efficiency": case "ies": return "IES";
                case "task_performance_index": case "tpi": return "TPI";
                case "low_interference_median_rt": case "lrt": return "LRT";
                case "high_interference_median_rt": case "hrt": return "HRT";
                case "stroop_rt_interference": case "sri": return "SRI";
                case "interference_ratio": case "ir": return "IR";
                case "stroop_error_interference": case "sei": return "SEI";
                case "trail_total_completion_time": case "tct": return "TCT";
                case "trail_sequence_error_count": case "sec": return "SEC";
                case "trail_completed_round_count": case "crc": return "CRC";
                case "trail_round_completion_rate": case "rcr": return "RCR";
                case "planning_optimal_solution_rate": case "osr": return "OSR";
                case "planning_median_excess_moves": case "exm": return "EXM";
                case "planning_median_initial_thinking_time": case "pit": return "PIT";
                case "planning_median_execution_time": case "ext": return "EXT";
                case "planning_rule_violation_count": case "rvc": return "RVC";
                case "planning_round_completion_rate": case "pcr": return "PCR";
                default: return string.Empty;
            }
        }

        static bool IsShortSessionId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 9 || value[0] != 'S') return false;
            for (int i = 1; i < value.Length; i++)
                if (value[i] < '0' || value[i] > '9') return false;
            return true;
        }

        static bool HasValidReservation(CognitiveAssessmentSession session)
        {
            if (session == null || !IsShortSessionId(session.uploadSessionId) ||
                string.IsNullOrWhiteSpace(session.uploadSessionToken)) return false;
            DateTime expiresAt;
            return DateTime.TryParse(session.uploadSessionExpiresAtUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out expiresAt) &&
                expiresAt.ToUniversalTime() > DateTime.UtcNow.AddMinutes(1);
        }

        [Serializable] sealed class StartAssessmentRequest { public string gameCode; }
        [Serializable] sealed class StartAssessmentResponse
        {
            public string sessionId; public string sessionToken; public string expiresAtUtc;
        }
        [Serializable] sealed class TrialRequest
        {
            public int trialIndex; public string trialType;
            public string expectedResponse; public string actualResponse; public int reactionTimeMs;
        }
        [Serializable] sealed class MetricRequest
        {
            public string metricCode; public float value; public string domainCode; public string qualityFlag;
        }
        [Serializable] sealed class AssessmentRequest
        {
			public string sessionId; public string sessionToken; public string gameCode;
			public string startedAtUtc; public string endedAtUtc;
            public string completionStatus;
            public List<TrialRequest> trials = new List<TrialRequest>();
            public List<MetricRequest> metrics = new List<MetricRequest>();
        }
    }
}
