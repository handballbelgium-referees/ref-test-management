using System.CodeDom.Compiler;
using System.Reflection;
using System.Xml.Linq;
using Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.Privacy;
using Handball.Belgium.RefTestManagement.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.UnitTests;

/// <summary>
/// Enforces the layered dependency rule recorded in <c>docs/adr/0001-layered-architecture.md</c>:
/// Domain ← Application ← Infrastructure / Api, with Api as the composition root.
/// </summary>
/// <remarks>
/// The checks read the <c>.csproj</c> files rather than compiled assembly metadata. The compiler
/// drops references whose types are never used, so a wrong-direction project reference can sit in
/// a project file unnoticed until the first <c>using</c> makes it real. Reading the declared graph
/// catches it the moment it is added.
/// </remarks>
public sealed class ArchitectureDependencyTests
{
    /// <summary>
    /// The project references each layer may declare. Api (composition root) and the test project
    /// are deliberately absent: they may reference anything.
    /// </summary>
    private static readonly Dictionary<string, string[]> AllowedProjectReferences = new()
    {
        ["RefTestManagement.Domain"] = [],
        ["RefTestManagement.Security"] = [],
        ["RefTestManagement.Application"] = ["RefTestManagement.Domain"],
        ["RefTestManagement.AuditLog"] = ["RefTestManagement.Domain"],
        ["RefTestManagement.Auth0"] = ["RefTestManagement.Security"],
        ["RefTestManagement.Infrastructure"] =
            ["RefTestManagement.Domain", "RefTestManagement.Application", "RefTestManagement.AuditLog"],
        ["RefTestManagement.Migrations.SqlServer"] = ["RefTestManagement.Infrastructure"],
        ["RefTestManagement.Migrations.PostgreSQL"] = ["RefTestManagement.Infrastructure"],
        ["RefTestManagement.Migrations.SQLite"] = ["RefTestManagement.Infrastructure"],
        ["RefTestManagement.Migrations.MySQL"] = ["RefTestManagement.Infrastructure"],
    };

    /// <summary>
    /// Package-name prefixes that belong to persistence, transport, HTTP or document generation and
    /// therefore must not appear in the Application layer.
    /// </summary>
    private static readonly string[] ForbiddenApplicationPackagePrefixes =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "HotChocolate",
        "StrawberryShake",
        "Microsoft.Extensions.Http",
        "QuestPDF",
        "ClosedXML",
        "StackExchange.Redis",
    ];

    public static TheoryData<string> LayeredProjects => [.. AllowedProjectReferences.Keys];

    [Theory]
    [MemberData(nameof(LayeredProjects))]
    public void ProjectReferencesFollowTheLayeringRule(string project)
    {
        var referenced = ReadIncludes(project, "ProjectReference")
            .Select(include => Path.GetFileNameWithoutExtension(include.Replace('\\', '/')));

        var disallowed = referenced.Except(AllowedProjectReferences[project]).ToList();

        Assert.True(disallowed.Count == 0,
            $"{project} references {string.Join(", ", disallowed)}, which the layering rule does not allow. " +
            "See docs/adr/0001-layered-architecture.md.");
    }

    [Fact]
    public void EveryLayeredProjectInTheSolutionIsCovered()
    {
        var projects = Directory.GetDirectories(RepositoryRoot, "RefTestManagement.*")
            .Select(Path.GetFileName)
            .Where(name => File.Exists(Path.Combine(RepositoryRoot, name!, $"{name}.csproj")))
            .Except(["RefTestManagement.Api", "RefTestManagement.UnitTests"])
            .ToList();

        var uncovered = projects.Except(AllowedProjectReferences.Keys).ToList();

        Assert.True(uncovered.Count == 0,
            $"Add {string.Join(", ", uncovered)} to {nameof(AllowedProjectReferences)} with the references its layer may use.");
    }

    [Fact]
    public void DomainDeclaresNoPackageReferences()
    {
        Assert.Empty(ReadIncludes("RefTestManagement.Domain", "PackageReference"));
    }

    [Fact]
    public void ApplicationDoesNotReferenceInfrastructurePackages()
    {
        var violations = ReadIncludes("RefTestManagement.Application", "PackageReference")
            .Where(IsForbiddenInApplication)
            .ToList();

        Assert.True(violations.Count == 0,
            $"RefTestManagement.Application references {string.Join(", ", violations)}. " +
            "Define a port in Application and implement it in Infrastructure instead.");
    }

    /// <summary>
    /// Application declares the ports and Infrastructure implements them (ADR 0001, rule 1). An
    /// interface declared in Infrastructure would make Api depend on an adapter project for a
    /// contract, so Infrastructure may declare none, apart from code generated for the IHF client.
    /// </summary>
    [Fact]
    public void InfrastructureDeclaresNoPorts()
    {
        var interfaces = typeof(RefTestManagementContext).Assembly.GetExportedTypes()
            .Where(type => type.IsInterface)
            .Where(type => !type.IsDefined(typeof(GeneratedCodeAttribute), inherit: false))
            .Select(type => type.FullName)
            .ToList();

        Assert.True(interfaces.Count == 0,
            $"RefTestManagement.Infrastructure declares {string.Join(", ", interfaces)}. " +
            "Declare the interface in RefTestManagement.Application.Abstractions and implement it in Infrastructure.");
    }

    [Fact]
    public void MutationMethodsDoNotExposePersistenceOrHttpDependencies()
    {
        var violations = typeof(PrivacyWithdrawalMutations).Assembly.GetTypes()
            .Where(type => type.Namespace is { } typeNamespace
                && (typeNamespace == "Handball.Belgium.RefTestManagement.Api.Graphql.Mutations"
                    || typeNamespace.StartsWith(
                        "Handball.Belgium.RefTestManagement.Api.Graphql.Mutations.",
                        StringComparison.Ordinal)))
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)
                .SelectMany(method => method.GetParameters()
                    .Where(parameter =>
                        parameter.ParameterType == typeof(IHttpContextAccessor)
                        || typeof(DbContext).IsAssignableFrom(parameter.ParameterType))
                    .Select(parameter => $"{type.FullName}.{method.Name}({parameter.ParameterType.FullName})")))
            .ToList();

        Assert.True(violations.Count == 0,
            $"GraphQL mutation methods expose persistence or HTTP dependencies: {string.Join(", ", violations)}.");
    }

    private static bool IsForbiddenInApplication(string package) =>
        ForbiddenApplicationPackagePrefixes.Any(prefix =>
            package.Equals(prefix, StringComparison.Ordinal)
            || package.StartsWith(prefix + ".", StringComparison.Ordinal));

    private static List<string> ReadIncludes(string project, string itemName)
    {
        var path = Path.Combine(RepositoryRoot, project, $"{project}.csproj");
        return XDocument.Load(path)
            .Descendants(itemName)
            .Select(element => (string?)element.Attribute("Include"))
            .OfType<string>()
            .ToList();
    }

    private static readonly string RepositoryRoot = FindRepositoryRoot();

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "RefTestManagement.slnx")))
                return directory.FullName;
        }

        throw new InvalidOperationException("Could not locate RefTestManagement.slnx above the test output directory.");
    }
}
