using Handball.Belgium.RefTestManagement.Infrastructure.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Handball.Belgium.RefTestManagement.UnitTests;

/// <summary>
/// A port declared in Application with no Infrastructure registration only fails when the first
/// request reaches it; this catches it at test time instead.
/// </summary>
public sealed class InfrastructureRegistrationTests
{
    [Fact]
    public void EveryApplicationPortHasAnInfrastructureRegistration()
    {
        var registered = new ServiceCollection()
            .AddInfrastructureServices()
            .Select(descriptor => descriptor.ServiceType)
            .ToHashSet();

        var ports = typeof(IEmailService).Assembly.GetExportedTypes()
            .Where(type => type.IsInterface && type.Namespace == typeof(IEmailService).Namespace);

        Assert.All(ports, port => Assert.Contains(port, registered));
    }
}
