using LtcCognitive.Api.Contracts;
using LtcCognitive.Api.Data;
using LtcCognitive.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LtcCognitive.Api.Endpoints;

public static class ParticipantEndpoints
{
    public static IEndpointRouteBuilder MapParticipantEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/v1/participants/resolve", async (UpsertParticipantRequest request, LtcDbContext db, CancellationToken ct) =>
        {
            var code = request.AnonymousCode.Trim();
            if (code.Length is < 3 or > 64)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["anonymousCode"] = ["代碼長度須為 3 到 64。"] });

            var participant = await db.Participants.SingleOrDefaultAsync(x => x.AnonymousCode == code, ct);
            if (participant is null)
            {
                participant = new Participant { AnonymousCode = code };
                db.Participants.Add(participant);
            }
            participant.DisplayName = request.DisplayName?.Trim();
            participant.BirthYear = request.BirthYear;
            participant.EducationBand = request.EducationBand?.Trim();
            participant.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { participant.Id, participant.AnonymousCode, participant.DisplayName });
        }).WithTags("Participants");
        return app;
    }
}
