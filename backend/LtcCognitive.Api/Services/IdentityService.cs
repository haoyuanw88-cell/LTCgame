using System.Security.Cryptography;
using System.Text;
using LtcCognitive.Api.Contracts;
using LtcCognitive.Api.Data;
using LtcCognitive.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LtcCognitive.Api.Services;

public sealed class IdentityOptions
{
    public const string SectionName = "Identity";
    public required string SubjectHashKeyBase64 { get; init; }
    public int SessionLifetimeDays { get; init; } = 1;
}

public interface IIdentityService
{
    Task<PlayerSessionResponse> SignInGuestAsync(GuestSignInRequest request, CancellationToken cancellationToken);
    Task<AuthenticatedPlayer?> AuthenticateAsync(string? authorizationHeader, CancellationToken cancellationToken);
    Task<bool> RevokeAsync(string? authorizationHeader, CancellationToken cancellationToken);
}

public sealed record AuthenticatedPlayer(long PlayerId, string PlayerCode, string? DisplayName);

public sealed class IdentityService(
    LtcDbContext db,
    IOptions<IdentityOptions> options,
    TimeProvider timeProvider) : IIdentityService
{
    private const string PlayerCodeAlphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
    private readonly byte[] signingKey = DecodeSigningKey(options.Value.SubjectHashKeyBase64);
    private readonly int sessionLifetimeDays = Math.Clamp(options.Value.SessionLifetimeDays, 1, 7);

    public async Task<PlayerSessionResponse> SignInGuestAsync(GuestSignInRequest request, CancellationToken cancellationToken)
    {
        Validate(request);
        var now = timeProvider.GetUtcNow();
        var subjectHash = HashSubject("guest", request.InstallationUid);
        var player = await db.Players.SingleOrDefaultAsync(
            x => x.AuthProvider == "guest" && x.AuthSubjectHash == subjectHash, cancellationToken);
        var isNewPlayer = player is null;

        if (player is null)
        {
            player = new Player
            {
                PlayerCode = await GenerateUniquePlayerCodeAsync(cancellationToken),
                AuthProvider = "guest",
                AuthSubjectHash = subjectHash,
                DisplayName = NormalizeDisplayName(request.DisplayName) ?? "玩家",
                Status = "active",
                CreatedAtUtc = now,
                LastLoginAtUtc = now
            };
            db.Players.Add(player);
        }
        else
        {
            player.LastLoginAtUtc = now;
            if (!string.IsNullOrWhiteSpace(request.DisplayName))
                player.DisplayName = NormalizeDisplayName(request.DisplayName) ?? player.DisplayName;
        }

        await db.SaveChangesAsync(cancellationToken);
        var expiresAt = now.AddDays(sessionLifetimeDays);
        return new PlayerSessionResponse(player.PlayerId, player.PlayerCode, player.DisplayName,
            CreateAccessToken(player.PlayerId, expiresAt), expiresAt, isNewPlayer);
    }

    public async Task<AuthenticatedPlayer?> AuthenticateAsync(string? authorizationHeader, CancellationToken cancellationToken)
    {
        var token = ExtractBearerToken(authorizationHeader);
        if (token is null || !TryValidateAccessToken(token, out var playerId)) return null;
        var player = await db.Players.AsNoTracking().SingleOrDefaultAsync(
            x => x.PlayerId == playerId && x.Status == "active", cancellationToken);
        return player is null ? null : new AuthenticatedPlayer(player.PlayerId, player.PlayerCode, player.DisplayName);
    }

    public Task<bool> RevokeAsync(string? authorizationHeader, CancellationToken cancellationToken)
    {
        var token = ExtractBearerToken(authorizationHeader);
        return Task.FromResult(token is not null && TryValidateAccessToken(token, out _));
    }

    private string CreateAccessToken(long playerId, DateTimeOffset expiresAt)
    {
        var payload = $"v1.{playerId}.{expiresAt.ToUnixTimeSeconds()}";
        using var hmac = new HMACSHA256(signingKey);
        var signature = Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        return payload + "." + signature;
    }

    private bool TryValidateAccessToken(string token, out long playerId)
    {
        playerId = 0;
        var parts = token.Split('.');
        if (parts.Length != 4 || parts[0] != "v1" ||
            !long.TryParse(parts[1], out playerId) || playerId <= 0 ||
            !long.TryParse(parts[2], out var expiresUnix)) return false;
        if (DateTimeOffset.FromUnixTimeSeconds(expiresUnix) <= timeProvider.GetUtcNow()) return false;

        var payload = string.Join('.', parts[0], parts[1], parts[2]);
        using var hmac = new HMACSHA256(signingKey);
        var expected = Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(parts[3]));
    }

    private async Task<string> GenerateUniquePlayerCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var random = RandomNumberGenerator.GetBytes(10);
            var suffix = new char[10];
            for (var index = 0; index < suffix.Length; index++)
                suffix[index] = PlayerCodeAlphabet[random[index] % PlayerCodeAlphabet.Length];
            var code = "LTC-U-" + new string(suffix);
            if (!await db.Players.AnyAsync(x => x.PlayerCode == code, cancellationToken)) return code;
        }
        throw new InvalidOperationException("無法產生唯一玩家代碼，請稍後再試。");
    }

    private string HashSubject(string provider, string subject)
    {
        using var hmac = new HMACSHA256(signingKey);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(provider + "\n" + subject))).ToLowerInvariant();
    }

    private static string? ExtractBearerToken(string? value)
    {
        const string prefix = "Bearer ";
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
        var token = value[prefix.Length..].Trim();
        return token.Length is >= 32 and <= 256 ? token : null;
    }

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] DecodeSigningKey(string value)
    {
        try
        {
            var key = Convert.FromBase64String(value);
            if (key.Length < 32) throw new InvalidOperationException();
            return key;
        }
        catch
        {
            throw new InvalidOperationException("Identity:SubjectHashKeyBase64 必須是至少 32 bytes 的 Base64 金鑰。");
        }
    }

    private static void Validate(GuestSignInRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.InstallationUid) || request.InstallationUid.Length is < 16 or > 64)
            throw new ArgumentException("installationUid 長度必須介於 16 到 64。", nameof(request));
    }

    private static string? NormalizeDisplayName(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed[..Math.Min(trimmed.Length, 40)];
    }
}
