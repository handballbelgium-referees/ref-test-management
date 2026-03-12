namespace Handball.Belgium.RefTestManagement.Permissions.AuditLog;

/// <summary>
/// Marks a property on a GraphQL mutation result type as the source of the audit log
/// resource ID. The <see cref="AuditLogTypeInterceptor"/> reads this automatically.
///
/// Supported property types:
///   - <see cref="System.Guid"/> — used directly
///   - <see cref="System.Collections.Generic.IEnumerable{T}"/> of objects with a
///     <c>Guid Id</c> property — all IDs are joined with ", "
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class AuditResultIdAttribute : Attribute;
