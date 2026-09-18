namespace TaskManagement.Domain;

/// <summary>
/// The lifecycle status of a <see cref="TaskItem"/>.
/// </summary>
/// <remarks>
/// Stored as text (see the entity configuration). <see cref="Completed"/> and <see cref="Cancelled"/> are final:
/// enforced as BR4 by <see cref="TaskItem.ChangeStatus"/>.
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
