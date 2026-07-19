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

    public enum TrialOutcome { Correct, Incorrect, Omitted, Aborted, ValidAction }

    [Serializable]
    public class CognitiveTrialRecord
    {
        public string sessionId;
        public string gameId;
        public string taskVersion;
        public int trialIndex;
        public int roundIndex;
        public int stepIndex;
        public string eventKind = "response";
        public int randomSeed;
        public int difficulty;
        public int stimulusCount;
        public string condition;
        public string stimulus;
        public string expectedAnswer;
        public string userAnswer;
        public TrialOutcome outcome;
        public long reactionTimeMs;
        public long roundElapsedMs;
        public long occurredAtUnixMs;
        public string errorType;
        public string exclusionReason;
        public bool isPractice;
        public bool timedOut;
        public float frameRate;
        public string inputMethod;
    }

    [Serializable]
    public class CognitiveMetric
    {
        public string key;
        public float value;
        public string unit;
        public string description;
    }

    [Serializable]
    public class CognitiveGameResult
    {
        public string gameId;
        public CognitiveDomain primaryDomain;
        public int trialCount;
        public int validResponseCount;
        public int excludedResponseCount;
        public int correctCount;
        public int incorrectCount;
        public int omissionCount;
        public float accuracy;
        public float medianCorrectReactionTimeMs;
        public float reactionTimeVariabilityMs;
        public float medianAbsoluteDeviationMs;
        public float inverseEfficiencyMs;
        public float conditionEffectMs;
        public float conditionEffectRatio;
        public float completionRate;
        public float difficultyReached;
        public float performanceScore;
        public bool dataQualityPassed;
        public string dataQualityNote;
        public List<string> qualityFlags = new List<string>();
        public List<CognitiveMetric> metrics = new List<CognitiveMetric>();
    }

    [Serializable]
    public class CognitiveAssessmentSession
    {
        public string schemaVersion = "2.0";
        public string sessionId;
        public string anonymousUserId;
        public string gameId;
        public string taskVersion;
        public string appVersion;
        public string deviceModel;
        public string operatingSystem;
        public string locale;
        public int screenWidth;
        public int screenHeight;
        public float screenDpi;
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
        public string disclaimer = "本結果是遊戲內的縱向表現指標，不是醫療診斷；尚未建立年齡與教育程度常模前，不應用單次分數判定認知障礙。";
    }
}
