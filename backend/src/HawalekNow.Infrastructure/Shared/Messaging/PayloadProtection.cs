using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace HawalekNow.Infrastructure.Shared.Messaging;

public sealed class PayloadProtection
{
    private readonly byte[] key;
    public PayloadProtection(IConfiguration configuration)
    {
        try { key = Convert.FromBase64String(configuration["Outbox:EncryptionKey"] ?? ""); if (key.Length != 32) throw new FormatException(); }
        catch (FormatException) { throw new InvalidOperationException("Configure a stable 32-byte Outbox:EncryptionKey outside source control."); }
    }
    public string Protect<T>(string kind, T payload)
    {
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(payload);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, Encoding.UTF8.GetBytes(kind));
        var result = new byte[28 + ciphertext.Length];
        nonce.CopyTo(result, 0); tag.CopyTo(result, 12); ciphertext.CopyTo(result, 28);
        CryptographicOperations.ZeroMemory(plaintext);
        return "v1." + Convert.ToBase64String(result);
    }
    public T Unprotect<T>(string kind, string payload)
    {
        if (!payload.StartsWith("v1.", StringComparison.Ordinal)) throw new CryptographicException("Protected message is invalid.");
        var data = Convert.FromBase64String(payload[3..]);
        if (data.Length < 28) throw new CryptographicException("Protected message is invalid.");
        var plaintext = new byte[data.Length - 28];
        using var aes = new AesGcm(key, 16);
        aes.Decrypt(data.AsSpan(0, 12), data.AsSpan(28), data.AsSpan(12, 16), plaintext, Encoding.UTF8.GetBytes(kind));
        try { return JsonSerializer.Deserialize<T>(plaintext) ?? throw new CryptographicException("Protected message is invalid."); }
        finally { CryptographicOperations.ZeroMemory(plaintext); }
    }
}
