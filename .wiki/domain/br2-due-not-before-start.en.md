---
okf_version: "0.2"
id: rule-br2-en
title: "BR2: DueAt is not earlier than PlannedStartAt"
type: BusinessRule
description: Business rule BR2 - task deadline DueAt cannot be earlier than PlannedStartAt
status: approved
updated: 2026-09-20
tags: [okf, domain, business-rule, br2, validation, database-check]
related: ["spec.en.md", "task-item.en.md"]
---

# BR2: `DueAt` can't be earlier than `PlannedStartAt`

[ Українська ](br2-due-not-before-start.md) · [ **English** ]

Both dates are nullable. The rule applies only when both are set; equal is allowed.

| Layer | Where (implemented: domain at fan-in A, database and service at fan-in B) | How |
|---|---|---|
| Domain | `TaskItem.Create` (guard 7) in `src/TaskManagement.Domain/TaskItem.cs` | after UTC normalisation, `dueAt < plannedStartAt` → `BusinessRuleViolationException` with `RuleId == "BR2"` |
| Database | `ck_tasks_br2_due_at_not_before_planned_start_at` on `tasks` | `planned_start_at IS NULL OR due_at IS NULL OR due_at >= planned_start_at` → SqlState `23514` |
| Application | `TaskService.CreateTaskAsync` | via the domain |

Dates cannot change after creation (no reschedule use case), so `Create` is the only domain entry point.

Tests ([[spec]] §15): `Create_DueAtBeforePlannedStartAt_ThrowsBusinessRuleViolationBR2`, `Create_DueAtEqualsPlannedStartAt_Succeeds`,
`Create_PlannedStartAtOrDueAtMissing_Succeeds`, `Create_DueAtBeforePlannedStartAtAfterUtcConversion_ThrowsBusinessRuleViolationBR2`,
`Insert_DueAtBeforePlannedStartAt_RejectedByBR2Check`, `Insert_DueAtEqualsPlannedStartAt_Accepted`,
`CreateTaskAsync_DueAtBeforePlannedStartAt_ThrowsBR2`.
