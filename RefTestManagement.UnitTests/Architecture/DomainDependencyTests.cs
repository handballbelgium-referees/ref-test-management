using System.Reflection;
using Handball.Belgium.RefTestManagement.Domain.RefTests;

namespace Handball.Belgium.RefTestManagement.UnitTests;

/// <summary>
/// Guards the dependency direction between the domain and the audit trail.
/// </summary>
/// <remarks>
/// The domain used to reference <c>RefTestManagement.AuditLog</c> only to reach the domain-event
/// abstractions that happened to be declared there. That made the innermost layer depend on an
/// outward concern: the domain cannot be compiled, referenced or reasoned about without dragging
/// in EF Core and ASP.NET Core HTTP abstractions, which are what the audit interceptor needs.
///
/// The abstractions now live in <c>Domain.Events</c> and the reference points the other way.
/// This test exists because that is easy to undo by accident — adding one <c>using</c> and letting
/// the IDE add the project reference restores the cycle in seconds, and nothing else would notice.
/// </remarks>
public sealed class DomainDependencyTests
{
    private static readonly Assembly DomainAssembly = typeof(RefTest).Assembly;

    [Fact]
    public void DomainDoesNotReferenceTheAuditLogAssembly()
    {
        var referenced = DomainAssembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();

        Assert.DoesNotContain("RefTestManagement.AuditLog", referenced);
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Microsoft.AspNetCore.Http.Abstractions")]
    public void DomainDoesNotReferenceInfrastructureConcerns(string assemblyName)
    {
        var referenced = DomainAssembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();

        Assert.DoesNotContain(assemblyName, referenced);
    }
}
