using System.Reflection;
using HotChocolate;
using HotChocolate.Configuration;
using HotChocolate.Resolvers;
using HotChocolate.Types.Descriptors.Definitions;
using HotChocolate.Types.Relay;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Handball.Belgium.RefTestManagement.Permissions.AuditLog;

/// <summary>
/// HotChocolate <see cref="TypeInterceptor"/> that finds every mutation method
/// decorated with <see cref="AuditActionAttribute"/> and wraps it with a field
/// middleware that logs the action after a successful execution.
///
/// Resource ID extraction order:
///   1. <see cref="IDAttribute"/> / <c>[ID&lt;T&gt;]</c> on properties of the input argument.
///   2. <see cref="AuditResultIdAttribute"/> on a property of the mutation result (for server-generated IDs).
/// </summary>
public sealed class AuditLogTypeInterceptor : TypeInterceptor
{
    public override void OnBeforeCompleteType(
        ITypeCompletionContext completionContext,
        DefinitionBase definition)
    {
        if (definition is not ObjectTypeDefinition { Name: "Mutation" } mutationDef)
            return;

        foreach (var field in mutationDef.Fields)
        {
            var auditAttr = field.Member?.GetCustomAttribute<AuditActionAttribute>();
            if (auditAttr is null)
                continue;

            var action = auditAttr.Action;

            field.MiddlewareDefinitions.Insert(0, new FieldMiddlewareDefinition(next => async ctx =>
            {
                await next(ctx);

                if (ctx.HasErrors)
                    return;

                var auditService = ctx.Services.GetRequiredService<IAuditLogService>();
                var httpCtx = ctx.Services.GetRequiredService<IHttpContextAccessor>();
                var httpUser = httpCtx.HttpContext?.User;
                var userEmail = httpUser?.GetEmail() ?? "unknown";
                var userName = httpUser?.GetName();
                var resourceId = TryExtractFromArguments(ctx) ?? TryExtractFromResult(ctx.Result);

                await auditService.LogAsync(
                    action,
                    userEmail,
                    userName,
                    resourceId,
                    cancellationToken: ctx.RequestAborted);
            }));
        }
    }

    private static string? TryExtractFromResult(object? result)
    {
        if (result is null) return null;
        try
        {
            var ids = result.GetType()
                .GetProperties()
                .Where(p => p.IsDefined(typeof(AuditResultIdAttribute), inherit: false))
                .SelectMany(p => p.GetValue(result) switch
                {
                    Guid g                 => [g.ToString()],
                    IEnumerable<object> many => many
                        .Select(item => item?.GetType().GetProperty("Id")?.GetValue(item) as Guid?)
                        .Where(g => g.HasValue)
                        .Select(g => g!.Value.ToString()),
                    _ => []
                })
                .ToList();

            return ids.Count > 0 ? string.Join(", ", ids) : null;
        }
        catch { return null; }
    }

    private static string? TryExtractFromArguments(IMiddlewareContext ctx)
    {
        try
        {
            foreach (var arg in ctx.Selection.Field.Arguments)
            {
                if (arg.Name != "input") continue;

                var input = ctx.ArgumentValue<object>("input");
                if (input is null) continue;

                // Reflect on properties decorated with [ID] / [ID<T>] — rename-safe,
                // no coupling to property names.
                var ids = input.GetType()
                    .GetProperties()
                    .Where(p => p.IsDefined(typeof(IDAttribute), inherit: true))
                    .SelectMany(p => p.GetValue(input) switch
                    {
                        IEnumerable<Guid> many => many.Select(g => g.ToString()),
                        Guid one              => [one.ToString()],
                        _                    => []
                    })
                    .ToList();

                if (ids.Count > 0)
                    return string.Join(", ", ids);
            }
        }
        catch { /* best-effort */ }

        return null;
    }
}