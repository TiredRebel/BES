namespace TaskManagement.Wpf.Services;

/// <summary>
/// Opens dialogs on behalf of view models, so view models never create windows (guideline 03, ADR 0010).
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Shows the modal "create task" dialog.
    /// </summary>
    /// <param name="employees">The employees offered as creator and assignee.</param>
    /// <param name="defaultCreator">The employee preselected as creator, or null.</param>
    /// <returns>True when a task was created; false when the dialog was cancelled.</returns>
    bool ShowCreateTask(IReadOnlyList<EmployeeOption> employees, EmployeeOption? defaultCreator);
}
