using Microsoft.EntityFrameworkCore;
using Npgsql;
using TaskManagement.Application;
using TaskManagement.Domain;
using TaskManagement.Infrastructure;

namespace TaskManagement.IntegrationTests;

/// <summary>
/// End-to-end integration tests for task reassignment and audit history tracking against a real PostgreSQL database.
/// </summary>
[Collection("Postgres")]
public sealed class TaskReassignmentAndHistoryTests : IAsyncLifetime
{
    private static readonly Guid AliceId = Guid.Parse("10000000-0000-0000-0000-000000000001"); // Olena (active)
    private static readonly Guid BobId = Guid.Parse("10000000-0000-0000-0000-000000000002");   // Bohdan (active)
    private static readonly Guid CarolId = Guid.Parse("10000000-0000-0000-0000-000000000003"); // Оксана (inactive)
    private static readonly Guid Task1Id = Guid.Parse("20000000-0000-0000-0000-000000000001"); // New, Olena -> Bohdan
    private static readonly Guid Task2Id = Guid.Parse("20000000-0000-0000-0000-000000000002"); // Completed, Bohdan -> Olena

    private readonly PostgresFixture _fixture;
    private readonly FixedTimeProvider _timeProvider = new();
    private string _connectionString = string.Empty;
    private TaskManagementDbContext? _dbContext;

    /// <summary>Initializes a new instance of the <see cref="TaskReassignmentAndHistoryTests"/> class.</summary>
    /// <param name="fixture">The shared PostgreSQL container fixture.</param>
    public TaskReassignmentAndHistoryTests(PostgresFixture fixture) => _fixture = fixture;

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

    /// <summary>Reassigning updates assignee_id and writes an audit record.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ReassignTaskAsync_ActiveEmployee_UpdatesAssigneeAndRecordsHistory()
    {
        // Insert a new employee David (active)
        var davidId = Guid.Parse("10000000-0000-0000-0000-000000000005");
        await _dbContext!.Database.ExecuteSqlAsync(
            $"INSERT INTO employees (id, full_name, email, is_active) VALUES ({davidId}, 'Давид Коваль', 'david.koval@example.com', true)");

        var service = new TaskService(_dbContext, _timeProvider);

        var updated = await service.ReassignTaskAsync(Task1Id, davidId, AliceId);

        Assert.Equal(davidId, updated.AssigneeId);

        await using var readContext = PostgresFixture.CreateContext(_connectionString);
        var persisted = await readContext.Tasks.AsNoTracking().SingleAsync(t => t.Id == Task1Id);
        Assert.Equal(davidId, persisted.AssigneeId);

        var history = await service.GetTaskHistoryAsync(Task1Id);
        var entry = Assert.Single(history);
        Assert.Equal(Task1Id, entry.TaskId);
        Assert.Equal(AliceId, entry.ChangedById);
        Assert.Equal(TaskChangeType.AssigneeChanged, entry.ChangeType);
        Assert.Equal(BobId.ToString(), entry.OldValue);
        Assert.Equal(davidId.ToString(), entry.NewValue);
        Assert.Equal(_timeProvider.GetUtcNow(), entry.ChangedAt);
    }

    /// <summary>Status change writes an audit record with the acting employee.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ChangeTaskStatusAsync_WithChangedById_UpdatesStatusAndRecordsHistory()
    {
        var service = new TaskService(_dbContext!, _timeProvider);

        var updated = await service.ChangeTaskStatusAsync(Task1Id, TaskItemStatus.InProgress, AliceId);

        Assert.Equal(TaskItemStatus.InProgress, updated.Status);

        var history = await service.GetTaskHistoryAsync(Task1Id);
        var entry = Assert.Single(history);
        Assert.Equal(Task1Id, entry.TaskId);
        Assert.Equal(AliceId, entry.ChangedById);
        Assert.Equal(TaskChangeType.StatusChanged, entry.ChangeType);
        Assert.Equal("New", entry.OldValue);
        Assert.Equal("InProgress", entry.NewValue);
        Assert.Equal(_timeProvider.GetUtcNow(), entry.ChangedAt);
    }

    /// <summary>Reassigning to the same assignee does not create a history entry.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ReassignTaskAsync_SameAssignee_DoesNotRecordHistory()
    {
        var service = new TaskService(_dbContext!, _timeProvider);

        var updated = await service.ReassignTaskAsync(Task1Id, BobId, AliceId);

        Assert.Equal(BobId, updated.AssigneeId);

        var history = await service.GetTaskHistoryAsync(Task1Id);
        Assert.Empty(history);
    }

    /// <summary>No-op status change does not create a history entry.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ChangeTaskStatusAsync_SameStatus_DoesNotRecordHistory()
    {
        var service = new TaskService(_dbContext!, _timeProvider);

        var updated = await service.ChangeTaskStatusAsync(Task1Id, TaskItemStatus.New, AliceId);

        Assert.Equal(TaskItemStatus.New, updated.Status);

        var history = await service.GetTaskHistoryAsync(Task1Id);
        Assert.Empty(history);
    }

    /// <summary>Reassignment to inactive employee fails.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ReassignTaskAsync_InactiveEmployee_ThrowsBusinessRuleViolationExceptionBR3()
    {
        var service = new TaskService(_dbContext!, _timeProvider);

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => service.ReassignTaskAsync(Task1Id, CarolId, AliceId));

        Assert.Equal("BR3", ex.RuleId);
    }

    /// <summary>Reassignment of completed task fails.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ReassignTaskAsync_CompletedTask_ThrowsBusinessRuleViolationExceptionBR4()
    {
        var service = new TaskService(_dbContext!, _timeProvider);

        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => service.ReassignTaskAsync(Task2Id, BobId, AliceId));

        Assert.Equal("BR4", ex.RuleId);
    }

    /// <summary>Reassignment to creator fails.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ReassignTaskAsync_CreatorAsNewAssignee_ThrowsBusinessRuleViolationExceptionBR5()
    {
        var service = new TaskService(_dbContext!, _timeProvider);

        // Task 1 was created by Alice, currently assigned to Bob. Reassigning to Alice violates BR5.
        var ex = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => service.ReassignTaskAsync(Task1Id, AliceId, BobId));

        Assert.Equal("BR5", ex.RuleId);
    }

    /// <summary>DB trigger blocks direct SQL update to inactive assignee.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Trigger_ReassignToInactiveEmployee_ThrowsCheckViolationBR3()
    {
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _dbContext!.Database.ExecuteSqlAsync(
                $"UPDATE tasks SET assignee_id = {CarolId} WHERE id = {Task1Id}"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        Assert.Equal("trg_tasks_br3_assignee_active", ex.ConstraintName);
    }

    /// <summary>DB trigger blocks direct SQL update on completed task.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Trigger_ReassignCompletedTask_ThrowsCheckViolationBR4()
    {
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            _dbContext!.Database.ExecuteSqlAsync(
                $"UPDATE tasks SET assignee_id = {BobId} WHERE id = {Task2Id}"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        Assert.Equal("trg_tasks_br4_final_status", ex.ConstraintName);
    }

    /// <summary>Failed save detaches entities, allowing subsequent operations on the context.</summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ReassignTaskAsync_AfterTriggerRejection_NextCallOnSameContextSucceeds()
    {
        // Insert a new employee David (active)
        var davidId = Guid.Parse("10000000-0000-0000-0000-000000000006");
        await _dbContext!.Database.ExecuteSqlAsync(
            $"INSERT INTO employees (id, full_name, email, is_active) VALUES ({davidId}, 'Емма Франко', 'emma.franko@example.com', true)");

        // Pre-load David into context, then concurrently deactivate him in DB to simulate a race condition
        await _dbContext.Employees.SingleAsync(e => e.Id == davidId);
        await _dbContext.Database.ExecuteSqlAsync(
            $"UPDATE employees SET is_active = false WHERE id = {davidId}");

        var service = new TaskService(_dbContext, _timeProvider);

        var rejection = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => service.ReassignTaskAsync(Task1Id, davidId, AliceId));
        Assert.IsType<DbUpdateException>(rejection.InnerException);

        // Next call on the same context to change status succeeds
        var updated = await service.ChangeTaskStatusAsync(Task1Id, TaskItemStatus.InProgress, AliceId);
        Assert.Equal(TaskItemStatus.InProgress, updated.Status);
    }

    /// <summary>
    /// When DB trigger trg_tasks_br4_final_status rejects status change, service translates it to BR4 exception
    /// and detaches entities.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ChangeTaskStatusAsync_TriggerBR4Rejection_ThrowsBR4ExceptionAndDetachesEntities()
    {
        // Load Task1 (New) into change tracker
        var task = await _dbContext!.Tasks.SingleAsync(t => t.Id == Task1Id);

        // Concurrently complete the task in database behind the context's back
        await _dbContext.Database.ExecuteSqlAsync(
            $"UPDATE tasks SET status = 'Completed', completed_at = now() WHERE id = {Task1Id}");

        // Align original xmin so the UPDATE matches the row and hits the trigger rather than OCC conflict
        var xminStr = await _dbContext.Database.SqlQuery<string>($"SELECT xmin::text AS \"Value\" FROM tasks WHERE id = {Task1Id}").SingleAsync();
        var currentXmin = uint.Parse(xminStr, System.Globalization.CultureInfo.InvariantCulture);
        _dbContext.Entry(task).Property(t => t.Version).OriginalValue = currentXmin;

        var service = new TaskService(_dbContext, _timeProvider);

        var rejection = await Assert.ThrowsAsync<BusinessRuleViolationException>(
            () => service.ChangeTaskStatusAsync(Task1Id, TaskItemStatus.InProgress, AliceId));

        Assert.Equal("BR4", rejection.RuleId);
        Assert.IsType<DbUpdateException>(rejection.InnerException);

        // Verify task and history are detached on failure
        Assert.Equal(EntityState.Detached, _dbContext.Entry(task).State);
    }
}

