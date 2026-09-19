namespace TaskManagement.Application;

/// <summary>
/// Represents a keyset pagination cursor based on the task deadline and identifier.
/// </summary>
/// <param name="DueAt">The deadline timestamp of the cursor task, or <see langword="null"/> if it has no deadline.</param>
/// <param name="Id">The unique identifier of the cursor task.</param>
public sealed record TaskCursor(DateTimeOffset? DueAt, Guid Id);
