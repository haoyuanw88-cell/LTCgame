using LtcCognitive.Api.Contracts;
using LtcCognitive.Api.Data;
using LtcCognitive.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LtcCognitive.Api.Endpoints;

public static class AssessmentEndpoints
{
    public static IEndpointRouteBuilder MapAssessmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/assessments").WithTags("Assessments");
        group.MapPost("/", UploadAsync);
        group.MapGet("/participants/{anonymousCode}/history", HistoryAsync);
        group.MapGet("/participants/{anonymousCode}/trends", TrendsAsync);
        return app;
    }

    private static async Task<IResult> UploadAsync(UploadAssessmentRequest request, LtcDbContext db, CancellationToken ct)
    {
        if (request.SessionId == Guid.Empty || request.EndedAtUtc < request.StartedAtUtc)
            return Results.BadRequest(new { error = "測驗識別碼或時間範圍不正確。" });
        if (await db.AssessmentSessions.AnyAsync(x => x.Id == request.SessionId, ct))
            return Results.Ok(new { request.SessionId, status = "already_received" });

        var participant = await db.Participants.SingleOrDefaultAsync(x => x.AnonymousCode == request.AnonymousCode.Trim(), ct);
        if (participant is null) return Results.NotFound(new { error = "找不到受測者，請先建立受測者。" });
        var game = await db.Games.SingleOrDefaultAsync(x => x.Code == request.GameCode, ct);
        if (game is null) return Results.BadRequest(new { error = $"未知遊戲代碼：{request.GameCode}" });

        Device? device = null;
        if (request.Device is not null)
        {
            device = await db.Devices.SingleOrDefaultAsync(x => x.InstallationId == request.Device.InstallationId, ct);
            if (device is null)
            {
                device = new Device { InstallationId = request.Device.InstallationId };
                db.Devices.Add(device);
            }
            device.Platform = request.Device.Platform;
            device.Model = request.Device.Model;
            device.ScreenWidth = request.Device.ScreenWidth;
            device.ScreenHeight = request.Device.ScreenHeight;
            device.Dpi = request.Device.Dpi;
            device.LastSeenAtUtc = DateTimeOffset.UtcNow;
        }

        var session = new AssessmentSession
        {
            Id = request.SessionId, Participant = participant, Device = device, Game = game,
            TaskVersion = request.TaskVersion, SchemaVersion = request.SchemaVersion,
            StartedAtUtc = request.StartedAtUtc, EndedAtUtc = request.EndedAtUtc,
            DurationMs = (int)Math.Clamp((request.EndedAtUtc - request.StartedAtUtc).TotalMilliseconds, 0, int.MaxValue),
            CompletionStatus = request.CompletionStatus ?? "completed", ExitReason = request.ExitReason,
            ClientTimezoneOffsetMinutes = request.ClientTimezoneOffsetMinutes
        };

        foreach (var trial in request.Trials ?? [])
            session.Trials.Add(new TrialEvent
            {
                TrialIndex = trial.TrialIndex, TrialType = trial.TrialType,
                StimulusJson = string.IsNullOrWhiteSpace(trial.StimulusJson) ? "{}" : trial.StimulusJson,
                ExpectedResponse = trial.ExpectedResponse, ActualResponse = trial.ActualResponse,
                IsCorrect = trial.IsCorrect, ReactionTimeMs = trial.ReactionTimeMs,
                PresentationDurationMs = trial.PresentationDurationMs, OccurredAtUtc = trial.OccurredAtUtc
            });

        var domainCodes = (request.Metrics ?? []).Where(x => x.DomainCode is not null).Select(x => x.DomainCode!).Distinct().ToArray();
        var domains = await db.CognitiveDomains.Where(x => domainCodes.Contains(x.Code)).ToDictionaryAsync(x => x.Code, ct);
        foreach (var metric in request.Metrics ?? [])
        {
            domains.TryGetValue(metric.DomainCode ?? string.Empty, out var domain);
            session.Metrics.Add(new DerivedMetric
            {
                Domain = domain, MetricCode = metric.MetricCode, Value = metric.Value, Unit = metric.Unit,
                CalculationVersion = metric.CalculationVersion, QualityFlag = metric.QualityFlag ?? "valid"
            });
        }

        db.AssessmentSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/assessments/{session.Id}", new
            { session.Id, status = "stored", trials = session.Trials.Count, metrics = session.Metrics.Count });
    }

    private static async Task<IResult> HistoryAsync(string anonymousCode, int? limit, LtcDbContext db, CancellationToken ct)
    {
        var rows = await db.AssessmentSessions.AsNoTracking()
            .Where(x => x.Participant.AnonymousCode == anonymousCode)
            .OrderByDescending(x => x.EndedAtUtc).Take(Math.Clamp(limit ?? 30, 1, 100))
            .Select(x => new
            {
                x.Id, gameCode = x.Game.Code, gameName = x.Game.NameZhTw, x.StartedAtUtc, x.EndedAtUtc,
                x.DurationMs, x.CompletionStatus,
                metrics = x.Metrics.Select(m => new { m.MetricCode, m.Value, m.Unit, domain = m.Domain != null ? m.Domain.Code : null })
            }).ToListAsync(ct);
        return Results.Ok(rows);
    }

    private static async Task<IResult> TrendsAsync(string anonymousCode, int? days, string? domain, LtcDbContext db, CancellationToken ct)
    {
        var range = Math.Clamp(days ?? 30, 1, 365);
        var since = DateTimeOffset.UtcNow.AddDays(-range);
        var query = db.DerivedMetrics.AsNoTracking().Where(x =>
            x.Session.Participant.AnonymousCode == anonymousCode && x.Session.EndedAtUtc >= since && x.QualityFlag == "valid");
        if (!string.IsNullOrWhiteSpace(domain)) query = query.Where(x => x.Domain != null && x.Domain.Code == domain);
        var points = await query.OrderBy(x => x.Session.EndedAtUtc).Select(x => new
        {
            timestampUtc = x.Session.EndedAtUtc, gameCode = x.Session.Game.Code,
            domainCode = x.Domain != null ? x.Domain.Code : null, x.MetricCode, x.Value, x.Unit, x.CalculationVersion
        }).ToListAsync(ct);
        return Results.Ok(new { anonymousCode, days = range, domain, points });
    }
}
