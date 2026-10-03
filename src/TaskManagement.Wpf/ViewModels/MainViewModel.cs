using System.Collections.ObjectModel;
using System.Data.Common;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.EntityFrameworkCore;

using TaskManagement.Application;
using TaskManagement.Domain;
using TaskManagement.Wpf.Services;

namespace TaskManagement.Wpf.ViewModels;

/// <summary>
/// One entry of the status filter: a label and the status it selects, or null for "All".
/// </summary>
/// <param name="Label">The text shown in the picker.</param>
/// <param name="Value">The status to filter by, or null for every status.</param>
public sealed record StatusFilter(string Label, TaskItemStatus? Value);

/// <summary>
/// State and commands of the main window: task list with keyset paging, change status, reassign, history (ADR 0010).
/// </summary>
/// <remarks>
/// Contains no business rules (ADR 0010 section 8): BR2-BR5 are enforced by the service and surface in
/// <see cref="ErrorMessage"/>. Commands are enabled by input completeness and the busy flag only.
/// </remarks>
public sealed partial class MainViewModel : ObservableObject
{
    private const string ReloadFailedSuffix = " (The list could not be reloaded.)";

    private readonly ITaskClient _client;
    private readonly IDialogService _dialogs;
    private readonly Stack<TaskCursor?> _previousCursors = new();
    private TaskCursor? _currentCursor;
    private TaskCursor? _nextCursor;
    private int _historyVersion;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainViewModel"/> class.
    /// </summary>
    /// <param name="client">The data-layer client.</param>
    /// <param name="dialogs">Opens the create-task dialog.</param>
    public MainViewModel(ITaskClient client, IDialogService dialogs)
    {
        _client = client;
        _dialogs = dialogs;
        SelectedStatusFilter = StatusFilters[0];
    }

    /// <summary>Gets every employee, for the acting-as, assignee filter and reassign pickers.</summary>
    public ObservableCollection<EmployeeOption> Employees { get; } = [];

    /// <summary>Gets the current page of tasks.</summary>
    public ObservableCollection<TaskRow> Tasks { get; } = [];

    /// <summary>Gets the change history of <see cref="SelectedTask"/>.</summary>
    public ObservableCollection<HistoryRow> History { get; } = [];

    /// <summary>Gets the status filter entries, "All" first.</summary>
    public IReadOnlyList<StatusFilter> StatusFilters { get; } =
    [
        new("All", null),
        new("New", TaskItemStatus.New),
        new("InProgress", TaskItemStatus.InProgress),
        new("Completed", TaskItemStatus.Completed),
        new("Cancelled", TaskItemStatus.Cancelled),
    ];

    /// <summary>Gets every status a task can be moved to. The service, not this list, rejects final-state changes.</summary>
    public IReadOnlyList<TaskItemStatus> StatusOptions { get; } = Enum.GetValues<TaskItemStatus>();

    /// <summary>Gets or sets the employee recorded as the author of changes.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateTaskCommand))]
    [NotifyCanExecuteChangedFor(nameof(ChangeStatusCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReassignCommand))]
    public partial EmployeeOption? ActingEmployee { get; set; }

    /// <summary>Gets or sets the assignee whose tasks are listed.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    public partial EmployeeOption? FilterAssignee { get; set; }

    /// <summary>Gets or sets the status filter.</summary>
    [ObservableProperty]
    public partial StatusFilter SelectedStatusFilter { get; set; }

    /// <summary>Gets or sets the task selected in the list.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ChangeStatusCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReassignCommand))]
    public partial TaskRow? SelectedTask { get; set; }

    /// <summary>Gets or sets the status chosen for the selected task.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ChangeStatusCommand))]
    public partial TaskItemStatus? NewStatus { get; set; }

    /// <summary>Gets or sets the employee chosen as the selected task's new assignee.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReassignCommand))]
    public partial EmployeeOption? NewAssignee { get; set; }

    /// <summary>Gets or sets a value indicating whether a database command is running.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(CreateTaskCommand))]
    [NotifyCanExecuteChangedFor(nameof(ChangeStatusCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReassignCommand))]
    public partial bool IsBusy { get; set; }

    /// <summary>Gets or sets the message shown in the message bar, or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    /// <summary>Gets or sets the one-based number of the page shown.</summary>
    [ObservableProperty]
    public partial int PageNumber { get; set; } = 1;

    /// <summary>Gets a value indicating whether no database command is running.</summary>
    public bool IsIdle => !IsBusy;

    /// <summary>Gets a value indicating whether the message bar is shown.</summary>
    public bool HasError => ErrorMessage is not null;

    partial void OnFilterAssigneeChanged(EmployeeOption? value)
    {
        if (!IsBusy)
        {
            _ = ReloadFirstPageAsync();
        }
    }

    partial void OnSelectedStatusFilterChanged(StatusFilter value)
    {
        if (!IsBusy)
        {
            _ = ReloadFirstPageAsync();
        }
    }

    partial void OnSelectedTaskChanged(TaskRow? value) => _ = LoadHistoryAsync(value);

    private bool CanRefresh() => !IsBusy && FilterAssignee is not null;

    private bool CanNextPage() => !IsBusy && _nextCursor is not null;

    private bool CanPreviousPage() => !IsBusy && _previousCursors.Count > 0;

    private bool CanCreateTask() => !IsBusy && ActingEmployee is not null;

    private bool CanChangeStatus() => !IsBusy && SelectedTask is not null && ActingEmployee is not null && NewStatus is not null;

    private bool CanReassign() => !IsBusy && SelectedTask is not null && ActingEmployee is not null && NewAssignee is not null;

    /// <summary>Loads the employees, picks the acting employee and shows the first page of their tasks.</summary>
    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task LoadAsync(CancellationToken cancellationToken) => RunAsync(
        async ct =>
        {
            var employees = await _client.GetEmployeesAsync(ct);
            Employees.Clear();
            foreach (var employee in employees)
            {
                Employees.Add(employee);
            }

            ActingEmployee = Employees.FirstOrDefault(e => e.IsActive) ?? Employees.FirstOrDefault();
            FilterAssignee = ActingEmployee;
            await LoadFirstPageAsync(ct);
        },
        cancellationToken);

    /// <summary>Reloads the current page.</summary>
    [RelayCommand(CanExecute = nameof(CanRefresh), AllowConcurrentExecutions = false)]
    private Task RefreshAsync(CancellationToken cancellationToken) => RunAsync(LoadCurrentPageAsync, cancellationToken);

    /// <summary>Shows the next page.</summary>
    [RelayCommand(CanExecute = nameof(CanNextPage), AllowConcurrentExecutions = false)]
    private Task NextPageAsync(CancellationToken cancellationToken) => RunAsync(
        async ct =>
        {
            var from = _currentCursor;
            await LoadPageAsync(_nextCursor, () => _previousCursors.Push(from), ct);
        },
        cancellationToken);

    /// <summary>Shows the previous page.</summary>
    [RelayCommand(CanExecute = nameof(CanPreviousPage), AllowConcurrentExecutions = false)]
    private Task PreviousPageAsync(CancellationToken cancellationToken) => RunAsync(
        ct => LoadPageAsync(_previousCursors.Peek(), () => _previousCursors.Pop(), ct),
        cancellationToken);

    /// <summary>Opens the create-task dialog and reloads the first page when a task was created.</summary>
    [RelayCommand(CanExecute = nameof(CanCreateTask), AllowConcurrentExecutions = false)]
    private Task CreateTaskAsync(CancellationToken cancellationToken) => RunAsync(
        async ct =>
        {
            if (_dialogs.ShowCreateTask(Employees.ToList(), ActingEmployee))
            {
                await LoadFirstPageAsync(ct);
            }
        },
        cancellationToken);

    /// <summary>Moves the selected task to <see cref="NewStatus"/>.</summary>
    [RelayCommand(CanExecute = nameof(CanChangeStatus), AllowConcurrentExecutions = false)]
    private Task ChangeStatusAsync(CancellationToken cancellationToken) => RunAsync(
        async ct =>
        {
            await _client.ChangeStatusAsync(SelectedTask!.Id, NewStatus!.Value, ActingEmployee!.Id, ct);
            await LoadCurrentPageAsync(ct);
            await LoadHistoryAsync(SelectedTask);
        },
        cancellationToken);

    /// <summary>Reassigns the selected task to <see cref="NewAssignee"/>.</summary>
    [RelayCommand(CanExecute = nameof(CanReassign), AllowConcurrentExecutions = false)]
    private Task ReassignAsync(CancellationToken cancellationToken) => RunAsync(
        async ct =>
        {
            await _client.ReassignAsync(SelectedTask!.Id, NewAssignee!.Id, ActingEmployee!.Id, ct);
            await LoadCurrentPageAsync(ct);
            await LoadHistoryAsync(SelectedTask);
        },
        cancellationToken);

    /// <summary>Hides the message bar.</summary>
    [RelayCommand]
    private void DismissError() => ErrorMessage = null;

    private Task ReloadFirstPageAsync() => RunAsync(LoadFirstPageAsync);

    private Task LoadFirstPageAsync(CancellationToken cancellationToken) =>
        LoadPageAsync(null, _previousCursors.Clear, cancellationToken);

    private Task LoadCurrentPageAsync(CancellationToken cancellationToken) =>
        LoadPageAsync(_currentCursor, static () => { }, cancellationToken);

    // Fetches the page at `cursor`; only after the fetch succeeded does it run `commitStack` (the back-stack change
    // belonging to this navigation), so a failed load leaves the paging state untouched.
    private async Task LoadPageAsync(TaskCursor? cursor, Action commitStack, CancellationToken cancellationToken)
    {
        var selectedId = SelectedTask?.Id;
        IReadOnlyList<TaskRow> rows = [];
        TaskCursor? next = null;
        if (FilterAssignee is { } assignee)
        {
            var page = await _client.ListTasksAsync(assignee.Id, SelectedStatusFilter.Value, cursor, cancellationToken);
            rows = page.Items;
            next = page.NextCursor;
        }

        commitStack();
        _currentCursor = cursor;
        _nextCursor = next;
        Tasks.Clear();
        foreach (var row in rows)
        {
            Tasks.Add(row);
        }

        SelectedTask = Tasks.FirstOrDefault(t => t.Id == selectedId);
        PageNumber = _previousCursors.Count + 1;
        NextPageCommand.NotifyCanExecuteChanged();
        PreviousPageCommand.NotifyCanExecuteChanged();
    }

    private async Task LoadHistoryAsync(TaskRow? task)
    {
        var version = ++_historyVersion;
        History.Clear();
        if (task is null)
        {
            return;
        }

        try
        {
            var rows = await _client.GetHistoryAsync(task.Id, CancellationToken.None);
            if (version == _historyVersion && SelectedTask?.Id == task.Id)
            {
                foreach (var row in rows)
                {
                    History.Add(row);
                }
            }
        }
        catch (Exception ex) when (ex is DbException || ex.InnerException is DbException)
        {
            ErrorMessage = "Cannot reach the database. Start it with 'docker compose up -d' and try again.";
        }
    }

    // The single error-handling path for database commands: expected failures become a message, never an exception.
    private async Task RunAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await action(cancellationToken);
        }
        catch (BusinessRuleViolationException ex)
        {
            await FailAndReloadAsync(ex.Message, cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await FailAndReloadAsync("Another user changed this task at the same time. The list has been reloaded.", cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            await FailAndReloadAsync("The database rejected the change: " + (ex.InnerException?.Message ?? ex.Message), cancellationToken);
        }
        catch (KeyNotFoundException ex)
        {
            await FailAndReloadAsync(ex.Message, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            ErrorMessage = ex.Message;
        }
        // Npgsql's execution strategy wraps a refused connection in an InvalidOperationException ("likely due to a
        // transient failure") with the NpgsqlException inside, so match both shapes.
        catch (Exception ex) when (ex is DbException || ex.InnerException is DbException)
        {
            ErrorMessage = "Cannot reach the database. Start it with 'docker compose up -d' and try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task FailAndReloadAsync(string message, CancellationToken cancellationToken)
    {
        ErrorMessage = message;
        try
        {
            await LoadCurrentPageAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is DbException || ex.InnerException is DbException)
        {
            ErrorMessage = message + ReloadFailedSuffix;
        }
    }
}
