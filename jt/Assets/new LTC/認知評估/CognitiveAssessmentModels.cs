using System;
using System.Collections.Generic;

namespace LTCCognitiveAssessment
{
    public enum CognitiveDomain
    {
        AttentionInhibitoryControl,
        ProcessingSpeedVisualSearch,
        ExecutiveFunctionNumericalReasoning,
        WorkingMemory,
        EpisodicMemory,
        Language,
        VisuospatialAbility,
        Orientation
    }

    public enum TrialOutcome
    {
        Correct,
        Incorrect,
        Omitted,
        Aborted
    }

    [Serializable]
    public class CognitiveTrialRecord
    {
        public string sessionId;
        public string gameId;
        public string taskVersion;
        public int trialIndex;
        public int randomSeed;
        public int difficulty;
        public string condition;
        public string stimulus;
        public string expectedAnswer;
        public string userAnswer;
        public TrialOutcome outcome;
        public long reactionTimeMs;
        public long occurredAtUnixMs;
        public string errorType;
        public float frameRate;
        public string inputMethod;
    }

    [Serializable]
    public class CognitiveGameResult
    {
        public string gameId;
        public CognitiveDomain primaryDomain;
        public int trialCount;
        public int correctCount;
        public int incorrectCount;
        public int omissionCount;
        public float accuracy;
        public float medianCorrectReactionTimeMs;
        public float reactionTimeVariabilityMs;
        public float conditionEffectMs;
        public float difficultyReached;
        public float performanceScore;
        public bool dataQualityPassed;
        public string dataQualityNote;
    }

    [Serializable]
    public class CognitiveAssessmentSession
    {
        public string sessionId;
        public string anonymousUserId;
        public string gameId;
        public string taskVersion;
        public string appVersion;
        public string deviceModel;
        public string operatingSystem;
        public string inputMethod;
        public long startedAtUnixMs;
        public long endedAtUnixMs;
        public bool completed;
        public List<CognitiveTrialRecord> trials = new List<CognitiveTrialRecord>();
        public CognitiveGameResult result;
    }

    [Serializable]
    public class CognitiveDomainScore
    {
        public CognitiveDomain domain;
        public float score;
        public int contributingSessions;
        public string interpretation;
    }

    [Serializable]
    public class CognitiveProfile
    {
        public string anonymousUserId;
        public long generatedAtUnixMs;
        public List<CognitiveDomainScore> domains = new List<CognitiveDomainScore>();
        public string disclaimer =
            "本結果為遊戲表現與長期變化參考，不代表醫療診斷。單次表現可能受疲勞、睡眠、視聽與操作熟悉度影響。";
    }
}
