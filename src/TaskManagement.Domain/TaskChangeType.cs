namespace TaskManagement.Domain;

/// <summary>
/// Type of change in task history.
/// </summary>
public enum TaskChangeType
{
    /// <summary>Status was changed.</summary>
    StatusChanged = 0,

    /// <summary>Assignee was changed.</summary>
    AssigneeChanged = 1,
}
