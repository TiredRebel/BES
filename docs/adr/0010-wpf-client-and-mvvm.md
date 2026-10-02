# 0010. WPF client over the data layer, MVVM with CommunityToolkit.Mvvm

Status: accepted (human, Phase 1 gate of `WPF_CLIENT_PROMPT.md`, 2026-10-02). Task description and an "all tasks" view are dropped (human decision at the same gate).

## Context

The data layer (Domain, Infrastructure, Application) is finished and is not to change. A desktop client is needed
to exercise its use cases by hand: list tasks, create a task, change status, reassign, and read the change history.
The binding guidelines are `docs/guidelines/01`–`04`: thin views, view models with commands and busy/error state,
no UI types below the presentation layer, async all the way, errors surfaced at the command boundary.

Facts read from the code that shape the design:

- `TaskService(TaskManagementDbContext dbContext, TimeProvider timeProvider)` is sealed, has no interface (by
  design: one implementation), and takes one context for its whole lifetime.
- `ChangeTaskStatusAsync` has two overloads. The 2-argument one records the current assignee as the author; only
  `ChangeTaskStatusAsync(Guid taskId, TaskItemStatus newStatus, Guid changedById, CancellationToken)` records the
  acting employee.
- `TaskListQuery.AssigneeId` is required. There is no "all tasks" listing; every page belongs to one assignee.
  Paging is forward-only keyset: `PagedResult<TaskItem>.NextCursor` (`TaskCursor(DueAt, Id)`).
- No service method lists employees. `TaskManagementDbContext.Employees` is public.
- `CreateTaskAsync(title, creatorId, assigneeId, plannedStartAt, dueAt, ct)` takes no description, and `TaskItem`
  has no description column.
- `TaskHistoryEntry.OldValue`/`NewValue` hold status names for `StatusChanged` and assignee GUIDs (as text) for
  `AssigneeChanged`; the author is `ChangedById`.

## Decision

1. **Project.** One new project `src/TaskManagement.Wpf` (`OutputType` `WinExe`, `TargetFramework`
   `net10.0-windows`, `UseWPF` true; the csproj value overrides `net10.0` from `Directory.Build.props`). It
   references `TaskManagement.Application` and `TaskManagement.Infrastructure`. Views and view models live in the
   same project (`Views/`, `ViewModels/`, `Services/`): a separate Presentation project buys nothing at this size
   (guideline 03: "Do NOT split into many projects merely to look clean"). The existing projects are untouched.
2. **Packages.** `CommunityToolkit.Mvvm` 8.4.2 and `Microsoft.Extensions.Hosting` 10.0.12 (latest stable on
   nuget.org, checked 2026-10-02). `Microsoft.Extensions.DependencyInjection` and
   `Microsoft.Extensions.Configuration.Json` 10.0.12 come with Hosting (its nuspec lists both), so they are not
   referenced separately. EF Core and Npgsql arrive through Infrastructure.
3. **MVVM.** `ObservableObject`, `[ObservableProperty]` and `[RelayCommand]` source generators from
   CommunityToolkit.Mvvm. No hand-rolled `INotifyPropertyChanged` base or `ICommand` class. Commands that touch
   the database are async (`AsyncRelayCommand`, `AllowConcurrentExecutions = false`); no `async void` outside
   framework event glue.
4. **One context per operation.** `AddDbContextFactory<TaskManagementDbContext>(o => o.UseNpgsql(...))` registers
   `IDbContextFactory<TaskManagementDbContext>`. Every client call does
   `await using var db = await factory.CreateDbContextAsync(ct)` and `new TaskService(db, timeProvider)`, then
   disposes the context before returning. No window or view model holds a `DbContext`: a long-lived context in a
   desktop app grows its change tracker forever and serves stale rows.
5. **Test seam in the client, not in the data layer.** One interface `ITaskClient` in the WPF project, with three
   implementations: `TaskClient` (real, the per-operation context above), a fake in the unit tests, and a
   design-time one for the XAML designer. `TaskClient` maps every entity to a small immutable record
   (`EmployeeOption`, `TaskRow`, `HistoryRow`) before the context is disposed, so view models never see an EF
   entity. `TaskCursor` passes through unchanged: it is an immutable Application record, not an entity.
6. **Employees.** `TaskClient` reads `Employees.AsNoTracking()` directly, because no service method exists and
   adding one would change the data layer. Read-only, no writes bypass `TaskService`. Inactive employees stay in
   every picker, labelled "(inactive)".
7. **History.** `TaskClient` resolves `ChangedById`, and the GUIDs in `AssigneeChanged` old/new values, to employee
   names. Status names are shown as stored.
8. **The UI does not re-implement BR1–BR5.** Command enablement depends only on: not busy, a task is selected, an
   acting employee is set. Final tasks can still be sent a status change (BR4), the creator stays in the reassign
   list (BR5), inactive employees stay pickable (BR3), dates are not compared (BR2). The service rejects these and
   the message bar shows the rejection. Only presentation validation that a request is well-formed stays (e.g. a
   picker has a value).
9. **Errors.** A command catches, in this order: `BusinessRuleViolationException`, `DbUpdateConcurrencyException`
   (before its base `DbUpdateException`), `DbUpdateException`, `KeyNotFoundException`, `ArgumentException` (blank
   or over-long title from the dialog), and `System.Data.Common.DbException` (database unreachable). The message
   goes to an inline, dismissible message bar, never a `MessageBox`; after a rule or concurrency error the list is
   reloaded, and if that reload fails the first message stays. A last-resort
   `Application.DispatcherUnhandledException` handler shows one message and shuts the app down, because state is
   unknown after an unexpected failure (guideline 02).
10. **Hosting.** `Host.CreateApplicationBuilder()` builds configuration (`appsettings.json`, connection string
    `ConnectionStrings:TaskManagement`) and DI. `App.OnStartup` resolves `MainWindow` from the container;
    `App.xaml` has no `StartupUri`. Views and view models are transient, `ITaskClient` and `TimeProvider.System`
    singletons. Dialogs open through a small `IDialogService` so view models never create windows (guideline 03).
11. **No authentication.** Out of scope. A toolbar combo box selects the acting employee; its id is passed as
    `changedById` to `ChangeTaskStatusAsync` (3-argument overload) and `ReassignTaskAsync`, and is the default
    creator in the create dialog.
12. **Local database.** `docker-compose.yml` at the repo root runs `postgres:17-alpine` (the image the integration
    tests use) with `POSTGRES_HOST_AUTH_METHOD=trust`, published on `127.0.0.1:5433` only (5433, not 5432, so it
    does not collide with a PostgreSQL already running on the default port). The connection string in
    `appsettings.json` then carries no password: the design-time factory's string with the port changed
    (`Host=localhost;Port=5433;Database=task_management;Username=postgres`). This is a deliberate local-development
    exception to guideline 02's "no connection strings in settings files": the file holds no secret. Migrations are
    applied with `dotnet ef database update`; the app never migrates on startup (AGENTS.md rule 3).
13. **Paging.** The list shows one assignee's page; status is an optional filter. "Next" uses `NextCursor`;
    "Previous" pops a stack of the cursors already used. Changing a filter resets to the first page.

## Consequences

- The data layer and its migrations are unchanged; the client is the only new production project.
- A stale grid does not raise `DbUpdateConcurrencyException`: each operation opens a fresh context and the service
  re-reads the row, so a change is applied to the current state of the task. Only a race between that read and the
  save within one call raises it. A "you were looking at an old version" check would need the `xmin` version in the
  service signature, which is a data-layer change and out of scope.
- No "all tasks" view and no task description, because the service exposes neither (see Context). Adding either
  is a data-layer change that needs its own decision.
- `ITaskClient` adds one interface to the solution; it has three implementations, so it is not a speculative
  abstraction.
- The WPF unit-test project targets `net10.0-windows` and runs only on Windows.
