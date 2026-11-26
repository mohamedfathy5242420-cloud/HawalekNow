using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HawalekNow.Infrastructure.Shared.Storage;

// Standalone runtime verifier: no Program host, SQL, Identity account or application database.
if (args.Length != 4 || args[0] is not ("write" or "read")) throw new InvalidOperationException("Expected an isolated storage verification command.");
var mode = args[0]; var root = args[1]; var key = args[2]; var receiptPath = args[3];
var files = new LocalFileStorage(root, TimeProvider.System);
if (mode == "write")
{
    var image = await files.WriteImageAsync(key, new MemoryStream(Png()), default);
    var bytes = await files.ReadImageAsync(key, default) ?? throw new InvalidOperationException("Written image unavailable.");
    await File.WriteAllTextAsync(receiptPath, JsonSerializer.Serialize(new Receipt(key, Convert.ToHexString(SHA256.HashData(bytes)), image.Length)));
    PrivateStoragePermissions.Apply(receiptPath, false);
}
else
{
    var expected = JsonSerializer.Deserialize<Receipt>(await File.ReadAllTextAsync(receiptPath)) ?? throw new InvalidOperationException("Missing verification receipt.");
    var bytes = await files.ReadImageAsync(key, default) ?? throw new InvalidOperationException("Image did not survive restart.");
    if (expected.Key != key || expected.Length != bytes.LongLength || expected.Sha256 != Convert.ToHexString(SHA256.HashData(bytes))) throw new InvalidOperationException("Restart changed the image.");
    PrivateStoragePermissions.Require(root, true);
    foreach (var path in Directory.EnumerateFiles(root)) PrivateStoragePermissions.Require(path, false);
}
Console.WriteLine("PASS: isolated store file " + mode + "; private permissions and normalized PNG verified.");

static byte[] Png()
{
    using var output = new MemoryStream(); output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
    var header = new byte[13]; BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), 1); BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), 1); header[8] = 8; header[9] = 2;
    Chunk(output, "IHDR", header);
    using var compressed = new MemoryStream(); using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, true)) zlib.Write(new byte[] { 0, 20, 40, 60 });
    Chunk(output, "IDAT", compressed.ToArray()); Chunk(output, "IEND", []); return output.ToArray();
}
static void Chunk(Stream target, string name, byte[] data)
{
    var header = new byte[4]; BinaryPrimitives.WriteInt32BigEndian(header, data.Length); target.Write(header);
    var type = Encoding.ASCII.GetBytes(name); target.Write(type); target.Write(data); uint crc = uint.MaxValue;
    foreach (var value in type.Concat(data)) { crc ^= value; for (var bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? 0xedb88320U ^ (crc >> 1) : crc >> 1; }
    BinaryPrimitives.WriteUInt32BigEndian(header, crc ^ uint.MaxValue); target.Write(header);
}
sealed record Receipt(string Key, string Sha256, long Length);
