# Task Management: data layer

Task Management is the internal CRM module for assigning, executing and controlling employees' tasks. This repository is its data layer on PostgreSQL + EF Core: domain entities, persistence, an application service, and tests. Scope is backend/data only: no web API, no UI, no server host.

| Job | Use case | Implementation |
|---|---|---|
| Assigning: a creator gives a task to an employee | `CreateTaskAsync` | domain creation guards, application service; enforces BR2, BR3, BR5 |
| Executing: the task moves through its statuses | `ChangeTaskStatusAsync` | domain status transition logic; enforces BR1, BR4 |
| Controlling: what does an employee have, in which status, due when | `ListTasksByAssigneeAsync` (optional status filter, ordered by deadline) | application service query; uses `ix_tasks_assignee_id_status` index |

## Solution layout

| Project (path) | Holds |
|---|---|
| `src/TaskManagement.Domain` | entities (`Employee`, `TaskItem`), enum (`TaskItemStatus`), domain exception (`BusinessRuleViolationException`) |
| `src/TaskManagement.Infrastructure` | `TaskManagementDbContext`, Fluent configurations, seed data (via `HasData`), migration `InitialCreate`, design-time factory |
| `src/TaskManagement.Application` | `TaskService` with the three use cases: `CreateTaskAsync`, `ChangeTaskStatusAsync`, `ListTasksByAssigneeAsync` |
| `tests/TaskManagement.UnitTests` | domain unit tests (34 test methods, 54 test cases after theory expansion); references Domain only |
| `tests/TaskManagement.IntegrationTests` | PostgreSQL database tests and service integration tests (33 test methods, 34 test cases); references Domain, Infrastructure, Application; runs against Testcontainers |

## Prerequisites

- **.NET SDK 10.0.401** (LTS), target framework `net10.0`. Verify: `dotnet --list-sdks`.
- **Docker** (for integration tests). Required image: `postgres:17-alpine`.
- **dotnet-ef 10.0.12** global tool. Install: `dotnet tool install --global dotnet-ef --version 10.0.12`.
- **PostgreSQL** (optional, only if applying migrations to your own database server).
- **Package versions** are pinned in the csproj files. Key dependencies: Microsoft.EntityFrameworkCore 10.0.12; Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3; xunit 2.9.3; Testcontainers.PostgreSql 4.15.0. See `AGENTS.md` stack table for complete list.

## Migrations

Add a new migration (if needed):

```bash
dotnet ef migrations add <Name> --project src/TaskManagement.Infrastructure
```

List migrations without connecting to a database:

```bash
dotnet ef migrations list --project src/TaskManagement.Infrastructure --no-connect
```

Apply migrations to a database:

```bash
dotnet ef database update --project src/TaskManagement.Infrastructure --connection "<connection string>"
```

The single migration `InitialCreate` creates all tables (`employees`, `tasks`), primary and foreign keys, indexes, CHECK constraints, the BR3/BR4 trigger, and seed rows.

## Tests

Build with warnings treated as errors:

```bash
dotnet build -warnaserror
```

Run unit tests only (no Docker required):

```bash
dotnet test --filter Category=Unit
```

Run all tests (54 unit test cases, 34 integration test cases; requires Docker running):

```bash
dotnet test
```

## Where each business rule is enforced

| Rule | Statement | Domain | Database | Application service |
|---|---|---|---|---|
| BR1 | `CompletedAt` is required when, and only when, status = `Completed` | `TaskItem.Create` sets `CompletedAt = null`; `TaskItem.ChangeStatus` sets it to `changedAt` (UTC) iff new status is `Completed`, else `null` | `ck_tasks_br1_completed_at_iff_completed` | `TaskService.ChangeTaskStatusAsync` passes `timeProvider.GetUtcNow()` as `changedAt` |
| BR2 | `DueAt` cannot be earlier than `PlannedStartAt` (nullable; rule applies only when both are set) | `TaskItem.Create` guard: after UTC normalisation, `dueAt < plannedStartAt` → exception | `ck_tasks_br2_due_at_not_before_planned_start_at` | `TaskService.CreateTaskAsync` via domain |
| BR3 | An inactive employee cannot be given a new task | `TaskItem.Create` guard: `!assignee.IsActive` → exception | `trg_tasks_br3_br4` trigger on INSERT: reads assignee `is_active` under `FOR SHARE`; inactive → `trg_tasks_br3_assignee_active` error | `TaskService.CreateTaskAsync` service check before domain; rethrows trigger error as `BusinessRuleViolationException("BR3", …, inner)` |
| BR4 | `Completed` and `Cancelled` are final (no transition out, including to self) | `TaskItem.ChangeStatus` guard: if current status is `Completed` or `Cancelled` → exception; nothing changes | `trg_tasks_br3_br4` trigger on `UPDATE OF status`: final status change attempt → `trg_tasks_br4_final_status` error; `xmin` concurrency token prevents concurrent writes | `TaskService.ChangeTaskStatusAsync` via domain; `DbUpdateConcurrencyException` propagates |
| BR5 | A task cannot be assigned to its creator (`assignee_id ≠ creator_id`) | `TaskItem.Create` guard: `creator.Id == assignee.Id` → exception | `ck_tasks_br5_assignee_not_creator` | `TaskService.CreateTaskAsync` via domain |

## Failing-then-passing tests per rule

| Rule | Tests | Red evidence |
|---|---|---|
| BR1 | `Create_ValidInput_StartsAsNewWithNullCompletedAt`, `ChangeStatus_TransitionTableRow_BehavesAsSpecified`, `ChangeStatus_ToCompleted_SetsCompletedAtToChangedAt`, `ChangeStatus_ToCompletedWithNonUtcOffset_StoresUtcInstant`, `Insert_CompletedWithoutCompletedAt_RejectedByBR1Check`, `Insert_NewWithCompletedAt_RejectedByBR1Check`, `Update_ClearCompletedAtOfCompletedTask_RejectedByBR1Check`, `ChangeTaskStatusAsync_NewToCompleted_PersistsStatusAndCompletedAt` | Domain: remove `CompletedAt` assignment in `ChangeStatus` → 5 tests red. Database: drop `ck_tasks_br1_completed_at_iff_completed` → 3 DB tests + schema test red. |
| BR2 | `Create_DueAtBeforePlannedStartAt_ThrowsBusinessRuleViolationBR2`, `Create_DueAtEqualsPlannedStartAt_Succeeds`, `Create_PlannedStartAtOrDueAtMissing_Succeeds`, `Create_DueAtBeforePlannedStartAtAfterUtcConversion_ThrowsBusinessRuleViolationBR2`, `Insert_DueAtBeforePlannedStartAt_RejectedByBR2Check`, `Insert_DueAtEqualsPlannedStartAt_Accepted`, `CreateTaskAsync_DueAtBeforePlannedStartAt_ThrowsBR2` | Domain: remove BR2 guard in `Create` → 2 tests red. Database: drop `ck_tasks_br2_due_at_not_before_planned_start_at` → BR2 DB test + schema test red. |
| BR3 | `Create_InactiveAssignee_ThrowsBusinessRuleViolationBR3`, `Create_InactiveCreatorActiveAssignee_Succeeds`, `Insert_TaskForInactiveAssignee_RejectedByBR3Trigger`, `Insert_TaskForUnknownAssignee_RejectedByForeignKeyNotBR3Trigger`, `CreateTaskAsync_InactiveAssignee_ThrowsBR3AndPersistsNothing`, `CreateTaskAsync_InactiveEmployeeAsCreatorAndAssignee_ThrowsBR3FromServiceCheck`, `CreateTaskAsync_AssigneeDeactivatedAfterRead_ThrowsBR3FromTrigger` | Domain: remove BR3 guard in `Create` → 1 test red. Database: trigger not created → `Insert_TaskForInactiveAssignee_RejectedByBR3Trigger`, `CreateTaskAsync_AssigneeDeactivatedAfterRead_ThrowsBR3FromTrigger` and the schema test red. Service: remove BR3 check → only `CreateTaskAsync_InactiveEmployeeAsCreatorAndAssignee_ThrowsBR3FromServiceCheck` red; remove trigger-error translation → only `CreateTaskAsync_AssigneeDeactivatedAfterRead_ThrowsBR3FromTrigger` red. |
| BR4 | `ChangeStatus_TransitionTableRow_BehavesAsSpecified` (8 disallowed rows), `ChangeStatus_FromCompleted_KeepsStatusAndCompletedAt`, `ChangeTaskStatusAsync_CompletedTask_ThrowsBR4AndLeavesRowUnchanged`, `ConcurrentStatusChange_SecondSave_ThrowsDbUpdateConcurrencyException`, `Update_StatusOfCompletedTask_RejectedByBR4Trigger` | Domain: remove BR4 guard in `ChangeStatus` → 9 tests red (8 disallowed transition rows + 1 single-row test). Database: trigger not created → `Update_StatusOfCompletedTask_RejectedByBR4Trigger` and the schema test red. (`ConcurrentStatusChange_SecondSave_ThrowsDbUpdateConcurrencyException` tests the `xmin` token, which stays in place.) |
| BR5 | `Create_AssigneeIsCreator_ThrowsBusinessRuleViolationBR5`, `Insert_AssigneeEqualsCreator_RejectedByBR5Check`, `CreateTaskAsync_AssigneeIsCreator_ThrowsBR5` | Domain: remove BR5 guard in `Create` → 1 test red. Database: drop `ck_tasks_br5_assignee_not_creator` → BR5 DB test + schema test red. |

Every test above was run red with its guard, CHECK constraint, or trigger removed, and green with it restored (confirmed in fan-in A and fan-in B log entries).

## Seed data

The seed rows are **demo data**. They live in the `InitialCreate` migration (via Fluent `HasData`), so every database the migration runs against gets them. **Do not apply this migration to production as-is** without reviewing and replacing the seed rows.

**Employees** (3 rows):
- Alice Morgan (`10000000-0000-0000-0000-000000000001`), email `alice.morgan@example.com`, active
- Bob Chen (`10000000-0000-0000-0000-000000000002`), email `bob.chen@example.com`, active
- Carol Diaz (`10000000-0000-0000-0000-000000000003`), email `carol.diaz@example.com`, inactive (for BR3 testing)

**Tasks** (3 rows):
- `20000000-0000-0000-0000-000000000001`: "Prepare Q4 sales report", `New`, created by Alice, assigned to Bob, planned 2026-10-01 09:00:00Z, due 2026-10-10 17:00:00Z
- `20000000-0000-0000-0000-000000000002`: "Call back key account", `Completed`, created by Bob, assigned to Alice, planned 2026-09-01 09:00:00Z, due 2026-09-05 17:00:00Z, completed 2026-09-04 15:30:00Z
- `20000000-0000-0000-0000-000000000003`: "Clean up duplicate contacts", `Cancelled`, created by Alice, assigned to Bob, no planned start, no due date

## Documentation

- **AGENTS.md**: Instructions for coding agents (Claude Code, Cursor, Codex, Antigravity). Read first every session.
- **.wiki/index.md**: Start here. Maps the domain spec, business rule pages, ADRs, the execution graph (`plan/graph.yaml`), the session log, the code map (built with CodeGraph) and the automatic progress checkpoints (`.wiki/checkpoints/`).
- **.wiki/domain/spec.md**: Complete data model specification (N1 spec), with entities, constraints, indexes, trigger SQL, test plan, and verification.
- **.wiki/domain/br1-completed-at.md, br2-due-not-before-start.md, br3-inactive-assignee.md, br4-final-statuses.md, br5-no-self-assignment.md**: Business rule pages (Layer | Where | How tables).
- **docs/adr/**: Nine Architecture Decision Records (0001–0009). ADR 0009 supersedes ADR 0004. 0001 solution layout, 0002 assignee column, 0003 UUIDv7 keys, 0004 no DB constraint for BR3/BR4 (superseded), 0005 e-mail uniqueness, 0006 UTC and explicit time, 0007 status as text and the transition rule, 0008 the business-rule exception, 0009 the BR3/BR4 trigger.
