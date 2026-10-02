using Microsoft.EntityFrameworkCore;
using TaskManagement.Application;
using TaskManagement.Domain;
using TaskManagement.Infrastructure;

namespace TaskManagement.Wpf.Services;

/// <summary>
/// <see cref="ITaskClient"/> over <see cref="TaskService"/>, with one short-lived context per operation (ADR 0010).
/// </summary>
/// <remarks>
/// Every call opens a context from the factory, builds a <see cref="TaskService"/> on it, maps the result to view
/// records and disposes the context before returning, so no window holds a long-lived context or a tracked entity.
/// BR1–BR5 are enforced by <see cref="TaskService"/> and the database; their exceptions pass through unchanged.
/// </remarks>
/// <param name="contextFactory">Creates the per-operation context.</param>
/// <param name="timeProvider">The clock handed to <see cref="TaskService"/>.</param>
public sealed class TaskClient(IDbContextFactory<TaskManagementDbContext> contextFactory, TimeProvider timeProvider)
    : ITaskClient
{
    /// <summary>The number of tasks on one page of the list.</summary>
    public const int PageSize = 20;

    /// <inheritdoc />
    /// <remarks>Reads the employees directly: <see cref="TaskService"/> has no employee query (ADR 0010).</remarks>
    public async Task<IReadOnlyList<EmployeeOption>> GetEmployeesAsync(CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Employees
            .AsNoTracking()
            .OrderBy(e => e.FullName)
            .Select(e => new EmployeeOption(e.Id, e.FullName, e.IsActive))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TaskPage> ListTasksAsync(
        Guid assigneeId,
        TaskItemStatus? status,
        TaskCursor? cursor,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var page = await new TaskService(db, timeProvider)
            .ListTasksByAssigneeAsync(assigneeId, status, PageSize, cursor, cancellationToken);

        var assigneeName = await db.Employees
            .Where(e => e.Id == assigneeId)
            .Select(e => e.FullName)
            .SingleOrDefaultAsync(cancellationToken) ?? assigneeId.ToString();

        var rows = page
            .Select(t => new TaskRow(
                t.Id,
                t.Title,
                t.Status,
                t.AssigneeId,
                assigneeName,
                t.PlannedStartAt?.ToLocalTime(),
                t.DueAt?.ToLocalTime(),
                t.CompletedAt?.ToLocalTime()))
            .ToList();

        return new TaskPage(rows, page.NextCursor);
    }

    /// <inheritdoc />
    public async Task CreateTaskAsync(
        string title,
        Guid creatorId,
        Guid assigneeId,
        DateTimeOffset? plannedStartAt,
        DateTimeOffset? dueAt,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await new TaskService(db, timeProvider)
            .CreateTaskAsync(title, creatorId, assigneeId, plannedStartAt, dueAt, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>Calls the overload that takes <c>changedById</c>; the other one records the assignee as the author.</remarks>
    public async Task ChangeStatusAsync(
        Guid taskId,
        TaskItemStatus newStatus,
        Guid changedById,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await new TaskService(db, timeProvider)
            .ChangeTaskStatusAsync(taskId, newStatus, changedById, cancellationToken);
    }

    /// <inheritdoc />
    public async Task ReassignAsync(
        Guid taskId,
        Guid newAssigneeId,
        Guid changedById,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await new TaskService(db, timeProvider)
            .ReassignTaskAsync(taskId, newAssigneeId, changedById, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <see cref="TaskChangeType.AssigneeChanged"/> stores assignee ids as text; they and the author id are resolved
    /// to names. An id that no longer matches an employee is shown as is.
    /// </remarks>
    public async Task<IReadOnlyList<HistoryRow>> GetHistoryAsync(Guid taskId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entries = await new TaskService(db, timeProvider).GetTaskHistoryAsync(taskId, cancellationToken);
        var names = await db.Employees
            .AsNoTracking()
            .ToDictionaryAsync(e => e.Id, e => e.FullName, cancellationToken);

        string Name(Guid id) => names.TryGetValue(id, out var name) ? name : id.ToString();
        string Value(TaskHistoryEntry entry, string value) =>
            entry.ChangeType == TaskChangeType.AssigneeChanged && Guid.TryParse(value, out var id) ? Name(id) : value;

        return entries
            .Select(h => new HistoryRow(
                h.ChangeType,
                Value(h, h.OldValue),
                Value(h, h.NewValue),
                h.ChangedAt.ToLocalTime(),
                Name(h.ChangedById)))
            .ToList();
    }
}
