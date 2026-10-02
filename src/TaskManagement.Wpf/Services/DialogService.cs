using Microsoft.Extensions.DependencyInjection;

using TaskManagement.Wpf.Views;

namespace TaskManagement.Wpf.Services;

/// <summary>
/// <see cref="IDialogService"/> that resolves each dialog window from the DI container (ADR 0010).
/// </summary>
/// <param name="services">The container the dialog windows are resolved from.</param>
public sealed class DialogService(IServiceProvider services) : IDialogService
{
    /// <inheritdoc />
    public bool ShowCreateTask(IReadOnlyList<EmployeeOption> employees, EmployeeOption? defaultCreator)
    {
        var window = services.GetRequiredService<CreateTaskWindow>();
        window.ViewModel.Initialize(employees, defaultCreator);
        window.Owner = System.Windows.Application.Current.MainWindow;
        return window.ShowDialog() == true;
    }
}
