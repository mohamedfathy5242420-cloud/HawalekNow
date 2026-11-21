using System.Security.Cryptography;
using System.Text;
using HawalekNow.Domain.Modules.Identity;
using Microsoft.Extensions.Configuration;

namespace HawalekNow.Infrastructure.Modules.Identity;

public sealed class OtpProtection
{
    private readonly byte[] key;
    public OtpProtection(IConfiguration configuration)
    {
        try { key = Convert.FromBase64String(configuration["Identity:OtpKey"] ?? ""); if (key.Length != 32) throw new FormatException(); }
        catch (FormatException) { throw new InvalidOperationException("Configure a stable 32-byte Identity:OtpKey outside source control."); }
    }
    public string Generate() => RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    public string Hash(Guid userId, OtpPurpose purpose, Guid generation, string code) => Convert.ToHexString(HMACSHA256.HashData(key,
        Encoding.UTF8.GetBytes($"{userId:N}:{(int)purpose}:{generation:N}:{code}")));
    public bool Matches(OtpChallenge challenge, string code)
    {
        if (code.Length != 6 || code.Any(character => character is < '0' or > '9')) return false;
        return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(challenge.Digest), Convert.FromHexString(Hash(challenge.UserId, challenge.Purpose, challenge.Generation, code)));
    }
}
