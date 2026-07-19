using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace LTCCognitiveAssessment
{
    public interface ICognitiveAssessmentRepository
    {
        void Save(CognitiveAssessmentSession session);
        List<CognitiveAssessmentSession> LoadRecent(string anonymousUserId, int limit);
    }

    public sealed class LocalJsonCognitiveRepository : ICognitiveAssessmentRepository
    {
        private readonly string rootPath;

        public LocalJsonCognitiveRepository()
        {
            rootPath = Path.Combine(Application.persistentDataPath, "CognitiveAssessment", "sessions");
        }

        public void Save(CognitiveAssessmentSession session)
        {
            Directory.CreateDirectory(rootPath);
            string safeUserId = Sanitize(session.anonymousUserId);
            string fileName = safeUserId + "_" + session.startedAtUnixMs + "_" + session.sessionId + ".json";
            File.WriteAllText(Path.Combine(rootPath, fileName), JsonUtility.ToJson(session, true));
        }

        public List<CognitiveAssessmentSession> LoadRecent(string anonymousUserId, int limit)
        {
            var sessions = new List<CognitiveAssessmentSession>();
            if (!Directory.Exists(rootPath))
            {
                return sessions;
            }

            string prefix = Sanitize(anonymousUserId) + "_";
            foreach (string path in Directory.GetFiles(rootPath, prefix + "*.json")
                         .OrderByDescending(File.GetLastWriteTimeUtc)
                         .Take(Mathf.Max(1, limit)))
            {
                try
                {
                    var session = JsonUtility.FromJson<CognitiveAssessmentSession>(File.ReadAllText(path));
                    if (session != null)
                    {
                        sessions.Add(session);
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("略過無法讀取的認知紀錄：" + exception.Message);
                }
            }

            return sessions;
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "guest";
            }

            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value;
        }
    }

    public static class CognitiveAssessmentService
    {
        private static readonly Dictionary<string, CognitiveAssessmentSession> ActiveSessions =
            new Dictionary<string, CognitiveAssessmentSession>();

        private static ICognitiveAssessmentRepository repository = new LocalJsonCognitiveRepository();

        public static void SetRepository(ICognitiveAssessmentRepository newRepository)
        {
            repository = newRepository ?? throw new ArgumentNullException(nameof(newRepository));
        }

        public static string BeginGame(string gameId, string taskVersion, string anonymousUserId = null,
            string inputMethod = "touch_or_mouse")
        {
            string sessionId = Guid.NewGuid().ToString("N");
            var session = new CognitiveAssessmentSession
            {
                sessionId = sessionId,
                anonymousUserId = ResolveUserId(anonymousUserId),
                gameId = gameId,
                taskVersion = taskVersion,
                appVersion = Application.version,
                deviceModel = SystemInfo.deviceModel,
                operatingSystem = SystemInfo.operatingSystem,
                inputMethod = inputMethod,
                startedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            ActiveSessions[sessionId] = session;
            return sessionId;
        }

        public static void RecordTrial(string sessionId, CognitiveTrialRecord trial)
        {
            if (!ActiveSessions.TryGetValue(sessionId, out CognitiveAssessmentSession session))
            {
                Debug.LogWarning("找不到認知測試 Session：" + sessionId);
                return;
            }

            trial.sessionId = sessionId;
            trial.gameId = session.gameId;
            trial.taskVersion = session.taskVersion;
            trial.occurredAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (trial.frameRate <= 0f)
            {
                trial.frameRate = 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            }
            if (string.IsNullOrEmpty(trial.inputMethod))
            {
                trial.inputMethod = session.inputMethod;
            }
            session.trials.Add(trial);
        }

        public static CognitiveGameResult CompleteGame(string sessionId, CognitiveDomain primaryDomain,
            float conditionEffectMs = 0f, float difficultyReached = 0f)
        {
            if (!ActiveSessions.TryGetValue(sessionId, out CognitiveAssessmentSession session))
            {
                throw new InvalidOperationException("找不到認知測試 Session：" + sessionId);
            }

            CognitiveGameResult result = CognitiveScoring.BuildGameResult(
                session.gameId, primaryDomain, session.trials, conditionEffectMs, difficultyReached);
            session.result = result;
            session.completed = true;
            session.endedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            repository.Save(session);
            ActiveSessions.Remove(sessionId);
            Debug.Log("認知測試資料已儲存：" + session.sessionId);
            return result;
        }

        public static CognitiveProfile BuildProfile(string anonymousUserId = null, int recentSessionLimit = 30)
        {
            string userId = ResolveUserId(anonymousUserId);
            return CognitiveScoring.BuildProfile(userId, repository.LoadRecent(userId, recentSessionLimit));
        }

        public static string CurrentUserId => ResolveUserId(null);

        private static string ResolveUserId(string userId)
        {
            if (!string.IsNullOrWhiteSpace(userId))
            {
                return userId.Trim();
            }

            string savedName = PlayerPrefs.GetString("SavedPlayerName",
                PlayerPrefs.GetString("AccountName", "guest"));
            return string.IsNullOrWhiteSpace(savedName) ? "guest" : savedName.Trim();
        }
    }

    public static class CognitiveScoring
    {
        public static CognitiveGameResult BuildGameResult(string gameId, CognitiveDomain domain,
            List<CognitiveTrialRecord> trials, float conditionEffectMs, float difficultyReached)
        {
            trials = trials ?? new List<CognitiveTrialRecord>();
            int correct = trials.Count(t => t.outcome == TrialOutcome.Correct);
            int incorrect = trials.Count(t => t.outcome == TrialOutcome.Incorrect);
            int omitted = trials.Count(t => t.outcome == TrialOutcome.Omitted);
            int answered = correct + incorrect;
            float accuracy = answered > 0 ? (float)correct / answered : 0f;
            var correctTimes = trials
                .Where(t => t.outcome == TrialOutcome.Correct && t.reactionTimeMs > 0)
                .Select(t => (float)t.reactionTimeMs)
                .OrderBy(value => value)
                .ToList();
            float median = Median(correctTimes);
            float variability = StandardDeviation(correctTimes);

            float accuracyScore = accuracy * 100f;
            float speedScore = median <= 0f ? 0f : Mathf.Clamp(100f - (median - 500f) / 25f, 0f, 100f);
            float stabilityScore = correctTimes.Count < 3
                ? 0f
                : Mathf.Clamp(100f - variability / 15f, 0f, 100f);
            float score = accuracyScore * 0.5f + speedScore * 0.3f + stabilityScore * 0.2f;
            bool qualityPassed = trials.Count >= 5 && correctTimes.Count >= 3;

            return new CognitiveGameResult
            {
                gameId = gameId,
                primaryDomain = domain,
                trialCount = trials.Count,
                correctCount = correct,
                incorrectCount = incorrect,
                omissionCount = omitted,
                accuracy = accuracy,
                medianCorrectReactionTimeMs = median,
                reactionTimeVariabilityMs = variability,
                conditionEffectMs = conditionEffectMs,
                difficultyReached = difficultyReached,
                performanceScore = Mathf.Round(score * 10f) / 10f,
                dataQualityPassed = qualityPassed,
                dataQualityNote = qualityPassed ? "資料品質良好" : "有效題數不足，分數僅供參考"
            };
        }

        public static CognitiveProfile BuildProfile(string userId, List<CognitiveAssessmentSession> sessions)
        {
            var profile = new CognitiveProfile
            {
                anonymousUserId = userId,
                generatedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            foreach (CognitiveDomain domain in Enum.GetValues(typeof(CognitiveDomain)))
            {
                var valid = sessions
                    .Where(s => s.completed && s.result != null && s.result.primaryDomain == domain &&
                                s.result.dataQualityPassed)
                    .Take(5)
                    .ToList();
                if (valid.Count == 0)
                {
                    continue;
                }

                float score = valid.Average(s => s.result.performanceScore);
                profile.domains.Add(new CognitiveDomainScore
                {
                    domain = domain,
                    score = Mathf.Round(score * 10f) / 10f,
                    contributingSessions = valid.Count,
                    interpretation = Interpret(score)
                });
            }

            return profile;
        }

        private static float Median(List<float> values)
        {
            if (values == null || values.Count == 0) return 0f;
            int middle = values.Count / 2;
            return values.Count % 2 == 0 ? (values[middle - 1] + values[middle]) * 0.5f : values[middle];
        }

        private static float StandardDeviation(List<float> values)
        {
            if (values == null || values.Count < 2) return 0f;
            float average = values.Average();
            float variance = values.Sum(value => (value - average) * (value - average)) / values.Count;
            return Mathf.Sqrt(variance);
        }

        private static string Interpret(float score)
        {
            if (score >= 80f) return "本次遊戲表現穩定";
            if (score >= 60f) return "本次遊戲表現位於中間範圍";
            return "本次表現較低，建議在精神狀況良好時再次測量並觀察趨勢";
        }
    }
}
