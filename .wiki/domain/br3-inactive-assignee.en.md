---
title: "BR3: an inactive employee can't be given a new task"
type: rule
status: approved
updated: 2026-09-18
related: ["[[spec]]", "[[employee]]", "[[task-item]]"]
---

# BR3: an inactive employee can't be given a new task

[ Українська ](br3-inactive-assignee.md) · [ **English** ]

"Given a new task" means: a `TaskItem` is created with that employee as `AssigneeId`. Creation is the only time
`AssigneeId` is set (no reassignment), so BR3 is checked at creation, in two places as the brief requires.
The creator's status is not checked, and tasks an employee already had before deactivation stay valid.

| Layer | Where (implemented: domain at fan-in A, database and service at fan-in B) | How |
|---|---|---|
| Application | `TaskService.CreateTaskAsync` in `src/TaskManagement.Application/TaskService.cs` | loads the assignee row, `!IsActive` → `BusinessRuleViolationException` (`RuleId == "BR3"`) before calling the domain |
| Domain | `TaskItem.Create` (guard 6) | `!assignee.IsActive` → same exception, so no caller can skip it |
| Database | trigger `trg_tasks_br3_br4` on insert (in `src/TaskManagement.Infrastructure/Migrations/20260918164447_InitialCreate.cs`) | reads the assignee's `is_active` under `FOR SHARE`; an inactive assignee → `23514` / `trg_tasks_br3_assignee_active`; the service rethrows it as `BusinessRuleViolationException("BR3")` ([ADR 0009](../../docs/adr/0009-br3-br4-enforced-by-trigger.md)) |

The trigger's `FOR SHARE` lock closes the race between the service's read and its insert: a concurrent deactivation
waits for the insert, and a deactivation committed before it makes the insert fail as BR3.

Tests ([[spec]] §15): `Create_InactiveAssignee_ThrowsBusinessRuleViolationBR3`, `Create_InactiveCreatorActiveAssignee_Succeeds`,
`Insert_TaskForInactiveAssignee_RejectedByBR3Trigger`, `Insert_TaskForUnknownAssignee_RejectedByForeignKeyNotBR3Trigger`,
`CreateTaskAsync_AssigneeDeactivatedAfterRead_ThrowsBR3FromTrigger`, `CreateTaskAsync_InactiveAssignee_ThrowsBR3AndPersistsNothing`,
`CreateTaskAsync_InactiveEmployeeAsCreatorAndAssignee_ThrowsBR3FromServiceCheck` (proves the service half). Precondition
(not a BR3 test): `Deactivate_ActiveEmployee_SetsIsActiveFalse`. Seed employee Carol,
`10000000-0000-0000-0000-000000000003`, is inactive.
