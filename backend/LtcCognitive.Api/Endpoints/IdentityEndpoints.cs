using LtcCognitive.Api.Contracts;
using LtcCognitive.Api.Services;

namespace LtcCognitive.Api.Endpoints;

public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v2/auth").WithTags("Identity").RequireRateLimiting("authentication");
        group.MapPost("/guest", async (GuestSignInRequest request, IIdentityService identity, CancellationToken ct) =>
        {
            try { return Results.Ok(await identity.SignInGuestAsync(request, ct)); }
            catch (ArgumentException exception)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["installationUid"] = [exception.Message] });
            }
        });
        group.MapGet("/me", async (HttpRequest request, IIdentityService identity, CancellationToken ct) =>
        {
            var player = await identity.AuthenticateAsync(request.Headers.Authorization, ct);
            return player is null ? Results.Unauthorized() : Results.Ok(player);
        });
        group.MapPost("/logout", async (HttpRequest request, IIdentityService identity, CancellationToken ct) =>
            await identity.RevokeAsync(request.Headers.Authorization, ct) ? Results.NoContent() : Results.Unauthorized());
        return app;
    }
}
