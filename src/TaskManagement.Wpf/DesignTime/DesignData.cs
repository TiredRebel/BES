using TaskManagement.Application;
using TaskManagement.Domain;
using TaskManagement.Wpf.Services;
using TaskManagement.Wpf.ViewModels;

namespace TaskManagement.Wpf.DesignTime;

/// <summary>
/// Canned view models for the XAML designer, so the views show data without a database (ADR 0010).
/// </summary>
public static class DesignData
{
    private static readonly EmployeeOption[] _employees =
    [
        new(Guid.Parse("00000000-0000-0000-0000-000000000001"), "Anna Petrova", true),
        new(Guid.Parse("00000000-0000-0000-0000-000000000002"), "Boris Ivanov", true),
        new(Guid.Parse("00000000-0000-0000-0000-000000000003"), "Clara Smith", false),
    ];

    private static readonly TaskRow[] _tasks =
    [
        new(Guid.Parse("00000000-0000-0000-0000-0000000000a1"), "Prepare the quarterly report", TaskItemStatus.InProgress, _employees[0].Id, _employees[0].FullName, new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 15, 18, 0, 0, TimeSpan.Zero), null),
        new(Guid.Parse("00000000-0000-0000-0000-0000000000a2"), "Update the onboarding guide", TaskItemStatus.New, _employees[0].Id, _employees[0].FullName, null, new DateTimeOffset(2026, 10, 20, 18, 0, 0, TimeSpan.Zero), null),
        new(Guid.Parse("00000000-0000-0000-0000-0000000000a3"), "Archive the old tickets", TaskItemStatus.Completed, _employees[0].Id, _employees[0].FullName, null, null, new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero)),
    ];

    private static readonly HistoryRow[] _history =
    [
        new(TaskChangeType.StatusChanged, "New", "InProgress", new DateTimeOffset(2026, 10, 1, 9, 30, 0, TimeSpan.Zero), _employees[0].FullName),
        new(TaskChangeType.AssigneeChanged, _employees[1].FullName, _employees[0].FullName, new DateTimeOffset(2026, 10, 2, 11, 0, 0, TimeSpan.Zero), _employees[1].FullName),
    ];

    /// <summary>Gets a main view model filled with three tasks and two history entries.</summary>
    public static MainViewModel Main { get; } = CreateMain();

    /// <summary>Gets a create-task view model with the pickers filled.</summary>
    public static CreateTaskViewModel CreateTask { get; } = CreateCreateTask();

    private static MainViewModel CreateMain()
    {
        var viewModel = new MainViewModel(new DesignTaskClient(), new DesignDialogService());
        foreach (var employee in _employees)
        {
            viewModel.Employees.Add(employee);
        }

        foreach (var task in _tasks)
        {
            viewModel.Tasks.Add(task);
        }

        foreach (var row in _history)
        {
            viewModel.History.Add(row);
        }

        viewModel.ActingEmployee = _employees[0];
        viewModel.FilterAssignee = _employees[0];
        viewModel.SelectedTask = _tasks[0];
        return viewModel;
    }

    private static CreateTaskViewModel CreateCreateTask()
    {
        var viewModel = new CreateTaskViewModel(new DesignTaskClient());
        viewModel.Initialize(_employees, _employees[0]);
        viewModel.Title = "Prepare the quarterly report";
        return viewModel;
    }

    private sealed class DesignTaskClient : ITaskClient
    {
        public Task<IReadOnlyList<EmployeeOption>> GetEmployeesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<EmployeeOption>>(_employees);

        public Task<TaskPage> ListTasksAsync(Guid assigneeId, TaskItemStatus? status, TaskCursor? cursor, CancellationToken cancellationToken) =>
            Task.FromResult(new TaskPage(_tasks, null));

        public Task CreateTaskAsync(string title, Guid creatorId, Guid assigneeId, DateTimeOffset? plannedStartAt, DateTimeOffset? dueAt, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ChangeStatusAsync(Guid taskId, TaskItemStatus newStatus, Guid changedById, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task ReassignAsync(Guid taskId, Guid newAssigneeId, Guid changedById, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<HistoryRow>> GetHistoryAsync(Guid taskId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<HistoryRow>>(_history);
    }

    private sealed class DesignDialogService : IDialogService
    {
        public bool ShowCreateTask(IReadOnlyList<EmployeeOption> employees, EmployeeOption? defaultCreator) => false;
    }
}
