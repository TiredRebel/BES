# .NET design guidelines for desktop applications

> Source: project guideline document ".net design guidelines for desktop applications".
> Normative for the WPF client in this repository.

Optimize for a responsive UI, strict separation between UI and application logic,
accessible/resizable layouts, and a deployment path users can trust. For a new Windows-only app
Microsoft currently recommends WinUI 3 with the Windows App SDK; WPF remains a strong choice for
mature, XAML-heavy line-of-business software; WinForms suits maintaining simpler existing apps.

| Need | Direction | Why |
|---|---|---|
| New Windows-first application | WinUI 3 + Windows App SDK | Microsoft's primary recommendation for new general-purpose Windows desktop apps |
| Mature business app, complex binding, custom controls | WPF on current .NET | Powerful XAML, mature tooling, templates, styling, established MVVM patterns |
| Existing WinForms product or simple internal tooling | WinForms on current .NET | Productive designer workflow, simple maintenance |
| Windows + macOS + Linux from one codebase | Avalonia | Pragmatic .NET option when cross-platform is a core requirement |
| Mobile plus desktop | .NET MAUI | When mobile is a first-class requirement; assess desktop UX carefully |

Do not select a framework because it is new. A WPF app with clear architecture, responsive
interactions, solid accessibility and dependable packaging beats a superficially modern rewrite.

## Architecture and boundaries

Use MVVM-like separation even outside WPF. Views own rendering and input wiring; view models own
presentation state and interaction logic; services own application, I/O and infrastructure concerns.

```
DesktopApp.sln
├─ src/
│  ├─ App.Desktop/          # WPF / WinUI / Avalonia UI
│  │  ├─ Views/
│  │  ├─ ViewModels/
│  │  ├─ Controls/
│  │  ├─ Converters/
│  │  └─ Platform/
│  ├─ App.Application/      # Use cases, commands, validation, interfaces
│  ├─ App.Domain/           # Business concepts and rules
│  ├─ App.Infrastructure/   # HTTP, filesystem, DB, OS integration
│  └─ App.Contracts/        # DTOs / shared contracts if genuinely needed
└─ tests/
   ├─ App.Application.Tests/
   ├─ App.Domain.Tests/
   └─ App.Desktop.Tests/
```

Application, Domain and Infrastructure projects MUST NOT reference WPF, WinUI, Avalonia,
`Dispatcher`, `Window`, `Control` or framework-specific dialog types.

Instead of this:

```csharp
public sealed class ImportService
{
    public async Task ImportAsync(Window owner)
    {
        // Reads files, performs work, updates UI directly.
    }
}
```

prefer a UI-agnostic service with explicit inputs and progress:

```csharp
public interface IImportService
{
    Task<ImportResult> ImportAsync(
        ImportRequest request,
        IProgress<ImportProgress>? progress,
        CancellationToken cancellationToken);
}
```

The view model owns UI-specific state: `IsBusy`, an error banner, button enablement, dialog
coordination, and dispatching results to UI-bound collections.

## Commands, not click handlers

Bind buttons, menu items, keyboard gestures and toolbar actions to commands:

```csharp
public sealed partial class ImportViewModel : ObservableObject
{
    private readonly IImportService _importService;
    private readonly IFilePicker _filePicker;

    [ObservableProperty]
    private bool isBusy;

    [RelayCommand(CanExecute = nameof(CanImport))]
    private async Task ImportAsync(CancellationToken cancellationToken)
    {
        var file = await _filePicker.PickFileAsync(cancellationToken);
        if (file is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var progress = new Progress<ImportProgress>(
                value => StatusText = value.Message);

            await _importService.ImportAsync(
                new ImportRequest(file.Path),
                progress,
                cancellationToken);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanImport() => !IsBusy;
}
```

`CommunityToolkit.Mvvm` is a pragmatic foundation for `ObservableObject`, observable properties,
relay commands, cancellation-aware commands and messaging. Keep it in the presentation layer rather
than leaking its types into domain code.

## Responsiveness and threading

The most damaging desktop failure mode is a UI that freezes during I/O, parsing, database work,
indexing, rendering or startup. WPF has separate UI and rendering threads, but UI elements have
thread affinity: access must occur from the UI thread that owns them. Treat view-bound state —
especially observable collections and bound properties — as UI-affine.

- Use `async`/`await` for network, file, database, process execution and retry operations.
- NEVER call `.Result`, `.Wait()` or `GetAwaiter().GetResult()` on UI paths; they freeze or deadlock
  the interface.
- Use `Task.Run` only for genuinely CPU-bound work: image processing, large parsing, compression,
  hashing, local-model orchestration.
- Pass `CancellationToken` from the command through every operation that supports it.
- Support cancellation in long-running operations with a visible Cancel action.
- Report progress with `IProgress<T>`, never by passing controls into service code.
- Batch UI updates when processing many records; adding thousands of items one at a time to a bound
  collection can dominate runtime.
- Show immediate feedback: busy state, progress, current operation, usable cancellation path.

Interaction rule: if an action can take longer than a moment, disable duplicate invocation, show
that work has begun, and make outcomes visible — success, failure, partial result or cancellation.

## Layout

Build for a window users can resize, move across monitors, scale to high DPI, and use without a
mouse.

- Prefer adaptive containers — `Grid`, stack panels, dock-style layouts, split views — over absolute
  coordinates.
- Define sensible `MinWidth` and `MinHeight`, but do not assume a full-HD display.
- Test at 100%, 125%, 150%, 200% and mixed-DPI multi-monitor setups.
- Persist window size, location, maximized state and selected panes only after validating that the
  restored bounds remain visible on currently connected monitors.
- Use scroll containers intentionally; do not solve small-window problems by shrinking fonts or
  clipping controls.
- Use standard platform controls for menus, dialogs, file pickers, keyboard shortcuts and context
  menus.
- Avoid pixel-perfect positioning and fixed text widths; localization and scaling will break them.

## Accessibility

- Every operation MUST be available from the keyboard.
- Provide visible keyboard focus; verify a logical Tab order.
- Never use color alone to communicate validation, status, selection or severity.
- Give icon-only controls accessible names and tooltips.
- Use semantic headings, labels, roles, values and automation identifiers where supported.
- Dialogs announce their purpose, trap focus while open, and return focus to the invoking control
  when closed.
- Respect high-contrast, light/dark themes, text scaling and system colors; avoid hard-coded styling
  that blocks high-contrast theme resources.
- Test critical paths with a screen reader and keyboard-only navigation, not only automated checks.

## Error handling

Desktop software sits close to the filesystem, local credentials, native APIs, printers, USB
hardware and corporate networks. Treat those boundaries as unreliable and potentially hostile.

- Catch exceptions at user-facing boundaries — commands, background-job coordinators, startup — and
  translate them into actionable messages.
- Log the full exception and technical context; show users a concise explanation plus a recovery
  choice.
- NEVER swallow exceptions silently.
- Distinguish transient failures, validation errors, cancellation, permission errors, unavailable
  dependencies and unexpected bugs.
- Preserve unsaved work where feasible, or make autosave/recovery behavior explicit.
- Use a central unhandled-exception policy to log and present a graceful last-resort message, but do
  not assume the process can safely continue after an unhandled failure.

## Secrets, privacy, persistence

- Never embed API keys, connection strings, tokens or private certificates in binaries, source,
  settings files or installer scripts.
- Store sensitive credentials in OS-backed secure storage; prefer interactive OAuth/device flows or
  enterprise identity over long-lived shared secrets.
- Treat local databases, import files, clipboard content, drag-and-drop files, deep links and IPC
  messages as untrusted input.
- Validate paths before file operations; prevent writes outside approved locations.
- Minimize telemetry, make it transparent, and redact tokens, personal data and document contents
  from logs.
- Separate user preferences from durable business data.
- Version configuration and local database schemas; supply migrations and a rollback/backup story.
- Write critical data atomically: temporary file, flush/validate, then replace the original.
- Use app-specific locations for cache, logs, settings and temporary work, with retention limits.
- Do not block startup on remote services — show the shell quickly, load noncritical data
  asynchronously.

## Packaging and operations

| Concern | Recommendation |
|---|---|
| Packaging | MSIX when its identity, deployment, update model or Windows integrations fit; traditional installers remain valid for drivers, prerequisites, enterprise deployment or legacy integration |
| Modern APIs | Add Windows App SDK / WinRT APIs selectively, including from existing WPF and WinForms projects |
| Runtime | Choose framework-dependent vs self-contained deliberately; test first launch on a clean machine |
| Signing | Code-sign installer and executable artifacts; protect signing keys in CI secret storage |
| Updates | Make update checks resilient and non-blocking; verify downloads and signatures; never interrupt unsaved work |
| Diagnostics | Structured local logs with rotation; a "copy diagnostics" or "open log folder" workflow that excludes secrets |
| Compatibility | Test clean install, upgrade, rollback, offline behavior, proxy-restricted networks and standard-user permissions |

## Engineering checklist

- The UI stays interactive during I/O and expensive computation.
- Every long operation exposes busy/progress/cancel state.
- Services and domain code have no UI-framework references.
- Commands — not code-behind event handlers — contain interaction behavior.
- The feature works with keyboard-only input and assistive technologies.
- Layout works at small window sizes, high DPI, display scaling and multi-monitor moves.
- Failures are logged with context and surfaced without exposing internals or secrets.
- State persistence is versioned, recoverable and robust against interrupted writes.
- Release artifacts are reproducible, signed, installable on a clean machine, upgrade-tested.
- Unit tests cover view-model and application/domain logic; UI automation covers a small number of
  critical end-to-end workflows.

For technical, automation-oriented desktop tools the most valuable design investment is not
decorative polish — it is a robust boundary between UI and work execution: cancellation, streaming
progress, recoverable jobs, structured logs, safe configuration, and a responsive interface while
operations run.

Sources: Microsoft Learn desktop guides, WPF threading model, Windows app best practices and
accessibility overview, desktop modernization guidance.
