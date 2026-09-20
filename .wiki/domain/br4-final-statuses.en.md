---
okf_version: "0.2"
id: rule-br4-en
title: "BR4: Completed and Cancelled are final"
type: BusinessRule
description: Business rule BR4 - Completed and Cancelled are terminal statuses, enforced in domain and DB trigger
status: approved
updated: 2026-09-20
tags: [okf, domain, business-rule, br4, state-machine, trigger, immutability]
related: ["spec.en.md", "task-item.en.md", "br1-completed-at.en.md"]
---

# BR4: `Completed` and `Cancelled` are final

[ Українська ](br4-final-statuses.md) · [ **English** ]

No transition out of a final status, including to the same status (`Completed → Completed` is rejected too, so
`CompletedAt` can never be overwritten). From `New` and `InProgress` every change is allowed. Full 16-row table:
[[spec]] §5.

| Layer | Where (implemented: domain at fan-in A, database and service at fan-in B) | How |
|---|---|---|
| Domain | `TaskItem.ChangeStatus` (step 2) in `src/TaskManagement.Domain/TaskItem.cs` | current status final → `BusinessRuleViolationException` (`RuleId == "BR4"`); nothing changes |
| Application | `TaskService.ChangeTaskStatusAsync` | calls the domain; `DbUpdateConcurrencyException` propagates |
| Concurrency | `TaskItem.Version` ↔ `xmin` | two writers that both read a non-final task cannot both save: the second gets `DbUpdateConcurrencyException`, so a final status is never silently overwritten |
| Database | trigger `trg_tasks_br3_br4` on `UPDATE OF status` (in `src/TaskManagement.Infrastructure/Migrations/20260918164447_InitialCreate.cs`) | a status change out of `Completed`/`Cancelled` → `23514` / `trg_tasks_br4_final_status`, for every writer including raw SQL ([ADR 0009](../../docs/adr/0009-br3-br4-enforced-by-trigger.md)) |

Tests ([[spec]] §15): `ChangeStatus_TransitionTableRow_BehavesAsSpecified` (8 disallowed rows),
`ChangeStatus_FromCompleted_KeepsStatusAndCompletedAt`, `ChangeTaskStatusAsync_CompletedTask_ThrowsBR4AndLeavesRowUnchanged`,
`ConcurrentStatusChange_SecondSave_ThrowsDbUpdateConcurrencyException`, `Update_StatusOfCompletedTask_RejectedByBR4Trigger`,
`Update_StatusOfCancelledTask_RejectedByBR4Trigger`.
