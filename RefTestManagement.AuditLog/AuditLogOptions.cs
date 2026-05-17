using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.AuditLog;

public class AuditLogOptions
{
    public bool EnableCleanup { get; set; } = true;
    public int CleanupIntervalHours { get; set; } = 24;
    public int RetentionDays { get; set; } = 90;

    internal HashSet<Type> ExcludedEntityTypes { get; } = [];
    internal HashSet<string> ExcludedPropertyNames { get; } = [];
    internal HashSet<string> ListPropertyNames { get; } = [];
    internal Dictionary<string, (string OutputKey, Func<object, DbContext, object?> Resolver)> PropertyResolvers { get; } = new();
    internal Dictionary<string, (string OutputKey, string ItemLabel)> ListSummaries { get; } = new();

    public AuditLogOptions ExcludeEntity<T>()
    {
        ExcludedEntityTypes.Add(typeof(T));
        return this;
    }

    public AuditLogOptions ExcludeProperty(string propertyName)
    {
        ExcludedPropertyNames.Add(propertyName);
        return this;
    }

    public AuditLogOptions TreatAsList(params string[] propertyNames)
    {
        foreach (var name in propertyNames)
            ListPropertyNames.Add(name);
        return this;
    }

    public AuditLogOptions ResolveProperty(string propertyName, string outputKey, Func<object, DbContext, object?> resolver)
    {
        PropertyResolvers[propertyName] = (outputKey, resolver);
        return this;
    }

    public AuditLogOptions SummarizeList(string propertyName, string outputKey, string itemLabel = "items")
    {
        ListSummaries[propertyName] = (outputKey, itemLabel);
        return this;
    }
}
