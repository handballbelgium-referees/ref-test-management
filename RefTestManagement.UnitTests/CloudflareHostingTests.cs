using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Handball.Belgium.RefTestManagement.Api.Services;
using Handball.Belgium.RefTestManagement.Hosting;
using Handball.Belgium.RefTestManagement.Hosting.Azure;
using Handball.Belgium.RefTestManagement.Hosting.Cloudflare;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class CloudflareHostingTests
{
    [Fact]
    public void DataProtectionCertificateUnprotectsKeysAfterProviderRestart()
    {
        const string password = "unit-test-pfx-password";
        var certificateBytes = CreateCertificatePfx(password);
        var configuration = CreateConfiguration(
            ("Hosting:Provider", "Cloudflare"),
            ("Cloudflare:DataProtection:PfxBase64", Convert.ToBase64String(certificateBytes)),
            ("Cloudflare:DataProtection:PfxPassword", password));
        var hostingProvider = HostingProviderConfiguration.Create(
            configuration,
            Host.CreateApplicationBuilder().Environment,
            [
                new AzureHostingProviderAdapter(),
                new CloudflareHostingProviderAdapter()
            ]);
        var configureKeyProtection = hostingProvider.CreateDataProtectionKeyProtection();
        Assert.NotNull(configureKeyProtection);

        var keyDirectory = Path.Combine(Path.GetTempPath(), $"cloudflare-dp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(keyDirectory);
        try
        {
            string protectedValue;
            using (var firstProvider = CreateDataProtectionProvider(keyDirectory, configureKeyProtection))
            {
                protectedValue = firstProvider.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("cloudflare-test")
                    .Protect("persistent test payload");
            }

            using var restartedProvider = CreateDataProtectionProvider(keyDirectory, configureKeyProtection);
            var unprotectedValue = restartedProvider.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("cloudflare-test")
                .Unprotect(protectedValue);

            Assert.Equal("persistent test payload", unprotectedValue);
        }
        finally
        {
            Directory.Delete(keyDirectory, recursive: true);
            CryptographicOperations.ZeroMemory(certificateBytes);
        }
    }

    [Fact]
    public void CloudflareDataProtectionRequiresASecretCertificateAndPassword()
    {
        var configuration = CreateConfiguration(("Hosting:Provider", "Cloudflare"));

        Assert.Throws<InvalidOperationException>(
            () => CloudflareDataProtectionConfiguration.CreateKeyProtection(configuration));
    }

    [Fact]
    public void CloudflareDataProtectionDoesNotHandleOtherProviders()
    {
        var configuration = CreateConfiguration(("Hosting:Provider", "Azure"));

        Assert.Null(CloudflareDataProtectionConfiguration.CreateKeyProtection(configuration));
    }

    [Fact]
    public async Task ValidWorkerAssertionAppliesClientContextAndRemovesForwardingHeaders()
    {
        const string secret = "0123456789abcdef0123456789abcdef";
        var configuration = CreateConfiguration(
            ("Hosting:Provider", "Cloudflare"),
            ("Cloudflare:ProxyAssertionSecret", secret));
        var downstreamCalled = false;
        var middleware = new CloudflareRequestAssertionMiddleware(
            _ =>
            {
                downstreamCalled = true;
                return Task.CompletedTask;
            },
            configuration);
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("container");
        context.Request.Headers[CloudflareRequestAssertionMiddleware.AssertionHeader] = secret;
        context.Request.Headers[CloudflareRequestAssertionMiddleware.ClientIpHeader] = "203.0.113.42";
        context.Request.Headers[CloudflareRequestAssertionMiddleware.ForwardedSchemeHeader] = "https";
        context.Request.Headers[CloudflareRequestAssertionMiddleware.ForwardedHostHeader] = "api.example.test";
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.99";
        context.Request.Headers["Forwarded"] = "for=198.51.100.99;proto=http;host=spoofed.example";
        context.Request.Headers["CF-Connecting-IP"] = "198.51.100.99";

        await middleware.InvokeAsync(context);

        Assert.True(downstreamCalled);
        Assert.Equal("203.0.113.42", new CloudflareClientIpResolver().Resolve(context));
        Assert.Equal(IPAddress.Parse("203.0.113.42"), context.Connection.RemoteIpAddress);
        Assert.Equal("https", context.Request.Scheme);
        Assert.Equal("api.example.test", context.Request.Host.Host);
        Assert.False(context.Request.Headers.ContainsKey("X-Forwarded-For"));
        Assert.False(context.Request.Headers.ContainsKey("Forwarded"));
        Assert.False(context.Request.Headers.ContainsKey("CF-Connecting-IP"));
        Assert.False(context.Request.Headers.ContainsKey(CloudflareRequestAssertionMiddleware.AssertionHeader));
    }

    [Fact]
    public async Task InvalidWorkerAssertionIsRejectedWithoutApplyingForwardedValues()
    {
        const string secret = "0123456789abcdef0123456789abcdef";
        var configuration = CreateConfiguration(
            ("Hosting:Provider", "Cloudflare"),
            ("Cloudflare:ProxyAssertionSecret", secret));
        var downstreamCalled = false;
        var middleware = new CloudflareRequestAssertionMiddleware(
            _ =>
            {
                downstreamCalled = true;
                return Task.CompletedTask;
            },
            configuration);
        var transportPeer = IPAddress.Parse("192.0.2.10");
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = transportPeer;
        context.Request.Scheme = "http";
        context.Request.Host = new HostString("container");
        context.Request.Headers[CloudflareRequestAssertionMiddleware.AssertionHeader] = "invalid";
        context.Request.Headers[CloudflareRequestAssertionMiddleware.ClientIpHeader] = "203.0.113.42";
        context.Request.Headers[CloudflareRequestAssertionMiddleware.ForwardedSchemeHeader] = "https";
        context.Request.Headers[CloudflareRequestAssertionMiddleware.ForwardedHostHeader] = "api.example.test";

        await middleware.InvokeAsync(context);

        Assert.False(downstreamCalled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal(transportPeer.ToString(), new CloudflareClientIpResolver().Resolve(context));
        Assert.Equal("http", context.Request.Scheme);
        Assert.Equal("container", context.Request.Host.Host);
    }

    [Fact]
    public void CloudflareProviderRequiresAHighEntropyAssertionSecretAtStartup()
    {
        var configuration = CreateConfiguration(
            ("Hosting:Provider", "Cloudflare"),
            ("Cloudflare:ProxyAssertionSecret", "too-short"));

        Assert.Throws<InvalidOperationException>(
            () => CloudflareRequestAssertionMiddleware.ValidateConfiguration(configuration));
    }

    private static ServiceProvider CreateDataProtectionProvider(
        string keyDirectory,
        Action<IDataProtectionBuilder> configureKeyProtection)
    {
        var services = new ServiceCollection();
        var builder = services.AddDataProtection()
            .SetApplicationName("CloudflareHostingTests")
            .PersistKeysToFileSystem(new DirectoryInfo(keyDirectory));
        configureKeyProtection(builder);
        return services.BuildServiceProvider();
    }

    private static byte[] CreateCertificatePfx(string password)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=CloudflareHostingTests",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(1));
        return certificate.Export(X509ContentType.Pfx, password);
    }

    private static IConfiguration CreateConfiguration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(value =>
                new KeyValuePair<string, string?>(value.Key, value.Value)))
            .Build();
}
