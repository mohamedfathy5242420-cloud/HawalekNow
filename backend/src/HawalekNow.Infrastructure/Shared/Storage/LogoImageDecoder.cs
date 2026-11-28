using HawalekNow.Application.Shared.Storage;
using ImageMagick;
using ImageMagick.Configuration;

namespace HawalekNow.Infrastructure.Shared.Storage;

public static class LogoImageDecoder
{
    private static readonly Lazy<bool> Initialized = new(Initialize);
    private static readonly SemaphoreSlim DecodeLock = new(1, 1);

    private static bool Initialize()
    {
        // Process-wide policy is installed before the first native decoder call.
        var directory = Path.Combine(Path.GetTempPath(), "HN_ImagePolicy_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory); PrivateStoragePermissions.Apply(directory, true);
        var files = ConfigurationFiles.Default;
        files.Policy.Data = """
            <policymap>
              <policy domain="delegate" rights="none" pattern="*" />
              <policy domain="filter" rights="none" pattern="*" />
              <policy domain="coder" rights="none" pattern="*" />
              <policy domain="coder" rights="read|write" pattern="{JPEG,PNG}" />
              <policy domain="path" rights="none" pattern="@*" />
            </policymap>
            """;
        MagickNET.Initialize(files, directory);
        foreach (var file in Directory.EnumerateFiles(directory)) PrivateStoragePermissions.Apply(file, false);
        ResourceLimits.Width = ImageStorageLimits.MaxDimension; ResourceLimits.Height = ImageStorageLimits.MaxDimension;
        ResourceLimits.Area = ImageStorageLimits.MaxDimension * ImageStorageLimits.MaxDimension;
        ResourceLimits.Memory = 64 * 1024 * 1024; ResourceLimits.MaxMemoryRequest = 64 * 1024 * 1024;
        // Native pixel-cache checks require a limit above the one-image list length.
        // Count is still explicitly one; APNG is rejected before decoding.
        ResourceLimits.MaxProfileSize = 1024 * 1024; ResourceLimits.Disk = 0; ResourceLimits.ListLength = 2; ResourceLimits.Thread = 1;
        return true;
    }

    public static async Task<(byte[] Bytes, int Width, int Height)> DecodeAsync(Stream input, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[16384];
        while (true)
        {
            var count = await input.ReadAsync(chunk, cancellationToken);
            if (count == 0) break;
            if (buffer.Length + count > ImageStorageLimits.MaxInputBytes) throw new InvalidImageException();
            buffer.Write(chunk, 0, count);
        }
        var bytes = buffer.ToArray();
        MagickFormat format;
        if (bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) format = MagickFormat.Png;
        else if (bytes.AsSpan().StartsWith(new byte[] { 255, 216, 255 })) format = MagickFormat.Jpeg;
        else throw new InvalidImageException();
        if (format == MagickFormat.Png && !SinglePng(bytes)) throw new InvalidImageException();
        await DecodeLock.WaitAsync(cancellationToken);
        try
        {
            _ = Initialized.Value;
            var settings = new MagickReadSettings { Format = format };
            using var images = new MagickImageCollection(bytes, settings);
            if (images.Count != 1) throw new InvalidImageException();
            var image = images[0];
            if (image.Width is 0 or > ImageStorageLimits.MaxDimension || image.Height is 0 or > ImageStorageLimits.MaxDimension) throw new InvalidImageException();
            image.AutoOrient(); image.Strip(); image.Depth = 8; image.ColorSpace = ColorSpace.sRGB;
            var normalized = image.ToByteArray(MagickFormat.Png);
            if (normalized.Length > ImageStorageLimits.MaxStoredBytes) throw new InvalidImageException();
            return (normalized, checked((int)image.Width), checked((int)image.Height));
        }
        catch (MagickException) { throw new InvalidImageException(); }
        finally { DecodeLock.Release(); }
    }

    private static bool SinglePng(byte[] bytes)
    {
        var offset = 8;
        while (offset <= bytes.Length - 12)
        {
            var length = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4));
            if (length > int.MaxValue || length > bytes.Length - offset - 12) return false;
            var type = bytes.AsSpan(offset + 4, 4);
            if (type.SequenceEqual("acTL"u8) || type.SequenceEqual("fcTL"u8) || type.SequenceEqual("fdAT"u8)) return false;
            offset += checked((int)length + 12);
            if (type.SequenceEqual("IEND"u8)) return offset == bytes.Length;
        }
        return false;
    }
}
