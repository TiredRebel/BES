namespace TaskManagement.Domain;

/// <summary>
/// The lifecycle status of a <see cref="TaskItem"/>.
/// </summary>
/// <remarks>
/// Stored as text (see the entity configuration), so the member names are persisted: renaming a member breaks
/// existing rows, the <c>ck_tasks_status_valid</c> CHECK and the BR3/BR4 trigger. <see cref="Completed"/> and
/// <see cref="Cancelled"/> are final: enforced as BR4 by <see cref="TaskItem.ChangeStatus"/> and by the trigger.
/// </remarks>
public enum TaskItemStatus
{
    /// <summary>The task has been created but work has not started.</summary>
    New = 0,

    /// <summary>Work on the task is underway.</summary>
    InProgress = 1,

    /// <summary>The task is finished. Final: see BR4.</summary>
    Completed = 2,

    /// <summary>The task was cancelled. Final: see BR4.</summary>
    Cancelled = 3,
}
