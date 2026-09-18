---
title: "BR2: DueAt is not earlier than PlannedStartAt"
type: rule
status: approved
updated: 2026-09-18
related: ["[[spec]]", "[[task-item]]"]
---

# BR2: `DueAt` can't be earlier than `PlannedStartAt`

Both dates are nullable. The rule applies only when both are set; equal is allowed.

| Layer | Where (planned) | How |
|---|---|---|
| Domain | `TaskItem.Create` (guard 7) in `src/TaskManagement.Domain/TaskItem.cs` | after UTC normalisation, `dueAt < plannedStartAt` → `BusinessRuleViolationException` with `RuleId == "BR2"` |
| Database | `ck_tasks_br2_due_at_not_before_planned_start_at` on `tasks` | `planned_start_at IS NULL OR due_at IS NULL OR due_at >= planned_start_at` → SqlState `23514` |
| Application | `TaskService.CreateTaskAsync` | via the domain |

Dates cannot change after creation (no reschedule use case), so `Create` is the only domain entry point.

Tests ([[spec]] §15): `Create_DueAtBeforePlannedStartAt_ThrowsBusinessRuleViolationBR2`, `Create_DueAtEqualsPlannedStartAt_Succeeds`,
`Create_PlannedStartAtOrDueAtMissing_Succeeds`, `Create_DueAtBeforePlannedStartAtAfterUtcConversion_ThrowsBusinessRuleViolationBR2`,
`Insert_DueAtBeforePlannedStartAt_RejectedByBR2Check`, `Insert_DueAtEqualsPlannedStartAt_Accepted`,
`CreateTaskAsync_DueAtBeforePlannedStartAt_ThrowsBR2`.
