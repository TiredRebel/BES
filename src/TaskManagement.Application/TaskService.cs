using System.Threading;
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
/// there is no mocking need.
/// </remarks>
public sealed class TaskService
{
    private readonly TaskManagementDbContext dbContext;
    private readonly TimeProvider timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="TaskService"/> class.
    /// </summary>
    /// <param name="dbContext">The database context the three use cases read and write through.</param>
    /// <param name="timeProvider">The clock used to time-stamp status changes.</param>
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
    /// <exception cref="KeyNotFoundException"><paramref name="creatorId"/> or <paramref name="assigneeId"/> does not match an employee.</exception>
    /// <exception cref="BusinessRuleViolationException">
    /// BR5: the creator and the assignee are the same employee. BR3: the assignee is inactive, whether caught by the
    /// application's own check, the domain's re-check, or the database trigger on a race. BR2: both dates are set
    /// and <paramref name="dueAt"/> is earlier than <paramref name="plannedStartAt"/>.
    /// </exception>
    /// <exception cref="DbUpdateException">The database rejected the insert for a reason other than the BR3 trigger.</exception>
    /// <remarks>Enforces BR2, BR3 and BR5.</remarks>
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

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
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
    /// <exception cref="DbUpdateConcurrencyException">Another writer changed the task since it was read.</exception>
    /// <remarks>Enforces BR1 and BR4 (both in <see cref="TaskItem.ChangeStatus"/>).</remarks>
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

        await dbContext.SaveChangesAsync(cancellationToken);

        return task;
    }

    /// <summary>
    /// Lists the tasks assigned to an employee, most-imminent deadline first.
    /// </summary>
    /// <param name="assigneeId">The identifier of the assignee.</param>
    /// <param name="status">When set, restricts the result to tasks in this status.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// The assignee's tasks, ordered by <see cref="TaskItem.DueAt"/> then <see cref="TaskItem.Id"/>. The returned
    /// tasks are untracked; a status change goes through <see cref="ChangeTaskStatusAsync"/>.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="status"/> has a value that is not a defined <see cref="TaskItemStatus"/> member.</exception>
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
