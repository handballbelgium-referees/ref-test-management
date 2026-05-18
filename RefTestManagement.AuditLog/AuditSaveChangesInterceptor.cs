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

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            await AddAuditEntriesAsync(eventData.Context, cancellationToken);

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private async Task AddAuditEntriesAsync(DbContext context, CancellationToken cancellationToken)
    {
        var (actorName, actorEmail) = GetActor();
        var timestamp = DateTime.UtcNow;

        var entries = context.ChangeTracker.Entries()
            .Where(e =>
                e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted &&
                e.Entity is not AuditEvent &&
                !options.ExcludedEntityTypes.Contains(e.Entity.GetType()))
            .ToList();

        if (entries.Count == 0) return;

        // Load current max versions from DB for all affected streams so new audit events
        // continue from the correct version number and don't violate the unique(StreamId, Version) index.
        var streamIds = entries
            .Select(GetEntityId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var versionTracker = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);

        var existingVersions = await context.Set<AuditEvent>()
            .Where(a => streamIds.Contains(a.StreamId))
            .GroupBy(a => a.StreamId)
            .Select(g => new { StreamId = g.Key, MaxVersion = g.Max(a => a.Version) })
            .ToListAsync(cancellationToken);

        foreach (var v in existingVersions)
            versionTracker[v.StreamId] = v.MaxVersion;

        foreach (var entry in entries)
        {
            var streamId = GetEntityId(entry);

            if (entry.Entity is IHasDomainEvents hasDomainEvents && hasDomainEvents.DomainEvents.Count > 0)
            {
                // Domain event path: one AuditEvent per domain event
                var headers = JsonSerializer.Serialize(new { entityType = entry.Entity.GetType().Name }, JsonOptions);

                foreach (var domainEvent in hasDomainEvents.DomainEvents)
                {
                    var version = NextVersion(versionTracker, streamId);
                    var changesData = domainEvent is IDomainEventWithResolution resolvable
                        ? resolvable.GetChanges((entityType, id) =>
                            options.EntityNameResolvers.TryGetValue(entityType, out var resolver)
                                ? resolver(id, context)
                                : null)
                        : domainEvent.GetChanges();
                    var auditEvent = new AuditEvent
                    {
                        StreamId = streamId,
                        Version = version,
                        Type = domainEvent.ActionName,
                        Data = changesData is null ? null : JsonSerializer.Serialize(changesData, JsonOptions),
                        Timestamp = domainEvent.OccurredAt,
                        ActorName = actorName,
                        ActorEmail = actorEmail,
                        Headers = headers
                    };
                    context.Add(auditEvent);
                }

                hasDomainEvents.ClearDomainEvents();
            }
            else
            {
                // Fallback property-diff path for entities without domain events
                var isSystemActor = string.IsNullOrEmpty(actorEmail) && actorName == "System";
                if (isSystemActor && options.ExcludedForSystemActorTypes.Contains(entry.Entity.GetType()))
                    continue;

                var eventType = entry.State switch
                {
                    EntityState.Added => "EntityCreated",
                    EntityState.Modified => "EntityModified",
                    EntityState.Deleted => "EntityDeleted",
                    _ => entry.State.ToString()
                };

                var version = NextVersion(versionTracker, streamId);
                var headers = JsonSerializer.Serialize(new { entityType = entry.Entity.GetType().Name }, JsonOptions);
                var auditEvent = new AuditEvent
                {
                    StreamId = streamId,
                    Version = version,
                    Type = eventType,
                    Data = BuildChangesJson(entry, context),
                    Timestamp = timestamp,
                    ActorName = actorName,
                    ActorEmail = actorEmail,
                    Headers = headers
                };
                context.Add(auditEvent);
            }
        }
    }

    private static long NextVersion(Dictionary<string, long> tracker, string streamId)
    {
        var current = tracker.GetValueOrDefault(streamId, 0L);
        var next = current + 1;
        tracker[streamId] = next;
        return next;
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
