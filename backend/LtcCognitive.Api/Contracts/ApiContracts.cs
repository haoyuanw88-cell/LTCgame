namespace LtcCognitive.Api.Contracts;

public sealed record UpsertParticipantRequest(string AnonymousCode, string? DisplayName, int? BirthYear, string? EducationBand);
public sealed record DeviceDto(string InstallationId, string? Platform, string? Model, int? ScreenWidth, int? ScreenHeight, float? Dpi);
public sealed record TrialDto(int TrialIndex, string? TrialType, string? StimulusJson, string? ExpectedResponse,
    string? ActualResponse, bool? IsCorrect, int? ReactionTimeMs, int? PresentationDurationMs, DateTimeOffset OccurredAtUtc);
public sealed record MetricDto(string MetricCode, double Value, string? Unit, string CalculationVersion,
    string? DomainCode, string? QualityFlag);
public sealed record UploadAssessmentRequest(Guid SessionId, string AnonymousCode, string GameCode,
    string TaskVersion, string SchemaVersion, DateTimeOffset StartedAtUtc, DateTimeOffset EndedAtUtc,
    string? CompletionStatus, string? ExitReason, int? ClientTimezoneOffsetMinutes, DeviceDto? Device,
    IReadOnlyList<TrialDto>? Trials, IReadOnlyList<MetricDto>? Metrics);
