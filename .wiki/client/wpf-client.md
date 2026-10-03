---
okf_version: "0.2"
id: wpf-client
title: WPF client
type: Component
description: Desktop client over the Task Management data layer (ADR 0010)
status: active
updated: 2026-10-02
tags: [okf, wpf, mvvm, client]
related: ["../index.en.md", "../codemap.md", "../decisions/index.en.md"]
---

# WPF client

A lightweight desktop client for exercising the Task Management use cases against the data layer. Built with WPF and MVVM using CommunityToolkit.Mvvm, one short-lived `DbContext` per operation via `IDbContextFactory`.

## Purpose

The WPF client is a standalone presentation layer that demonstrates the full feature set of the data layer in a graphical user interface. It is not part of the production system and exists only for developer convenience and testing.

**Project:** `src/TaskManagement.Wpf` (`net10.0-windows`, `UseWPF` enabled).  
**Test project:** `tests/TaskManagement.Wpf.UnitTests` (18 view-model unit tests with fakes).  
**Architecture Decision:** [ADR 0010 - WPF client over the data layer, MVVM with CommunityToolkit.Mvvm](../../docs/adr/0010-wpf-client-and-mvvm.md).

## Run

**Prerequisites:** Windows, .NET SDK 10, Docker Desktop.

```bash
# Start the database
docker compose up -d

# Apply migrations and seed data
dotnet ef database update \
  --project src/TaskManagement.Infrastructure \
  --connection "Host=localhost;Port=5433;Database=task_management;Username=postgres"

# Run the client
dotnet run --project src/TaskManagement.Wpf
```

**Stop the database:**
```bash
docker compose down
```

The PostgreSQL database listens on `127.0.0.1:5433` (not the default 5432) to avoid collision with an existing PostgreSQL instance. Authentication is trust (no password); this is intentional for development.

## Structure

| File / Namespace | Responsibility |
|---|---|
| `App.xaml` / `App.xaml.cs` | XAML application root; registers services and DI container; sets main window. |
| `Views/MainWindow.xaml` / `Views/MainWindow.xaml.cs` | Task list view: grid of `TaskRow`, status/assignee filters, Previous/Next paging, message bar for BR violations. |
| `Views/CreateTaskWindow.xaml` / `Views/CreateTaskWindow.xaml.cs` | Dialog for creating a task: title, creator, assignee, planned start, due date. |
| `ViewModels/MainViewModel.cs` | State and commands for the main window: commands `Load`, `Refresh`, `NextPage`, `PreviousPage`, `CreateTask`, `ChangeStatus`, `Reassign`, `DismissError`; filter properties; busy/error state. |
| `ViewModels/CreateTaskViewModel.cs` | State and commands for the create-task dialog. |
| `Services/ITaskClient.cs` | Seam: abstraction over the Task Management service and database. |
| `Services/TaskClient.cs` | Real implementation: builds a `DbContext` per call, invokes `TaskService` methods, maps entities to records; exceptions pass through to the view models. |
| `Services/IDialogService.cs` | Seam: abstraction for showing dialogs. |
| `Services/DialogService.cs` | WPF implementation of dialog service. |
| `Services/ViewData.cs` | Immutable records: `EmployeeOption`, `TaskRow`, `TaskPage`, `HistoryRow`. Never expose EF entities to the presentation layer. |
| `DesignTime/DesignData.cs` | Fake `ITaskClient` for XAML design-time preview. |
| `appsettings.json` | Connection string: `ConnectionStrings:TaskManagement`. |

## Rules for the UI layer

1. **No EF entities in the presentation layer.** View models and views bind to immutable records (`TaskRow`, `HistoryRow`, `EmployeeOption`), never to `TaskItem` or `Employee`.
2. **Async everywhere.** All I/O operations are async; commands use `IAsyncRelayCommand`. View models track busy state and surface errors.
3. **Thin views.** Logic lives in view models. Views are XAML markup and code-behind for UI setup only (event handlers that immediately delegate to commands).
4. **One `DbContext` per operation.** Use `IDbContextFactory<TaskManagementDbContext>` to create a fresh context for each call. After the operation completes (or fails), the context is disposed.
5. **Business rules enforced at the data layer.** The service and database enforce BR1–BR5. When the service rejects a violation, the view model catches `BusinessRuleViolationException` (and `DbUpdateConcurrencyException`, `DbUpdateException`, `KeyNotFoundException`, `ArgumentException`, `DbException`) and shows its message in the message bar. The list is reloaded.
6. **Acting as:** An employee combo box records who is making changes. There is no authentication; the employee is selected by the user.

## Tests

`tests/TaskManagement.Wpf.UnitTests`: 18 view-model unit tests with fakes.

- `MainViewModelTests`: list loading, filtering, paging (Previous/Next), create, change status, reassign, error display.
- `CreateTaskViewModelTests`: validation, command execution, success and error cases.

All tests use `[Trait("Category", "Unit")]` and run without a database.

## Not included

- **Task description:** The data layer has no description column; the UI does not show it.
- **"All tasks" view:** `TaskListQuery.AssigneeId` is required; every page belongs to one assignee. There is no super-user view across all employees.
- **Task deletion:** The data layer has no delete operation; the UI does not expose one.
- **Authentication:** No login or identity provider; the "Acting as" combo box is for development convenience.
- **Web API:** This client is direct to the data layer. There is no intermediate API server.
