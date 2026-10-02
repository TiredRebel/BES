using TaskManagement.Application;
using TaskManagement.Domain;

namespace TaskManagement.Wpf.Services;

/// <summary>
/// The view models' only path to the data layer. Returns immutable view records, never EF entities.
/// </summary>
/// <remarks>
/// Three implementations (ADR 0010): <see cref="TaskClient"/> over <c>TaskService</c>, a fake in the unit tests, and
/// a design-time one for the XAML designer. Business rules BR1–BR5 are enforced behind this interface; a violation
/// surfaces as <see cref="BusinessRuleViolationException"/>.
/// </remarks>
public interface ITaskClient
{
    /// <summary>
    /// Gets every employee, active and inactive, ordered by name.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The employees.</returns>
    Task<IReadOnlyList<EmployeeOption>> GetEmployeesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets one page of an employee's tasks, most-imminent deadline first.
    /// </summary>
    /// <param name="assigneeId">The assignee whose tasks are listed.</param>
    /// <param name="status">When set, only tasks in this status.</param>
    /// <param name="cursor">When set, the page after this cursor; null for the first page.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The page and the cursor for the next one.</returns>
    Task<TaskPage> ListTasksAsync(
        Guid assigneeId,
        TaskItemStatus? status,
        TaskCursor? cursor,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates a task.
    /// </summary>
    /// <param name="title">The task title.</param>
    /// <param name="creatorId">The creating employee.</param>
    /// <param name="assigneeId">The assigned employee.</param>
    /// <param name="plannedStartAt">The planned start, or null.</param>
    /// <param name="dueAt">The deadline, or null.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the task is saved.</returns>
    /// <exception cref="BusinessRuleViolationException">BR2, BR3 or BR5 is violated.</exception>
    /// <exception cref="ArgumentException">The title is blank or longer than 200 characters.</exception>
    Task CreateTaskAsync(
        string title,
        Guid creatorId,
        Guid assigneeId,
        DateTimeOffset? plannedStartAt,
        DateTimeOffset? dueAt,
        CancellationToken cancellationToken);

    /// <summary>
    /// Moves a task to another status, recording <paramref name="changedById"/> as the author.
    /// </summary>
    /// <param name="taskId">The task to change.</param>
    /// <param name="newStatus">The status to move to.</param>
    /// <param name="changedById">The acting employee.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the change is saved.</returns>
    /// <exception cref="BusinessRuleViolationException">BR4: the task is final.</exception>
    Task ChangeStatusAsync(Guid taskId, TaskItemStatus newStatus, Guid changedById, CancellationToken cancellationToken);

    /// <summary>
    /// Reassigns a task, recording <paramref name="changedById"/> as the author.
    /// </summary>
    /// <param name="taskId">The task to reassign.</param>
    /// <param name="newAssigneeId">The employee the task moves to.</param>
    /// <param name="changedById">The acting employee.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes when the change is saved.</returns>
    /// <exception cref="BusinessRuleViolationException">BR3, BR4 or BR5 is violated.</exception>
    Task ReassignAsync(Guid taskId, Guid newAssigneeId, Guid changedById, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a task's change history, oldest first.
    /// </summary>
    /// <param name="taskId">The task whose history is read.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The history entries.</returns>
    Task<IReadOnlyList<HistoryRow>> GetHistoryAsync(Guid taskId, CancellationToken cancellationToken);
}
