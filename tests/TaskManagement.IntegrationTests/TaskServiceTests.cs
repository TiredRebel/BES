using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using TaskManagement.Application;
using TaskManagement.Domain;
using TaskManagement.Infrastructure;

namespace TaskManagement.IntegrationTests;

/// <summary>
/// End-to-end tests for <see cref="TaskService"/>'s three use cases against a real, migrated and seeded PostgreSQL
/// database. Every test uses a fixed <see cref="FixedTimeProvider"/> (2026-09-18T12:00:00Z). Tests that check what
/// was persisted re-read it through a second <see cref="TaskManagementDbContext"/>, so those assertions do not rely
/// on the first context's change tracker.
/// </summary>
[Collection("Postgres")]
public sealed class TaskServiceTests : IAsyncLifetime
{
    private static readonly Guid AliceId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid BobId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid CarolId = Guid.Parse("10000000-0000-0000-0000-000000000003");
    private static readonly Guid DanId = Guid.Parse("10000000-0000-0000-0000-000000000004");
    private static readonly Guid[] BobsTaskIdsOrderedByDueAt =
    [
        Guid.Parse("20000000-0000-0000-0000-000000000001"),
        Guid.Parse("30000000-0000-0000-0000-000000000101"),
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

    /// <summary>
    /// Verifies BR3 through the service: an inactive assignee is rejected and nothing is persisted. The service's
    /// own check fires first; the domain would reject it too if the service check were removed. The service half
    /// alone is proven by
    /// <c>CreateTaskAsync_InactiveEmployeeAsCreatorAndAssignee_ThrowsBR3FromServiceCheck</c>.
    /// </summary>
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
    /// Verifies that when the same inactive employee is both creator and assignee, the service's own BR3 check throws
    /// before <see cref="TaskItem.Create"/> runs. It goes red only if the service check is removed: the domain would
    /// then throw BR5 first, because <see cref="TaskItem.Create"/> checks BR5 before BR3. That domain order is pinned
    /// by the unit test <c>Create_InactiveEmployeeAsCreatorAndAssignee_ThrowsBusinessRuleViolationBR5</c>.
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
    /// Verifies use case 3: listing Bob's tasks returns them ordered by <c>DueAt</c>, with the null-<c>DueAt</c> task
    /// last. A third Bob task (due 2026-11-01, planned 2026-09-20) makes that order differ from ordering by id, by
    /// planned start, or by insertion.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ListTasksByAssigneeAsync_Bob_ReturnsHisTasksOrderedByDueAt()
    {
        var service = new TaskService(_dbContext!, _timeProvider);

        await _dbContext!.Database.ExecuteSqlRawAsync(
            "INSERT INTO tasks (id, title, status, creator_id, assignee_id, planned_start_at, due_at, completed_at) VALUES " +
            "('30000000-0000-0000-0000-000000000101', 'Later deadline', 'New', '10000000-0000-0000-0000-000000000001', " +
            "'10000000-0000-0000-0000-000000000002', '2026-09-20T09:00:00+00:00', '2026-11-01T17:00:00+00:00', NULL)");

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
    /// is returned. Alice gets a <c>New</c> and a <c>Cancelled</c> task first, so a filter that dropped the assignee
    /// condition would return two tasks.
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

        await _dbContext!.Database.ExecuteSqlRawAsync(
            "INSERT INTO tasks (id, title, status, creator_id, assignee_id, planned_start_at, due_at, completed_at) VALUES " +
            "('30000000-0000-0000-0000-000000000102', 'Alice new', 'New', '10000000-0000-0000-0000-000000000002', " +
            "'10000000-0000-0000-0000-000000000001', NULL, NULL, NULL), " +
            "('30000000-0000-0000-0000-000000000103', 'Alice cancelled', 'Cancelled', '10000000-0000-0000-0000-000000000002', " +
            "'10000000-0000-0000-0000-000000000001', NULL, NULL, NULL)");

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

    /// <summary>Verifies that passing a null query object raises <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ListTasksAsync_NullQuery_ThrowsArgumentNullException()
    {
        var service = new TaskService(_dbContext!, _timeProvider);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.ListTasksAsync(null!));
    }

    /// <summary>
    /// Verifies that filtering tasks with a date range restricts results to deadlines within that interval.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ListTasksAsync_WithDueRange_ReturnsOnlyMatchingTasks()
    {
        var service = new TaskService(_dbContext!, _timeProvider);

        await _dbContext!.Database.ExecuteSqlRawAsync(
            "INSERT INTO tasks (id, title, status, creator_id, assignee_id, planned_start_at, due_at, completed_at) VALUES " +
            "('30000000-0000-0000-0000-000000000201', 'Early task', 'New', '10000000-0000-0000-0000-000000000001', " +
            "'10000000-0000-0000-0000-000000000002', '2026-09-01T09:00:00+00:00', '2026-09-15T17:00:00+00:00', NULL), " +
            "('30000000-0000-0000-0000-000000000202', 'Late task', 'New', '10000000-0000-0000-0000-000000000001', " +
            "'10000000-0000-0000-0000-000000000002', '2026-11-01T09:00:00+00:00', '2026-11-15T17:00:00+00:00', NULL)");

        var query = new TaskListQuery(
            AssigneeId: BobId,
            DueFrom: new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
            DueTo: new DateTimeOffset(2026, 10, 31, 23, 59, 59, TimeSpan.Zero));

        var tasks = await service.ListTasksAsync(query);

        var task = Assert.Single(tasks);
        Assert.Equal(Guid.Parse("20000000-0000-0000-0000-000000000001"), task.Id);
    }

    /// <summary>
    /// Verifies that filtering tasks by creator returns only tasks created by that specific employee.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ListTasksAsync_WithCreatorId_ReturnsOnlyMatchingTasks()
    {
        var service = new TaskService(_dbContext!, _timeProvider);

        await _dbContext!.Database.ExecuteSqlRawAsync(
            "INSERT INTO employees (id, full_name, email, is_active) VALUES " +
            "('10000000-0000-0000-0000-000000000004', 'Dan Smith', 'dan.smith@example.com', true) " +
            "ON CONFLICT (id) DO NOTHING; " +
            "INSERT INTO tasks (id, title, status, creator_id, assignee_id, planned_start_at, due_at, completed_at) VALUES " +
            "('30000000-0000-0000-0000-000000000203', 'Dan created task', 'New', '10000000-0000-0000-0000-000000000004', " +
            "'10000000-0000-0000-0000-000000000002', NULL, NULL, NULL)");

        var query = new TaskListQuery(AssigneeId: BobId, CreatorId: DanId);

        var tasks = await service.ListTasksAsync(query);

        var task = Assert.Single(tasks);
        Assert.Equal(Guid.Parse("30000000-0000-0000-0000-000000000203"), task.Id);
    }

    /// <summary>
    /// Verifies keyset pagination: requesting a page smaller than the total item count returns only that many
    /// items, signals <see cref="PagedResult{T}.HasNextPage"/> as <see langword="true"/>, and produces a valid cursor.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ListTasksAsync_Pagination_ReturnsFirstPageAndNextCursor()
    {
        var service = new TaskService(_dbContext!, _timeProvider);

        var query = new TaskListQuery(AssigneeId: BobId, PageSize: 1);

        var page = await service.ListTasksAsync(query);

        Assert.Single(page);
        Assert.True(page.HasNextPage);
        Assert.NotNull(page.NextCursor);
        Assert.Equal(page[0].DueAt, page.NextCursor.DueAt);
        Assert.Equal(page[0].Id, page.NextCursor.Id);
    }

    /// <summary>
    /// Verifies that querying with the cursor returned from page 1 retrieves the next page without duplicating
    /// page 1's items.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ListTasksAsync_Pagination_SecondPageUsingCursor_ReturnsRemainingItems()
    {
        var service = new TaskService(_dbContext!, _timeProvider);

        var page1 = await service.ListTasksAsync(new TaskListQuery(AssigneeId: BobId, PageSize: 1));
        Assert.True(page1.HasNextPage);
        Assert.NotNull(page1.NextCursor);

        var page2 = await service.ListTasksAsync(new TaskListQuery(AssigneeId: BobId, PageSize: 10, Cursor: page1.NextCursor));

        Assert.NotEmpty(page2);
        Assert.DoesNotContain(page2, t => t.Id == page1[0].Id);
    }

    /// <summary>
    /// Verifies that specifying a page size of zero or less is clamped to <see cref="TaskListQuery.MinPageSize"/> (1).
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ListTasksAsync_PageSizeClamped_ZeroOrNegativeClampedTo1()
    {
        var service = new TaskService(_dbContext!, _timeProvider);

        var page = await service.ListTasksAsync(new TaskListQuery(AssigneeId: BobId, PageSize: -5));

        Assert.Single(page);
    }

    /// <summary>
    /// Verifies that specifying a page size exceeding <see cref="TaskListQuery.MaxPageSize"/> (100) is accepted
    /// and clamped without throwing.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ListTasksAsync_PageSizeClamped_ExceedingMaxClampedTo100()
    {
        var service = new TaskService(_dbContext!, _timeProvider);

        var page = await service.ListTasksAsync(new TaskListQuery(AssigneeId: BobId, PageSize: 500));

        Assert.NotEmpty(page);
        Assert.True(page.Count <= TaskListQuery.MaxPageSize);
    }

    /// <summary>
    /// Verifies the BR3 race (grill Q4): the service's query does run, but a context that already tracks Bob keeps
    /// its tracked, stale copy (still active), so only the database trigger catches a deactivation committed after
    /// the read. The service must translate the trigger's rejection into <see cref="BusinessRuleViolationException"/>
    /// while keeping the original exception chain, and must not leave a persisted task behind.
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

    /// <summary>
    /// Verifies that after the BR3 trigger rejects a task, the same context and service can create the next task.
    /// The rejected task must not stay queued in the context: otherwise the next save would send it again and fail
    /// (or, once the assignee is active again, persist it after the caller was told it was rejected).
    /// </summary>
    /// <remarks>Verifies the failed-save cleanup in <see cref="TaskService.CreateTaskAsync"/> (D3 findings F1/SF1).</remarks>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateTaskAsync_AfterTriggerRejection_NextCallOnSameContextSucceeds()
    {
        await _dbContext!.Employees.SingleAsync(e => e.Id == BobId);
        await _dbContext.Database.ExecuteSqlRawAsync(
            "UPDATE employees SET is_active = false WHERE id = '10000000-0000-0000-0000-000000000002'");
        var service = new TaskService(_dbContext, _timeProvider);
        var rejection = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => service.CreateTaskAsync("Race", AliceId, BobId, null, null));
        Assert.IsType<DbUpdateException>(rejection.InnerException);

        var next = await service.CreateTaskAsync("Next", BobId, AliceId, null, null);

        await using var readContext = PostgresFixture.CreateContext(_connectionString);
        Assert.False(await readContext.Tasks.AnyAsync(t => t.Title == "Race"));
        Assert.True(await readContext.Tasks.AnyAsync(t => t.Id == next.Id));
    }

    /// <summary>
    /// Verifies that after a concurrency conflict, retrying the same status change on the same context succeeds
    /// against the current row. The failed change must not stay in the context: otherwise the retry would see its
    /// own unsaved <c>Completed</c> and be rejected with a false BR4.
    /// </summary>
    /// <remarks>Verifies the failed-save cleanup in <see cref="TaskService.ChangeTaskStatusAsync(Guid, TaskItemStatus, CancellationToken)"/> (D3 finding SF2).</remarks>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ChangeTaskStatusAsync_AfterConcurrencyConflict_RetryOnSameContextSucceeds()
    {
        var taskId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        await _dbContext!.Tasks.SingleAsync(t => t.Id == taskId);
        await using (var otherContext = PostgresFixture.CreateContext(_connectionString))
        {
            await new TaskService(otherContext, _timeProvider).ChangeTaskStatusAsync(taskId, TaskItemStatus.InProgress);
        }

        var service = new TaskService(_dbContext, _timeProvider);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => service.ChangeTaskStatusAsync(taskId, TaskItemStatus.Completed));

        var retried = await service.ChangeTaskStatusAsync(taskId, TaskItemStatus.Completed);

        Assert.Equal(TaskItemStatus.Completed, retried.Status);
        await using var readContext = PostgresFixture.CreateContext(_connectionString);
        var persisted = await readContext.Tasks.AsNoTracking().SingleAsync(t => t.Id == taskId);
        Assert.Equal(TaskItemStatus.Completed, persisted.Status);
        Assert.Equal(new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero), persisted.CompletedAt);
    }

    /// <summary>
    /// Verifies that a database error other than the BR3 trigger is not translated: an assignee deleted between the
    /// service's read and its insert surfaces as the foreign-key <see cref="DbUpdateException"/>
    /// (<c>fk_tasks_employees_assignee_id</c>), not as a business-rule violation, and nothing is persisted. A save
    /// interceptor deletes the assignee on another connection just before the insert, a real interleaving.
    /// </summary>
    /// <remarks>
    /// Verifies that the BR3 catch filter in <see cref="TaskService.CreateTaskAsync"/> lets a non-check-violation error
    /// through untranslated, and that the context stays usable after an untranslated failure. The filter's
    /// constraint-name condition is not covered: no other <c>23514</c> error can be reached through the service.
    /// </remarks>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateTaskAsync_AssigneeDeletedBeforeInsert_ThrowsForeignKeyDbUpdateException()
    {
        await _dbContext!.Database.ExecuteSqlRawAsync(
            "INSERT INTO employees (id, full_name, email, is_active) VALUES " +
            "('10000000-0000-0000-0000-000000000004', 'Dan Temp', 'dan.temp@example.com', true)");
        var options = new DbContextOptionsBuilder<TaskManagementDbContext>()
            .UseNpgsql(_connectionString)
            .AddInterceptors(new DeleteEmployeeBeforeSaveInterceptor(_connectionString, DanId))
            .Options;
        await using var context = new TaskManagementDbContext(options);
        var service = new TaskService(context, _timeProvider);

        var ex = await Assert.ThrowsAsync<DbUpdateException>(
            () => service.CreateTaskAsync("Orphan", AliceId, DanId, null, null));

        var postgresException = Assert.IsType<PostgresException>(ex.InnerException);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, postgresException.SqlState);
        Assert.Equal("fk_tasks_employees_assignee_id", postgresException.ConstraintName);
        var next = await service.CreateTaskAsync("After orphan", AliceId, BobId, null, null);
        await using var readContext = PostgresFixture.CreateContext(_connectionString);
        Assert.False(await readContext.Tasks.AnyAsync(t => t.Title == "Orphan"));
        Assert.True(await readContext.Tasks.AnyAsync(t => t.Id == next.Id));
    }

    /// <summary>Deletes one employee on its own connection just before each save, to force an FK race.</summary>
    private sealed class DeleteEmployeeBeforeSaveInterceptor(string connectionString, Guid employeeId) : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("DELETE FROM employees WHERE id = @id", connection);
            command.Parameters.AddWithValue("id", employeeId);
            await command.ExecuteNonQueryAsync(cancellationToken);
            return result;
        }
    }
}
