namespace LtcCognitive.Api.Contracts;

public sealed record GuestSignInRequest(string InstallationUid, string? DisplayName);

public sealed record PlayerSessionResponse(
    long PlayerId,
    string PlayerCode,
    string? DisplayName,
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    bool IsNewPlayer);
