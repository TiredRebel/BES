---
title: "BR3: an inactive employee can't be given a new task"
type: rule
status: approved
updated: 2026-09-18
related: ["[[spec]]", "[[employee]]", "[[task-item]]"]
---

# BR3: an inactive employee can't be given a new task

"Given a new task" means: a `TaskItem` is created with that employee as `AssigneeId`. Creation is the only time
`AssigneeId` is set (no reassignment), so BR3 is checked at creation, in two places as the brief requires.
The creator's status is not checked, and tasks an employee already had before deactivation stay valid.

| Layer | Where (planned) | How |
|---|---|---|
| Application | `TaskService.CreateTaskAsync` in `src/TaskManagement.Application/TaskService.cs` | loads the assignee row, `!IsActive` → `BusinessRuleViolationException` (`RuleId == "BR3"`) before calling the domain |
| Domain | `TaskItem.Create` (guard 6) | `!assignee.IsActive` → same exception, so no caller can skip it |
| Database | none | a row-local CHECK cannot read `employees.is_active`; triggers were not requested ([ADR 0004](../../docs/adr/0004-no-db-constraint-for-br3-br4.md)) |

Known gap: a deactivation committed between the service's read and its insert is not detected. Out of scope.

Tests ([[spec]] §15): `Create_InactiveAssignee_ThrowsBusinessRuleViolationBR3`, `Create_InactiveCreatorActiveAssignee_Succeeds`,
`CreateTaskAsync_InactiveAssignee_ThrowsBR3AndPersistsNothing`,
`CreateTaskAsync_InactiveEmployeeAsCreatorAndAssignee_ThrowsBR3FromServiceCheck` (proves the service half). Precondition
(not a BR3 test): `Deactivate_ActiveEmployee_SetsIsActiveFalse`. Seed employee Carol,
`10000000-0000-0000-0000-000000000003`, is inactive.
