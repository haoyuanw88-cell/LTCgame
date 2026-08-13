using LtcCognitive.Api.Contracts;
using LtcCognitive.Api.Data;
using LtcCognitive.Api.Domain;
using LtcCognitive.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace LtcCognitive.Api.Endpoints;

public static class AssessmentEndpoints
{
    public static IEndpointRouteBuilder MapAssessmentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/assessments").WithTags("Assessments");
        group.MapPost("/", UploadAsync);
        group.MapGet("/mine/history", HistoryAsync);
        group.MapGet("/mine/trends", TrendsAsync);
        return app;
    }

    private static async Task<IResult> UploadAsync(UploadAssessmentRequest request, HttpRequest httpRequest,
        IIdentityService identity, LtcDbContext db, CancellationToken ct)
    {
        var player = await identity.AuthenticateAsync(httpRequest.Headers.Authorization, ct);
        if (player is null) return Results.Unauthorized();
        if (!Guid.TryParse(request.SessionId, out var sessionId) || sessionId == Guid.Empty || request.EndedAtUtc < request.StartedAtUtc)
            return Results.BadRequest(new { error = "場次識別碼或時間範圍無效。" });
        if (await db.AssessmentSessions.AnyAsync(x => x.Id == sessionId, ct))
            return Results.Ok(new { sessionId, status = "already_received" });

        var game = await db.Games.Include(x => x.Domain)
            .SingleOrDefaultAsync(x => x.Code == request.GameCode && x.IsActive, ct);
        if (game is null) return Results.BadRequest(new { error = $"未知或已停用的遊戲：{request.GameCode}" });

        var trials = request.Trials ?? [];
        if (trials.Any(x => x.TrialIndex is < 0 or > short.MaxValue) ||
            trials.GroupBy(x => x.TrialIndex).Any(x => x.Count() > 1))
            return Results.BadRequest(new { error = "trialIndex 必須唯一且介於 0 到 32767。" });

        var session = new AssessmentSession
        {
            Id = sessionId,
            PlayerId = player.PlayerId,
            GameId = game.Id,
            TaskVersion = request.TaskVersion,
            SchemaVersion = request.SchemaVersion,
            StartedAtUtc = request.StartedAtUtc,
            EndedAtUtc = request.EndedAtUtc,
            CompletionStatus = request.CompletionStatus ?? "completed",
            ExitReason = request.ExitReason,
            ReceivedAtUtc = DateTimeOffset.UtcNow
        };

        foreach (var trial in trials)
            session.Trials.Add(new TrialEvent
            {
                TrialIndex = (short)trial.TrialIndex,
                TrialType = trial.TrialType,
                StimulusJson = string.IsNullOrWhiteSpace(trial.StimulusJson) ? null : trial.StimulusJson,
                ExpectedResponse = trial.ExpectedResponse,
                ActualResponse = trial.ActualResponse,
                IsCorrect = trial.IsCorrect,
                ReactionTimeMs = trial.ReactionTimeMs,
                PresentationDurationMs = trial.PresentationDurationMs
            });

        var requestedDomainCodes = (request.Metrics ?? []).Where(x => !string.IsNullOrWhiteSpace(x.DomainCode))
            .Select(x => x.DomainCode!).Distinct().ToArray();
        var domains = await db.CognitiveDomains.Where(x => requestedDomainCodes.Contains(x.Code))
            .ToDictionaryAsync(x => x.Code, ct);
        foreach (var metric in request.Metrics ?? [])
        {
            var domain = !string.IsNullOrWhiteSpace(metric.DomainCode) && domains.TryGetValue(metric.DomainCode, out var selected)
                ? selected : game.Domain;
            session.Metrics.Add(new DerivedMetric
            {
                DomainId = domain.Id,
                MetricCode = metric.MetricCode,
                Value = metric.Value,
                Unit = metric.Unit,
                CalculationVersion = metric.CalculationVersion,
                QualityFlag = metric.QualityFlag ?? "valid"
            });
        }

        db.AssessmentSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/assessments/{session.Id}", new
            { session.Id, status = "stored", trials = session.Trials.Count, metrics = session.Metrics.Count });
    }

    private static async Task<IResult> HistoryAsync(int? limit, HttpRequest request, IIdentityService identity,
        LtcDbContext db, CancellationToken ct)
    {
        var player = await identity.AuthenticateAsync(request.Headers.Authorization, ct);
        if (player is null) return Results.Unauthorized();
        var rows = await db.AssessmentSessions.AsNoTracking().Where(x => x.PlayerId == player.PlayerId)
            .OrderByDescending(x => x.EndedAtUtc).Take(Math.Clamp(limit ?? 30, 1, 100))
            .Select(x => new
            {
                sessionId = x.Id, gameCode = x.Game.Code, gameName = x.Game.NameZhTw,
                x.StartedAtUtc, x.EndedAtUtc, x.CompletionStatus,
                metrics = x.Metrics.Select(m => new { m.MetricCode, m.Value, m.Unit, domain = m.Domain.Code })
            }).ToListAsync(ct);
        return Results.Ok(rows);
    }

    private static async Task<IResult> TrendsAsync(int? days, string? domain, HttpRequest request,
        IIdentityService identity, LtcDbContext db, CancellationToken ct)
    {
        var player = await identity.AuthenticateAsync(request.Headers.Authorization, ct);
        if (player is null) return Results.Unauthorized();
        var range = Math.Clamp(days ?? 30, 1, 365);
        var since = DateTimeOffset.UtcNow.AddDays(-range);
        var query = db.DerivedMetrics.AsNoTracking().Where(x => x.Session.PlayerId == player.PlayerId &&
            x.Session.EndedAtUtc >= since && x.QualityFlag == "valid");
        if (!string.IsNullOrWhiteSpace(domain)) query = query.Where(x => x.Domain.Code == domain);
        var points = await query.OrderBy(x => x.Session.EndedAtUtc).Select(x => new
        {
            timestampUtc = x.Session.EndedAtUtc, gameCode = x.Session.Game.Code,
            domainCode = x.Domain.Code, x.MetricCode, x.Value, x.Unit, x.CalculationVersion
        }).ToListAsync(ct);
        return Results.Ok(new { days = range, domain, points });
    }
}
