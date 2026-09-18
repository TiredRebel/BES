using Microsoft.EntityFrameworkCore;
using Npgsql;
using TaskManagement.Application;
using TaskManagement.Domain;
using TaskManagement.Infrastructure;

namespace TaskManagement.IntegrationTests;

/// <summary>
/// End-to-end tests for <see cref="TaskService"/>'s three use cases against a real, migrated and seeded PostgreSQL
/// database. Every test uses a fixed <see cref="FixedTimeProvider"/> (2026-09-18T12:00:00Z) and re-reads persisted
/// state through a second <see cref="TaskManagementDbContext"/> so assertions never rely on the first context's
/// change tracker.
/// </summary>
[Collection("Postgres")]
public sealed class TaskServiceTests : IAsyncLifetime
{
    private static readonly Guid AliceId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid BobId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid CarolId = Guid.Parse("10000000-0000-0000-0000-000000000003");
    private static readonly Guid[] BobsTaskIdsOrderedByDueAt =
    [
        Guid.Parse("20000000-0000-0000-0000-000000000001"),
        Guid.Parse("20000000-0000-0000-0000-000000000003"),
    ];

    private readonly PostgresFixture _fixture;
    private readonly FixedTimeProvider _timeProvider = new();
    private string _connectionString = string.Empty;
    private TaskManagementDbContext? _dbContext;

    /// <summary>Initializes a new instance of the <see cref="TaskServiceTests"/> class.</summary>
    /// <param name="fixture">The shared PostgreSQL container fixture.</param>
    public TaskServiceTests(PostgresFixture fixture) => _fixture = fixture;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        _connectionString = await _fixture.CreateDatabaseAsync();
        _dbContext = PostgresFixture.CreateContext(_connectionString);
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (_dbContext is not null)
        {
            await _dbContext.DisposeAsync();
        }
    }

    /// <summary>Verifies that use case 1 persists a valid new task.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateTaskAsync_ValidInput_PersistsNewTask()
    {
        var service = new TaskService(_dbContext!, _timeProvider);

        var created = await service.CreateTaskAsync("New task", AliceId, BobId, null, null);

        await using var readContext = PostgresFixture.CreateContext(_connectionString);
        var persisted = await readContext.Tasks.AsNoTracking().SingleAsync(t => t.Id == created.Id);
        Assert.Equal("New task", persisted.Title);
        Assert.Equal(TaskItemStatus.New, persisted.Status);
        Assert.Equal(AliceId, persisted.CreatorId);
        Assert.Equal(BobId, persisted.AssigneeId);
    }

    /// <summary>Verifies BR3 (service check): an inactive assignee is rejected and nothing is persisted.</summary>
    /// <remarks>Enforces BR3.</remarks>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateTaskAsync_InactiveAssignee_ThrowsBR3AndPersistsNothing()
    {
        var service = new TaskService(_dbContext!, _timeProvider);

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => service.CreateTaskAsync("Given to Carol", AliceId, CarolId, null, null));
        Assert.Equal("BR3", ex.RuleId);

        await using var readContext = PostgresFixture.CreateContext(_connectionString);
        Assert.False(await readContext.Tasks.AnyAsync(t => t.Title == "Given to Carol"));
    }

    /// <summary>
    /// Verifies that when the same inactive employee is both creator and assignee, the service's BR3 check throws
    /// before the domain would throw BR5. This pins the service check running (and BR5 in
    /// <see cref="TaskItem.Create"/> being ordered) so it goes red if either check is removed or reordered.
    /// </summary>
    /// <remarks>Enforces BR3 (service half).</remarks>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateTaskAsync_InactiveEmployeeAsCreatorAndAssignee_ThrowsBR3FromServiceCheck()
    {
        var service = new TaskService(_dbContext!, _timeProvider);

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => service.CreateTaskAsync("Self task", CarolId, CarolId, null, null));

        Assert.Equal("BR3", ex.RuleId);
    }

    /// <summary>Verifies BR5: an active employee cannot be assigned a task they created themselves.</summary>
    /// <remarks>Enforces BR5.</remarks>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateTaskAsync_AssigneeIsCreator_ThrowsBR5()
    {
        var service = new TaskService(_dbContext!, _timeProvider);

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => service.CreateTaskAsync("Self task", AliceId, AliceId, null, null));

        Assert.Equal("BR5", ex.RuleId);
    }

    /// <summary>Verifies BR2: a due date earlier than the planned start date is rejected.</summary>
    /// <remarks>Enforces BR2.</remarks>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateTaskAsync_DueAtBeforePlannedStartAt_ThrowsBR2()
    {
        var service = new TaskService(_dbContext!, _timeProvider);
        var plannedStartAt = new DateTimeOffset(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);
        var dueAt = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => service.CreateTaskAsync("Bad dates", AliceId, BobId, plannedStartAt, dueAt));

        Assert.Equal("BR2", ex.RuleId);
    }

    /// <summary>Verifies that an unknown creator id raises <see cref="KeyNotFoundException"/>.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateTaskAsync_UnknownCreator_ThrowsKeyNotFoundException()
    {
        var service = new TaskService(_dbContext!, _timeProvider);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.CreateTaskAsync("Orphan", Guid.NewGuid(), BobId, null, null));
    }

    /// <summary>Verifies that an unknown assignee id raises <see cref="KeyNotFoundException"/>.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateTaskAsync_UnknownAssignee_ThrowsKeyNotFoundException()
    {
        var service = new TaskService(_dbContext!, _timeProvider);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.CreateTaskAsync("Orphan", AliceId, Guid.NewGuid(), null, null));
    }

    /// <summary>
    /// Verifies use case 2: moving a task to <see cref="TaskItemStatus.Completed"/> persists the status and sets
    /// <c>CompletedAt</c> to the fixed time provider's instant.
    /// </summary>
    /// <remarks>Enforces BR1.</remarks>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ChangeTaskStatusAsync_NewToCompleted_PersistsStatusAndCompletedAt()
    {
        var service = new TaskService(_dbContext!, _timeProvider);
        var taskId = Guid.Parse("20000000-0000-0000-0000-000000000001");

        var updated = await service.ChangeTaskStatusAsync(taskId, TaskItemStatus.Completed);

        Assert.Equal(TaskItemStatus.Completed, updated.Status);
        Assert.Equal(_timeProvider.GetUtcNow(), updated.CompletedAt);

        await using var readContext = PostgresFixture.CreateContext(_connectionString);
        var persisted = await readContext.Tasks.AsNoTracking().SingleAsync(t => t.Id == taskId);
        Assert.Equal(TaskItemStatus.Completed, persisted.Status);
        Assert.Equal(_timeProvider.GetUtcNow(), persisted.CompletedAt);
    }

    /// <summary>
    /// Verifies BR4: changing the status of the seeded <c>Completed</c> task throws and leaves the row unchanged.
    /// </summary>
    /// <remarks>Enforces BR4.</remarks>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ChangeTaskStatusAsync_CompletedTask_ThrowsBR4AndLeavesRowUnchanged()
    {
        var service = new TaskService(_dbContext!, _timeProvider);
        var taskId = Guid.Parse("20000000-0000-0000-0000-000000000002");

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => service.ChangeTaskStatusAsync(taskId, TaskItemStatus.New));
        Assert.Equal("BR4", ex.RuleId);

        await using var readContext = PostgresFixture.CreateContext(_connectionString);
        var persisted = await readContext.Tasks.AsNoTracking().SingleAsync(t => t.Id == taskId);
        Assert.Equal(TaskItemStatus.Completed, persisted.Status);
        Assert.NotNull(persisted.CompletedAt);
    }

    /// <summary>Verifies that an unknown task id raises <see cref="KeyNotFoundException"/>.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ChangeTaskStatusAsync_UnknownTask_ThrowsKeyNotFoundException()
    {
        var service = new TaskService(_dbContext!, _timeProvider);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.ChangeTaskStatusAsync(Guid.NewGuid(), TaskItemStatus.InProgress));
    }

    /// <summary>
    /// Verifies use case 3: listing Bob's tasks returns both of his seeded tasks ordered by <c>DueAt</c>, with the
    /// null-<c>DueAt</c> task last.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ListTasksByAssigneeAsync_Bob_ReturnsHisTasksOrderedByDueAt()
    {
        var service = new TaskService(_dbContext!, _timeProvider);

        var tasks = await service.ListTasksByAssigneeAsync(BobId);

        Assert.Equal(BobsTaskIdsOrderedByDueAt, tasks.Select(t => t.Id));
    }

    /// <summary>Verifies that an assignee with no tasks returns an empty list, not an exception.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ListTasksByAssigneeAsync_EmployeeWithoutTasks_ReturnsEmpty()
    {
        var service = new TaskService(_dbContext!, _timeProvider);

        var tasks = await service.ListTasksByAssigneeAsync(CarolId);

        Assert.Empty(tasks);
    }

    /// <summary>Verifies that an unknown assignee id returns an empty list, not an exception.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ListTasksByAssigneeAsync_UnknownEmployee_ReturnsEmpty()
    {
        var service = new TaskService(_dbContext!, _timeProvider);

        var tasks = await service.ListTasksByAssigneeAsync(Guid.NewGuid());

        Assert.Empty(tasks);
    }

    /// <summary>
    /// Verifies use case 3's optional status filter: only the task matching both the assignee and the given status
    /// is returned.
    /// </summary>
    /// <param name="status">The status to filter by.</param>
    /// <param name="expectedTaskId">The id of the single task expected to match.</param>
    [Theory]
    [InlineData(TaskItemStatus.New, "20000000-0000-0000-0000-000000000001")]
    [InlineData(TaskItemStatus.Cancelled, "20000000-0000-0000-0000-000000000003")]
    [Trait("Category", "Integration")]
    public async Task ListTasksByAssigneeAsync_BobFilteredByStatus_ReturnsOnlyMatchingTasks(
        TaskItemStatus status,
        string expectedTaskId)
    {
        var service = new TaskService(_dbContext!, _timeProvider);

        var tasks = await service.ListTasksByAssigneeAsync(BobId, status);

        var task = Assert.Single(tasks);
        Assert.Equal(Guid.Parse(expectedTaskId), task.Id);
    }

    /// <summary>Verifies that an undefined status filter raises <see cref="ArgumentOutOfRangeException"/>.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ListTasksByAssigneeAsync_UndefinedStatus_ThrowsArgumentOutOfRangeException()
    {
        var service = new TaskService(_dbContext!, _timeProvider);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.ListTasksByAssigneeAsync(BobId, (TaskItemStatus)99));
    }

    /// <summary>
    /// Verifies the BR3 race (grill Q4): a context that already tracks Bob as active does not re-read the row, so
    /// only the database trigger catches a deactivation committed after the read. The service must translate the
    /// trigger's rejection into <see cref="BusinessRuleViolationException"/> while keeping the original exception
    /// chain, and must not leave a persisted task behind.
    /// </summary>
    /// <remarks>Enforces BR3 at the database, translated by the service (ADR 0009).</remarks>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateTaskAsync_AssigneeDeactivatedAfterRead_ThrowsBR3FromTrigger()
    {
        await _dbContext!.Employees.SingleAsync(e => e.Id == BobId);
        await _dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE employees SET is_active = false WHERE id = '10000000-0000-0000-0000-000000000002'");

        var service = new TaskService(_dbContext, _timeProvider);
        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => service.CreateTaskAsync("Race", AliceId, BobId, null, null));

        Assert.Equal("BR3", ex.RuleId);
        var dbUpdateException = Assert.IsType<DbUpdateException>(ex.InnerException);
        var postgresException = Assert.IsType<PostgresException>(dbUpdateException.InnerException);
        Assert.Equal("trg_tasks_br3_assignee_active", postgresException.ConstraintName);

        await using var readContext = PostgresFixture.CreateContext(_connectionString);
        Assert.False(await readContext.Tasks.AnyAsync(t => t.Title == "Race"));
    }
}
