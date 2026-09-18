---
title: "BR5: a task can't be assigned to its creator"
type: rule
status: draft
updated: 2026-09-18
related: ["[[spec]]", "[[task-item]]", "[[employee]]"]
---

# BR5: a task can't be assigned to its creator (assignee ≠ creator)

Decided in the brief: this is the meaning of "assigned to oneself".

| Layer | Where (planned) | How |
|---|---|---|
| Domain | `TaskItem.Create` (guard 5) in `src/TaskManagement.Domain/TaskItem.cs` | `creator.Id == assignee.Id` → `BusinessRuleViolationException` (`RuleId == "BR5"`) |
| Database | `ck_tasks_br5_assignee_not_creator` on `tasks` | `assignee_id <> creator_id` → SqlState `23514` |
| Application | `TaskService.CreateTaskAsync` | via the domain |

The CHECK is possible only because assignee and creator live on the same row
([ADR 0002](../../docs/adr/0002-assignee-column-on-task.md)). Both columns are non-null, so the CHECK never sees NULL.

Tests ([[spec]] §15): `Create_AssigneeIsCreator_ThrowsBusinessRuleViolationBR5`, `Insert_AssigneeEqualsCreator_RejectedByBR5Check`,
`CreateTaskAsync_AssigneeIsCreator_ThrowsBR5`.
