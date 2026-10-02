using System.Windows;
using System.Windows.Threading;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using TaskManagement.Infrastructure;
using TaskManagement.Wpf.Services;
using TaskManagement.Wpf.ViewModels;
using TaskManagement.Wpf.Views;

namespace TaskManagement.Wpf;

/// <summary>
/// The composition root: builds the host (configuration and DI) and shows the main window (ADR 0010).
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = AppContext.BaseDirectory,
        });

        var connectionString = builder.Configuration.GetConnectionString("TaskManagement")
            ?? throw new InvalidOperationException("Connection string 'TaskManagement' is missing from appsettings.json.");

        builder.Services.AddDbContextFactory<TaskManagementDbContext>(o => o.UseNpgsql(connectionString));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<ITaskClient, TaskClient>();
        builder.Services.AddSingleton<IDialogService, DialogService>();
        builder.Services.AddTransient<MainViewModel>();
        builder.Services.AddTransient<MainWindow>();
        builder.Services.AddTransient<CreateTaskViewModel>();
        builder.Services.AddTransient<CreateTaskWindow>();

        _host = builder.Build();

        MainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow.Show();
    }

    /// <inheritdoc />
    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }

    // Last resort (guideline 02): expected failures are handled by the commands; anything reaching here leaves the
    // app in an unknown state, so report it once and shut down instead of continuing.
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"An unexpected error occurred and the application will close.\n\n{e.Exception.Message}",
            "Task Management",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
        Shutdown(1);
    }
}
