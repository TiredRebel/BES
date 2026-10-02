using TaskManagement.Application;
using TaskManagement.Domain;

namespace TaskManagement.Wpf.Services;

/// <summary>
/// An employee as shown in a picker. Inactive employees stay pickable (ADR 0010): the service, not the UI, enforces BR3.
/// </summary>
/// <param name="Id">The employee identifier.</param>
/// <param name="FullName">The employee's full name.</param>
/// <param name="IsActive">Whether the employee can be given a new task.</param>
public sealed record EmployeeOption(Guid Id, string FullName, bool IsActive)
{
    /// <summary>Gets the picker label: the full name, marked "(inactive)" when the employee is inactive.</summary>
    public string DisplayName => IsActive ? FullName : $"{FullName} (inactive)";
}

/// <summary>
/// One task in the list. Timestamps are converted to local time for display.
/// </summary>
/// <param name="Id">The task identifier.</param>
/// <param name="Title">The task title.</param>
/// <param name="Status">The current status.</param>
/// <param name="AssigneeId">The identifier of the assigned employee.</param>
/// <param name="AssigneeName">The assigned employee's full name.</param>
/// <param name="PlannedStartAt">The planned start, in local time, or null.</param>
/// <param name="DueAt">The deadline, in local time, or null.</param>
/// <param name="CompletedAt">The completion time, in local time, or null.</param>
public sealed record TaskRow(
    Guid Id,
    string Title,
    TaskItemStatus Status,
    Guid AssigneeId,
    string AssigneeName,
    DateTimeOffset? PlannedStartAt,
    DateTimeOffset? DueAt,
    DateTimeOffset? CompletedAt);

/// <summary>
/// One page of the task list.
/// </summary>
/// <param name="Items">The tasks on this page.</param>
/// <param name="NextCursor">The cursor for the next page, or null on the last page.</param>
public sealed record TaskPage(IReadOnlyList<TaskRow> Items, TaskCursor? NextCursor);

/// <summary>
/// One entry of a task's change history, with employee ids resolved to names.
/// </summary>
/// <param name="ChangeType">What changed.</param>
/// <param name="OldValue">The previous status name, or the previous assignee's name.</param>
/// <param name="NewValue">The new status name, or the new assignee's name.</param>
/// <param name="ChangedAt">When the change happened, in local time.</param>
/// <param name="AuthorName">The name of the employee recorded as the author.</param>
public sealed record HistoryRow(
    TaskChangeType ChangeType,
    string OldValue,
    string NewValue,
    DateTimeOffset ChangedAt,
    string AuthorName);
