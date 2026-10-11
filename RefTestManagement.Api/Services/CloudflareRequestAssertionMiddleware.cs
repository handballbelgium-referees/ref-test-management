using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace Handball.Belgium.RefTestManagement.Api.Services;

public sealed class CloudflareRequestAssertionMiddleware
{
    private const string ProviderName = "Cloudflare";
    private const string SecretConfigurationKey = "Cloudflare:ProxyAssertionSecret";
    private const int MinimumSecretBytes = 32;

    internal const string ClientIpHeader = "X-RefTest-Client-IP";
    internal const string ForwardedSchemeHeader = "X-RefTest-Forwarded-Proto";
    internal const string ForwardedHostHeader = "X-RefTest-Forwarded-Host";
    internal const string AssertionHeader = "X-RefTest-Proxy-Assertion";

    private static readonly string[] ForwardingHeadersToRemove =
    [
        "Forwarded",
        "X-Forwarded-For",
        "X-Forwarded-Host",
        "X-Forwarded-Port",
        "X-Forwarded-Prefix",
        "X-Forwarded-Proto",
        "X-Real-IP",
        "CF-Connecting-IP",
        ClientIpHeader,
        ForwardedSchemeHeader,
        ForwardedHostHeader,
        AssertionHeader
    ];

    private readonly RequestDelegate _next;
    private readonly byte[] _expectedAssertion;

    public CloudflareRequestAssertionMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        if (!IsCloudflareProvider(configuration))
            throw new InvalidOperationException(
                $"{nameof(CloudflareRequestAssertionMiddleware)} requires Hosting:Provider={ProviderName}.");

        ValidateConfiguration(configuration);
        _expectedAssertion = Encoding.UTF8.GetBytes(configuration[SecretConfigurationKey]!);
    }

    public static bool IsCloudflareProvider(IConfiguration configuration) =>
        string.Equals(configuration["Hosting:Provider"], ProviderName, StringComparison.OrdinalIgnoreCase);

    public static void ValidateConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (!IsCloudflareProvider(configuration))
            return;

        var secret = configuration[SecretConfigurationKey];
        if (string.IsNullOrWhiteSpace(secret) ||
            Encoding.UTF8.GetByteCount(secret) < MinimumSecretBytes)
        {
            throw new InvalidOperationException(
                $"{SecretConfigurationKey} must be configured with at least {MinimumSecretBytes} UTF-8 bytes when Hosting:Provider={ProviderName}.");
        }
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!TryGetSingleHeader(context, AssertionHeader, out var assertion) ||
            !TryValidateAssertion(assertion) ||
            !TryGetSingleHeader(context, ClientIpHeader, out var clientIp) ||
            !IPAddress.TryParse(clientIp, out var address) ||
            !TryGetSingleHeader(context, ForwardedSchemeHeader, out var scheme) ||
            !TryGetSingleHeader(context, ForwardedHostHeader, out var host) ||
            !TryGetOrigin(scheme, host, out var origin))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        context.Items[CloudflareClientIpResolver.TrustedClientIpItemKey] = address;
        context.Connection.RemoteIpAddress = address;
        context.Request.Scheme = origin.Scheme;
        context.Request.Host = HostString.FromUriComponent(origin.Authority);

        foreach (var header in ForwardingHeadersToRemove)
            context.Request.Headers.Remove(header);

        await _next(context);
    }

    private bool TryValidateAssertion(string assertion)
    {
        var supplied = Encoding.UTF8.GetBytes(assertion);
        try
        {
            return supplied.Length == _expectedAssertion.Length &&
                   CryptographicOperations.FixedTimeEquals(supplied, _expectedAssertion);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(supplied);
        }
    }

    private static bool TryGetSingleHeader(HttpContext context, string name, out string value)
    {
        var values = context.Request.Headers[name];
        if (values.Count == 1 && !string.IsNullOrWhiteSpace(values[0]))
        {
            value = values[0]!;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetOrigin(string scheme, string host, out Uri origin)
    {
        origin = null!;
        if ((!string.Equals(scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) ||
            !Uri.TryCreate($"{scheme}://{host}/", UriKind.Absolute, out var parsed) ||
            string.IsNullOrWhiteSpace(parsed.Host) ||
            !string.IsNullOrEmpty(parsed.UserInfo) ||
            parsed.AbsolutePath != "/" ||
            !string.IsNullOrEmpty(parsed.Query) ||
            !string.IsNullOrEmpty(parsed.Fragment))
        {
            return false;
        }

        origin = parsed;
        return true;
    }
}
