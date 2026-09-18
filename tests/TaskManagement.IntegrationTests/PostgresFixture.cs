using Microsoft.EntityFrameworkCore;
using Npgsql;
using TaskManagement.Infrastructure;
using Testcontainers.PostgreSql;

namespace TaskManagement.IntegrationTests;

/// <summary>
/// Starts one <c>postgres:17-alpine</c> container for the whole test run and hands out a fresh, migrated database
/// per test through <see cref="CreateDatabaseAsync"/>, so seed rows are always pristine and tests never interfere
/// with each other.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine").Build();

    /// <summary>Starts the shared PostgreSQL container.</summary>
    /// <returns>A task that completes once the container is ready to accept connections.</returns>
    public async Task InitializeAsync() => await _container.StartAsync();

    /// <summary>Stops and removes the shared PostgreSQL container.</summary>
    /// <returns>A task that completes once the container has been disposed.</returns>
    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>
    /// Creates a new, uniquely named database on the shared container and applies every migration to it.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The connection string of the freshly migrated database.</returns>
    public async Task<string> CreateDatabaseAsync(CancellationToken cancellationToken = default)
    {
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = $"tdb_{Guid.NewGuid():N}",
        };
        var connectionString = connectionStringBuilder.ConnectionString;

        await using var context = CreateContext(connectionString);
        await context.Database.MigrateAsync(cancellationToken);
        return connectionString;
    }

    /// <summary>
    /// Builds a <see cref="TaskManagementDbContext"/> for <paramref name="connectionString"/>. Does not migrate it:
    /// use <see cref="CreateDatabaseAsync"/> first, or point at a database it already migrated.
    /// </summary>
    /// <param name="connectionString">The connection string of the target database.</param>
    /// <returns>A new <see cref="TaskManagementDbContext"/>.</returns>
    public static TaskManagementDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<TaskManagementDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new TaskManagementDbContext(options);
    }
}

/// <summary>
/// Groups every integration test class under the single <see cref="PostgresFixture"/> shared for the test run.
/// </summary>
[CollectionDefinition("Postgres")]
public sealed class PostgresCollectionDefinition : ICollectionFixture<PostgresFixture>
{
}

/// <summary>
/// A <see cref="TimeProvider"/> that always reports 2026-09-18T12:00:00Z, so <c>TaskServiceTests</c> can assert
/// exact <c>CompletedAt</c> values without depending on wall-clock time.
/// </summary>
public sealed class FixedTimeProvider : TimeProvider
{
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => FixedUtcNow;
}
