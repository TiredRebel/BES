using Microsoft.EntityFrameworkCore;
using TaskManagement.Domain;
using TaskManagement.Infrastructure;

namespace TaskManagement.IntegrationTests;

/// <summary>
/// Proves that <c>InitialCreate</c> applies cleanly to an empty database, creates every schema object listed in
/// spec &#167;11 (constraints and indexes are checked for presence; the <c>trg_tasks_br3_br4</c> trigger is checked
/// to be the only user trigger on <c>tasks</c>), and seeds the rows of spec &#167;12.
/// </summary>
[Collection("Postgres")]
public sealed class MigrationAndSeedTests : IAsyncLifetime
{
    private static readonly string[] ExpectedConstraintNames =
    [
        "pk_employees",
        "pk_tasks",
        "fk_tasks_employees_creator_id",
        "fk_tasks_employees_assignee_id",
        "ck_tasks_status_valid",
        "ck_tasks_br1_completed_at_iff_completed",
        "ck_tasks_br2_due_at_not_before_planned_start_at",
        "ck_tasks_br5_assignee_not_creator",
    ];

    private static readonly string[] ExpectedIndexNames =
    [
        "ux_employees_email",
        "ix_tasks_assignee_id_status",
        "ix_tasks_due_at",
        "ix_tasks_creator_id",
    ];

    private static readonly string[] ExpectedTriggerNames = ["trg_tasks_br3_br4"];

    private readonly PostgresFixture _fixture;
    private TaskManagementDbContext? _dbContext;

    /// <summary>Initializes a new instance of the <see cref="MigrationAndSeedTests"/> class.</summary>
    /// <param name="fixture">The shared PostgreSQL container fixture.</param>
    public MigrationAndSeedTests(PostgresFixture fixture) => _fixture = fixture;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        var connectionString = await _fixture.CreateDatabaseAsync();
        _dbContext = PostgresFixture.CreateContext(connectionString);
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (_dbContext is not null)
        {
            await _dbContext.DisposeAsync();
        }
    }

    /// <summary>
    /// Verifies that migrating an empty database applies every migration and leaves none pending.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Migrate_EmptyDatabase_AppliesAllMigrationsAndLeavesNonePending()
    {
        var pending = await _dbContext!.Database.GetPendingMigrationsAsync();
        var applied = await _dbContext.Database.GetAppliedMigrationsAsync();
        var all = _dbContext.Database.GetMigrations();

        Assert.Empty(pending);
        Assert.NotEmpty(applied);
        Assert.Equal(all, applied);
    }

    /// <summary>
    /// Verifies that every constraint, index and the <c>trg_tasks_br3_br4</c> trigger from spec &#167;11 exist after
    /// migration, and that no other trigger is present on <c>tasks</c>.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Schema_AfterMigration_HasSpecifiedConstraintsAndIndexes()
    {
        var constraintNames = await _dbContext!.Database
            .SqlQueryRaw<string>(
                "SELECT conname AS \"Value\" FROM pg_constraint WHERE conrelid IN ('employees'::regclass, 'tasks'::regclass)")
            .ToListAsync();
        var indexNames = await _dbContext.Database
            .SqlQueryRaw<string>("SELECT indexname AS \"Value\" FROM pg_indexes WHERE tablename IN ('employees', 'tasks')")
            .ToListAsync();
        var triggerNames = await _dbContext.Database
            .SqlQueryRaw<string>(
                "SELECT tgname AS \"Value\" FROM pg_trigger WHERE tgrelid = 'tasks'::regclass AND NOT tgisinternal")
            .ToListAsync();

        foreach (var expected in ExpectedConstraintNames)
        {
            Assert.Contains(expected, constraintNames);
        }

        foreach (var expected in ExpectedIndexNames)
        {
            Assert.Contains(expected, indexNames);
        }

        Assert.Equal(ExpectedTriggerNames, triggerNames);
    }

    /// <summary>
    /// Verifies that the three seeded <see cref="Employee"/> rows of spec &#167;12 match, column for column.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Seed_Employees_MatchSpec()
    {
        var employees = await _dbContext!.Employees.AsNoTracking().ToListAsync();

        var olena = Assert.Single(employees, e => e.Id == Guid.Parse("10000000-0000-0000-0000-000000000001"));
        Assert.Equal("Олена Коваленко", olena.FullName);
        Assert.Equal("olena.kovalenko@example.com", olena.Email);
        Assert.True(olena.IsActive);

        var bohdan = Assert.Single(employees, e => e.Id == Guid.Parse("10000000-0000-0000-0000-000000000002"));
        Assert.Equal("Богдан Шевченко", bohdan.FullName);
        Assert.Equal("bohdan.shevchenko@example.com", bohdan.Email);
        Assert.True(bohdan.IsActive);

        var oksana = Assert.Single(employees, e => e.Id == Guid.Parse("10000000-0000-0000-0000-000000000003"));
        Assert.Equal("Оксана Мельник", oksana.FullName);
        Assert.Equal("oksana.melnyk@example.com", oksana.Email);
        Assert.False(oksana.IsActive);

        Assert.Equal(3, employees.Count);
    }

    /// <summary>
    /// Verifies that the three seeded <see cref="TaskItem"/> rows of spec &#167;12 match, column for column.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Seed_Tasks_MatchSpec()
    {
        var tasks = await _dbContext!.Tasks.AsNoTracking().ToListAsync();
        var olenaId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var bohdanId = Guid.Parse("10000000-0000-0000-0000-000000000002");

        var report = Assert.Single(tasks, t => t.Id == Guid.Parse("20000000-0000-0000-0000-000000000001"));
        Assert.Equal("Підготувати квартальний звіт з продажів", report.Title);
        Assert.Equal(TaskItemStatus.New, report.Status);
        Assert.Equal(olenaId, report.CreatorId);
        Assert.Equal(bohdanId, report.AssigneeId);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero), report.PlannedStartAt);
        Assert.Equal(new DateTimeOffset(2026, 10, 10, 17, 0, 0, TimeSpan.Zero), report.DueAt);
        Assert.Null(report.CompletedAt);

        var callBack = Assert.Single(tasks, t => t.Id == Guid.Parse("20000000-0000-0000-0000-000000000002"));
        Assert.Equal("Передзвонити ключовому клієнту", callBack.Title);
        Assert.Equal(TaskItemStatus.Completed, callBack.Status);
        Assert.Equal(bohdanId, callBack.CreatorId);
        Assert.Equal(olenaId, callBack.AssigneeId);
        Assert.Equal(new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero), callBack.PlannedStartAt);
        Assert.Equal(new DateTimeOffset(2026, 9, 5, 17, 0, 0, TimeSpan.Zero), callBack.DueAt);
        Assert.Equal(new DateTimeOffset(2026, 9, 4, 15, 30, 0, TimeSpan.Zero), callBack.CompletedAt);

        var cleanup = Assert.Single(tasks, t => t.Id == Guid.Parse("20000000-0000-0000-0000-000000000003"));
        Assert.Equal("Очистити дублікати контактів", cleanup.Title);
        Assert.Equal(TaskItemStatus.Cancelled, cleanup.Status);
        Assert.Equal(olenaId, cleanup.CreatorId);
        Assert.Equal(bohdanId, cleanup.AssigneeId);
        Assert.Null(cleanup.PlannedStartAt);
        Assert.Null(cleanup.DueAt);
        Assert.Null(cleanup.CompletedAt);

        Assert.Equal(3, tasks.Count);
    }
}
