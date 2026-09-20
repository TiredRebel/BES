---
okf_version: "0.2"
id: rule-br5-en
title: "BR5: a task can't be assigned to its creator"
type: BusinessRule
description: Business rule BR5 - task assignee cannot be the same person as creator (assignee != creator)
status: approved
updated: 2026-09-20
tags: [okf, domain, business-rule, br5, validation, database-check]
related: ["spec.en.md", "task-item.en.md", "employee.en.md"]
---

# BR5: a task can't be assigned to its creator (assignee ≠ creator)

[ Українська ](br5-no-self-assignment.md) · [ **English** ]

Decided in the brief: this is the meaning of "assigned to oneself".

| Layer | Where (implemented: domain at fan-in A, database and service at fan-in B) | How |
|---|---|---|
| Domain | `TaskItem.Create` (guard 5) in `src/TaskManagement.Domain/TaskItem.cs` | `creator.Id == assignee.Id` → `BusinessRuleViolationException` (`RuleId == "BR5"`) |
| Database | `ck_tasks_br5_assignee_not_creator` on `tasks` | `assignee_id <> creator_id` → SqlState `23514` |
| Application | `TaskService.CreateTaskAsync` | via the domain |

The CHECK is possible only because assignee and creator live on the same row
([ADR 0002](../../docs/adr/0002-assignee-column-on-task.md)). Both columns are non-null, so the CHECK never sees NULL.

Tests ([[spec]] §15): `Create_AssigneeIsCreator_ThrowsBusinessRuleViolationBR5`, `Create_InactiveEmployeeAsCreatorAndAssignee_ThrowsBusinessRuleViolationBR5` (guard order), `Insert_AssigneeEqualsCreator_RejectedByBR5Check`,
`CreateTaskAsync_AssigneeIsCreator_ThrowsBR5`.
