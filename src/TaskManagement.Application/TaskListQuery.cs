using TaskManagement.Domain;

namespace TaskManagement.Application;

/// <summary>
/// Query criteria for listing tasks assigned to an employee with optional filters and keyset pagination.
/// </summary>
/// <param name="AssigneeId">The identifier of the assignee.</param>
/// <param name="Status">When set, restricts the result to tasks in this status.</param>
/// <param name="DueFrom">When set, restricts the result to tasks with a deadline at or after this timestamp (inclusive).</param>
/// <param name="DueTo">When set, restricts the result to tasks with a deadline at or before this timestamp (inclusive).</param>
/// <param name="CreatorId">When set, restricts the result to tasks created by this employee.</param>
/// <param name="PageSize">The maximum number of items to return on a single page (clamped between 1 and 100; defaults to 50).</param>
/// <param name="Cursor">When set, continues pagination starting immediately after this cursor.</param>
public sealed record TaskListQuery(
    Guid AssigneeId,
    TaskItemStatus? Status = null,
    DateTimeOffset? DueFrom = null,
    DateTimeOffset? DueTo = null,
    Guid? CreatorId = null,
    int PageSize = TaskListQuery.DefaultPageSize,
    TaskCursor? Cursor = null)
{
    /// <summary>The default page size when none is specified.</summary>
    public const int DefaultPageSize = 50;

    /// <summary>The maximum allowed page size.</summary>
    public const int MaxPageSize = 100;

    /// <summary>The minimum allowed page size.</summary>
    public const int MinPageSize = 1;
}
