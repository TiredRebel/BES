using Microsoft.EntityFrameworkCore;
using Npgsql;
using TaskManagement.Domain;
using TaskManagement.Infrastructure;

namespace TaskManagement.Application;

/// <summary>
/// Implements the module's use cases: assigning a task, moving it through its statuses, reassigning it, listing what an
/// employee has, and viewing its change history.
/// </summary>
/// <remarks>
/// No interface: there is one implementation and the integration tests exercise it against a real database, so
/// there is no mocking need. When a save fails, <see cref="CreateTaskAsync"/>, <see cref="ChangeTaskStatusAsync(Guid, TaskItemStatus, Guid, CancellationToken)"/>
/// and <see cref="ReassignTaskAsync"/> detach the entities they added or changed, so a caller that keeps using the same
/// context never has that failed change retried by a later save.
/// </remarks>
/// <param name="dbContext">The database context the three use cases read and write through.</param>
/// <param name="timeProvider">The clock that supplies <see cref="TaskItem.CompletedAt"/> when a task is completed.</param>
public sealed class TaskService(TaskManagementDbContext dbContext, TimeProvider timeProvider)
{

    /// <summary>
    /// Assigns a new task from <paramref name="creatorId"/> to <paramref name="assigneeId"/>.
    /// </summary>
    /// <param name="title">The task title.</param>
    /// <param name="creatorId">The identifier of the employee creating the task.</param>
    /// <param name="assigneeId">The identifier of the employee the task is assigned to.</param>
    /// <param name="plannedStartAt">The planned start date and time, or null.</param>
    /// <param name="dueAt">The deadline, or null.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The persisted <see cref="TaskItem"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="title"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="title"/> is empty, whitespace, or longer than 200 characters once trimmed.</exception>
    /// <exception cref="KeyNotFoundException"><paramref name="creatorId"/> or <paramref name="assigneeId"/> does not match an employee when they are read.</exception>
    /// <exception cref="BusinessRuleViolationException">
    /// BR3: the assignee is inactive, caught by this method's own check or, on a race, by the database trigger (the
    /// domain re-checks BR3 too, but through this method it sees the same employee, so its check never fires first). BR5: the creator and the assignee are the same (active) employee. BR2: both dates
    /// are set and <paramref name="dueAt"/> is earlier than <paramref name="plannedStartAt"/>. Only the first
    /// violated rule is reported (see remarks).
    /// </exception>
    /// <exception cref="DbUpdateException">
    /// The database rejected the insert for a reason other than the BR3 trigger, for example the foreign key when an
    /// employee is deleted between the read and the insert. The new task is already detached when this reaches the
    /// caller, so the exception's entries cannot be saved again; call this method again instead.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    /// <remarks>
    /// Enforces BR2, BR3 and BR5. Check order: this method checks BR3 before calling
    /// <see cref="TaskItem.Create"/>, which checks BR5, then BR3, then BR2. So when one inactive employee is both
    /// creator and assignee, BR3 is reported. If the save fails, the new task is detached from the context.
    /// The save also sends any other pending changes the caller left in the context. If one of those is rejected by
    /// the BR3 trigger, it is reported as BR3 for this call's assignee, so save or discard your own pending changes
    /// before calling this method.
    /// </remarks>
    public async Task<TaskItem> CreateTaskAsync(
        string title,
        Guid creatorId,
        Guid assigneeId,
        DateTimeOffset? plannedStartAt,
        DateTimeOffset? dueAt,
        CancellationToken cancellationToken = default)
    {
        var creator = await dbContext.Employees.SingleOrDefaultAsync(e => e.Id == creatorId, cancellationToken)
            ?? throw new KeyNotFoundException($"Employee {creatorId} was not found.");

        var assignee = await dbContext.Employees.SingleOrDefaultAsync(e => e.Id == assigneeId, cancellationToken)
            ?? throw new KeyNotFoundException($"Employee {assigneeId} was not found.");

        if (!assignee.IsActive)
        {
            throw new BusinessRuleViolationException(
                "BR3",
                $"BR3: Employee {assigneeId} is inactive and cannot be given a new task.");
        }

        var task = TaskItem.Create(title, creator, assignee, plannedStartAt, dueAt);

        dbContext.Tasks.Add(task);

        var saved = false;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            saved = true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg
            && pg.SqlState == PostgresErrorCodes.CheckViolation
            && pg.ConstraintName == "trg_tasks_br3_assignee_active")
        {
            throw new BusinessRuleViolationException(
                "BR3",
                $"BR3: Employee {assigneeId} is inactive and cannot be given a new task.",
                ex);
        }
        finally
        {
            if (!saved)
            {
                // A task left in the Added state would be sent again by the caller's next save on this context.
                dbContext.Entry(task).State = EntityState.Detached;
            }
        }

        return task;
    }

    /// <summary>
    /// Changes task status using current assignee as author.
    /// </summary>
    public Task<TaskItem> ChangeTaskStatusAsync(
        Guid taskId,
        TaskItemStatus newStatus,
        CancellationToken cancellationToken = default) =>
        ChangeTaskStatusInternalAsync(taskId, newStatus, null, cancellationToken);

    /// <summary>
    /// Changes task status and saves change to history.
    /// </summary>
    public Task<TaskItem> ChangeTaskStatusAsync(
        Guid taskId,
        TaskItemStatus newStatus,
        Guid changedById,
        CancellationToken cancellationToken = default) =>
        ChangeTaskStatusInternalAsync(taskId, newStatus, changedById, cancellationToken);

    private async Task<TaskItem> ChangeTaskStatusInternalAsync(
        Guid taskId,
        TaskItemStatus newStatus,
        Guid? changedById,
        CancellationToken cancellationToken)
    {
        var task = await dbContext.Tasks.SingleOrDefaultAsync(t => t.Id == taskId, cancellationToken)
            ?? throw new KeyNotFoundException($"Task {taskId} not found.");

        var actorId = changedById ?? task.AssigneeId;
        if (changedById.HasValue && !await dbContext.Employees.AnyAsync(e => e.Id == actorId, cancellationToken))
        {
            throw new KeyNotFoundException($"Employee {actorId} not found.");
        }

        var oldStatus = task.Status;
        task.ChangeStatus(newStatus, timeProvider.GetUtcNow());

        TaskHistoryEntry? history = null;
        if (oldStatus != task.Status)
        {
            history = new TaskHistoryEntry(
                task.Id,
                actorId,
                timeProvider.GetUtcNow(),
                TaskChangeType.StatusChanged,
                oldStatus.ToString(),
                task.Status.ToString());
            dbContext.TaskHistory.Add(history);
        }

        var saved = false;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            saved = true;
        }
        finally
        {
            if (!saved)
            {
                // A task left in the Modified state would make a retry on this context start from the failed change.
                dbContext.Entry(task).State = EntityState.Detached;
                if (history is not null)
                {
                    dbContext.Entry(history).State = EntityState.Detached;
                }
            }
        }

        return task;
    }

    /// <summary>
    /// Reassigns task to a new employee and saves change to history.
    /// </summary>
    public async Task<TaskItem> ReassignTaskAsync(
        Guid taskId,
        Guid newAssigneeId,
        Guid changedById,
        CancellationToken cancellationToken = default)
    {
        var task = await dbContext.Tasks.SingleOrDefaultAsync(t => t.Id == taskId, cancellationToken)
            ?? throw new KeyNotFoundException($"Task {taskId} not found.");

        if (!await dbContext.Employees.AnyAsync(e => e.Id == changedById, cancellationToken))
        {
            throw new KeyNotFoundException($"Employee {changedById} not found.");
        }

        var newAssignee = await dbContext.Employees.SingleOrDefaultAsync(e => e.Id == newAssigneeId, cancellationToken)
            ?? throw new KeyNotFoundException($"Employee {newAssigneeId} not found.");

        var oldAssigneeId = task.AssigneeId;

        task.Reassign(newAssignee);

        TaskHistoryEntry? history = null;
        if (oldAssigneeId != task.AssigneeId)
        {
            history = new TaskHistoryEntry(
                task.Id,
                changedById,
                timeProvider.GetUtcNow(),
                TaskChangeType.AssigneeChanged,
                oldAssigneeId.ToString(),
                task.AssigneeId.ToString());
            dbContext.TaskHistory.Add(history);
        }

        var saved = false;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            saved = true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg
            && pg.SqlState == PostgresErrorCodes.CheckViolation
            && pg.ConstraintName == "trg_tasks_br3_assignee_active")
        {
            throw new BusinessRuleViolationException(
                "BR3",
                $"BR3: Cannot assign task to inactive employee {newAssigneeId}.",
                ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg
            && pg.SqlState == PostgresErrorCodes.CheckViolation
            && pg.ConstraintName == "trg_tasks_br4_final_status")
        {
            throw new BusinessRuleViolationException(
                "BR4",
                $"BR4: Cannot reassign task {taskId} because it is {task.Status}.",
                ex);
        }
        finally
        {
            if (!saved)
            {
                dbContext.Entry(task).State = EntityState.Detached;
                if (history is not null)
                {
                    dbContext.Entry(history).State = EntityState.Detached;
                }
            }
        }

        return task;
    }

    /// <summary>
    /// Gets task history ordered by date.
    /// </summary>
    public async Task<IReadOnlyList<TaskHistoryEntry>> GetTaskHistoryAsync(
        Guid taskId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.TaskHistory
            .AsNoTracking()
            .Where(h => h.TaskId == taskId)
            .OrderBy(h => h.ChangedAt)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Lists a page of tasks matching the specified query criteria, most-imminent deadline first,
    /// using keyset pagination.
    /// </summary>
    /// <param name="query">The query parameters and pagination criteria to filter tasks by.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A <see cref="PagedResult{TaskItem}"/> containing the matching tasks for the requested page, ordered by
    /// <see cref="TaskItem.DueAt"/> then <see cref="TaskItem.Id"/> (tasks without a deadline come last), along with
    /// the <see cref="TaskCursor"/> for the next page if more tasks exist. The returned tasks are untracked; a
    /// status change goes through <see cref="ChangeTaskStatusAsync(Guid, TaskItemStatus, Guid, CancellationToken)"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="TaskListQuery.Status"/> has a value that is not a defined <see cref="TaskItemStatus"/> member.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public async Task<PagedResult<TaskItem>> ListTasksAsync(
        TaskListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Status.HasValue && !Enum.IsDefined(query.Status.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.Status, "Unknown task status.");
        }

        var effectivePageSize = Math.Clamp(query.PageSize, TaskListQuery.MinPageSize, TaskListQuery.MaxPageSize);

        var dbQuery = dbContext.Tasks.AsNoTracking().Where(t => t.AssigneeId == query.AssigneeId);

        if (query.Status.HasValue)
        {
            dbQuery = dbQuery.Where(t => t.Status == query.Status.Value);
        }

        if (query.CreatorId.HasValue)
        {
            dbQuery = dbQuery.Where(t => t.CreatorId == query.CreatorId.Value);
        }

        if (query.DueFrom.HasValue)
        {
            dbQuery = dbQuery.Where(t => t.DueAt >= query.DueFrom.Value);
        }

        if (query.DueTo.HasValue)
        {
            dbQuery = dbQuery.Where(t => t.DueAt <= query.DueTo.Value);
        }

        if (query.Cursor is not null)
        {
            if (query.Cursor.DueAt.HasValue)
            {
                var cursorDueAt = query.Cursor.DueAt.Value;
                var cursorId = query.Cursor.Id;
                dbQuery = dbQuery.Where(t =>
                    t.DueAt > cursorDueAt ||
                    (t.DueAt == cursorDueAt && t.Id > cursorId) ||
                    t.DueAt == null);
            }
            else
            {
                var cursorId = query.Cursor.Id;
                dbQuery = dbQuery.Where(t => t.DueAt == null && t.Id > cursorId);
            }
        }

        var items = await dbQuery
            .OrderBy(t => t.DueAt)
            .ThenBy(t => t.Id)
            .Take(effectivePageSize + 1)
            .ToListAsync(cancellationToken);

        TaskCursor? nextCursor = null;
        if (items.Count > effectivePageSize)
        {
            items.RemoveAt(items.Count - 1);
            var lastItem = items[^1];
            nextCursor = new TaskCursor(lastItem.DueAt, lastItem.Id);
        }

        return new PagedResult<TaskItem>(items, nextCursor);
    }

    /// <summary>
    /// Lists the tasks assigned to an employee, most-imminent deadline first.
    /// </summary>
    /// <param name="assigneeId">The identifier of the assignee.</param>
    /// <param name="status">When set, restricts the result to tasks in this status.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// The assignee's tasks, ordered by <see cref="TaskItem.DueAt"/> then <see cref="TaskItem.Id"/>; tasks without a
    /// deadline come last. An unknown employee, or one without tasks, gets an empty list. The returned tasks are
    /// untracked; a status change goes through <see cref="ChangeTaskStatusAsync(Guid, TaskItemStatus, Guid, CancellationToken)"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> has a value that is not a defined <see cref="TaskItemStatus"/> member.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    /// <remarks>
    /// Convenience overload delegating to <see cref="ListTasksAsync(TaskListQuery, CancellationToken)"/>.
    /// </remarks>
    public async Task<IReadOnlyList<TaskItem>> ListTasksByAssigneeAsync(
        Guid assigneeId,
        TaskItemStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        return status.HasValue && !Enum.IsDefined(status.Value)
            ? throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown task status.")
            : await ListTasksAsync(new TaskListQuery(assigneeId, status), cancellationToken);
    }
}
