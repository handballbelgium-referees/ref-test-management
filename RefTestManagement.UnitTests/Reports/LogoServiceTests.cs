using System.Net;
using Handball.Belgium.RefTestManagement.Application.Configurations;
using Handball.Belgium.RefTestManagement.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Handball.Belgium.RefTestManagement.UnitTests;

public sealed class LogoServiceTests
{
    [Fact]
    public async Task GetLogoBytesAsyncUsesConfiguredLogoBaseUrl()
    {
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        var service = new LogoService(
            NullLogger<LogoService>.Instance,
            new EmailConfiguration
            {
                BaseUrl = "https://localhost:7039",
                LogoBaseUrl = "http://10.250.241.12:8080/"
            },
            httpClient);

        var bytes = await service.GetLogoBytesAsync(TestContext.Current.CancellationToken);

        Assert.Equal("http://10.250.241.12:8080/RefTest-logo.png", handler.RequestUri?.ToString());
        Assert.Equal(new byte[] { 1, 2, 3 }, bytes);
    }

    [Fact]
    public async Task GetLogoBytesAsyncFallsBackToBaseUrlWhenLogoBaseUrlIsMissing()
    {
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        var service = new LogoService(
            NullLogger<LogoService>.Instance,
            new EmailConfiguration { BaseUrl = "https://localhost:7039/" },
            httpClient);

        await service.GetLogoBytesAsync(TestContext.Current.CancellationToken);

        Assert.Equal("https://localhost:7039/RefTest-logo.png", handler.RequestUri?.ToString());
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[] { 1, 2, 3 })
            });
        }
    }
}
