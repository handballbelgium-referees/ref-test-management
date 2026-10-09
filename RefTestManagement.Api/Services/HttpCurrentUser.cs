using System.Security.Claims;
using Handball.Belgium.RefTestManagement.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Handball.Belgium.RefTestManagement.Api.Services;

public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public string DisplayName =>
        Principal?.FindFirst("name")?.Value
        ?? Principal?.FindFirst(ClaimTypes.Name)?.Value
        ?? Principal?.FindFirst("email")?.Value
        ?? Principal?.FindFirst(ClaimTypes.Email)?.Value
        ?? "Unknown";

    public string Email =>
        Principal?.FindFirst("email")?.Value
        ?? Principal?.FindFirst(ClaimTypes.Email)?.Value
        ?? string.Empty;

    public string CorrelationId => httpContextAccessor.HttpContext?.TraceIdentifier ?? "(none)";

    public IReadOnlySet<string> Permissions =>
        Principal?.FindAll("permissions").Select(claim => claim.Value).ToHashSet()
        ?? new HashSet<string>();
}
