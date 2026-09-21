namespace TaskManagement.Domain;

/// <summary>
/// History record for task changes.
/// </summary>
public sealed class TaskHistoryEntry
{
    /// <summary>Max length for values.</summary>
    public const int ValueMaxLength = 200;

    private TaskHistoryEntry()
    {
        OldValue = string.Empty;
        NewValue = string.Empty;
    }

    /// <summary>Record ID.</summary>
    public Guid Id { get; private set; }

    /// <summary>Task ID.</summary>
    public Guid TaskId { get; private set; }

    /// <summary>ID of employee who made the change.</summary>
    public Guid ChangedById { get; private set; }

    /// <summary>Date and time of change (UTC).</summary>
    public DateTimeOffset ChangedAt { get; private set; }

    /// <summary>Type of change.</summary>
    public TaskChangeType ChangeType { get; private set; }

    /// <summary>Old value.</summary>
    public string OldValue { get; private set; }

    /// <summary>New value.</summary>
    public string NewValue { get; private set; }

    /// <summary>
    /// Creates a new history record.
    /// </summary>
    public static TaskHistoryEntry Create(
        Guid taskId,
        Guid changedById,
        DateTimeOffset changedAt,
        TaskChangeType changeType,
        string oldValue,
        string newValue)
    {
        if (taskId == Guid.Empty)
        {
            throw new ArgumentException("Task ID cannot be empty.", nameof(taskId));
        }

        if (changedById == Guid.Empty)
        {
            throw new ArgumentException("Employee ID cannot be empty.", nameof(changedById));
        }

        if (!Enum.IsDefined(changeType))
        {
            throw new ArgumentOutOfRangeException(nameof(changeType), changeType, "Invalid change type.");
        }

        if (oldValue is null)
        {
            throw new ArgumentNullException(nameof(oldValue), "Old value cannot be null.");
        }

        if (newValue is null)
        {
            throw new ArgumentNullException(nameof(newValue), "New value cannot be null.");
        }

        if (oldValue.Length > ValueMaxLength)
        {
            throw new ArgumentException($"Old value cannot be longer than {ValueMaxLength} characters.", nameof(oldValue));
        }

        if (newValue.Length > ValueMaxLength)
        {
            throw new ArgumentException($"New value cannot be longer than {ValueMaxLength} characters.", nameof(newValue));
        }

        return new TaskHistoryEntry
        {
            Id = Guid.CreateVersion7(),
            TaskId = taskId,
            ChangedById = changedById,
            ChangedAt = changedAt.ToUniversalTime(),
            ChangeType = changeType,
            OldValue = oldValue,
            NewValue = newValue,
        };
    }
}
