using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace HawalekNow.IntegrationTests;

public static class TestIdentityConfiguration
{
    public static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    public static Dictionary<string, string?> Values(string connection, string key) => new()
    {
        ["ConnectionStrings:Application"] = connection,
        ["Identity:IdentifierKey"] = key
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
