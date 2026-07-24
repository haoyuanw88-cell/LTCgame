using System;
using System.Collections.Generic;
using LTCCognitiveAssessment;
using UnityEditor;
using UnityEngine;

public static class CognitiveScoringScientificValidityChecks
{
    [InitializeOnLoadMethod]
    static void VerifyAfterScriptReload()
    {
        try
        {
            VerifyLowAccuracyIsNotTechnicalInvalidity();
            VerifyPracticeTrialsAreExcluded();
            VerifyUnbalancedConditionsAreRejected();
            VerifyPoorRoundPerformanceCanRemainValid();
            VerifyLegacyHistoryRemainsVisible();
            Debug.Log("[CognitiveValidity] 協定 3.0 與歷史顯示規則自動檢查通過（5/5）。");
        }
        catch (Exception exception)
        {
            Debug.LogError("[CognitiveValidity] 科學規則自動檢查失敗：" + exception);
        }
    }

    static void VerifyLowAccuracyIsNotTechnicalInvalidity()
    {
        var session = CreateSession("stroop_color_match");
        for (int index = 0; index < 12; index++)
            session.trials.Add(Response(index + 1, index % 2 == 0 ? "match_low_conflict" : "mismatch_high_conflict",
                index < 4 ? TrialOutcome.Correct : TrialOutcome.Incorrect));
        var result = CognitiveScoring.BuildGameResult(session,
            CognitiveDomain.AttentionInhibitoryControl, 0f, 1f);
        Require(result.accuracy < .55f, "測試資料應為低正確率");
        Require(result.dataQualityPassed && result.eligibleForTrend, "低表現不可被誤判為技術無效");
    }

    static void VerifyPracticeTrialsAreExcluded()
    {
        var session = CreateSession("stroop_color_match");
        for (int index = 0; index < 4; index++)
        {
            var practice = Response(index + 1, index % 2 == 0 ? "match_low_conflict" : "mismatch_high_conflict",
                TrialOutcome.Incorrect);
            practice.isPractice = true;
            session.trials.Add(practice);
        }
        for (int index = 0; index < 12; index++)
            session.trials.Add(Response(index + 5, index % 2 == 0 ? "match_low_conflict" : "mismatch_high_conflict",
                TrialOutcome.Correct));
        var result = CognitiveScoring.BuildGameResult(session,
            CognitiveDomain.AttentionInhibitoryControl, 0f, 1f);
        Require(result.practiceTrialCount == 4 && result.trialCount == 12 && Mathf.Approximately(result.accuracy, 1f),
            "練習題必須保存但不得進入正式計分");
    }

    static void VerifyUnbalancedConditionsAreRejected()
    {
        var session = CreateSession("stroop_color_match");
        for (int index = 0; index < 12; index++)
            session.trials.Add(Response(index + 1, "match_low_conflict", TrialOutcome.Correct));
        var result = CognitiveScoring.BuildGameResult(session,
            CognitiveDomain.AttentionInhibitoryControl, 0f, 1f);
        Require(!result.eligibleForTrend && result.qualityFlags.Contains("unbalanced_condition_trials"),
            "條件不平衡資料不得進入個人趨勢");
    }

    static void VerifyPoorRoundPerformanceCanRemainValid()
    {
        var session = CreateSession("number_order");
        for (int index = 0; index < 3; index++)
        {
            var trial = Response(index + 1, "positive_only", TrialOutcome.Incorrect);
            trial.eventKind = "round_summary";
            trial.reactionTimeMs = 4000;
            trial.roundElapsedMs = 4000;
            session.trials.Add(trial);
        }
        var result = CognitiveScoring.BuildGameResult(session,
            CognitiveDomain.ProcessingSpeedVisualSearch, 0f, 3f);
        Require(Mathf.Approximately(result.performanceScore, 0f) && result.eligibleForTrend,
            "表現為零仍可能是技術上有效的觀察值");
    }

    static void VerifyLegacyHistoryRemainsVisible()
    {
        var legacy = CreateSession("number_sum");
        legacy.taskVersion = "2.0.0";
        legacy.result = new CognitiveGameResult
        {
            primaryDomain = CognitiveDomain.ExecutiveFunctionNumericalReasoning,
            performanceScore = 83f,
            dataQualityPassed = false
        };

        CognitiveProfile profile = CognitiveScoring.BuildProfile("guest-test",
            new List<CognitiveAssessmentSession> { legacy });
        Require(profile.domains.Count == 1 && Mathf.Approximately(profile.domains[0].score, 83f),
            "既有舊版完成紀錄應繼續顯示於統計頁");
    }

    static CognitiveAssessmentSession CreateSession(string gameId)
    {
        return new CognitiveAssessmentSession
        {
            sessionId = "scientific-validity-check",
            gameId = gameId,
            taskVersion = CognitiveProtocolRegistry.ProtocolVersion,
            startedAtUnixMs = 1_000_000,
            endedAtUnixMs = 1_060_000,
            completed = true,
            trials = new List<CognitiveTrialRecord>()
        };
    }

    static CognitiveTrialRecord Response(int index, string condition, TrialOutcome outcome)
    {
        return new CognitiveTrialRecord
        {
            trialIndex = index,
            eventKind = "response",
            condition = condition,
            outcome = outcome,
            reactionTimeMs = 800,
            frameRate = 60f
        };
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
