using Handball.Belgium.RefTestManagement.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Handball.Belgium.RefTestManagement.UnitTests;

/// <summary>
/// A throwaway SQLite database backed by a shared in-memory connection.
/// </summary>
/// <remarks>
/// Some behaviour in this application only exists as SQL — claiming a job is a conditional
/// <c>UPDATE</c>, and the expiration sweep is a translated predicate. Neither can be proven against
/// an in-memory fake, because the fake never runs the translation. SQLite is the cheapest provider
/// that does: it runs in-process, needs no server, and supports <c>ExecuteUpdate</c>.
///
/// The connection is held open for the lifetime of the instance on purpose. An in-memory SQLite
/// database exists only while at least one connection to it is open, so letting EF open and close
/// per operation would discard the schema between calls. Several contexts can be opened over the
/// same connection, which is what makes it possible to simulate two workers racing.
/// </remarks>
internal sealed class SqliteTestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    private SqliteTestDatabase(SqliteConnection connection)
    {
        _connection = connection;
    }

    public static SqliteTestDatabase Create()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var database = new SqliteTestDatabase(connection);

        using var context = database.CreateContext();
        context.Database.EnsureCreated();

        return database;
    }

    /// <summary>
    /// Opens an independent context over the same database. Each one has its own change tracker,
    /// which is how a second worker is represented.
    /// </summary>
    public RefTestManagementContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<RefTestManagementContext>()
            .UseSqlite(_connection)
            .Options;

        return new RefTestManagementContext(options);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
