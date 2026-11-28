using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using HawalekNow.Application.Shared.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HawalekNow.Infrastructure.Shared.Storage;

public sealed class LocalFileStorage(string root, TimeProvider clock) : IFileStorage
{
    private const string OwnerPrefix = "HawalekNow.Images.v1:";
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> RootLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly string directory = SafeRoot(root);
    private Guid? installation;

    public async Task<StoredImage> WriteImageAsync(string key, Stream input, CancellationToken cancellationToken)
    {
        Key(key); await EnsureRootAsync(cancellationToken);
        var decoded = await LogoImageDecoder.DecodeAsync(input, cancellationToken);
        var manifest = new Manifest(installation!.Value, key, Convert.ToHexString(SHA256.HashData(decoded.Bytes)),
            decoded.Width, decoded.Height, decoded.Bytes.LongLength, clock.GetUtcNow());
        var metadata = JsonSerializer.SerializeToUtf8Bytes(manifest);
        var blob = Blob(key); var record = Record(key);
        if (File.Exists(blob) || File.Exists(record)) throw new StorageIntegrityException();
        var temporaryBlob = blob + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var temporaryRecord = record + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var moved = false; var blobReady = false; var recordReady = false;
        try
        {
            await WritePrivateAsync(temporaryBlob, decoded.Bytes, cancellationToken); blobReady = true;
            await WritePrivateAsync(temporaryRecord, metadata, cancellationToken); recordReady = true;
            PrivateStoragePermissions.Require(directory, true);
            File.Move(temporaryBlob, blob, false); moved = true;
            File.Move(temporaryRecord, record, false);
            return Info(manifest);
        }
        catch
        {
            // Only exact bytes produced by this attempt may be removed; partial/changed files are retained.
            if (blobReady) TryDeleteExact(temporaryBlob, decoded.Bytes);
            if (recordReady) TryDeleteExact(temporaryRecord, metadata);
            if (moved) TryDeleteExact(blob, decoded.Bytes);
            throw;
        }
    }

    public async Task<byte[]?> ReadImageAsync(string key, CancellationToken cancellationToken)
    {
        Key(key); await EnsureRootAsync(cancellationToken);
        var manifest = await ReadManifestAsync(key, cancellationToken);
        if (manifest is null)
        {
            if (File.Exists(Blob(key))) throw new StorageIntegrityException();
            return null;
        }
        return await VerifiedBytesAsync(manifest, cancellationToken);
    }

    public async Task DeleteOwnedAsync(string key, CancellationToken cancellationToken)
    {
        Key(key); await EnsureRootAsync(cancellationToken);
        var manifest = await ReadManifestAsync(key, cancellationToken);
        if (manifest is null)
        {
            if (File.Exists(Blob(key))) throw new StorageIntegrityException();
            return;
        }
        if (File.Exists(Blob(key)))
        {
            _ = await VerifiedBytesAsync(manifest, cancellationToken);
            File.Delete(Blob(key));
        }
        PrivateStoragePermissions.Require(Record(key), false);
        File.Delete(Record(key));
    }

    public async IAsyncEnumerable<StoredImage> OldImagesAsync(DateTimeOffset olderThan, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await EnsureRootAsync(cancellationToken);
        foreach (var path in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = Path.GetFileNameWithoutExtension(path);
            if (!ImageStorageLimits.ValidKey(key)) continue;
            Manifest? manifest;
            try { manifest = await ReadManifestAsync(key, cancellationToken); }
            catch (StorageIntegrityException) { continue; }
            if (manifest is not null && manifest.CreatedAt < olderThan) yield return Info(manifest);
        }
    }

    private async Task<byte[]> VerifiedBytesAsync(Manifest manifest, CancellationToken cancellationToken)
    {
        var path = Blob(manifest.Key);
        if (!File.Exists(path)) throw new StorageIntegrityException();
        PrivateStoragePermissions.Require(path, false);
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 16384, FileOptions.Asynchronous);
        if (stream.Length != manifest.Length || stream.Length > ImageStorageLimits.MaxStoredBytes) throw new StorageIntegrityException();
        var bytes = new byte[checked((int)stream.Length)];
        await stream.ReadExactlyAsync(bytes, cancellationToken);
        if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(bytes), Convert.FromHexString(manifest.Sha256))) throw new StorageIntegrityException();
        return bytes;
    }

    private async Task<Manifest?> ReadManifestAsync(string key, CancellationToken cancellationToken)
    {
        var path = Record(key);
        if (!File.Exists(path)) return null;
        PrivateStoragePermissions.Require(path, false);
        if (new FileInfo(path).Length > 4096) throw new StorageIntegrityException();
        Manifest? value;
        try { value = JsonSerializer.Deserialize<Manifest>(await File.ReadAllBytesAsync(path, cancellationToken)); }
        catch (JsonException) { throw new StorageIntegrityException(); }
        if (value is null || value.Installation != installation || value.Key != key || value.Length is <= 0 or > ImageStorageLimits.MaxStoredBytes ||
            value.Width is <= 0 or > ImageStorageLimits.MaxDimension || value.Height is <= 0 or > ImageStorageLimits.MaxDimension ||
            value.CreatedAt == default || value.Sha256 is not { Length: 64 } || !value.Sha256.All(Uri.IsHexDigit)) throw new StorageIntegrityException();
        return value;
    }

    private async Task EnsureRootAsync(CancellationToken cancellationToken)
    {
        var gate = RootLocks.GetOrAdd(directory, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            PrivateStoragePermissions.RejectLinks(directory);
            var ownerFile = Path.Combine(directory, ".hawaleknow-owner");
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory); PrivateStoragePermissions.Apply(directory, true);
            }
            if (!File.Exists(ownerFile))
            {
                // A populated directory without this marker is foreign; never change its permissions or contents.
                if (Directory.EnumerateFileSystemEntries(directory).Any()) throw new StorageIntegrityException();
                PrivateStoragePermissions.Apply(directory, true);
                await WritePrivateAsync(ownerFile, System.Text.Encoding.ASCII.GetBytes(OwnerPrefix + Guid.NewGuid().ToString("N")), cancellationToken);
            }
            PrivateStoragePermissions.Require(directory, true); PrivateStoragePermissions.Require(ownerFile, false);
            if (new FileInfo(ownerFile).Length != OwnerPrefix.Length + 32) throw new StorageIntegrityException();
            var owner = await File.ReadAllTextAsync(ownerFile, cancellationToken);
            if (!owner.StartsWith(OwnerPrefix, StringComparison.Ordinal) || !Guid.TryParseExact(owner[OwnerPrefix.Length..], "N", out var id) || id == Guid.Empty ||
                installation is not null && installation != id) throw new StorageIntegrityException();
            installation = id;
        }
        finally { gate.Release(); }
    }

    private static async Task WritePrivateAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        PrivateStoragePermissions.RejectLinks(path);
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 16384, FileOptions.Asynchronous | FileOptions.WriteThrough);
        PrivateStoragePermissions.Apply(path, false);
        await file.WriteAsync(bytes, cancellationToken); await file.FlushAsync(cancellationToken); file.Flush(true);
    }

    private static void TryDeleteExact(string path, byte[] expected)
    {
        try
        {
            if (!File.Exists(path)) return;
            PrivateStoragePermissions.Require(path, false);
            if (new FileInfo(path).Length == expected.LongLength && CryptographicOperations.FixedTimeEquals(SHA256.HashData(File.ReadAllBytes(path)), SHA256.HashData(expected))) File.Delete(path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static string SafeRoot(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value) || value.StartsWith(@"\\", StringComparison.Ordinal)) throw new StorageIntegrityException();
        var full = Path.GetFullPath(value).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (full.Length < 4 || string.Equals(full, Path.GetPathRoot(value)?.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)) throw new StorageIntegrityException();
        return full;
    }
    private static void Key(string key) { if (!ImageStorageLimits.ValidKey(key)) throw new InvalidImageException(); }
    private string Blob(string key) => Path.Combine(directory, key + ".png");
    private string Record(string key) => Path.Combine(directory, key + ".json");
    private static StoredImage Info(Manifest value) => new(value.Key, value.Width, value.Height, value.Length, value.CreatedAt);
    private sealed record Manifest(Guid Installation, string Key, string Sha256, int Width, int Height, long Length, DateTimeOffset CreatedAt);
}

public static class FileStorageRegistration
{
    public static IServiceCollection AddLocalFileStorage(this IServiceCollection services) => services.AddSingleton<IFileStorage>(provider =>
    {
        var configuration = provider.GetRequiredService<IConfiguration>(); var environment = provider.GetRequiredService<IHostEnvironment>();
        var root = configuration["FileStorage:Root"] ?? Path.Combine(environment.ContentRootPath, "App_Data", "StoreFiles");
        var webroot = Path.GetFullPath(provider.GetService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>()?.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"));
        var full = Path.GetFullPath(root);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (full.Equals(webroot, comparison) || full.StartsWith(webroot + Path.DirectorySeparatorChar, comparison)) throw new StorageIntegrityException();
        return new LocalFileStorage(root, provider.GetRequiredService<TimeProvider>());
    });
}
