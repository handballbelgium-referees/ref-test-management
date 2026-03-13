using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.AuditLog;

internal class AuditLogContext(DbContextOptions<AuditLogContext> options)
    : DbContext(options), IAuditLogContext
{
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}
