using System.Collections.ObjectModel;
using System.Data.Common;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.EntityFrameworkCore;

using TaskManagement.Domain;
using TaskManagement.Wpf.Services;

namespace TaskManagement.Wpf.ViewModels;

/// <summary>
/// State and commands of the "create task" dialog (ADR 0010).
/// </summary>
/// <remarks>
/// Only input completeness is checked here. BR2, BR3 and BR5 are enforced by the service and shown in the message bar.
/// </remarks>
public sealed partial class CreateTaskViewModel : ObservableObject
{
    private readonly ITaskClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateTaskViewModel"/> class.
    /// </summary>
    /// <param name="client">The data-layer client.</param>
    public CreateTaskViewModel(ITaskClient client)
    {
        _client = client;
        Title = string.Empty;
    }

    /// <summary>Raised after the task was saved.</summary>
    public event EventHandler? Created;

    /// <summary>Gets the employees offered as creator and assignee.</summary>
    public ObservableCollection<EmployeeOption> Employees { get; } = [];

    /// <summary>Gets or sets the task title.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string Title { get; set; }

    /// <summary>Gets or sets the creating employee.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial EmployeeOption? Creator { get; set; }

    /// <summary>Gets or sets the assigned employee.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial EmployeeOption? Assignee { get; set; }

    /// <summary>Gets or sets the planned start date, or null.</summary>
    [ObservableProperty]
    public partial DateTime? PlannedStartDate { get; set; }

    /// <summary>Gets or sets the due date, or null.</summary>
    [ObservableProperty]
    public partial DateTime? DueDate { get; set; }

    /// <summary>Gets or sets a value indicating whether a save is running.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool IsBusy { get; set; }

    /// <summary>Gets or sets the message shown in the message bar, or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    /// <summary>Gets a value indicating whether the message bar is shown.</summary>
    public bool HasError => ErrorMessage is not null;

    /// <summary>
    /// Fills the pickers and preselects the creator.
    /// </summary>
    /// <param name="employees">The employees offered as creator and assignee.</param>
    /// <param name="defaultCreator">The employee preselected as creator, or null.</param>
    public void Initialize(IReadOnlyList<EmployeeOption> employees, EmployeeOption? defaultCreator)
    {
        Employees.Clear();
        foreach (var employee in employees)
        {
            Employees.Add(employee);
        }

        Creator = defaultCreator;
    }

    private static DateTimeOffset? ToOffset(DateTime? d) => d is null ? null : new DateTimeOffset(d.Value);

    private bool CanSave() => !IsBusy && !string.IsNullOrWhiteSpace(Title) && Creator is not null && Assignee is not null;

    [RelayCommand(CanExecute = nameof(CanSave), AllowConcurrentExecutions = false)]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await _client.CreateTaskAsync(
                Title,
                Creator!.Id,
                Assignee!.Id,
                ToOffset(PlannedStartDate),
                ToOffset(DueDate),
                cancellationToken);
            Created?.Invoke(this, EventArgs.Empty);
        }
        catch (BusinessRuleViolationException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (DbUpdateException ex)
        {
            ErrorMessage = "The database rejected the change: " + (ex.InnerException?.Message ?? ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (ArgumentException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (DbException)
        {
            ErrorMessage = "Cannot reach the database. Start it with 'docker compose up -d' and try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
