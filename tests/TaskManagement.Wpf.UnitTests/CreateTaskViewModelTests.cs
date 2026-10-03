using TaskManagement.Domain;
using TaskManagement.Wpf.Services;
using TaskManagement.Wpf.ViewModels;

namespace TaskManagement.Wpf.UnitTests;

/// <summary>
/// Unit tests for <see cref="CreateTaskViewModel"/> against a fake client.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CreateTaskViewModelTests
{
    private static readonly EmployeeOption Alice = new(Guid.NewGuid(), "Alice", true);
    private static readonly EmployeeOption Bob = new(Guid.NewGuid(), "Bob", true);

    private readonly FakeTaskClient _client = new();
    private readonly CreateTaskViewModel _vm;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateTaskViewModelTests"/> class.
    /// </summary>
    public CreateTaskViewModelTests()
    {
        _vm = new CreateTaskViewModel(_client);
        _vm.Initialize([Alice, Bob], Alice);
        _vm.Assignee = Bob;
        _vm.Title = "Write report";
    }

    /// <summary>
    /// Verifies that a blank title disables save.
    /// </summary>
    [Fact]
    public void SaveCommand_BlankTitle_CannotExecute()
    {
        _vm.Title = "   ";

        Assert.False(_vm.SaveCommand.CanExecute(null));
    }

    /// <summary>
    /// Verifies that the UI does not encode BR5: creator equal to assignee still enables save.
    /// </summary>
    [Fact]
    public void SaveCommand_CreatorEqualsAssignee_CanStillExecute()
    {
        _vm.Assignee = Alice;

        Assert.True(_vm.SaveCommand.CanExecute(null));
    }

    /// <summary>
    /// Verifies that a violation is shown as a message and does not raise Created.
    /// </summary>
    /// <returns>A task that completes when the test is done.</returns>
    [Fact]
    public async Task SaveCommand_ServiceThrowsBusinessRuleViolation_SetsErrorMessageAndDoesNotRaiseCreated()
    {
        _client.CreateError = new BusinessRuleViolationException("BR5", "Creator and assignee must differ.");
        var created = 0;
        _vm.Created += (_, _) => created++;

        await _vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Creator and assignee must differ.", _vm.ErrorMessage);
        Assert.Equal(0, created);
        Assert.False(_vm.IsBusy);
    }

    /// <summary>
    /// Verifies that a successful save raises Created and passes the dates on as offsets.
    /// </summary>
    /// <returns>A task that completes when the test is done.</returns>
    [Fact]
    public async Task SaveCommand_Succeeds_RaisesCreatedAndPassesDatesAsOffsets()
    {
        var start = new DateTime(2026, 10, 5, 9, 0, 0, DateTimeKind.Local);
        var due = new DateTime(2026, 10, 9, 17, 30, 0, DateTimeKind.Local);
        _vm.PlannedStartDate = start;
        _vm.DueDate = due;
        var created = 0;
        _vm.Created += (_, _) => created++;

        await _vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(1, created);
        Assert.Null(_vm.ErrorMessage);
        Assert.Equal(
            ("Write report", Alice.Id, Bob.Id, (DateTimeOffset?)new DateTimeOffset(start), (DateTimeOffset?)new DateTimeOffset(due)),
            _client.LastCreate);
    }
}
