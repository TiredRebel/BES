---
okf_version: "0.2"
id: codemap
title: Карта коду та синтаксичний граф (Code Map)
type: CodeMap
description: Матеріалізований граф синтаксичних символів, залежностей та зв'язків компонентів з індексу CodeGraph
status: active
updated: 2026-10-02
tags: [okf, codemap, codegraph, ast, architecture]
related: ["index.md", "domain/spec.md"]
---

# Code map

Generated from the CodeGraph index (`codegraph sync .`, CodeGraph 1.6.0) after fan-in B; test counts updated after D4 (review fixes). Regenerated after every fan-in. Source of truth for contracts: [[spec]].

## Index

21 files, 289 nodes, 595 edges (after the final D4 fixes; per-kind counts as of fan-in B: method:106, import:56, class:20, file:20, namespace:19, property:16, constant:13, field:12, function:10, enum_member:4, variable:3, enum:1). Includes `.claude/hooks/wiki_checkpoint.py` (tooling).

## Projects

| Project | Path | Project references | Package references |
|---|---|---|---|
| TaskManagement.Domain | src/TaskManagement.Domain | none | none |
| TaskManagement.Infrastructure | src/TaskManagement.Infrastructure | TaskManagement.Domain | Microsoft.EntityFrameworkCore 10.0.12, Microsoft.EntityFrameworkCore.Relational 10.0.12, Microsoft.EntityFrameworkCore.Design 10.0.12, Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3 |
| TaskManagement.Application | src/TaskManagement.Application | TaskManagement.Domain, TaskManagement.Infrastructure | none |
| TaskManagement.Wpf | src/TaskManagement.Wpf | TaskManagement.Application, TaskManagement.Infrastructure | CommunityToolkit.Mvvm 8.4.2, Microsoft.Extensions.Hosting 10.0.12 |
| TaskManagement.UnitTests | tests/TaskManagement.UnitTests | TaskManagement.Domain | Microsoft.NET.Test.Sdk 17.14.1, xunit 2.9.3, xunit.runner.visualstudio 3.1.4 |
| TaskManagement.Wpf.UnitTests | tests/TaskManagement.Wpf.UnitTests | TaskManagement.Wpf | Microsoft.NET.Test.Sdk 17.14.1, xunit 2.9.3, xunit.runner.visualstudio 3.1.4 |
| TaskManagement.IntegrationTests | tests/TaskManagement.IntegrationTests | TaskManagement.Domain, TaskManagement.Infrastructure, TaskManagement.Application | Microsoft.NET.Test.Sdk 17.14.1, Testcontainers.PostgreSql 4.15.0, xunit 2.9.3, xunit.runner.visualstudio 3.1.4 |

## Namespaces and types

### TaskManagement.Domain
| Type | Kind | File | Members |
|---|---|---|---|
| Employee | class | src/TaskManagement.Domain/Employee.cs | FullNameMaxLength, EmailMaxLength, Id, FullName, Email, IsActive, Create, Deactivate |
| TaskItem | class | src/TaskManagement.Domain/TaskItem.cs | TitleMaxLength, Id, Title, Status, CreatorId, AssigneeId, PlannedStartAt, DueAt, CompletedAt, Version, Create, ChangeStatus |
| TaskItemStatus | enum | src/TaskManagement.Domain/TaskItemStatus.cs | New, InProgress, Completed, Cancelled |
| BusinessRuleViolationException | class | src/TaskManagement.Domain/BusinessRuleViolationException.cs | RuleId |

### TaskManagement.Infrastructure (+ .Configurations, .Migrations)
| Type | Kind | File | Members / purpose |
|---|---|---|---|
| TaskManagementDbContext | class | src/TaskManagement.Infrastructure/TaskManagementDbContext.cs | Employees, Tasks, OnModelCreating |
| TaskManagementDbContextFactory | class | src/TaskManagement.Infrastructure/TaskManagementDbContextFactory.cs | CreateDbContext |
| EmployeeConfiguration | class | src/TaskManagement.Infrastructure/Configurations/EmployeeConfiguration.cs | Configure |
| TaskItemConfiguration | class | src/TaskManagement.Infrastructure/Configurations/TaskItemConfiguration.cs | Configure |
| InitialCreate | EF Core migration | src/TaskManagement.Infrastructure/Migrations/ | 20260918164447_InitialCreate.cs, 20260918164447_InitialCreate.Designer.cs, TaskManagementDbContextModelSnapshot.cs |

### TaskManagement.Application
| Type | Kind | File | Members |
|---|---|---|---|
| TaskService | class | src/TaskManagement.Application/TaskService.cs | dbContext, timeProvider, CreateTaskAsync, ChangeTaskStatusAsync, ListTasksByAssigneeAsync |

### TaskManagement.Wpf
| Type | Kind | File | Members |
|---|---|---|---|
| App | class | src/TaskManagement.Wpf/App.xaml.cs | OnStartup (host, DI, `AddDbContextFactory`), OnExit, last-resort exception handler |
| ITaskClient | interface | src/TaskManagement.Wpf/Services/ITaskClient.cs | GetEmployeesAsync, ListTasksAsync, CreateTaskAsync, ChangeStatusAsync, ReassignAsync, GetHistoryAsync |
| TaskClient | class | src/TaskManagement.Wpf/Services/TaskClient.cs | PageSize; implements ITaskClient with one context per call over TaskService |
| IDialogService | interface | src/TaskManagement.Wpf/Services/IDialogService.cs | ShowCreateTask |
| DialogService | class | src/TaskManagement.Wpf/Services/DialogService.cs | ShowCreateTask |
| EmployeeOption | record | src/TaskManagement.Wpf/Services/ViewData.cs | Id, FullName, IsActive, DisplayName |
| TaskRow | record | src/TaskManagement.Wpf/Services/ViewData.cs | Id, Title, Status, AssigneeId, AssigneeName, PlannedStartAt, DueAt, CompletedAt |
| TaskPage | record | src/TaskManagement.Wpf/Services/ViewData.cs | Items, NextCursor |
| HistoryRow | record | src/TaskManagement.Wpf/Services/ViewData.cs | ChangeType, OldValue, NewValue, ChangedAt, AuthorName |
| MainViewModel | class | src/TaskManagement.Wpf/ViewModels/MainViewModel.cs | Employees, Tasks, History, StatusFilters, StatusOptions, ActingEmployee, FilterAssignee, SelectedStatusFilter, SelectedTask, NewStatus, NewAssignee, IsBusy, ErrorMessage, PageNumber, LoadCommand, RefreshCommand, NextPageCommand, PreviousPageCommand, CreateTaskCommand, ChangeStatusCommand, ReassignCommand, DismissErrorCommand |
| StatusFilter | record | src/TaskManagement.Wpf/ViewModels/MainViewModel.cs | Label, Value |
| CreateTaskViewModel | class | src/TaskManagement.Wpf/ViewModels/CreateTaskViewModel.cs | Employees, Title, Creator, Assignee, PlannedStartDate, DueDate, IsBusy, ErrorMessage, HasError, Created, Initialize, SaveCommand |
| MainWindow | class | src/TaskManagement.Wpf/Views/MainWindow.xaml.cs | constructor runs LoadCommand on Loaded |
| CreateTaskWindow | class | src/TaskManagement.Wpf/Views/CreateTaskWindow.xaml.cs | ViewModel; closes on Created |
| DesignData | class | src/TaskManagement.Wpf/DesignTime/DesignData.cs | Main, CreateTask (design-time view models over a canned ITaskClient) |

### Tests
| Test class | Project | File | Test methods |
|---|---|---|---|
| EmployeeTests | TaskManagement.UnitTests | tests/TaskManagement.UnitTests/EmployeeTests.cs | 13 |
| TaskItemCreateTests | TaskManagement.UnitTests | tests/TaskManagement.UnitTests/TaskItemCreateTests.cs | 18 |
| TaskItemChangeStatusTests | TaskManagement.UnitTests | tests/TaskManagement.UnitTests/TaskItemChangeStatusTests.cs | 5 |
| MainViewModelTests | TaskManagement.Wpf.UnitTests | tests/TaskManagement.Wpf.UnitTests/MainViewModelTests.cs | 14 |
| CreateTaskViewModelTests | TaskManagement.Wpf.UnitTests | tests/TaskManagement.Wpf.UnitTests/CreateTaskViewModelTests.cs | 4 |
| MigrationAndSeedTests | TaskManagement.IntegrationTests | tests/TaskManagement.IntegrationTests/MigrationAndSeedTests.cs | 4 |
| DatabaseConstraintTests | TaskManagement.IntegrationTests | tests/TaskManagement.IntegrationTests/DatabaseConstraintTests.cs | 14 |
| TaskServiceTests | TaskManagement.IntegrationTests | tests/TaskManagement.IntegrationTests/TaskServiceTests.cs | 19 |
| PostgresFixture | TaskManagement.IntegrationTests | tests/TaskManagement.IntegrationTests/PostgresFixture.cs | fixture |

## Dependencies

| From | To | Via |
|---|---|---|
| TaskServiceTests | TaskService | CreateTaskAsync |
| TaskServiceTests | TaskService | ChangeTaskStatusAsync |
| TaskServiceTests | TaskService | ListTasksByAssigneeAsync |
| TaskItemChangeStatusTests | TaskItem | ChangeStatus |
| TaskService | TaskManagementDbContext | constructor injection |
| TaskItem | Employee | `Create(creator, assignee)` parameters (no navigations) |
| TaskItem | TaskItemStatus | Status property |
| TaskItem | BusinessRuleViolationException | thrown by Create (BR2, BR3, BR5) and ChangeStatus (BR4) |
| TaskItemConfiguration | TaskItem | Configure |
| EmployeeConfiguration | Employee | Configure |
| TaskManagementDbContext | TaskItemConfiguration | OnModelCreating |
| TaskManagementDbContext | EmployeeConfiguration | OnModelCreating |
| DatabaseConstraintTests | TaskManagementDbContext | raw SQL via `Database.ExecuteSqlRawAsync`; EF for the concurrency test |
| MigrationAndSeedTests | TaskManagementDbContext | migrations API and seed queries |
| PostgresFixture | TaskManagementDbContext | creates a migrated database and context per test |
| EmployeeTests | Employee | Create, Deactivate |
| TaskItemCreateTests | TaskItem, Employee | TaskItem.Create, Employee.Create |
| TaskService | TaskItem | Create, ChangeStatus |

## Diagram

```mermaid
flowchart LR
  subgraph Domain["TaskManagement.Domain"]
    Employee
    TaskItem
    TaskItemStatus
    BusinessRuleViolationException
  end
  subgraph Infrastructure["TaskManagement.Infrastructure"]
    TaskManagementDbContext
    TaskManagementDbContextFactory
    EmployeeConfiguration
    TaskItemConfiguration
    InitialCreate
  end
  subgraph Application["TaskManagement.Application"]
    TaskService
  end
  subgraph Tests["tests"]
    EmployeeTests
    TaskItemCreateTests
    TaskItemChangeStatusTests
    MigrationAndSeedTests
    DatabaseConstraintTests
    TaskServiceTests
    PostgresFixture
  end
  TaskItem --> Employee
  TaskItem --> TaskItemStatus
  TaskItem --> BusinessRuleViolationException
  TaskItemConfiguration --> TaskItem
  EmployeeConfiguration --> Employee
  TaskManagementDbContext --> TaskItemConfiguration
  TaskManagementDbContext --> EmployeeConfiguration
  TaskService --> TaskManagementDbContext
  TaskService --> TaskItem
  TaskServiceTests --> TaskService
  TaskItemChangeStatusTests --> TaskItem
  DatabaseConstraintTests --> TaskManagementDbContext
  EmployeeTests --> Employee
  TaskItemCreateTests --> TaskItem
  MigrationAndSeedTests --> TaskManagementDbContext
  PostgresFixture --> TaskManagementDbContext
  Application -.-> Domain
  Application -.-> Infrastructure
  Infrastructure -.-> Domain
  Tests -.-> Domain
  Tests -.-> Infrastructure
  Tests -.-> Application
```
