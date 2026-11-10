using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace HawalekNow.Infrastructure.Modules.Identity;

public sealed class SessionTokens
{
    private readonly SymmetricSecurityKey key;
    public string Issuer { get; }
    public string Audience { get; }
    public SessionTokens(IConfiguration configuration)
    {
        try
        {
            var bytes = Convert.FromBase64String(configuration["Jwt:SigningKey"] ?? "");
            if (bytes.Length != 32) throw new FormatException();
            key = new(bytes);
        }
        catch (FormatException) { throw new InvalidOperationException("Configure a stable 32-byte Jwt:SigningKey outside source control."); }
        Issuer = Required(configuration["Jwt:Issuer"]); Audience = Required(configuration["Jwt:Audience"]);
    }
    private static string Required(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 200 && !value.Any(char.IsControl)
        ? value : throw new InvalidOperationException("Configure Jwt:Issuer and Jwt:Audience.");
    public string Access(Guid userId, Guid sessionId, DateTimeOffset now, DateTimeOffset expiresAt) =>
        new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(Issuer, Audience,
            [new("sub", userId.ToString("N")), new("sid", sessionId.ToString("N")), new("jti", Guid.CreateVersion7().ToString("N"))],
            notBefore: now.UtcDateTime, expires: expiresAt.UtcDateTime, signingCredentials: new(key, SecurityAlgorithms.HmacSha256)));

    public TokenValidationParameters Validation(TimeProvider clock) => new()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = key,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        RequireSignedTokens = true,
        ValidateIssuer = true,
        ValidIssuer = Issuer,
        ValidateAudience = true,
        ValidAudience = Audience,
        RequireExpirationTime = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero,
        LifetimeValidator = (notBefore, expires, _, _) => notBefore.HasValue && expires.HasValue && notBefore.Value <= clock.GetUtcNow().UtcDateTime && expires.Value > clock.GetUtcNow().UtcDateTime,
        NameClaimType = "sub",
        RoleClaimType = "account_type"
    };
    public static (RefreshCredential Row, string Token) Refresh(Guid sessionId, DateTimeOffset expiresAt)
    {
        var id = Guid.CreateVersion7(); var secret = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        return (new() { Id = id, SessionId = sessionId, Digest = Digest(secret), ExpiresAt = expiresAt }, id.ToString("N") + "." + secret);
    }
    public static bool TryRead(string? token, out Guid id, out string secret)
    {
        id = Guid.Empty; secret = "";
        if (token is null || token.Length != 76 || token[32] != '.' || !Guid.TryParseExact(token[..32], "N", out id)) return false;
        secret = token[33..];
        return secret.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
    }
    public static bool Matches(RefreshCredential row, string secret) => CryptographicOperations.FixedTimeEquals(Convert.FromHexString(row.Digest), Convert.FromHexString(Digest(secret)));
    private static string Digest(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(secret)));
}
