using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using HawalekNow.Application.Modules.Locations;

namespace HawalekNow.Infrastructure.Modules.Locations;

public sealed class EmbeddedReferenceSeedSource : IReferenceSeedSource
{
    private readonly Lazy<ReferenceSeedDataset> source = new(Load);
    public ReferenceSeedDataset Read() => source.Value;
    private static ReferenceSeedDataset Load()
    {
        var dataset = new ReferenceSeedDataset(ReferenceSeedRules.AcceptedCommit,
            ReadFile("all-governorate.json", "EA86B0A7C7219D1DC7C79D6FAECF1C66325F78443277CC491D8D39A94CC9CA27", 27, true),
            ReadFile("all-district.json", "F265357BDF7248FD162F1CC23B7C8A68F966CA4BC2988FED408AEF078C1C01D8", 365, false),
            ReadFile("all-shiyakha.json", "A4458361D556ADB1617C836D804FDE399757FA849D0ED7351A5C428E21A51002", 5716, false));
        ReferenceSeedRules.Validate(dataset); return dataset;
    }
    private static ImmutableArray<SeedPlace> ReadFile(string file, string hash, int count, bool roots)
    {
        using var stream = typeof(EmbeddedReferenceSeedSource).Assembly.GetManifestResourceStream("HawalekNow.ReferenceSeed." + file)
            ?? throw new InvalidOperationException("The pinned reference dataset is missing.");
        using var memory = new MemoryStream(); stream.CopyTo(memory); var bytes = memory.ToArray();
        if (Convert.ToHexString(SHA256.HashData(bytes)) != hash) throw new InvalidOperationException("The pinned reference dataset checksum is invalid.");
        using var json = JsonDocument.Parse(bytes);
        if (json.RootElement.ValueKind != JsonValueKind.Array || json.RootElement.GetArrayLength() != count) throw new InvalidOperationException("The pinned reference dataset count is invalid.");
        return json.RootElement.EnumerateArray().Select(value => new SeedPlace(value.GetProperty("id").GetString()!,
            roots ? null : value.GetProperty("parent").GetProperty("id").GetString(), value.GetProperty("name").GetProperty("local").GetString()!, value.GetProperty("name").GetProperty("en").GetString()!)).ToImmutableArray();
    }
}
