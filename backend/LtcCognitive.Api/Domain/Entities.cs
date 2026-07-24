namespace LtcCognitive.Api.Domain;

public sealed class CognitiveDomain
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Code { get; set; }
    public required string NameZhTw { get; set; }
    public string? Description { get; set; }
    public List<GameDefinition> Games { get; set; } = [];
}

public sealed class GameDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Code { get; set; }
    public required string NameZhTw { get; set; }
    public Guid DomainId { get; set; }
    public CognitiveDomain Domain { get; set; } = null!;
    public required string MappingVersion { get; set; }
    public string? EvidenceNote { get; set; }
    public bool IsActive { get; set; } = true;
    public List<AssessmentSession> Sessions { get; set; } = [];
}

public sealed class AssessmentSession
{
    public Guid Id { get; set; }
    public long PlayerId { get; set; }
    public Player Player { get; set; } = null!;
    public Guid GameId { get; set; }
    public GameDefinition Game { get; set; } = null!;
    public required string TaskVersion { get; set; }
    public required string SchemaVersion { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset EndedAtUtc { get; set; }
    public string CompletionStatus { get; set; } = "completed";
    public string? ExitReason { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<TrialEvent> Trials { get; set; } = [];
    public List<DerivedMetric> Metrics { get; set; } = [];
}

public sealed class TrialEvent
{
    public Guid SessionId { get; set; }
    public AssessmentSession Session { get; set; } = null!;
    public short TrialIndex { get; set; }
    public string? TrialType { get; set; }
    public string? StimulusJson { get; set; }
    public string? ExpectedResponse { get; set; }
    public string? ActualResponse { get; set; }
    public bool? IsCorrect { get; set; }
    public int? ReactionTimeMs { get; set; }
    public int? PresentationDurationMs { get; set; }
}

public sealed class DerivedMetric
{
    public long MetricId { get; set; }
    public Guid SessionId { get; set; }
    public AssessmentSession Session { get; set; } = null!;
    public Guid DomainId { get; set; }
    public CognitiveDomain Domain { get; set; } = null!;
    public required string MetricCode { get; set; }
    public double Value { get; set; }
    public string? Unit { get; set; }
    public required string CalculationVersion { get; set; }
    public string QualityFlag { get; set; } = "valid";
}
