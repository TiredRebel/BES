---
title: TaskItem (entity) and TaskItemStatus (enum)
type: entity
status: approved
updated: 2026-09-18
related: ["[[spec]]", "[[employee]]", "[[br1-completed-at]]", "[[br2-due-not-before-start]]", "[[br3-inactive-assignee]]", "[[br4-final-statuses]]", "[[br5-no-self-assignment]]"]
---

# TaskItem

A unit of work created by one employee and assigned to another. Named `TaskItem` (not `Task`) and its status enum
`TaskItemStatus` (not `TaskStatus`) to avoid `System.Threading.Tasks`. Full contract: [[spec]] §3.2–§3.4, §5, §10–§12.

- Code: `src/TaskManagement.Domain/TaskItem.cs`, `TaskItemStatus.cs`, `BusinessRuleViolationException.cs`
  (implemented at fan-in A; tests in `tests/TaskManagement.UnitTests/TaskItemCreateTests.cs` and
  `TaskItemChangeStatusTests.cs`). Implemented at fan-in B: mapping in `src/TaskManagement.Infrastructure/Configurations/TaskItemConfiguration.cs`;
  use cases in `src/TaskManagement.Application/TaskService.cs`.
- Table: `tasks`, PK `pk_tasks`.

| Property | CLR type | Column | Rules |
|---|---|---|---|
| `Id` | `Guid` | `id uuid` | `Guid.CreateVersion7()` in `Create` |
| `Title` | `string` | `title varchar(200)` | required, trimmed, max `TitleMaxLength` = 200 |
| `Status` | `TaskItemStatus` | `status varchar(20)` | `New` on create; text; `ck_tasks_status_valid` |
| `CreatorId` | `Guid` | `creator_id uuid` | FK `fk_tasks_employees_creator_id`, RESTRICT |
| `AssigneeId` | `Guid` | `assignee_id uuid` | FK `fk_tasks_employees_assignee_id`, RESTRICT; ≠ `CreatorId` ([[br5-no-self-assignment]]); set once |
| `PlannedStartAt` | `DateTimeOffset?` | `planned_start_at timestamptz` | UTC ([[br2-due-not-before-start]]) |
| `DueAt` | `DateTimeOffset?` | `due_at timestamptz` | UTC, the deadline, ≥ `PlannedStartAt` when both set |
| `CompletedAt` | `DateTimeOffset?` | `completed_at timestamptz` | set iff `Completed` ([[br1-completed-at]]) |
| `Version` | `uint` | `xmin` (system) | concurrency token; never written by the domain |

## Operations

- `TaskItem.Create(string title, Employee creator, Employee assignee, DateTimeOffset? plannedStartAt, DateTimeOffset? dueAt)`
  guards in order: title, null employees, UTC normalisation, BR5, BR3, BR2.
- `ChangeStatus(TaskItemStatus newStatus, DateTimeOffset changedAt)`: unknown enum value, then BR4, then no-op if
  unchanged, then apply (sets or clears `CompletedAt`, BR1).
- There is no reassign, rename, reschedule or delete operation: the brief's use cases do not need one.

## Statuses

`New = 0`, `InProgress = 1`, `Completed = 2`, `Cancelled = 3`. `Completed` and `Cancelled` are final
([[br4-final-statuses]]). Any change from `New` or `InProgress` is allowed, including to the same status (no-op).
The 16-row table is in [[spec]] §5.

## Indexes

`ix_tasks_assignee_id_status` (assignee + status), `ix_tasks_due_at` (deadline), `ix_tasks_creator_id` (FK).
