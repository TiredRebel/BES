# .NET MVVM architecture best practices

> Source: project guideline document ".net mvvm architecture best practices guide".
> Normative for the WPF client in this repository.

A maintainable MVVM application keeps the View thin, makes ViewModels responsible for UI-facing
state and commands, and moves business rules, I/O, persistence and platform integration into
testable application services. `CommunityToolkit.Mvvm` is a lightweight implementation layer that
works across WPF, WinUI 3, WinForms, MAUI and Uno.

MVVM is a presentation architecture, not a replacement for application/domain architecture.

## Dependency direction

```
Desktop UI        Views, controls, XAML, styles, navigation   → depends on Presentation
Presentation      ViewModels, UI state, validation, commands  → depends on Application
Application       Use cases, workflows, DTOs, ports           → depends on Domain
Domain            Business rules, value objects, entities     → depends on nothing
Infrastructure    HTTP, database, filesystem, OS clients      → implements Application/Domain interfaces
```

A practical layout for a medium-to-large app:

```
src/
├─ Product.Desktop/        # WPF, WinUI, Avalonia or MAUI UI
├─ Product.Presentation/   # ViewModels, presentation services, navigation
├─ Product.Application/    # Use cases and interfaces
├─ Product.Domain/         # Business model and rules
├─ Product.Infrastructure/ # HTTP, database, filesystem, settings
└─ Product.Contracts/      # Optional shared DTO contracts
tests/
├─ Product.Domain.Tests/
├─ Product.Application.Tests/
├─ Product.Presentation.Tests/
└─ Product.Desktop.UiTests/
```

Do NOT split into many projects merely to look clean. For a small utility, Desktop, Application and
Infrastructure may be enough. The constraint that matters is dependency direction: UI dependencies
must not leak into domain or application code.

## Responsibilities

| Layer | Owns | Must not own |
|---|---|---|
| View | Layout, bindings, visual states, templates, simple visual behavior | Business logic, network/file/database work, workflow orchestration |
| ViewModel | Bindable state, commands, UI validation state, presentation mapping, busy/error state | Framework dialogs, controls, windows, HTTP/database/filesystem calls |
| Application | Use cases, transactions, workflow decisions, authorization/business validation, service interfaces | XAML, `ICommand`, `ObservableCollection`, `Dispatcher`, `MessageBox` |
| Domain | Business invariants and rules | UI concerns, persistence details, API client code |
| Infrastructure | Concrete data, filesystem, HTTP, OS, telemetry implementations | UI-bound state, application workflow decisions |

## View guidelines

A View is mostly XAML and declarative bindings:

```xml
<Button Content="Import" Command="{Binding ImportCommand}" />
<ProgressBar IsIndeterminate="{Binding IsBusy}" />
<TextBlock Text="{Binding StatusMessage}" />
```

Code-behind is not forbidden. Keep it limited to genuinely visual or framework-lifecycle work:
focus management, animation and visual-state transitions, drag/drop plumbing, platform-specific
control quirks, and view lifetime hooks that delegate to a ViewModel command.

Never put application decisions in click handlers — file selection, parsing, database writes,
retries and UI state become hard to test and reuse.

## ViewModel guidelines

A ViewModel adapts the UI to application use cases. It exposes bound properties, `ICommand`
implementations, presentation-level validation and user-facing status, a small amount of state
coordination (selection, loading, errors, busy), and mapping from application/domain results to
UI-friendly data.

Do NOT pass a `Window`, `Control`, `Dispatcher`, `Page`, `MessageBox` or framework navigation class
into application services. Model those interactions as narrow presentation interfaces:

```csharp
public interface IFilePicker
{
    Task<PickedFile?> PickOpenFileAsync(
        FilePickerOptions options,
        CancellationToken cancellationToken);
}

public interface IUserNotificationService
{
    Task ShowErrorAsync(string title, string message, CancellationToken cancellationToken);
}
```

The UI project supplies framework-specific implementations; the ViewModel depends on the interfaces.

## CommunityToolkit.Mvvm pattern

```bash
dotnet add package CommunityToolkit.Mvvm
```

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public sealed partial class ImportViewModel : ObservableObject
{
    private readonly IFilePicker _filePicker;
    private readonly IImportDocumentsUseCase _importDocuments;
    private readonly IUserNotificationService _notifications;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ImportCommand))]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = "Ready";

    [ObservableProperty]
    private ImportSummaryViewData? lastImport;

    public ImportViewModel(
        IFilePicker filePicker,
        IImportDocumentsUseCase importDocuments,
        IUserNotificationService notifications)
    {
        _filePicker = filePicker;
        _importDocuments = importDocuments;
        _notifications = notifications;
    }

    private bool CanImport() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanImport))]
    private async Task ImportAsync(CancellationToken cancellationToken)
    {
        var file = await _filePicker.PickOpenFileAsync(
            new FilePickerOptions([".csv", ".json"]),
            cancellationToken);

        if (file is null)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Importing…";
        try
        {
            var progress = new Progress<ImportProgress>(
                update => StatusMessage = update.Message);

            var result = await _importDocuments.ExecuteAsync(
                new ImportDocumentsRequest(file.Path),
                progress,
                cancellationToken);

            LastImport = ImportSummaryViewData.From(result);
            StatusMessage = $"Imported {result.ImportedCount} records.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            StatusMessage = "Import cancelled.";
        }
        catch (ImportValidationException ex)
        {
            StatusMessage = "Import failed validation.";
            await _notifications.ShowErrorAsync("Import failed", ex.UserMessage, CancellationToken.None);
        }
        catch (Exception)
        {
            StatusMessage = "Import failed.";
            await _notifications.ShowErrorAsync(
                "Unexpected error",
                "The import could not be completed. Review diagnostics and try again.",
                CancellationToken.None);
            // Log the exception via an injected logger; do not expose internals to the user.
        }
        finally
        {
            IsBusy = false;
        }
    }
}
```

## Observable properties

Make a property observable only when a View, another ViewModel concern, or a command's `CanExecute`
genuinely needs to react. For derived state use normal read-only properties and notify explicitly:

```csharp
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(HasResults))]
private int resultCount;

public bool HasResults => ResultCount > 0;
```

## Commands, async, cancellation

| Situation | Approach |
|---|---|
| Toggle, local selection, simple state change | `RelayCommand` |
| Load, save, file import/export, HTTP, database work | `AsyncRelayCommand` / generated async relay command |
| CPU-heavy parsing, image processing, compression, local model work | Async command plus a deliberate background-work strategy |
| Long-running operation | Async command with `CancellationToken`, visible progress, cancel action |
| Parameterized interaction | `RelayCommand<T>` / `AsyncRelayCommand<T>` |
| Button availability | `CanExecute` predicate plus `NotifyCanExecuteChangedFor` |

NEVER use `async void` for ViewModel actions except unavoidable UI event-handler glue. Commands
return `Task` so failures, cancellation, state transitions and tests stay observable.

```xml
<Button Content="Cancel"
        Command="{Binding ImportCommandCancel}"
        IsEnabled="{Binding ImportCommand.CanBeCanceled}" />
```

Use `AllowConcurrentExecutions = false` for commands that must not run twice concurrently — Save,
Import, Refresh Token, Run Migration:

```csharp
[RelayCommand(
    CanExecute = nameof(CanImport),
    AllowConcurrentExecutions = false,
    IncludeCancelCommand = true)]
private async Task ImportAsync(CancellationToken cancellationToken) { }
```

The generated command surface varies by toolkit version — verify against the version you pin.

## UI-thread rule

- Update UI-bound properties and collections on the UI thread.
- Keep HTTP, filesystem, database, parsing and business logic UI-agnostic.
- Do not block the UI thread with `.Result`, `.Wait()` or synchronous I/O.
- Use `Task.Run` only for genuinely CPU-bound work, never to wrap naturally async I/O.
- For bulk collection updates, build a plain list off-thread, then apply one batched update on the
  UI thread.
- A `Task<List<T>>` is not itself a bindable result: await it, then update a bindable collection or
  property with the completed data.

## Navigation, dialogs, messaging

ViewModels must not create or manipulate windows directly. Put UI interactions behind small
abstractions:

```csharp
public interface INavigationService
{
    Task NavigateToAsync<TViewModel>(object? parameter = null, CancellationToken cancellationToken = default);
}

public interface IDialogService
{
    Task<bool> ConfirmAsync(ConfirmationRequest request, CancellationToken cancellationToken = default);
}
```

Prefer direct use-case calls for a straightforward user action. Use a messenger or event aggregator
only for genuinely decoupled, cross-cutting notifications — "authentication state changed",
"document was externally modified" — with clear message ownership and lifetime.
`WeakReferenceMessenger` must not become hidden global control flow.

## Dependency injection

Build the dependency graph once at startup. Register Views/ViewModels and services in the
composition root; avoid service locators inside ViewModels.

```csharp
services.AddSingleton<ISettingsStore, JsonSettingsStore>();
services.AddSingleton<IImportDocumentsUseCase, ImportDocumentsUseCase>();
services.AddSingleton<IFilePicker, DesktopFilePicker>();
services.AddSingleton<IUserNotificationService, DesktopNotificationService>();
services.AddTransient<ImportViewModel>();
services.AddTransient<ImportPage>();
```

| Lifetime | Best for |
|---|---|
| Singleton | App configuration, logging, shared caches, navigation coordinator, authenticated session, long-lived clients |
| Transient | Views, short-lived ViewModels, per-navigation presentation state |
| Scoped / explicit session | A document/workspace/editing session that owns resources and is disposed together |

Avoid singletons for ViewModels that represent a page, document, form or navigation instance — they
retain stale state, event subscriptions and user data longer than intended.

## Validation, errors, state

Use two levels of validation:

- **Presentation** — required input, parseability, range checks, immediate feedback, button
  availability.
- **Application/domain** — authoritative business rules, permissions, data consistency, external
  state.

NEVER rely solely on UI validation: the same use case may later be triggered from automation, an
API, a batch process, tests or another screen.

For interactive field validation implement `INotifyDataErrorInfo`, use `ObservableValidator`, or
wrap a dedicated validator. Keep messages user-readable and field-specific.

Model screen state explicitly instead of combining ambiguous booleans:

```csharp
public enum LoadState
{
    NotStarted,
    Loading,
    Loaded,
    Empty,
    Failed
}
```

- **Loading** — show progress and disable conflicting actions.
- **Empty** — explain why no data exists and offer the next useful action.
- **Failed** — retain retry capability and show a safe message.
- **Loaded** — show content and normal commands.

## Testing strategy

| Test type | Test | Avoid |
|---|---|---|
| Domain unit | Business invariants, calculations, value objects | UI framework setup |
| Application unit | Use-case behavior, validation, workflows, retries | WPF/WinUI/Avalonia types |
| ViewModel unit | Initial state, command `CanExecute`, busy transitions, cancellation, mapping, user-visible errors | Real dialogs, network, database, filesystem |
| Integration | Real persistence/API adapter behavior, migrations, serialization | Testing every XAML binding case |
| UI automation | Critical end-to-end journeys, keyboard navigation, accessibility smoke tests | Duplicating all ViewModel tests through the UI |

```csharp
[Fact]
public async Task ImportCommand_SetsBusyAndShowsSummary_WhenImportSucceeds()
{
    var import = new FakeImportDocumentsUseCase
    {
        Result = new ImportResult(ImportedCount: 12)
    };

    var viewModel = new ImportViewModel(
        new FakeFilePicker("orders.csv"),
        import,
        new FakeNotificationService());

    await viewModel.ImportCommand.ExecuteAsync(null);

    Assert.False(viewModel.IsBusy);
    Assert.Equal("Imported 12 records.", viewModel.StatusMessage);
    Assert.Equal(12, viewModel.LastImport!.ImportedCount);
}
```

## Common failure modes

- **Fat ViewModels** — a 1,000-line ViewModel querying databases, calling APIs, managing dialogs and
  holding business rules. Extract use cases and focused presentation services.
- **Code-behind migration, not MVVM** — handlers moved into a ViewModel without improving
  boundaries.
- **`async void` commands** — exceptions hard to observe, weak cancellation, unreliable tests.
- **Blocking UI work** — `.Result`, `.Wait()`, synchronous I/O or CPU work on the dispatcher thread.
- **Framework leakage** — application services referencing `Window`, `Control`, `Dispatcher`,
  `MessageBox`.
- **God messenger** — application flow hidden in event subscriptions instead of explicit use cases.
- **Global ViewModels** — singleton page state causing stale data, leaks and navigation bugs.
- **Property-notification storms** — everything observable, expensive recalculation in setters,
  excessive rerendering.
- **Over-abstracted UI** — generic repositories, generic ViewModels or a custom framework added
  before real repetition exists.
- **UI-only validation** — a business rule enforced only by a disabled button or textbox validation.

## Recommended defaults

- `CommunityToolkit.Mvvm` for observable state, source-generated commands, validation helpers and
  selective messaging.
- One ViewModel per page, dialog, document or reusable interactive control.
- Views limited to XAML, visual behavior and framework glue.
- ViewModels depending on small interfaces for navigation, dialogs, pickers and clipboard.
- Application use cases accepting request DTOs, returning result DTOs, reporting `IProgress<T>` and
  accepting `CancellationToken`.
- Nullable reference types enabled, analyzers enforced, structured logging, tests centered on
  domain/application/view-model behavior.
- Explicit cancellation, progress, error, empty and busy states for every potentially long operation.
- A small UI automation suite for critical workflows and accessibility, with most behavior tested
  below the UI layer.

Sources: Microsoft Learn CommunityToolkit.Mvvm docs, `AsyncRelayCommand` reference, WPF data
binding, async MVVM patterns.
