using System.Windows;

using TaskManagement.Wpf.ViewModels;

namespace TaskManagement.Wpf.Views;

/// <summary>
/// The main window: task list, filters, change status, reassign and history. Pure binding; no logic (ADR 0010).
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class and loads the data once it is shown.
    /// </summary>
    /// <param name="viewModel">The window's view model.</param>
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) => viewModel.LoadCommand.Execute(null);
    }
}
