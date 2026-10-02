using System.Windows;

using TaskManagement.Wpf.ViewModels;

namespace TaskManagement.Wpf.Views;

/// <summary>
/// The modal "create task" dialog. Pure binding; closes with a positive result once the task was saved (ADR 0010).
/// </summary>
public partial class CreateTaskWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateTaskWindow"/> class.
    /// </summary>
    /// <param name="viewModel">The dialog's view model.</param>
    public CreateTaskWindow(CreateTaskViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        viewModel.Created += (_, _) => DialogResult = true;
    }

    /// <summary>Gets the dialog's view model, so the dialog service can initialize it.</summary>
    public CreateTaskViewModel ViewModel { get; }
}
