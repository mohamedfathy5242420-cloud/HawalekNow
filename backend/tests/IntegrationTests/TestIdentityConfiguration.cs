using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace HawalekNow.IntegrationTests;

public static class TestIdentityConfiguration
{
    public static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    public static Dictionary<string, string?> Values(string connection, string key) => new()
    {
        ["ConnectionStrings:Application"] = connection,
        ["Jwt:SigningKey"] = Convert.ToBase64String(HMACSHA256.HashData(Convert.FromBase64String(key), System.Text.Encoding.UTF8.GetBytes("test-jwt"))),
        ["Jwt:Issuer"] = "hawaleknow-test",
        ["Jwt:Audience"] = "hawaleknow-test-client",
        ["Identity:IdentifierKey"] = key
        ,
        ["Identity:OtpKey"] = Convert.ToBase64String(HMACSHA256.HashData(Convert.FromBase64String(key), System.Text.Encoding.UTF8.GetBytes("test-otp")))
        ,
        ["Outbox:EncryptionKey"] = Convert.ToBase64String(HMACSHA256.HashData(Convert.FromBase64String(key), System.Text.Encoding.UTF8.GetBytes("test-outbox")))
        ,
        ["Background:Enabled"] = "false"
        ,
        ["Email:SmtpHost"] = "127.0.0.1"
        ,
        ["Email:SmtpPort"] = Environment.GetEnvironmentVariable("HAWALEKNOW_TEST_SMTP_PORT") ?? "2525"
    };
    public static IConfiguration Create(string connection, string key) =>
        new ConfigurationBuilder().AddInMemoryCollection(Values(connection, key)).Build();
}

public sealed class ManualTimeProvider(DateTimeOffset initial) : TimeProvider
{
    private long utcTicks = initial.UtcTicks;
    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref utcTicks), TimeSpan.Zero);
    public void Advance(TimeSpan duration) => Interlocked.Add(ref utcTicks, duration.Ticks);
}
