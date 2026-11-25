using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// Standalone build-time verifier. Never loads Program, starts a host, or connects to SQL.
var publish = args[0];
using var expected = JsonDocument.Parse(File.ReadAllText(args[1]));
var marker = args[2];
var assembly = Assembly.LoadFrom(Path.Combine(publish, "HawalekNow.Infrastructure.dll"));
var resources = assembly.GetManifestResourceNames()
    .Where(name => name.StartsWith("HawalekNow.ReferenceSeed.", StringComparison.Ordinal)).ToArray();
if (resources.Length != 3) throw new InvalidOperationException("Expected exactly three embedded seed resources.");
foreach (var entry in expected.RootElement.GetProperty("resources").EnumerateArray())
{
    using var stream = assembly.GetManifestResourceStream(entry.GetProperty("name").GetString()!)
        ?? throw new InvalidOperationException("Missing embedded seed resource.");
    if (Convert.ToHexString(SHA256.HashData(stream)) != entry.GetProperty("sha256").GetString())
        throw new InvalidOperationException("Embedded seed resource checksum changed.");
}
foreach (var entry in expected.RootElement.GetProperty("content").EnumerateArray())
{
    var file = Path.Combine(publish, entry.GetProperty("path").GetString()!);
    if (!File.Exists(file) || Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))) != entry.GetProperty("sha256").GetString())
        throw new InvalidOperationException("Missing or changed published source attribution/license.");
}
var utf8 = Encoding.UTF8.GetBytes(marker);
var utf16 = Encoding.Unicode.GetBytes(marker);
var count = 0;
foreach (var file in Directory.EnumerateFiles(publish, "*", SearchOption.AllDirectories))
{
    var name = Path.GetFileName(file);
    var forbidden = name.StartsWith(".env", StringComparison.OrdinalIgnoreCase)
        || name.Equals("secrets.json", StringComparison.OrdinalIgnoreCase)
        || (name.StartsWith("appsettings.", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".Local.json", StringComparison.OrdinalIgnoreCase))
        || name.Equals("appsettings.Local.json", StringComparison.OrdinalIgnoreCase)
        || new[] { ".pfx", ".p12", ".key", ".pem", ".p8", ".ppk", ".jks", ".keystore", ".pvk", ".snk", ".cer", ".crt", ".der" }
            .Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase);
    var bytes = File.ReadAllBytes(file);
    if (forbidden || bytes.AsSpan().IndexOf(utf8) >= 0 || bytes.AsSpan().IndexOf(utf16) >= 0)
        throw new InvalidOperationException("Forbidden fixture input reached the published image.");
    count++;
}
Console.WriteLine($"PASS: image assembly has three exact seed resources; SOURCE.md/LICENSE.txt unchanged; {count} published files contain no forbidden names or test markers.");
