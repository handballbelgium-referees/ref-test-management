using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Handball.Belgium.RefTestManagement.AuditLog;

public class AuditSaveChangesInterceptor(
    IHttpContextAccessor httpContextAccessor,
    AuditLogOptions options) : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            AddAuditEntries(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AddAuditEntries(DbContext context)
    {
        var (actorName, actorEmail) = GetActor();
        var timestamp = DateTime.UtcNow;
        var isSystemActor = string.IsNullOrEmpty(actorEmail) && actorName == "System";

        var entries = context.ChangeTracker.Entries()
            .Where(e =>
                e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted &&
                e.Entity is not AuditLogEntry &&
                !options.ExcludedEntityTypes.Contains(e.Entity.GetType()) &&
                !(isSystemActor && options.ExcludedForSystemActorTypes.Contains(e.Entity.GetType())))
            .ToList();

        foreach (var entry in entries)
        {
            var auditEntry = new AuditLogEntry
            {
                EntityType = entry.Entity.GetType().Name,
                EntityId = GetEntityId(entry),
                Action = entry.State switch
                {
                    EntityState.Added => "Created",
                    EntityState.Modified => "Modified",
                    EntityState.Deleted => "Deleted",
                    _ => entry.State.ToString()
                },
                Changes = BuildChangesJson(entry, context),
                ActorName = actorName,
                ActorEmail = actorEmail,
                Timestamp = timestamp
            };

            context.Add(auditEntry);
        }
    }

    private (string name, string email) GetActor()
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user is null)
            return ("System", string.Empty);

        var name = user.FindFirst("name")?.Value
                    ?? user.FindFirst(ClaimTypes.Name)?.Value
                   ?? user.FindFirst("email")?.Value
                     ?? user.FindFirst(ClaimTypes.Email)?.Value
                   ?? "System";
        var email = user.FindFirst("email")?.Value ?? user.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty;

        return (name, email);
    }

    private static string GetEntityId(EntityEntry entry)
    {
        var keyValues = entry.Metadata.FindPrimaryKey()
            ?.Properties
            .Select(p => entry.Property(p.Name).CurrentValue?.ToString() ?? string.Empty)
            .ToArray();

        return keyValues is { Length: > 0 }
            ? string.Join(",", keyValues)
            : string.Empty;
    }

    private string? BuildChangesJson(EntityEntry entry, DbContext context)
    {
        var changes = entry.State switch
        {
            EntityState.Added => BuildAddedChanges(entry, context),
            EntityState.Modified => BuildModifiedChanges(entry, context),
            EntityState.Deleted => BuildDeletedChanges(entry, context),
            _ => null
        };

        return changes is null or { Count: 0 }
            ? null
            : JsonSerializer.Serialize(changes, JsonOptions);
    }

    private Dictionary<string, object?> BuildAddedChanges(EntityEntry entry, DbContext context)
    {
        var result = new Dictionary<string, object?>();
        foreach (var prop in entry.Properties)
        {
            if (options.PropertyResolvers.TryGetValue(prop.Metadata.Name, out var resolverInfo))
            {
                var resolved = resolverInfo.Resolver(entry.Entity, context);
                result[resolverInfo.OutputKey] = new { newValue = SerializeValue(resolved ?? prop.CurrentValue) };
                continue;
            }

            if (options.ListSummaries.TryGetValue(prop.Metadata.Name, out var summaryInfo))
            {
                var items = SplitList(prop.CurrentValue as string ?? string.Empty);
                if (items.Length > 0)
                    result[summaryInfo.OutputKey] = new { newValue = $"{items.Length} {summaryInfo.ItemLabel}" };
                continue;
            }

            if (options.ExcludedPropertyNames.Contains(prop.Metadata.Name))
                continue;

            var value = prop.CurrentValue;
            if (options.ListPropertyNames.Contains(prop.Metadata.Name) && value is string listStr)
                result[prop.Metadata.Name] = new { values = SplitList(listStr) };
            else
                result[prop.Metadata.Name] = new { newValue = SerializeValue(value) };
        }
        return result;
    }

    private Dictionary<string, object?> BuildModifiedChanges(EntityEntry entry, DbContext context)
    {
        var result = new Dictionary<string, object?>();
        foreach (var prop in entry.Properties)
        {
            if (!prop.IsModified)
                continue;

            if (options.PropertyResolvers.TryGetValue(prop.Metadata.Name, out var resolverInfo))
            {
                var resolved = resolverInfo.Resolver(entry.Entity, context);
                result[resolverInfo.OutputKey] = new
                {
                    oldValue = SerializeValue(prop.OriginalValue),
                    newValue = SerializeValue(resolved ?? prop.CurrentValue)
                };
                continue;
            }

            if (options.ListSummaries.TryGetValue(prop.Metadata.Name, out var summaryInfo))
            {
                var oldItems = SplitList(prop.OriginalValue as string ?? string.Empty);
                var newItems = SplitList(prop.CurrentValue as string ?? string.Empty);
                var added = newItems.Except(oldItems).Count();
                var removed = oldItems.Except(newItems).Count();
                if (added > 0 || removed > 0)
                {
                    var diff = string.Join(", ", new[]
                    {
                        added > 0 ? $"+{added}" : null,
                        removed > 0 ? $"-{removed}" : null
                    }.Where(x => x is not null));
                    result[summaryInfo.OutputKey] = new
                    {
                        oldValue = $"{oldItems.Length} {summaryInfo.ItemLabel}",
                        newValue = $"{newItems.Length} {summaryInfo.ItemLabel} ({diff})"
                    };
                }
                continue;
            }

            if (options.ExcludedPropertyNames.Contains(prop.Metadata.Name))
                continue;

            var oldValue = prop.OriginalValue;
            var newValue = prop.CurrentValue;

            if (options.ListPropertyNames.Contains(prop.Metadata.Name))
            {
                var oldList = SplitList(oldValue as string ?? string.Empty);
                var newList = SplitList(newValue as string ?? string.Empty);
                result[prop.Metadata.Name] = new
                {
                    added = newList.Except(oldList).ToArray(),
                    removed = oldList.Except(newList).ToArray()
                };
            }
            else
            {
                result[prop.Metadata.Name] = new { oldValue = SerializeValue(oldValue), newValue = SerializeValue(newValue) };
            }
        }
        return result;
    }

    private Dictionary<string, object?> BuildDeletedChanges(EntityEntry entry, DbContext context)
    {
        var result = new Dictionary<string, object?>();
        foreach (var prop in entry.Properties)
        {
            if (options.PropertyResolvers.TryGetValue(prop.Metadata.Name, out var resolverInfo))
            {
                var resolved = resolverInfo.Resolver(entry.Entity, context);
                result[resolverInfo.OutputKey] = new { oldValue = SerializeValue(resolved ?? prop.OriginalValue) };
                continue;
            }

            if (options.ListSummaries.TryGetValue(prop.Metadata.Name, out var summaryInfo))
            {
                var items = SplitList(prop.OriginalValue as string ?? string.Empty);
                if (items.Length > 0)
                    result[summaryInfo.OutputKey] = new { oldValue = $"{items.Length} {summaryInfo.ItemLabel}" };
                continue;
            }

            if (options.ExcludedPropertyNames.Contains(prop.Metadata.Name))
                continue;

            var value = prop.OriginalValue;
            if (options.ListPropertyNames.Contains(prop.Metadata.Name) && value is string listStr)
                result[prop.Metadata.Name] = new { values = SplitList(listStr) };
            else
                result[prop.Metadata.Name] = new { oldValue = SerializeValue(value) };
        }
        return result;
    }

    private static string[] SplitList(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static object? SerializeValue(object? value) =>
        value is Enum e ? e.ToString() : value;
}
