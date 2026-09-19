using Microsoft.EntityFrameworkCore;
using Npgsql;
using TaskManagement.Domain;
using TaskManagement.Infrastructure;

namespace TaskManagement.Application;

/// <summary>
/// Implements the module's three use cases: assigning a task, moving it through its statuses, and listing what an
/// employee has.
/// </summary>
/// <remarks>
/// No interface: there is one implementation and the integration tests exercise it against a real database, so
/// there is no mocking need. When a save fails, <see cref="CreateTaskAsync"/> and <see cref="ChangeTaskStatusAsync"/>
/// detach the task they added or changed, so a caller that keeps using the same context never has that failed change
/// retried by a later save.
/// </remarks>
public sealed class TaskService
{
    private readonly TaskManagementDbContext dbContext;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskService"/> class.
    /// </summary>
    /// <param name="dbContext">The database context the three use cases read and write through.</param>
    /// <param name="timeProvider">The clock that supplies <see cref="TaskItem.CompletedAt"/> when a task is completed.</param>
    public TaskService(TaskManagementDbContext dbContext, TimeProvider timeProvider)
    {
        this.dbContext = dbContext;
        this.timeProvider = timeProvider;
    }

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
        var creator = await dbContext.Employees.SingleOrDefaultAsync(e => e.Id == creatorId, cancellationToken);
        if (creator is null)
        {
            throw new KeyNotFoundException($"Employee {creatorId} was not found.");
        }

        var assignee = await dbContext.Employees.SingleOrDefaultAsync(e => e.Id == assigneeId, cancellationToken);
        if (assignee is null)
        {
            throw new KeyNotFoundException($"Employee {assigneeId} was not found.");
        }

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
    /// Moves a task to <paramref name="newStatus"/>.
    /// </summary>
    /// <param name="taskId">The identifier of the task to change.</param>
    /// <param name="newStatus">The status to move to.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The updated <see cref="TaskItem"/>.</returns>
    /// <exception cref="KeyNotFoundException"><paramref name="taskId"/> does not match a task.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="newStatus"/> is not a defined <see cref="TaskItemStatus"/> member.</exception>
    /// <exception cref="BusinessRuleViolationException">BR4: the task's current status is <see cref="TaskItemStatus.Completed"/> or <see cref="TaskItemStatus.Cancelled"/>, which are final.</exception>
    /// <exception cref="DbUpdateConcurrencyException">
    /// Another writer changed the task since it was read. The task is already detached when this reaches the caller,
    /// so EF's usual recovery from the exception's entries (such as "client wins") saves nothing; resolve the
    /// conflict by calling this method again, which reloads the current row.
    /// </exception>
    /// <exception cref="DbUpdateException">The database rejected the update for another reason.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    /// <remarks>
    /// Enforces BR1 and BR4 (both in <see cref="TaskItem.ChangeStatus"/>). If the save fails, the task is detached
    /// from the context, so a retry on the same context reloads the current row instead of reusing the failed change.
    /// </remarks>
    public async Task<TaskItem> ChangeTaskStatusAsync(
        Guid taskId,
        TaskItemStatus newStatus,
        CancellationToken cancellationToken = default)
    {
        var task = await dbContext.Tasks.SingleOrDefaultAsync(t => t.Id == taskId, cancellationToken);
        if (task is null)
        {
            throw new KeyNotFoundException($"Task {taskId} was not found.");
        }

        task.ChangeStatus(newStatus, timeProvider.GetUtcNow());

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
            }
        }

        return task;
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
    /// untracked; a status change goes through <see cref="ChangeTaskStatusAsync"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> has a value that is not a defined <see cref="TaskItemStatus"/> member.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public async Task<IReadOnlyList<TaskItem>> ListTasksByAssigneeAsync(
        Guid assigneeId,
        TaskItemStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        if (status.HasValue && !Enum.IsDefined(status.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown task status.");
        }

        var query = dbContext.Tasks.AsNoTracking().Where(t => t.AssigneeId == assigneeId);

        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        return await query.OrderBy(t => t.DueAt).ThenBy(t => t.Id).ToListAsync(cancellationToken);
    }
}
