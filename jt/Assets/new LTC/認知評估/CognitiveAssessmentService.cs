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
        readonly string rootPath = Path.Combine(Application.persistentDataPath, "CognitiveAssessment", "sessions");
        public void Save(CognitiveAssessmentSession session)
        {
            Directory.CreateDirectory(rootPath);
            File.WriteAllText(Path.Combine(rootPath, Sanitize(session.anonymousUserId) + "_" + session.startedAtUnixMs + "_" + session.sessionId + ".json"), JsonUtility.ToJson(session, true));
        }
        public List<CognitiveAssessmentSession> LoadRecent(string userId, int limit)
        {
            var result = new List<CognitiveAssessmentSession>();
            if (!Directory.Exists(rootPath)) return result;
            foreach (string path in Directory.GetFiles(rootPath, Sanitize(userId) + "_*.json").OrderByDescending(File.GetLastWriteTimeUtc).Take(Mathf.Max(1, limit)))
                try { var s = JsonUtility.FromJson<CognitiveAssessmentSession>(File.ReadAllText(path)); if (s != null) result.Add(s); }
                catch (Exception e) { Debug.LogWarning("無法讀取認知評估紀錄：" + e.Message); }
            return result;
        }
        static string Sanitize(string value)
        {
            value = string.IsNullOrWhiteSpace(value) ? "guest" : value;
            foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
            return value;
        }
    }

    public static class CognitiveAssessmentService
    {
        static readonly Dictionary<string, CognitiveAssessmentSession> ActiveSessions = new Dictionary<string, CognitiveAssessmentSession>();
        static ICognitiveAssessmentRepository repository = new LocalJsonCognitiveRepository();
        public static void SetRepository(ICognitiveAssessmentRepository value) { repository = value ?? throw new ArgumentNullException(nameof(value)); }
        public static string BeginGame(string gameId, string taskVersion, string anonymousUserId = null, string inputMethod = "touch_or_mouse")
        {
            string id = Guid.NewGuid().ToString("N");
            ActiveSessions[id] = new CognitiveAssessmentSession {
                sessionId=id, anonymousUserId=ResolveUserId(anonymousUserId), gameId=gameId, taskVersion=taskVersion,
                appVersion=Application.version, deviceModel=SystemInfo.deviceModel, operatingSystem=SystemInfo.operatingSystem,
                locale=System.Globalization.CultureInfo.CurrentCulture.Name, screenWidth=Screen.width, screenHeight=Screen.height,
                screenDpi=Screen.dpi, inputMethod=inputMethod, startedAtUnixMs=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            return id;
        }
        public static void RecordTrial(string id, CognitiveTrialRecord trial)
        {
            if (!ActiveSessions.TryGetValue(id, out var session)) { Debug.LogWarning("找不到評估 Session：" + id); return; }
            trial.sessionId=id; trial.gameId=session.gameId; trial.taskVersion=session.taskVersion;
            trial.occurredAtUnixMs=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (trial.frameRate <= 0f) trial.frameRate=1f/Mathf.Max(Time.unscaledDeltaTime, .0001f);
            if (string.IsNullOrEmpty(trial.inputMethod)) trial.inputMethod=session.inputMethod;
            session.trials.Add(trial);
        }
        public static CognitiveGameResult CompleteGame(string id, CognitiveDomain domain, float conditionEffectMs=0f, float difficultyReached=0f)
        {
            if (!ActiveSessions.TryGetValue(id, out var session)) throw new InvalidOperationException("找不到評估 Session：" + id);
            session.result=CognitiveScoring.BuildGameResult(session.gameId, domain, session.trials, conditionEffectMs, difficultyReached);
            session.completed=true; session.endedAtUnixMs=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); repository.Save(session); ActiveSessions.Remove(id);
            return session.result;
        }
        public static CognitiveProfile BuildProfile(string userId=null, int limit=30) { string id=ResolveUserId(userId); return CognitiveScoring.BuildProfile(id, repository.LoadRecent(id, limit)); }
        public static string CurrentUserId => ResolveUserId(null);
        static string ResolveUserId(string id) { if (!string.IsNullOrWhiteSpace(id)) return id.Trim(); string s=PlayerPrefs.GetString("SavedPlayerName", PlayerPrefs.GetString("AccountName", "guest")); return string.IsNullOrWhiteSpace(s)?"guest":s.Trim(); }
    }

    public static class CognitiveScoring
    {
        const long MinimumReactionTimeMs=150, MaximumReactionTimeMs=10000;
        public static CognitiveGameResult BuildGameResult(string gameId, CognitiveDomain domain, List<CognitiveTrialRecord> all, float suppliedEffect, float difficulty)
        {
            all=all??new List<CognitiveTrialRecord>();
            bool roundScored = (gameId=="number_order" || gameId=="number_sum") && all.Any(t=>t.eventKind=="round_summary");
            var responses=all.Where(t => roundScored ? t.eventKind=="round_summary" :
                (string.IsNullOrEmpty(t.eventKind)||t.eventKind=="response")).Where(t=>t.outcome!=TrialOutcome.ValidAction).ToList();
            foreach (var t in responses) if (t.reactionTimeMs>0 && t.reactionTimeMs<MinimumReactionTimeMs) t.exclusionReason="anticipatory_rt"; else if (t.reactionTimeMs>MaximumReactionTimeMs) t.exclusionReason="extreme_rt";
            var valid=responses.Where(t=>string.IsNullOrEmpty(t.exclusionReason)).ToList();
            int correct=valid.Count(t=>t.outcome==TrialOutcome.Correct), incorrect=valid.Count(t=>t.outcome==TrialOutcome.Incorrect), omitted=valid.Count(t=>t.outcome==TrialOutcome.Omitted);
            int answered=correct+incorrect; float accuracy=answered>0?(float)correct/answered:0f;
            var times=valid.Where(t=>t.outcome==TrialOutcome.Correct&&t.reactionTimeMs>0).Select(t=>(float)t.reactionTimeMs).OrderBy(x=>x).ToList();
            float median=Median(times), mad=Median(times.Select(x=>Mathf.Abs(x-median)).OrderBy(x=>x).ToList());
            var result=new CognitiveGameResult { gameId=gameId, primaryDomain=domain, trialCount=responses.Count, validResponseCount=valid.Count, excludedResponseCount=responses.Count-valid.Count,
                correctCount=correct, incorrectCount=incorrect, omissionCount=omitted, accuracy=accuracy, medianCorrectReactionTimeMs=median,
                reactionTimeVariabilityMs=mad*1.4826f, medianAbsoluteDeviationMs=mad, inverseEfficiencyMs=accuracy>0?median/accuracy:0,
                difficultyReached=difficulty, completionRate=responses.Count>0?(float)(correct+incorrect)/responses.Count:0 };
            AddMetric(result,"accuracy",accuracy*100,"percent","有效作答正確率"); AddMetric(result,"median_correct_rt",median,"ms","正確反應時間中位數");
            AddMetric(result,"rt_mad",mad,"ms","反應時間中位絕對偏差"); AddMetric(result,"inverse_efficiency",result.inverseEfficiencyMs,"ms","速度與正確率綜合指標，越低越好");
            if (gameId=="stroop_color_match") AddConflictMetrics(result,valid,suppliedEffect);
            if (gameId=="number_order") AddRoundMetrics(result,all,"search");
            if (gameId=="number_sum") AddRoundMetrics(result,all,"planning");
            if (valid.Count<8) result.qualityFlags.Add("insufficient_valid_responses");
            if (answered>0&&accuracy<.55f) result.qualityFlags.Add("accuracy_below_interpretation_floor");
            if (responses.Count>0&&result.excludedResponseCount/(float)responses.Count>.2f) result.qualityFlags.Add("excessive_rt_exclusions");
            result.dataQualityPassed=result.qualityFlags.Count==0; result.dataQualityNote=result.dataQualityPassed?"資料品質通過；可用於個人趨勢比較。":"資料品質未通過："+string.Join(", ",result.qualityFlags);
            // Demo 指數只表達本次任務完成品質；沒有常模前不可作臨床分數。
            result.performanceScore=Mathf.Round(Mathf.Clamp01(accuracy*result.completionRate)*1000f)/10f;
            return result;
        }
        static void AddConflictMetrics(CognitiveGameResult r,List<CognitiveTrialRecord> trials,float fallback)
        {
            var low=trials.Where(t=>t.outcome==TrialOutcome.Correct&&t.condition!=null&&t.condition.Contains("low_conflict")).Select(t=>(float)t.reactionTimeMs).OrderBy(x=>x).ToList();
            var high=trials.Where(t=>t.outcome==TrialOutcome.Correct&&t.condition!=null&&t.condition.Contains("high_conflict")).Select(t=>(float)t.reactionTimeMs).OrderBy(x=>x).ToList();
            float l=Median(low),h=Median(high); r.conditionEffectMs=low.Count>0&&high.Count>0?h-l:fallback; r.conditionEffectRatio=l>0?h/l:0;
            AddMetric(r,"conflict_cost",r.conditionEffectMs,"ms","高干擾減低干擾的中位反應時間"); AddMetric(r,"conflict_ratio",r.conditionEffectRatio,"ratio","高干擾與低干擾反應時間比值");
            if(low.Count<3||high.Count<3) r.qualityFlags.Add("insufficient_trials_per_conflict_condition");
        }
        static void AddRoundMetrics(CognitiveGameResult r,List<CognitiveTrialRecord> all,string prefix)
        {
            var rounds=all.Where(t=>t.eventKind=="round_summary").ToList(); int completed=rounds.Count(t=>t.outcome==TrialOutcome.Correct);
            r.completionRate=rounds.Count>0?(float)completed/rounds.Count:0;
            var durations=rounds.Where(t=>t.outcome==TrialOutcome.Correct&&t.roundElapsedMs>0).Select(t=>(float)t.roundElapsedMs).OrderBy(x=>x).ToList();
            AddMetric(r,prefix+"_round_completion_rate",r.completionRate*100,"percent","完成回合比例"); AddMetric(r,prefix+"_median_round_time",Median(durations),"ms","完成回合時間中位數");
            var actions=all.Where(t=>(t.eventKind=="response"||t.eventKind=="selection")&&t.reactionTimeMs>=MinimumReactionTimeMs&&t.reactionTimeMs<=MaximumReactionTimeMs).Select(t=>(float)t.reactionTimeMs).OrderBy(x=>x).ToList();
            AddMetric(r,prefix+"_median_action_time",Median(actions),"ms","回合內操作反應時間中位數");
            if(rounds.Count<3) r.qualityFlags.Add("insufficient_completed_rounds");
        }
        static void AddMetric(CognitiveGameResult r,string key,float value,string unit,string description){r.metrics.Add(new CognitiveMetric{key=key,value=value,unit=unit,description=description});}
        static float Median(List<float> v){if(v==null||v.Count==0)return 0;int m=v.Count/2;return v.Count%2==0?(v[m-1]+v[m])*.5f:v[m];}
        public static CognitiveProfile BuildProfile(string userId,List<CognitiveAssessmentSession> sessions)
        {
            var p=new CognitiveProfile{anonymousUserId=userId,generatedAtUnixMs=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()};
            foreach(CognitiveDomain d in Enum.GetValues(typeof(CognitiveDomain))){var v=sessions.Where(s=>s.completed&&s.result!=null&&s.result.primaryDomain==d&&s.result.dataQualityPassed).Take(5).ToList();if(v.Count==0)continue;float score=v.Average(s=>s.result.performanceScore);p.domains.Add(new CognitiveDomainScore{domain=d,score=Mathf.Round(score*10)/10f,contributingSessions=v.Count,interpretation="僅供個人近期趨勢比較"});}
            return p;
        }
    }
}
