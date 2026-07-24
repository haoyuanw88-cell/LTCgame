using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LTC.Identity;
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
        readonly string rootPath = Path.Combine(Application.persistentDataPath, "CognitiveAssessment", "sessions");

        public void Save(CognitiveAssessmentSession session)
        {
            Directory.CreateDirectory(rootPath);
            string fileName = Sanitize(session.anonymousUserId) + "_" + session.startedAtUnixMs + "_" + session.sessionId + ".json";
            File.WriteAllText(Path.Combine(rootPath, fileName), JsonUtility.ToJson(session, true));
        }

        public List<CognitiveAssessmentSession> LoadRecent(string userId, int limit)
        {
            var result = new List<CognitiveAssessmentSession>();
            if (!Directory.Exists(rootPath)) return result;
            foreach (string path in Directory.GetFiles(rootPath, Sanitize(userId) + "_*.json")
                         .OrderByDescending(File.GetLastWriteTimeUtc).Take(Mathf.Max(1, limit)))
            {
                try
                {
                    var session = JsonUtility.FromJson<CognitiveAssessmentSession>(File.ReadAllText(path));
                    if (session != null) result.Add(session);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("無法讀取認知評估紀錄：" + exception.Message);
                }
            }
            return result;
        }

        static string Sanitize(string value)
        {
            value = string.IsNullOrWhiteSpace(value) ? "guest" : value;
            foreach (char character in Path.GetInvalidFileNameChars()) value = value.Replace(character, '_');
            return value;
        }
    }

    public sealed class CognitiveAssessmentProtocol
    {
        public string GameId;
        public string ProtocolId;
        public string TaskVersion;
        public bool UsesRoundSummaries;
        public int MinimumValidTrials;
        public int MinimumTrialsPerCondition;
        public long MinimumReactionTimeMs;
        public long MaximumReactionTimeMs;
        public long MinimumSessionDurationMs;
        public float MaximumExclusionRate = .20f;
        public float MinimumFrameRate = 20f;
    }

    public static class CognitiveProtocolRegistry
    {
        public const string ProtocolVersion = "3.0.0";
        public const string ScoringVersion = "3.0.0";

        static readonly Dictionary<string, CognitiveAssessmentProtocol> Protocols =
            new Dictionary<string, CognitiveAssessmentProtocol>(StringComparer.Ordinal)
            {
                ["stroop_color_match"] = new CognitiveAssessmentProtocol
                {
                    GameId = "stroop_color_match",
                    ProtocolId = "LTC-ATT-STROOP-MATCH-01",
                    TaskVersion = ProtocolVersion,
                    UsesRoundSummaries = false,
                    MinimumValidTrials = 12,
                    MinimumTrialsPerCondition = 4,
                    MinimumReactionTimeMs = 200,
                    MaximumReactionTimeMs = 10000,
                    MinimumSessionDurationMs = 45000
                },
                ["number_order"] = new CognitiveAssessmentProtocol
                {
                    GameId = "number_order",
                    ProtocolId = "LTC-PS-NUMORDER-01",
                    TaskVersion = ProtocolVersion,
                    UsesRoundSummaries = true,
                    MinimumValidTrials = 3,
                    MinimumReactionTimeMs = 500,
                    MaximumReactionTimeMs = 120000,
                    MinimumSessionDurationMs = 45000
                },
                ["number_sum"] = new CognitiveAssessmentProtocol
                {
                    GameId = "number_sum",
                    ProtocolId = "LTC-EF-NUMSUM-01",
                    TaskVersion = ProtocolVersion,
                    UsesRoundSummaries = true,
                    MinimumValidTrials = 3,
                    MinimumReactionTimeMs = 500,
                    MaximumReactionTimeMs = 120000,
                    MinimumSessionDurationMs = 45000
                }
            };

        public static CognitiveAssessmentProtocol Get(string gameId)
        {
            if (gameId != null && Protocols.TryGetValue(gameId, out var protocol)) return protocol;
            return new CognitiveAssessmentProtocol
            {
                GameId = gameId ?? "unknown",
                ProtocolId = "LTC-PILOT-UNREGISTERED",
                TaskVersion = ProtocolVersion,
                MinimumValidTrials = 8,
                MinimumReactionTimeMs = 200,
                MaximumReactionTimeMs = 10000,
                MinimumSessionDurationMs = 30000
            };
        }
    }

    public static class CognitiveAssessmentService
    {
        static readonly Dictionary<string, CognitiveAssessmentSession> ActiveSessions = new Dictionary<string, CognitiveAssessmentSession>();
        static ICognitiveAssessmentRepository repository = new LocalJsonCognitiveRepository();

        public static void SetRepository(ICognitiveAssessmentRepository value)
        {
            repository = value ?? throw new ArgumentNullException(nameof(value));
        }

        public static string BeginGame(string gameId, string taskVersion, string anonymousUserId = null,
            string inputMethod = "touch_or_mouse")
        {
            string id = Guid.NewGuid().ToString("N");
            var protocol = CognitiveProtocolRegistry.Get(gameId);
            if (!string.Equals(taskVersion, protocol.TaskVersion, StringComparison.Ordinal))
                Debug.LogWarning($"{gameId} 的任務版本 {taskVersion} 與評估協定 {protocol.TaskVersion} 不一致，本次只保存為描述性資料。");

            ActiveSessions[id] = new CognitiveAssessmentSession
            {
                sessionId = id,
                anonymousUserId = ResolveUserId(anonymousUserId),
                gameId = gameId,
                taskVersion = taskVersion,
                protocolId = protocol.ProtocolId,
                protocolVersion = CognitiveProtocolRegistry.ProtocolVersion,
                scoringVersion = CognitiveProtocolRegistry.ScoringVersion,
                appVersion = Application.version,
                deviceModel = SystemInfo.deviceModel,
                operatingSystem = SystemInfo.operatingSystem,
                locale = System.Globalization.CultureInfo.CurrentCulture.Name,
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                screenDpi = Screen.dpi,
                inputMethod = inputMethod,
                startedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            return id;
        }

        public static void RecordTrial(string id, CognitiveTrialRecord trial)
        {
            if (!ActiveSessions.TryGetValue(id, out var session))
            {
                Debug.LogWarning("找不到認知評估 Session：" + id);
                return;
            }
            trial.sessionId = id;
            trial.gameId = session.gameId;
            trial.taskVersion = session.taskVersion;
            trial.occurredAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (trial.frameRate <= 0f) trial.frameRate = 1f / Mathf.Max(Time.unscaledDeltaTime, .0001f);
            if (string.IsNullOrEmpty(trial.inputMethod)) trial.inputMethod = session.inputMethod;
            session.trials.Add(trial);
        }

        public static CognitiveGameResult CompleteGame(string id, CognitiveDomain domain,
            float conditionEffectMs = 0f, float difficultyReached = 0f)
        {
            if (!ActiveSessions.TryGetValue(id, out var session))
                throw new InvalidOperationException("找不到認知評估 Session：" + id);
            session.completed = true;
            session.endedAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            session.result = CognitiveScoring.BuildGameResult(session, domain, conditionEffectMs, difficultyReached);
            repository.Save(session);
            CognitiveApiUploader.Enqueue(session);
            ActiveSessions.Remove(id);
            return session.result;
        }

        public static CognitiveProfile BuildProfile(string userId = null, int limit = 30)
        {
            string id = ResolveUserId(userId);
            return CognitiveScoring.BuildProfile(id, LoadCurrentUserHistory(id, limit));
        }

        public static float[] BuildDailyTrend(CognitiveDomain? domain = null, int days = 30)
        {
            days = Mathf.Clamp(days, 2, 365);
            string userId = ResolveUserId(null);
            var values = Enumerable.Repeat(float.NaN, days).ToArray();
            var sessions = LoadCurrentUserHistory(userId, 500);
            DateTime todayUtc = DateTime.UtcNow.Date;
            DateTime firstDayUtc = todayUtc.AddDays(-(days - 1));

            var matchingSessions = sessions
                .Where(session => session != null && session.completed && session.result != null &&
                                  session.endedAtUnixMs > 0 &&
                                  (!domain.HasValue || session.result.primaryDomain == domain.Value))
                .ToList();
            var currentProtocolSessions = matchingSessions
                .Where(CognitiveScoring.IsCurrentTrendEligible)
                .ToList();
            var displayedSessions = currentProtocolSessions.Count > 0
                ? currentProtocolSessions
                : matchingSessions.Where(CognitiveScoring.IsLegacyHistoryVisible).ToList();

            var dailyGroups = displayedSessions
                .Select(session => new
                {
                    Session = session,
                    Day = DateTimeOffset.FromUnixTimeMilliseconds(session.endedAtUnixMs).UtcDateTime.Date
                })
                .Where(item => item.Day >= firstDayUtc && item.Day <= todayUtc)
                .GroupBy(item => item.Day);

            foreach (var group in dailyGroups)
            {
                int index = (int)(group.Key - firstDayUtc).TotalDays;
                if (index >= 0 && index < values.Length)
                    values[index] = group.Average(item => item.Session.result.performanceScore);
            }
            return values;
        }

        public static string CurrentUserId => ResolveUserId(null);

        static List<CognitiveAssessmentSession> LoadCurrentUserHistory(string userId, int limit)
        {
            var sessions = repository.LoadRecent(userId, limit);

            // Early local builds stored this same player's records under the literal "guest" key.
            if (userId.StartsWith("guest-", StringComparison.Ordinal))
                sessions.AddRange(repository.LoadRecent("guest", limit));

            return sessions
                .Where(session => session != null)
                .GroupBy(session => session.sessionId ?? string.Empty)
                .Select(group => group.OrderByDescending(session => session.endedAtUnixMs).First())
                .OrderByDescending(session => session.endedAtUnixMs)
                .Take(Mathf.Max(1, limit))
                .ToList();
        }

        static string ResolveUserId(string id)
        {
            if (!string.IsNullOrWhiteSpace(id)) return id.Trim();
            if (!Application.isPlaying) return "editor-preview";
            return PlayerIdentityService.Current.StableLocalPlayerKey;
        }
    }

    public static class CognitiveScoring
    {
        public static CognitiveGameResult BuildGameResult(CognitiveAssessmentSession session, CognitiveDomain domain,
            float suppliedEffect, float difficulty)
        {
            var protocol = CognitiveProtocolRegistry.Get(session.gameId);
            var all = session.trials ?? new List<CognitiveTrialRecord>();
            var practice = all.Where(trial => trial.isPractice).ToList();
            bool roundScored = protocol.UsesRoundSummaries && all.Any(trial => trial.eventKind == "round_summary");
            var responses = all.Where(trial => !trial.isPractice)
                .Where(trial => roundScored
                    ? trial.eventKind == "round_summary"
                    : string.IsNullOrEmpty(trial.eventKind) || trial.eventKind == "response")
                .Where(trial => trial.outcome != TrialOutcome.ValidAction).ToList();

            foreach (var trial in responses)
            {
                if (trial.outcome != TrialOutcome.Omitted && trial.reactionTimeMs <= 0)
                    trial.exclusionReason = "missing_rt";
                else if (trial.reactionTimeMs > 0 && trial.reactionTimeMs < protocol.MinimumReactionTimeMs)
                    trial.exclusionReason = "anticipatory_rt";
                else if (trial.reactionTimeMs > protocol.MaximumReactionTimeMs)
                    trial.exclusionReason = "extreme_rt";
            }

            var valid = responses.Where(trial => string.IsNullOrEmpty(trial.exclusionReason)).ToList();
            int correct = valid.Count(trial => trial.outcome == TrialOutcome.Correct);
            int incorrect = valid.Count(trial => trial.outcome == TrialOutcome.Incorrect);
            int omitted = valid.Count(trial => trial.outcome == TrialOutcome.Omitted);
            int answered = correct + incorrect;
            float accuracy = answered > 0 ? (float)correct / answered : 0f;
            float completionRate = valid.Count > 0 ? (float)answered / valid.Count : 0f;
            var correctTimes = valid.Where(trial => trial.outcome == TrialOutcome.Correct && trial.reactionTimeMs > 0)
                .Select(trial => (float)trial.reactionTimeMs).OrderBy(value => value).ToList();
            float median = Median(correctTimes);
            float mad = Median(correctTimes.Select(value => Mathf.Abs(value - median)).OrderBy(value => value).ToList());

            var result = new CognitiveGameResult
            {
                gameId = session.gameId,
                protocolId = protocol.ProtocolId,
                protocolVersion = CognitiveProtocolRegistry.ProtocolVersion,
                scoringVersion = CognitiveProtocolRegistry.ScoringVersion,
                primaryDomain = domain,
                practiceTrialCount = practice.Count,
                trialCount = responses.Count,
                validResponseCount = valid.Count,
                excludedResponseCount = responses.Count - valid.Count,
                correctCount = correct,
                incorrectCount = incorrect,
                omissionCount = omitted,
                accuracy = accuracy,
                medianCorrectReactionTimeMs = median,
                reactionTimeVariabilityMs = mad * 1.4826f,
                medianAbsoluteDeviationMs = mad,
                inverseEfficiencyMs = accuracy >= .50f ? median / accuracy : 0f,
                difficultyReached = difficulty,
                completionRate = completionRate
            };

            AddMetric(result, "valid_trial_count", valid.Count, "count", "通過技術品質檢查的正式試次數");
            AddMetric(result, "excluded_trial_rate", responses.Count > 0 ? result.excludedResponseCount * 100f / responses.Count : 0f,
                "percent", "因反應時間或資料缺失排除的試次比例");
            AddMetric(result, "completion_rate", completionRate * 100f, "percent", "有實際作答的正式試次比例");
            AddMetric(result, "accuracy", accuracy * 100f, "percent", "有效且有作答試次的正確率");
            AddMetric(result, "omission_rate", valid.Count > 0 ? omitted * 100f / valid.Count : 0f, "percent", "正式試次漏答比例");
            AddMetric(result, "median_correct_rt", median, "ms", "正確反應時間中位數");
            AddMetric(result, "rt_mad", mad, "ms", "正確反應時間中位絕對偏差");
            AddMetric(result, "robust_rt_variability", result.reactionTimeVariabilityMs, "ms", "以 MAD 換算的穩健反應時間變異");
            if (accuracy >= .50f)
                AddMetric(result, "inverse_efficiency", result.inverseEfficiencyMs, "ms", "反應時間除以正確率，越低越好");

            if (session.gameId == "stroop_color_match") AddInterferenceMetrics(result, valid, suppliedEffect);
            if (session.gameId == "number_order") AddRoundMetrics(result, all, "search");
            if (session.gameId == "number_sum") AddRoundMetrics(result, all, "planning");

            ApplyQualityRules(session, protocol, responses, valid, result);
            float taskIndex = roundScored ? result.completionRate * 100f : (valid.Count > 0 ? correct * 100f / valid.Count : 0f);
            result.performanceScore = Mathf.Round(taskIndex * 10f) / 10f;
            AddMetric(result, "task_performance_index", result.performanceScore, "score_0_100",
                "僅描述本任務完成表現，不是常模或臨床分數");
            return result;
        }

        static void ApplyQualityRules(CognitiveAssessmentSession session, CognitiveAssessmentProtocol protocol,
            List<CognitiveTrialRecord> responses, List<CognitiveTrialRecord> valid, CognitiveGameResult result)
        {
            if (!string.Equals(session.taskVersion, protocol.TaskVersion, StringComparison.Ordinal))
                result.qualityFlags.Add("protocol_version_mismatch");
            if (responses.GroupBy(trial => trial.trialIndex).Any(group => group.Count() > 1))
                result.qualityFlags.Add("duplicate_trial_index");
            if (valid.Count < protocol.MinimumValidTrials)
                result.qualityFlags.Add("insufficient_valid_responses");
            if (responses.Count > 0 && result.excludedResponseCount / (float)responses.Count > protocol.MaximumExclusionRate)
                result.qualityFlags.Add("excessive_rt_exclusions");

            var measuredFrameRates = responses.Where(trial => trial.frameRate > 0f).Select(trial => trial.frameRate).ToList();
            if (measuredFrameRates.Count > 0 && measuredFrameRates.Count(value => value < protocol.MinimumFrameRate) /
                (float)measuredFrameRates.Count > .20f)
                result.qualityFlags.Add("unstable_frame_rate");

            long durationMs = Math.Max(0, session.endedAtUnixMs - session.startedAtUnixMs);
            if (durationMs < protocol.MinimumSessionDurationMs)
                result.qualityFlags.Add("session_too_short");

            if (protocol.MinimumTrialsPerCondition > 0)
            {
                int low = valid.Count(trial => trial.condition != null && trial.condition.Contains("low_conflict"));
                int high = valid.Count(trial => trial.condition != null && trial.condition.Contains("high_conflict"));
                if (low < protocol.MinimumTrialsPerCondition || high < protocol.MinimumTrialsPerCondition)
                    result.qualityFlags.Add("unbalanced_condition_trials");
            }

            if (result.accuracy < .55f) result.interpretationWarnings.Add("accuracy_below_interpretation_floor");
            if (result.accuracy >= .98f) result.interpretationWarnings.Add("possible_ceiling_effect");
            if (result.omissionCount > 0 && result.omissionCount / (float)Mathf.Max(1, result.validResponseCount) > .30f)
                result.interpretationWarnings.Add("high_omission_rate");

            result.dataQualityPassed = result.qualityFlags.Count == 0;
            result.eligibleForTrend = result.dataQualityPassed &&
                                      string.Equals(session.taskVersion, protocol.TaskVersion, StringComparison.Ordinal);
            result.interpretationLevel = result.eligibleForTrend ? "within_person_trend" : "descriptive_only";
            result.normReferenceStatus = "not_available";

            if (!result.dataQualityPassed)
                result.dataQualityNote = "本次僅保留原始紀錄，不納入趨勢：" + DescribeQualityCodes(result.qualityFlags);
            else if (result.interpretationWarnings.Count > 0)
                result.dataQualityNote = "可納入個人趨勢，但需注意：" + DescribeQualityCodes(result.interpretationWarnings);
            else
                result.dataQualityNote = "資料品質通過，可納入個人趨勢；目前尚無年齡常模，不代表臨床診斷。";
        }

        static void AddInterferenceMetrics(CognitiveGameResult result, List<CognitiveTrialRecord> trials, float fallback)
        {
            var lowCorrect = trials.Where(trial => trial.outcome == TrialOutcome.Correct &&
                                                   trial.condition != null && trial.condition.Contains("low_conflict"))
                .Select(trial => (float)trial.reactionTimeMs).OrderBy(value => value).ToList();
            var highCorrect = trials.Where(trial => trial.outcome == TrialOutcome.Correct &&
                                                    trial.condition != null && trial.condition.Contains("high_conflict"))
                .Select(trial => (float)trial.reactionTimeMs).OrderBy(value => value).ToList();
            var lowAll = trials.Where(trial => trial.condition != null && trial.condition.Contains("low_conflict")).ToList();
            var highAll = trials.Where(trial => trial.condition != null && trial.condition.Contains("high_conflict")).ToList();
            float lowMedian = Median(lowCorrect);
            float highMedian = Median(highCorrect);
            result.conditionEffectMs = lowCorrect.Count > 0 && highCorrect.Count > 0 ? highMedian - lowMedian : fallback;
            result.conditionEffectRatio = lowMedian > 0 ? highMedian / lowMedian : 0f;
            float lowAccuracy = AnsweredAccuracy(lowAll);
            float highAccuracy = AnsweredAccuracy(highAll);

            AddMetric(result, "low_interference_median_rt", lowMedian, "ms", "低干擾條件正確反應時間中位數");
            AddMetric(result, "high_interference_median_rt", highMedian, "ms", "高干擾條件正確反應時間中位數");
            AddMetric(result, "interference_cost", result.conditionEffectMs, "ms", "高干擾減低干擾的反應時間差");
            AddMetric(result, "interference_ratio", result.conditionEffectRatio, "ratio", "高干擾與低干擾反應時間比值");
            AddMetric(result, "interference_accuracy_cost", (lowAccuracy - highAccuracy) * 100f, "percentage_point",
                "低干擾正確率減高干擾正確率");
        }

        static void AddRoundMetrics(CognitiveGameResult result, List<CognitiveTrialRecord> all, string prefix)
        {
            var formal = all.Where(trial => !trial.isPractice).ToList();
            var rounds = formal.Where(trial => trial.eventKind == "round_summary" && string.IsNullOrEmpty(trial.exclusionReason)).ToList();
            int completed = rounds.Count(trial => trial.outcome == TrialOutcome.Correct);
            result.completionRate = rounds.Count > 0 ? (float)completed / rounds.Count : 0f;
            var durations = rounds.Where(trial => trial.outcome == TrialOutcome.Correct && trial.roundElapsedMs > 0)
                .Select(trial => (float)trial.roundElapsedMs).OrderBy(value => value).ToList();
            var actions = formal.Where(trial => (trial.eventKind == "response" || trial.eventKind == "selection") &&
                                                trial.reactionTimeMs >= 150 && trial.reactionTimeMs <= 10000)
                .Select(trial => (float)trial.reactionTimeMs).OrderBy(value => value).ToList();

            AddMetric(result, prefix + "_attempted_round_count", rounds.Count, "count", "正式測驗嘗試回合數");
            AddMetric(result, prefix + "_completed_round_count", completed, "count", "正式測驗完成回合數");
            AddMetric(result, prefix + "_round_completion_rate", result.completionRate * 100f, "percent", "完成回合比例");
            AddMetric(result, prefix + "_median_round_time", Median(durations), "ms", "完成回合時間中位數");
            AddMetric(result, prefix + "_median_action_time", Median(actions), "ms", "回合內操作反應時間中位數");

            if (prefix == "planning")
            {
                var selections = formal.Where(trial => trial.eventKind == "selection").ToList();
                int invalid = selections.Count(trial => trial.outcome == TrialOutcome.Incorrect);
                AddMetric(result, "planning_invalid_action_rate",
                    selections.Count > 0 ? invalid * 100f / selections.Count : 0f, "percent", "造成超過目標的無效操作比例");
                AddMetric(result, "planning_actions_per_completed_round",
                    completed > 0 ? selections.Count / (float)completed : 0f, "ratio", "每個完成回合所需操作數");
            }
        }

        static float AnsweredAccuracy(List<CognitiveTrialRecord> trials)
        {
            int correct = trials.Count(trial => trial.outcome == TrialOutcome.Correct);
            int incorrect = trials.Count(trial => trial.outcome == TrialOutcome.Incorrect);
            return correct + incorrect > 0 ? correct / (float)(correct + incorrect) : 0f;
        }

        static void AddMetric(CognitiveGameResult result, string key, float value, string unit, string description)
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return;
            result.metrics.Add(new CognitiveMetric { key = key, value = value, unit = unit, description = description });
        }

        static float Median(List<float> values)
        {
            if (values == null || values.Count == 0) return 0f;
            int middle = values.Count / 2;
            return values.Count % 2 == 0 ? (values[middle - 1] + values[middle]) * .5f : values[middle];
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
                var matching = sessions.Where(session => session.completed && session.result != null &&
                                                         session.result.primaryDomain == domain).ToList();
                var current = matching.Where(IsCurrentTrendEligible).ToList();
                var valid = (current.Count > 0 ? current : matching.Where(IsLegacyHistoryVisible))
                    .OrderByDescending(session => session.endedAtUnixMs).Take(5).ToList();
                if (valid.Count == 0) continue;
                float score = valid.Average(session => session.result.performanceScore);
                profile.domains.Add(new CognitiveDomainScore
                {
                    domain = domain,
                    score = Mathf.Round(score * 10f) / 10f,
                    contributingSessions = valid.Count,
                    interpretation = "任務內個人近期趨勢；尚未建立年齡常模"
                });
            }
            return profile;
        }

        public static bool IsCurrentTrendEligible(CognitiveAssessmentSession session)
        {
            if (session == null || !session.completed || session.result == null) return false;
            return session.result.eligibleForTrend &&
                   string.Equals(session.taskVersion, CognitiveProtocolRegistry.ProtocolVersion, StringComparison.Ordinal) &&
                   string.Equals(session.result.scoringVersion, CognitiveProtocolRegistry.ScoringVersion, StringComparison.Ordinal);
        }

        public static bool IsLegacyHistoryVisible(CognitiveAssessmentSession session)
        {
            if (session == null || !session.completed || session.result == null || session.endedAtUnixMs <= 0) return false;
            if (string.Equals(session.taskVersion, CognitiveProtocolRegistry.ProtocolVersion, StringComparison.Ordinal))
                return false;
            float score = session.result.performanceScore;
            return !float.IsNaN(score) && !float.IsInfinity(score) && score >= 0f && score <= 100f;
        }

        static string DescribeQualityCodes(IEnumerable<string> codes)
        {
            return string.Join("、", codes.Select(code =>
            {
                switch (code)
                {
                    case "protocol_version_mismatch": return "遊戲版本與評估協定不一致";
                    case "duplicate_trial_index": return "題目序號重複";
                    case "insufficient_valid_responses": return "有效正式試次不足";
                    case "excessive_rt_exclusions": return "異常反應時間比例過高";
                    case "unstable_frame_rate": return "執行期間畫面更新率不穩";
                    case "session_too_short": return "測驗時間短於標準協定";
                    case "unbalanced_condition_trials": return "高低干擾條件題數不足或不平衡";
                    case "accuracy_below_interpretation_floor": return "正確率偏低，解讀反應時間時需謹慎";
                    case "possible_ceiling_effect": return "接近滿分，可能存在天花板效應";
                    case "high_omission_rate": return "漏答比例偏高";
                    default: return code;
                }
            }));
        }

        public static void RefreshLegacyResult(CognitiveAssessmentSession session)
        {
            if (session == null || session.result == null) return;
            if (session.result.scoringVersion == CognitiveProtocolRegistry.ScoringVersion) return;
            session.result = BuildGameResult(session, session.result.primaryDomain,
                session.result.conditionEffectMs, session.result.difficultyReached);
        }
    }
}
