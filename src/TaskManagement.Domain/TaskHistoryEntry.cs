#pragma warning disable CS1591

namespace TaskManagement.Domain;

public sealed class TaskHistoryEntry
{
    public const int ValueMaxLength = 200;

    private TaskHistoryEntry()
    {
        OldValue = string.Empty;
        NewValue = string.Empty;
    }

    public TaskHistoryEntry(
        Guid taskId,
        Guid changedById,
        DateTimeOffset changedAt,
        TaskChangeType changeType,
        string oldValue,
        string newValue)
    {
        ValidateId(taskId, nameof(taskId), "Task ID cannot be empty.");
        ValidateId(changedById, nameof(changedById), "Employee ID cannot be empty.");

        if (!Enum.IsDefined(changeType))
        {
            throw new ArgumentOutOfRangeException(nameof(changeType), changeType, "Invalid change type.");
        }

        ValidateValue(oldValue, nameof(oldValue), "Old value");
        ValidateValue(newValue, nameof(newValue), "New value");

        Id = Guid.CreateVersion7();
        TaskId = taskId;
        ChangedById = changedById;
        ChangedAt = changedAt.ToUniversalTime();
        ChangeType = changeType;
        OldValue = oldValue;
        NewValue = newValue;
    }

    public Guid Id { get; private set; }
    public Guid TaskId { get; private set; }
    public Guid ChangedById { get; private set; }
    public DateTimeOffset ChangedAt { get; private set; }
    public TaskChangeType ChangeType { get; private set; }
    public string OldValue { get; private set; }
    public string NewValue { get; private set; }

    private static void ValidateId(Guid id, string paramName, string message)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(message, paramName);
        }
    }

    private static void ValidateValue(string value, string paramName, string fieldName)
    {
        if (value is null)
        {
            throw new ArgumentNullException(paramName, $"{fieldName} cannot be null.");
        }

        if (value.Length > ValueMaxLength)
        {
            throw new ArgumentException($"{fieldName} cannot be longer than {ValueMaxLength} characters.", paramName);
        }
    }
}
