using System.Data.Common;

using TaskManagement.Application;
using TaskManagement.Domain;
using TaskManagement.Wpf.Services;

namespace TaskManagement.Wpf.UnitTests;

/// <summary>
/// A database exception that can be thrown by <see cref="FakeTaskClient"/>, since <see cref="DbException"/> is abstract.
/// </summary>
public sealed class FakeDbException : DbException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FakeDbException"/> class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public FakeDbException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// A synchronous, scriptable <see cref="ITaskClient"/> that records its calls.
/// </summary>
public sealed class FakeTaskClient : ITaskClient
{
    /// <summary>Gets or sets the employees returned by <see cref="GetEmployeesAsync"/>.</summary>
    public List<EmployeeOption> Employees { get; set; } = [];

    /// <summary>Gets or sets the page returned for a given cursor; empty by default.</summary>
    public Func<TaskCursor?, TaskPage> Pages { get; set; } = _ => new TaskPage([], null);

    /// <summary>Gets or sets the exception thrown by <see cref="ChangeStatusAsync"/>.</summary>
    public Exception? ChangeStatusError { get; set; }

    /// <summary>Gets or sets the exception thrown by <see cref="ReassignAsync"/>.</summary>
    public Exception? ReassignError { get; set; }

    /// <summary>Gets or sets the exception thrown by <see cref="CreateTaskAsync"/>.</summary>
    public Exception? CreateError { get; set; }

    /// <summary>Gets or sets the exception thrown by <see cref="GetEmployeesAsync"/>.</summary>
    public Exception? EmployeesError { get; set; }

    /// <summary>Gets the number of <see cref="ListTasksAsync"/> calls.</summary>
    public int ListCalls { get; private set; }

    /// <summary>Gets the cursor of every <see cref="ListTasksAsync"/> call, in order.</summary>
    public List<TaskCursor?> ListCursors { get; } = [];

    /// <summary>Gets the arguments of the last <see cref="ChangeStatusAsync"/> call.</summary>
    public (Guid TaskId, TaskItemStatus Status, Guid ChangedById)? LastStatusChange { get; private set; }

    /// <summary>Gets the arguments of the last <see cref="ReassignAsync"/> call.</summary>
    public (Guid TaskId, Guid NewAssigneeId, Guid ChangedById)? LastReassign { get; private set; }

    /// <summary>Gets the number of <see cref="CreateTaskAsync"/> calls.</summary>
    public int CreateCalls { get; private set; }

    /// <summary>Gets the arguments of the last <see cref="CreateTaskAsync"/> call.</summary>
    public (string Title, Guid CreatorId, Guid AssigneeId, DateTimeOffset? PlannedStartAt, DateTimeOffset? DueAt)? LastCreate { get; private set; }

    /// <inheritdoc />
    public Task<IReadOnlyList<EmployeeOption>> GetEmployeesAsync(CancellationToken cancellationToken) =>
        EmployeesError is null
            ? Task.FromResult<IReadOnlyList<EmployeeOption>>(Employees.ToList())
            : Task.FromException<IReadOnlyList<EmployeeOption>>(EmployeesError);

    /// <inheritdoc />
    public Task<TaskPage> ListTasksAsync(Guid assigneeId, TaskItemStatus? status, TaskCursor? cursor, CancellationToken cancellationToken)
    {
        ListCalls++;
        ListCursors.Add(cursor);
        return Task.FromResult(Pages(cursor));
    }

    /// <inheritdoc />
    public Task CreateTaskAsync(string title, Guid creatorId, Guid assigneeId, DateTimeOffset? plannedStartAt, DateTimeOffset? dueAt, CancellationToken cancellationToken)
    {
        CreateCalls++;
        LastCreate = (title, creatorId, assigneeId, plannedStartAt, dueAt);
        return CreateError is null ? Task.CompletedTask : Task.FromException(CreateError);
    }

    /// <inheritdoc />
    public Task ChangeStatusAsync(Guid taskId, TaskItemStatus newStatus, Guid changedById, CancellationToken cancellationToken)
    {
        LastStatusChange = (taskId, newStatus, changedById);
        return ChangeStatusError is null ? Task.CompletedTask : Task.FromException(ChangeStatusError);
    }

    /// <inheritdoc />
    public Task ReassignAsync(Guid taskId, Guid newAssigneeId, Guid changedById, CancellationToken cancellationToken)
    {
        LastReassign = (taskId, newAssigneeId, changedById);
        return ReassignError is null ? Task.CompletedTask : Task.FromException(ReassignError);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<HistoryRow>> GetHistoryAsync(Guid taskId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<HistoryRow>>([]);
}

/// <summary>
/// An <see cref="IDialogService"/> that returns a preset result and counts calls.
/// </summary>
public sealed class FakeDialogService : IDialogService
{
    /// <summary>Gets or sets the value <see cref="ShowCreateTask"/> returns.</summary>
    public bool Result { get; set; }

    /// <summary>Gets the number of <see cref="ShowCreateTask"/> calls.</summary>
    public int Calls { get; private set; }

    /// <inheritdoc />
    public bool ShowCreateTask(IReadOnlyList<EmployeeOption> employees, EmployeeOption? defaultCreator)
    {
        Calls++;
        return Result;
    }
}
