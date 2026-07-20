namespace LtcCognitive.Api.Domain;

public sealed class Participant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string AnonymousCode { get; set; }
    public string? DisplayName { get; set; }
    public int? BirthYear { get; set; }
    public string? EducationBand { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<AssessmentSession> Sessions { get; set; } = [];
}

public sealed class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string InstallationId { get; set; }
    public string? Platform { get; set; }
    public string? Model { get; set; }
    public int? ScreenWidth { get; set; }
    public int? ScreenHeight { get; set; }
    public float? Dpi { get; set; }
    public DateTimeOffset FirstSeenAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CognitiveDomain
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Code { get; set; }
    public required string NameZhTw { get; set; }
    public string? Description { get; set; }
}

public sealed class GameDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Code { get; set; }
    public required string NameZhTw { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class GameDomainMapping
{
    public Guid GameId { get; set; }
    public GameDefinition Game { get; set; } = null!;
    public Guid DomainId { get; set; }
    public CognitiveDomain Domain { get; set; } = null!;
    public decimal Weight { get; set; }
    public required string MappingVersion { get; set; }
    public string? EvidenceNote { get; set; }
}

public sealed class AssessmentSession
{
    public Guid Id { get; set; }
    public Guid ParticipantId { get; set; }
    public Participant Participant { get; set; } = null!;
    public Guid? DeviceId { get; set; }
    public Device? Device { get; set; }
    public Guid GameId { get; set; }
    public GameDefinition Game { get; set; } = null!;
    public required string TaskVersion { get; set; }
    public required string SchemaVersion { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset EndedAtUtc { get; set; }
    public int DurationMs { get; set; }
    public string CompletionStatus { get; set; } = "completed";
    public string? ExitReason { get; set; }
    public int? ClientTimezoneOffsetMinutes { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public List<TrialEvent> Trials { get; set; } = [];
    public List<DerivedMetric> Metrics { get; set; } = [];
}

public sealed class TrialEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public AssessmentSession Session { get; set; } = null!;
    public int TrialIndex { get; set; }
    public string? TrialType { get; set; }
    public string StimulusJson { get; set; } = "{}";
    public string? ExpectedResponse { get; set; }
    public string? ActualResponse { get; set; }
    public bool? IsCorrect { get; set; }
    public int? ReactionTimeMs { get; set; }
    public int? PresentationDurationMs { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; }
}

public sealed class DerivedMetric
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public AssessmentSession Session { get; set; } = null!;
    public Guid? DomainId { get; set; }
    public CognitiveDomain? Domain { get; set; }
    public required string MetricCode { get; set; }
    public double Value { get; set; }
    public string? Unit { get; set; }
    public required string CalculationVersion { get; set; }
    public string QualityFlag { get; set; } = "valid";
}
