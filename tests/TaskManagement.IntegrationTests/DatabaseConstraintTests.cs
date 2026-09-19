using Microsoft.EntityFrameworkCore;
using Npgsql;
using TaskManagement.Domain;
using TaskManagement.Infrastructure;

namespace TaskManagement.IntegrationTests;

/// <summary>
/// Proves, against a real PostgreSQL database, that every CHECK constraint, unique index, restrict foreign key,
/// the <c>trg_tasks_br3_br4</c> trigger and the <c>xmin</c> concurrency token reject the row spec &#167;15 pins for
/// them, and that the boundary-accepted rows are not rejected.
/// </summary>
[Collection("Postgres")]
public sealed class DatabaseConstraintTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;
    private string _connectionString = string.Empty;
    private TaskManagementDbContext? _dbContext;

    /// <summary>Initializes a new instance of the <see cref="DatabaseConstraintTests"/> class.</summary>
    /// <param name="fixture">The shared PostgreSQL container fixture.</param>
    public DatabaseConstraintTests(PostgresFixture fixture) => _fixture = fixture;

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

    /// <summary>
    /// Verifies that inserting a <c>Completed</c> task with a null <c>completed_at</c> is rejected by BR1's CHECK.
    /// </summary>
    /// <remarks>Enforces BR1.</remarks>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Insert_CompletedWithoutCompletedAt_RejectedByBR1Check()
    {
        var ex = await AssertRejectedAsync(
            "INSERT INTO tasks (id, title, status, creator_id, assignee_id, planned_start_at, due_at, completed_at) " +
            "VALUES ('30000000-0000-0000-0000-000000000001', 'BR1 violation', 'Completed', " +
            "'10000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000002', NULL, NULL, NULL)");

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        Assert.Equal("ck_tasks_br1_completed_at_iff_completed", ex.ConstraintName);
    }

    /// <summary>
    /// Verifies that inserting a <c>New</c> task with a non-null <c>completed_at</c> is rejected by BR1's CHECK.
    /// </summary>
    /// <remarks>Enforces BR1.</remarks>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Insert_NewWithCompletedAt_RejectedByBR1Check()
    {
        var ex = await AssertRejectedAsync(
            "INSERT INTO tasks (id, title, status, creator_id, assignee_id, planned_start_at, due_at, completed_at) " +
            "VALUES ('30000000-0000-0000-0000-000000000002', 'BR1 violation', 'New', " +
            "'10000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000002', NULL, NULL, " +
            "'2026-09-18T12:00:00+00:00')");

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        Assert.Equal("ck_tasks_br1_completed_at_iff_completed", ex.ConstraintName);
    }

    /// <summary>
    /// Verifies that clearing <c>completed_at</c> on an already-<c>Completed</c> seeded task is rejected by BR1's
    /// CHECK.
    /// </summary>
    /// <remarks>Enforces BR1.</remarks>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Update_ClearCompletedAtOfCompletedTask_RejectedByBR1Check()
    {
        var ex = await AssertRejectedAsync(
            "UPDATE tasks SET completed_at = NULL WHERE id = '20000000-0000-0000-0000-000000000002'");

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        Assert.Equal("ck_tasks_br1_completed_at_iff_completed", ex.ConstraintName);
    }

    /// <summary>
    /// Verifies that inserting a task whose <c>due_at</c> is earlier than its <c>planned_start_at</c> is rejected
    /// by BR2's CHECK.
    /// </summary>
    /// <remarks>Enforces BR2.</remarks>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Insert_DueAtBeforePlannedStartAt_RejectedByBR2Check()
    {
        var ex = await AssertRejectedAsync(
            "INSERT INTO tasks (id, title, status, creator_id, assignee_id, planned_start_at, due_at, completed_at) " +
            "VALUES ('30000000-0000-0000-0000-000000000004', 'BR2 violation', 'New', " +
            "'10000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000002', " +
            "'2026-10-10T09:00:00+00:00', '2026-10-01T09:00:00+00:00', NULL)");

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        Assert.Equal("ck_tasks_br2_due_at_not_before_planned_start_at", ex.ConstraintName);
    }

    /// <summary>
    /// Verifies that a task whose <c>due_at</c> equals its <c>planned_start_at</c> is accepted: BR2 forbids earlier,
    /// not equal.
    /// </summary>
    /// <remarks>Enforces BR2 (boundary).</remarks>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Insert_DueAtEqualsPlannedStartAt_Accepted()
    {
        var rowsAffected = await _dbContext!.Database.ExecuteSqlRawAsync(
            "INSERT INTO tasks (id, title, status, creator_id, assignee_id, planned_start_at, due_at, completed_at) " +
            "VALUES ('30000000-0000-0000-0000-000000000005', 'BR2 boundary', 'New', " +
            "'10000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000002', " +
            "'2026-10-01T09:00:00+00:00', '2026-10-01T09:00:00+00:00', NULL)");

        Assert.Equal(1, rowsAffected);
    }

    /// <summary>
    /// Verifies that inserting a task whose <c>assignee_id</c> equals its <c>creator_id</c> is rejected by BR5's
    /// CHECK.
    /// </summary>
    /// <remarks>Enforces BR5.</remarks>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Insert_AssigneeEqualsCreator_RejectedByBR5Check()
    {
        var ex = await AssertRejectedAsync(
            "INSERT INTO tasks (id, title, status, creator_id, assignee_id, planned_start_at, due_at, completed_at) " +
            "VALUES ('30000000-0000-0000-0000-000000000006', 'BR5 violation', 'New', " +
            "'10000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', NULL, NULL, NULL)");

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        Assert.Equal("ck_tasks_br5_assignee_not_creator", ex.ConstraintName);
    }

    /// <summary>
    /// Verifies that inserting a task with a <c>status</c> value outside the four defined statuses is rejected by
    /// the enum CHECK.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Insert_UnknownStatus_RejectedByStatusCheck()
    {
        var ex = await AssertRejectedAsync(
            "INSERT INTO tasks (id, title, status, creator_id, assignee_id, planned_start_at, due_at, completed_at) " +
            "VALUES ('30000000-0000-0000-0000-000000000007', 'Bad status', 'Done', " +
            "'10000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000002', NULL, NULL, NULL)");

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        Assert.Equal("ck_tasks_status_valid", ex.ConstraintName);
    }

    /// <summary>
    /// Verifies that inserting an employee with an already-used e-mail address is rejected by the unique index.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Insert_DuplicateEmail_RejectedByUniqueIndex()
    {
        var ex = await AssertRejectedAsync(
            "INSERT INTO employees (id, full_name, email, is_active) VALUES " +
            "('10000000-0000-0000-0000-000000000009', 'Олена Дублікат', 'olena.kovalenko@example.com', true)");

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);
        Assert.Equal("ux_employees_email", ex.ConstraintName);
    }

    /// <summary>
    /// Verifies that deleting an employee who is still referenced by a task is rejected by the restrict foreign
    /// key, not silently cascaded.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Delete_EmployeeWithTasks_RejectedByRestrictForeignKey()
    {
        var ex = await AssertRejectedAsync("DELETE FROM employees WHERE id = '10000000-0000-0000-0000-000000000002'");

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, ex.SqlState);

        // Bob is both a creator and an assignee, so either restrict FK may fire first (spec §15).
        Assert.True(
            ex.ConstraintName is "fk_tasks_employees_creator_id" or "fk_tasks_employees_assignee_id",
            $"Unexpected constraint: {ex.ConstraintName}");
    }

    /// <summary>
    /// Verifies that when two contexts load the same task and both change its status, the second
    /// <c>SaveChangesAsync</c> fails with a concurrency exception instead of silently overwriting the first
    /// writer's final status.
    /// </summary>
    /// <remarks>Protects BR4 via the <c>xmin</c> concurrency token.</remarks>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task ConcurrentStatusChange_SecondSave_ThrowsDbUpdateConcurrencyException()
    {
        var taskId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        await using var secondContext = PostgresFixture.CreateContext(_connectionString);

        var firstTask = await _dbContext!.Tasks.SingleAsync(t => t.Id == taskId);
        var secondTask = await secondContext.Tasks.SingleAsync(t => t.Id == taskId);

        firstTask.ChangeStatus(TaskItemStatus.Completed, DateTimeOffset.UtcNow);
        await _dbContext.SaveChangesAsync();

        secondTask.ChangeStatus(TaskItemStatus.Cancelled, DateTimeOffset.UtcNow);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondContext.SaveChangesAsync());
    }

    /// <summary>
    /// Verifies that inserting a task for an inactive assignee is rejected by the <c>trg_tasks_br3_br4</c> trigger.
    /// </summary>
    /// <remarks>Enforces BR3 at the database, catching every writer including raw SQL.</remarks>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Insert_TaskForInactiveAssignee_RejectedByBR3Trigger()
    {
        var ex = await AssertRejectedAsync(
            "INSERT INTO tasks (id, title, status, creator_id, assignee_id, planned_start_at, due_at, completed_at) " +
            "VALUES ('30000000-0000-0000-0000-000000000008', 'BR3 violation', 'New', " +
            "'10000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000003', NULL, NULL, NULL)");

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        Assert.Equal("trg_tasks_br3_assignee_active", ex.ConstraintName);
    }

    /// <summary>
    /// Verifies that inserting a task for an unknown assignee fails with the foreign key, not the BR3 trigger: the
    /// trigger must not mask a referential-integrity error.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Insert_TaskForUnknownAssignee_RejectedByForeignKeyNotBR3Trigger()
    {
        var ex = await AssertRejectedAsync(
            "INSERT INTO tasks (id, title, status, creator_id, assignee_id, planned_start_at, due_at, completed_at) " +
            "VALUES ('30000000-0000-0000-0000-000000000009', 'Unknown assignee', 'New', " +
            "'10000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000009', NULL, NULL, NULL)");

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, ex.SqlState);
        Assert.Equal("fk_tasks_employees_assignee_id", ex.ConstraintName);
    }

    /// <summary>
    /// Verifies that changing the status of an already-<c>Completed</c> seeded task is rejected by the
    /// <c>trg_tasks_br3_br4</c> trigger.
    /// </summary>
    /// <remarks>Enforces BR4 at the database, catching every writer including raw SQL.</remarks>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Update_StatusOfCompletedTask_RejectedByBR4Trigger()
    {
        var ex = await AssertRejectedAsync(
            "UPDATE tasks SET status = 'New', completed_at = NULL WHERE id = '20000000-0000-0000-0000-000000000002'");

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        Assert.Equal("trg_tasks_br4_final_status", ex.ConstraintName);
    }

    /// <summary>
    /// Verifies that changing the status of an already-<c>Cancelled</c> seeded task is rejected by the
    /// <c>trg_tasks_br3_br4</c> trigger, covering the trigger's second final status.
    /// </summary>
    /// <remarks>Verifies BR4 at the database for <c>Cancelled</c>.</remarks>
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Update_StatusOfCancelledTask_RejectedByBR4Trigger()
    {
        var ex = await AssertRejectedAsync(
            "UPDATE tasks SET status = 'New' WHERE id = '20000000-0000-0000-0000-000000000003'");

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
        Assert.Equal("trg_tasks_br4_final_status", ex.ConstraintName);
    }

    /// <summary>
    /// Runs <paramref name="sql"/> against this test's database and returns the <see cref="PostgresException"/> it
    /// is expected to raise.
    /// </summary>
    /// <param name="sql">The raw SQL statement expected to violate a constraint or trigger.</param>
    /// <returns>The <see cref="PostgresException"/> raised by PostgreSQL.</returns>
    private Task<PostgresException> AssertRejectedAsync(string sql) =>
        Assert.ThrowsAsync<PostgresException>(() => _dbContext!.Database.ExecuteSqlRawAsync(sql));
}
