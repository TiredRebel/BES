using Microsoft.EntityFrameworkCore;

using TaskManagement.Application;
using TaskManagement.Domain;
using TaskManagement.Wpf.Services;
using TaskManagement.Wpf.ViewModels;

namespace TaskManagement.Wpf.UnitTests;

/// <summary>
/// Unit tests for <see cref="MainViewModel"/> against fake services.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MainViewModelTests
{
    private static readonly EmployeeOption Inactive = new(Guid.NewGuid(), "Ivan Inactive", false);
    private static readonly EmployeeOption Active = new(Guid.NewGuid(), "Anna Active", true);

    private readonly FakeTaskClient _client = new() { Employees = [Inactive, Active] };
    private readonly FakeDialogService _dialogs = new();
    private readonly MainViewModel _vm;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainViewModelTests"/> class.
    /// </summary>
    public MainViewModelTests() => _vm = new MainViewModel(_client, _dialogs);

    private static TaskRow Row(TaskItemStatus status = TaskItemStatus.New) =>
        new(Guid.NewGuid(), "Task", status, Active.Id, Active.FullName, null, null, null);

    private async Task<TaskRow> LoadWithSelectedTaskAsync(TaskItemStatus status = TaskItemStatus.New)
    {
        var row = Row(status);
        _client.Pages = _ => new TaskPage([row], null);
        await _vm.LoadCommand.ExecuteAsync(null);
        _vm.SelectedTask = row;
        return row;
    }

    /// <summary>
    /// Verifies that loading skips an inactive first employee and lists the active one's tasks.
    /// </summary>
    /// <returns>A task that completes when the test is done.</returns>
    [Fact]
    public async Task LoadCommand_ActiveEmployeesExist_SelectsFirstActiveAsActingAndListsTheirTasks()
    {
        var row = Row();
        _client.Pages = _ => new TaskPage([row], null);

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.Equal(Active, _vm.ActingEmployee);
        Assert.Equal(Active, _vm.FilterAssignee);
        Assert.Equal([Inactive, Active], _vm.Employees);
        Assert.Equal([row], _vm.Tasks);
    }

    /// <summary>
    /// Verifies that change status is disabled without a selected task.
    /// </summary>
    [Fact]
    public void ChangeStatusCommand_NoSelectedTask_CannotExecute()
    {
        _vm.ActingEmployee = Active;
        _vm.NewStatus = TaskItemStatus.InProgress;

        Assert.False(_vm.ChangeStatusCommand.CanExecute(null));
    }

    /// <summary>
    /// Verifies that change status is enabled once task, status and acting employee are set.
    /// </summary>
    [Fact]
    public void ChangeStatusCommand_TaskStatusAndActingSelected_CanExecute()
    {
        _vm.ActingEmployee = Active;
        _vm.SelectedTask = Row();
        _vm.NewStatus = TaskItemStatus.InProgress;

        Assert.True(_vm.ChangeStatusCommand.CanExecute(null));
    }

    /// <summary>
    /// Verifies that the UI does not encode BR4: a completed task still enables change status.
    /// </summary>
    [Fact]
    public void ChangeStatusCommand_SelectedTaskIsCompleted_CanStillExecute()
    {
        _vm.ActingEmployee = Active;
        _vm.SelectedTask = Row(TaskItemStatus.Completed);
        _vm.NewStatus = TaskItemStatus.InProgress;

        Assert.True(_vm.ChangeStatusCommand.CanExecute(null));
    }

    /// <summary>
    /// Verifies that the UI does not encode BR3: an inactive new assignee still enables reassign.
    /// </summary>
    [Fact]
    public void ReassignCommand_NewAssigneeIsInactive_CanStillExecute()
    {
        _vm.ActingEmployee = Active;
        _vm.SelectedTask = Row();
        _vm.NewAssignee = Inactive;

        Assert.True(_vm.ReassignCommand.CanExecute(null));
    }

    /// <summary>
    /// Verifies that a business rule violation becomes a message and the list is reloaded.
    /// </summary>
    /// <returns>A task that completes when the test is done.</returns>
    [Fact]
    public async Task ChangeStatusCommand_ServiceThrowsBusinessRuleViolation_SetsErrorMessageAndReloadsList()
    {
        await LoadWithSelectedTaskAsync(TaskItemStatus.Completed);
        _vm.NewStatus = TaskItemStatus.InProgress;
        _client.ChangeStatusError = new BusinessRuleViolationException("BR4", "The task is final.");
        var listCallsBefore = _client.ListCalls;

        await _vm.ChangeStatusCommand.ExecuteAsync(null);

        Assert.Equal("The task is final.", _vm.ErrorMessage);
        Assert.True(_vm.HasError);
        Assert.True(_client.ListCalls > listCallsBefore);
        Assert.False(_vm.IsBusy);
    }

    /// <summary>
    /// Verifies that the acting employee, not the assignee, is recorded as author and the list is reloaded.
    /// </summary>
    /// <returns>A task that completes when the test is done.</returns>
    [Fact]
    public async Task ChangeStatusCommand_Succeeds_RecordsActingEmployeeAsAuthorAndReloadsList()
    {
        var row = await LoadWithSelectedTaskAsync();
        _vm.ActingEmployee = Inactive; // differs from the task's assignee (Active)
        _vm.NewStatus = TaskItemStatus.InProgress;
        var listCallsBefore = _client.ListCalls;

        await _vm.ChangeStatusCommand.ExecuteAsync(null);

        Assert.Equal((row.Id, TaskItemStatus.InProgress, Inactive.Id), _client.LastStatusChange);
        Assert.True(_client.ListCalls > listCallsBefore);
        Assert.Null(_vm.ErrorMessage);
    }

    /// <summary>
    /// Verifies that a concurrency conflict becomes a message and the list is reloaded.
    /// </summary>
    /// <returns>A task that completes when the test is done.</returns>
    [Fact]
    public async Task ReassignCommand_ServiceThrowsConcurrencyException_SetsMessageAndReloads()
    {
        await LoadWithSelectedTaskAsync();
        _vm.NewAssignee = Inactive;
        _client.ReassignError = new DbUpdateConcurrencyException("x");
        var listCallsBefore = _client.ListCalls;

        await _vm.ReassignCommand.ExecuteAsync(null);

        Assert.Contains("Another user changed this task", _vm.ErrorMessage);
        Assert.True(_client.ListCalls > listCallsBefore);
        Assert.False(_vm.IsBusy);
    }

    /// <summary>
    /// Verifies that an unreachable database becomes a message instead of an exception.
    /// </summary>
    /// <returns>A task that completes when the test is done.</returns>
    [Fact]
    public async Task LoadCommand_DatabaseUnreachable_SetsErrorMessageWithoutThrowing()
    {
        _client.EmployeesError = new FakeDbException("down");

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.Contains("Cannot reach the database", _vm.ErrorMessage);
        Assert.False(_vm.IsBusy);
    }

    /// <summary>
    /// Verifies the shape Npgsql's execution strategy really throws for a refused connection: an
    /// <see cref="InvalidOperationException"/> wrapping the database exception. Found by running the app with the
    /// database stopped, which crashed before this case was handled.
    /// </summary>
    /// <returns>A task that completes when the test is done.</returns>
    [Fact]
    public async Task LoadCommand_DatabaseUnreachableWrappedByExecutionStrategy_SetsErrorMessageWithoutThrowing()
    {
        _client.EmployeesError = new InvalidOperationException(
            "An exception has been raised that is likely due to a transient failure.",
            new FakeDbException("Failed to connect to 127.0.0.1:5433"));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.Contains("Cannot reach the database", _vm.ErrorMessage);
        Assert.False(_vm.IsBusy);
    }

    /// <summary>
    /// Verifies keyset paging forwards and back, including the page number and button enablement.
    /// </summary>
    /// <returns>A task that completes when the test is done.</returns>
    [Fact]
    public async Task NextPageCommand_PageHasNextCursor_LoadsNextPageAndEnablesPrevious()
    {
        var cursor = new TaskCursor(DateTimeOffset.UnixEpoch, Guid.NewGuid());
        var first = Row();
        var second = Row();
        _client.Pages = c => c is null ? new TaskPage([first], cursor) : new TaskPage([second], null);

        await _vm.LoadCommand.ExecuteAsync(null);
        Assert.Equal(1, _vm.PageNumber);
        Assert.True(_vm.NextPageCommand.CanExecute(null));
        Assert.False(_vm.PreviousPageCommand.CanExecute(null));

        await _vm.NextPageCommand.ExecuteAsync(null);
        Assert.Equal(cursor, _client.ListCursors[^1]);
        Assert.Equal(2, _vm.PageNumber);
        Assert.Equal([second], _vm.Tasks);
        Assert.True(_vm.PreviousPageCommand.CanExecute(null));
        Assert.False(_vm.NextPageCommand.CanExecute(null));

        await _vm.PreviousPageCommand.ExecuteAsync(null);
        Assert.Null(_client.ListCursors[^1]);
        Assert.Equal(1, _vm.PageNumber);
        Assert.Equal([first], _vm.Tasks);
    }

    /// <summary>
    /// Verifies that a created task reloads the first page.
    /// </summary>
    /// <returns>A task that completes when the test is done.</returns>
    [Fact]
    public async Task CreateTaskCommand_DialogReportsCreated_ReloadsFirstPage()
    {
        await _vm.LoadCommand.ExecuteAsync(null);
        _dialogs.Result = true;
        var listCallsBefore = _client.ListCalls;

        await _vm.CreateTaskCommand.ExecuteAsync(null);

        Assert.Equal(1, _dialogs.Calls);
        Assert.Equal(listCallsBefore + 1, _client.ListCalls);
        Assert.Null(_client.ListCursors[^1]);
    }

    /// <summary>
    /// Verifies that a cancelled dialog does not reload.
    /// </summary>
    /// <returns>A task that completes when the test is done.</returns>
    [Fact]
    public async Task CreateTaskCommand_DialogCancelled_DoesNotReload()
    {
        await _vm.LoadCommand.ExecuteAsync(null);
        _dialogs.Result = false;
        var listCallsBefore = _client.ListCalls;

        await _vm.CreateTaskCommand.ExecuteAsync(null);

        Assert.Equal(1, _dialogs.Calls);
        Assert.Equal(listCallsBefore, _client.ListCalls);
    }

    /// <summary>
    /// Verifies that dismissing clears the message bar.
    /// </summary>
    [Fact]
    public void DismissErrorCommand_ErrorShown_ClearsMessage()
    {
        _vm.ErrorMessage = "boom";
        Assert.True(_vm.HasError);

        _vm.DismissErrorCommand.Execute(null);

        Assert.Null(_vm.ErrorMessage);
        Assert.False(_vm.HasError);
    }
}
