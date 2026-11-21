using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;

namespace HawalekNow.IntegrationTests;

// Real TLS socket to Program. Only this ephemeral test CA is trusted; no machine trust changes.
public sealed class TestHttpsClient : IAsyncDisposable
{
    private readonly RSA rootKey = RSA.Create(2048);
    private readonly RSA serverKey = RSA.Create(2048);
    private readonly X509Certificate2 root;
    private readonly X509Certificate2 server;
    public IdentityApiFactory Factory { get; }
    public CookieContainer Cookies { get; } = new();
    public HttpClient Client { get; }
    public Uri Address { get; }

    public TestHttpsClient(SqlDatabase database, TimeProvider? clock = null)
    {
        var now = DateTimeOffset.UtcNow;
        var rootRequest = new CertificateRequest("CN=HawalekNow isolated test CA", rootKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        rootRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        root = rootRequest.CreateSelfSigned(now.AddDays(-1), now.AddDays(7));
        var request = new CertificateRequest("CN=localhost", serverKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, true));
        var names = new SubjectAlternativeNameBuilder(); names.AddDnsName("localhost"); names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        using var signed = request.Create(root, now.AddHours(-1), now.AddDays(2), RandomNumberGenerator.GetBytes(16));
        using var withKey = signed.CopyWithPrivateKey(serverKey);
        var pkcs12 = withKey.Export(X509ContentType.Pkcs12);
        try
        {
            // Schannel needs a temporary named key. No PersistKeySet or certificate-store installation;
            // disposing this certificate removes its key. Other platforms can keep it wholly in memory.
            server = X509CertificateLoader.LoadPkcs12(pkcs12, null,
                OperatingSystem.IsWindows() ? X509KeyStorageFlags.DefaultKeySet : X509KeyStorageFlags.EphemeralKeySet);
        }
        finally { CryptographicOperations.ZeroMemory(pkcs12); }
        Factory = new(database, clock);
        Factory.UseKestrel(options => options.Listen(IPAddress.Loopback, 0, listener => listener.UseHttps(server)));
        var addresses = Factory.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses;
        Address = new(addresses.Single(address => address.StartsWith("https:", StringComparison.Ordinal)));
        Client = CreateClient(Cookies);
    }
    public HttpClient CreateClient(CookieContainer? cookies = null)
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            CookieContainer = cookies ?? new(),
            ServerCertificateCustomValidationCallback = (_, certificate, _, errors) =>
            {
                if (certificate is null || (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) != SslPolicyErrors.None) return false;
                using var chain = new X509Chain();
                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                chain.ChainPolicy.CustomTrustStore.Add(root);
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                chain.ChainPolicy.DisableCertificateDownloads = true;
                return chain.Build(certificate);
            }
        };
        return new(handler) { BaseAddress = Address };
    }
    public async ValueTask DisposeAsync()
    {
        Client.Dispose(); await Factory.DisposeAsync(); server.Dispose(); root.Dispose(); serverKey.Dispose(); rootKey.Dispose();
    }
}
