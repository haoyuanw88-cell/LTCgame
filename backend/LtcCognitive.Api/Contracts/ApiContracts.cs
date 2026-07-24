namespace LtcCognitive.Api.Contracts;

public sealed record TrialDto(int TrialIndex, string? TrialType, string? StimulusJson, string? ExpectedResponse,
    string? ActualResponse, bool? IsCorrect, int? ReactionTimeMs, int? PresentationDurationMs);
public sealed record MetricDto(string MetricCode, double Value, string? Unit, string CalculationVersion,
    string? DomainCode, string? QualityFlag);
public sealed record UploadAssessmentRequest(string SessionId, string GameCode,
    string TaskVersion, string SchemaVersion, DateTimeOffset StartedAtUtc, DateTimeOffset EndedAtUtc,
    string? CompletionStatus, string? ExitReason,
    IReadOnlyList<TrialDto>? Trials, IReadOnlyList<MetricDto>? Metrics);
