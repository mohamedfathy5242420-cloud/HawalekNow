using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using HawalekNow.Application.Shared.Storage;
using HawalekNow.Infrastructure.Shared.Storage;

namespace HawalekNow.IntegrationTests;

public sealed class LocalFileStorageTests
{
    private static string Root()
    {
        var path = Path.Combine(Path.GetTempPath(), "HN_StorageTest_" + Guid.NewGuid().ToString("N"), "images");
        // Fixtures retain only their own isolated temporary files; no recursive cleanup of external directories.
        return path;
    }
    private static string Key() => Guid.CreateVersion7().ToString("N");

    [Fact]
    public async Task Actual_decode_strips_metadata_and_survives_a_new_storage_instance_with_private_permissions()
    {
        var root = Root(); var key = Key(); var first = new LocalFileStorage(root, TimeProvider.System);
        var info = await first.WriteImageAsync(key, new MemoryStream(StorageTestImages.Png(3, 2, "private-location-marker")), default);
        Assert.Equal(3, info.Width); Assert.Equal(2, info.Height); Assert.Equal(key, info.Key);
        var bytes = await new LocalFileStorage(root, TimeProvider.System).ReadImageAsync(key, default);
        Assert.NotNull(bytes); Assert.Equal(info.Length, bytes.LongLength);
        Assert.DoesNotContain("private-location-marker", Encoding.UTF8.GetString(bytes));
        Assert.True(bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }));
        PrivateStoragePermissions.Require(root, true);
        foreach (var path in Directory.EnumerateFiles(root)) PrivateStoragePermissions.Require(path, false);
        await new LocalFileStorage(root, TimeProvider.System).DeleteOwnedAsync(key, default);
        await first.DeleteOwnedAsync(key, default); // Retry after successful deletion is safe.
        Assert.Null(await first.ReadImageAsync(key, default));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task Content_not_a_filename_controls_acceptance(int variant)
    {
        var bytes = variant switch
        {
            0 => Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'><script>secret</script></svg>"),
            1 => Encoding.ASCII.GetBytes("GIF89a-not-accepted"),
            2 => new byte[] { 255, 216, 255, 0 },
            3 => StorageTestImages.Png(1, 1)[..30],
            4 => StorageTestImages.Png(1, 1).Concat(Encoding.ASCII.GetBytes("<script>trailing</script>")).ToArray(),
            _ => StorageTestImages.Png(1, 1, animated: true)
        };
        var root = Root(); var storage = new LocalFileStorage(root, TimeProvider.System);
        await Assert.ThrowsAsync<InvalidImageException>(() => storage.WriteImageAsync(Key(), new MemoryStream(bytes), default));
        Assert.Empty(Directory.EnumerateFiles(root, "*.png"));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("/tmp/escape")]
    [InlineData("C:\\escape.png")]
    [InlineData("https://example.com/x")]
    [InlineData("0123456789012345678901234567890A")]
    [InlineData("01234567890123456789012345678901.png")]
    [InlineData("")]
    public async Task Paths_and_nongenerated_keys_never_reach_the_filesystem(string key)
    {
        var root = Root(); var storage = new LocalFileStorage(root, TimeProvider.System);
        await Assert.ThrowsAsync<InvalidImageException>(() => storage.WriteImageAsync(key, new MemoryStream(StorageTestImages.Png()), default));
        await Assert.ThrowsAsync<InvalidImageException>(() => storage.ReadImageAsync(key, default));
        await Assert.ThrowsAsync<InvalidImageException>(() => storage.DeleteOwnedAsync(key, default));
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public async Task Input_and_decoded_dimension_limits_reject_small_compressed_and_large_stream_inputs()
    {
        var root = Root(); var storage = new LocalFileStorage(root, TimeProvider.System);
        await Assert.ThrowsAsync<InvalidImageException>(() => storage.WriteImageAsync(Key(), new MemoryStream(new byte[ImageStorageLimits.MaxInputBytes + 1]), default));
        await Assert.ThrowsAsync<InvalidImageException>(() => storage.WriteImageAsync(Key(), new MemoryStream(StorageTestImages.Png(2049, 1)), default));
        Assert.Empty(Directory.EnumerateFiles(root, "*.png"));
    }

    [Fact]
    public async Task Foreign_directory_and_changed_or_unrecorded_files_are_never_deleted()
    {
        var foreignRoot = Root(); Directory.CreateDirectory(foreignRoot);
        var foreignPath = Path.Combine(foreignRoot, "private.cs"); await File.WriteAllTextAsync(foreignPath, "foreign-keep");
        var foreign = new LocalFileStorage(foreignRoot, TimeProvider.System);
        await Assert.ThrowsAsync<StorageIntegrityException>(() => foreign.WriteImageAsync(Key(), new MemoryStream(StorageTestImages.Png()), default));
        Assert.Equal("foreign-keep", await File.ReadAllTextAsync(foreignPath)); Assert.Single(Directory.EnumerateFiles(foreignRoot));
        var root = Root(); var key = Key(); var storage = new LocalFileStorage(root, TimeProvider.System);
        await storage.WriteImageAsync(key, new MemoryStream(StorageTestImages.Png()), default);
        await File.WriteAllTextAsync(Path.Combine(root, key + ".png"), "foreign-replacement-keep");
        await Assert.ThrowsAsync<StorageIntegrityException>(() => storage.ReadImageAsync(key, default));
        await Assert.ThrowsAsync<StorageIntegrityException>(() => storage.DeleteOwnedAsync(key, default));
        Assert.Equal("foreign-replacement-keep", await File.ReadAllTextAsync(Path.Combine(root, key + ".png")));
        var unrecorded = Key(); var unrecordedPath = Path.Combine(root, unrecorded + ".png");
        await File.WriteAllTextAsync(unrecordedPath, "unrecorded-keep");
        await Assert.ThrowsAsync<StorageIntegrityException>(() => storage.DeleteOwnedAsync(unrecorded, default));
        Assert.Equal("unrecorded-keep", await File.ReadAllTextAsync(unrecordedPath));
    }

    [Fact]
    public async Task Concurrent_writes_never_overwrite_an_existing_key()
    {
        var root = Root(); var key = Key(); var first = new LocalFileStorage(root, TimeProvider.System); var second = new LocalFileStorage(root, TimeProvider.System);
        async Task<bool> Write(LocalFileStorage storage)
        {
            try { await storage.WriteImageAsync(key, new MemoryStream(StorageTestImages.Png()), default); return true; }
            catch (IOException) { return false; }
        }
        var results = await Task.WhenAll(Write(first), Write(second));
        Assert.Single(results, value => value); Assert.Single(results, value => !value);
        Assert.NotNull(await new LocalFileStorage(root, TimeProvider.System).ReadImageAsync(key, default));
        Assert.Single(Directory.EnumerateFiles(root, "*.png")); Assert.Single(Directory.EnumerateFiles(root, "*.json"));
    }

    [Fact]
    public async Task A_real_filesystem_collision_keeps_the_foreign_target_and_does_not_publish_a_manifest()
    {
        var root = Root(); var storage = new LocalFileStorage(root, TimeProvider.System);
        await storage.DeleteOwnedAsync(Key(), default); // Initialize an owned private directory.
        var key = Key(); var target = Path.Combine(root, key + ".png"); Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(Path.Combine(target, "keep.txt"), "foreign-directory");
        await Assert.ThrowsAnyAsync<IOException>(() => storage.WriteImageAsync(key, new MemoryStream(StorageTestImages.Png()), default));
        Assert.Equal("foreign-directory", await File.ReadAllTextAsync(Path.Combine(target, "keep.txt")));
        Assert.False(File.Exists(Path.Combine(root, key + ".json")));
    }

    [Fact]
    public async Task Restart_preserves_creation_time_for_orphan_reconciliation()
    {
        var root = Root(); var key = Key(); var storage = new LocalFileStorage(root, TimeProvider.System);
        var info = await storage.WriteImageAsync(key, new MemoryStream(StorageTestImages.Png()), default);
        var old = new List<StoredImage>();
        await foreach (var image in new LocalFileStorage(root, TimeProvider.System).OldImagesAsync(info.CreatedAt.AddHours(25), default)) old.Add(image);
        Assert.Equal(info, Assert.Single(old));
        await foreach (var image in storage.OldImagesAsync(info.CreatedAt.AddHours(-1), default)) Assert.Fail("A new file was selected as old.");
    }

    [Fact]
    public async Task Valid_JPEG_is_decoded_and_reencoded_without_comments_or_appended_payloads()
    {
        _ = await LogoImageDecoder.DecodeAsync(new MemoryStream(StorageTestImages.Png()), default);
        using var image = new ImageMagick.MagickImage(StorageTestImages.Png(2, 2));
        image.SetAttribute("comment", "private-jpeg-comment");
        var bytes = image.ToByteArray(ImageMagick.MagickFormat.Jpeg).Concat(Encoding.ASCII.GetBytes("<script>private-tail</script>")).ToArray();
        var storage = new LocalFileStorage(Root(), TimeProvider.System); var key = Key();
        await storage.WriteImageAsync(key, new MemoryStream(bytes), default);
        var stored = await storage.ReadImageAsync(key, default); Assert.NotNull(stored);
        Assert.True(stored.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }));
        Assert.DoesNotContain("private-jpeg-comment", Encoding.UTF8.GetString(stored)); Assert.DoesNotContain("private-tail", Encoding.UTF8.GetString(stored));
    }

    [Fact]
    public async Task A_directory_link_or_linked_ancestor_is_rejected_before_writing_to_its_target()
    {
        var fixture = Path.GetDirectoryName(Root())!; Directory.CreateDirectory(fixture);
        var target = Path.Combine(fixture, "foreign-target"); Directory.CreateDirectory(target);
        var link = Path.Combine(fixture, "directory-link");
        if (OperatingSystem.IsWindows())
        {
            var start = new System.Diagnostics.ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"))
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in new[] { "/c", "mklink", "/J", link, target }) start.ArgumentList.Add(argument);
            using var process = System.Diagnostics.Process.Start(start)!; await process.WaitForExitAsync(); Assert.Equal(0, process.ExitCode);
        }
        else Directory.CreateSymbolicLink(link, target);
        foreach (var root in new[] { link, Path.Combine(link, "nested", "images") })
            await Assert.ThrowsAsync<StorageIntegrityException>(() => new LocalFileStorage(root, TimeProvider.System).WriteImageAsync(Key(), new MemoryStream(StorageTestImages.Png()), default));
        Assert.Empty(Directory.EnumerateFileSystemEntries(target));
    }

    [Fact]
    public async Task Broadening_file_permissions_causes_reads_and_deletion_to_fail_closed()
    {
        var root = Root(); var key = Key(); var storage = new LocalFileStorage(root, TimeProvider.System);
        await storage.WriteImageAsync(key, new MemoryStream(StorageTestImages.Png()), default);
        var path = Path.Combine(root, key + ".png");
        try
        {
            if (OperatingSystem.IsWindows()) BroadenWindows(path);
            else File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.OtherRead);
            await Assert.ThrowsAsync<StorageIntegrityException>(() => storage.ReadImageAsync(key, default));
            await Assert.ThrowsAsync<StorageIntegrityException>(() => storage.DeleteOwnedAsync(key, default)); Assert.True(File.Exists(path));
        }
        finally { PrivateStoragePermissions.Apply(path, false); }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static void BroadenWindows(string path)
    {
        var file = new FileInfo(path); var acl = System.IO.FileSystemAclExtensions.GetAccessControl(file);
        acl.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.WorldSid, null),
            System.Security.AccessControl.FileSystemRights.Read, System.Security.AccessControl.AccessControlType.Allow));
        System.IO.FileSystemAclExtensions.SetAccessControl(file, acl);
    }
}

public static class StorageTestImages
{
    // Independent minimal PNG fixture, using the documented PNG chunk format and BCL compression.
    public static byte[] Png(int width = 1, int height = 1, string? text = null, bool animated = false)
    {
        using var output = new MemoryStream(); output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var header = new byte[13]; BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width); BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height); header[8] = 8; header[9] = 2;
        Chunk(output, "IHDR", header);
        if (text is not null) Chunk(output, "tEXt", Encoding.ASCII.GetBytes("Comment\0" + text));
        if (animated) Chunk(output, "acTL", new byte[] { 0, 0, 0, 1, 0, 0, 0, 0 });
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, true))
            for (var row = 0; row < height; row++) { zlib.WriteByte(0); zlib.Write(new byte[checked(width * 3)]); }
        Chunk(output, "IDAT", compressed.ToArray()); Chunk(output, "IEND", []); return output.ToArray();
    }
    private static void Chunk(Stream target, string name, byte[] data)
    {
        var header = new byte[4]; BinaryPrimitives.WriteInt32BigEndian(header, data.Length); target.Write(header);
        var type = Encoding.ASCII.GetBytes(name); target.Write(type); target.Write(data);
        uint crc = uint.MaxValue;
        foreach (var value in type.Concat(data)) { crc ^= value; for (var bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? 0xedb88320U ^ (crc >> 1) : crc >> 1; }
        BinaryPrimitives.WriteUInt32BigEndian(header, crc ^ uint.MaxValue); target.Write(header);
    }
}
