---
title: "BR1: CompletedAt is set iff status is Completed"
type: rule
status: approved
updated: 2026-09-18
related: ["[[spec]]", "[[task-item]]", "[[br4-final-statuses]]"]
---

# BR1: `CompletedAt` is required when, and only when, status = `Completed`

| Layer | Where (planned) | How |
|---|---|---|
| Domain | `TaskItem.Create`, `TaskItem.ChangeStatus` in `src/TaskManagement.Domain/TaskItem.cs` | Structural: `Create` sets `CompletedAt = null`; `ChangeStatus` sets `CompletedAt = changedAt.ToUniversalTime()` when the new status is `Completed` and `null` otherwise. No setter is public, so the API cannot express a violation and nothing throws. BR4 keeps a final task's value frozen. |
| Database | `ck_tasks_br1_completed_at_iff_completed` on `tasks` | `(status = 'Completed' AND completed_at IS NOT NULL) OR (status <> 'Completed' AND completed_at IS NULL)` → SqlState `23514` |
| Application | `TaskService.ChangeTaskStatusAsync` | passes `timeProvider.GetUtcNow()` as `changedAt` |

Tests ([[spec]] §15): `Create_ValidInput_StartsAsNewWithNullCompletedAt`, `ChangeStatus_TransitionTableRow_BehavesAsSpecified`,
`ChangeStatus_ToCompleted_SetsCompletedAtToChangedAt`, `ChangeStatus_ToCompletedWithNonUtcOffset_StoresUtcInstant`,
`Insert_CompletedWithoutCompletedAt_RejectedByBR1Check`, `Insert_NewWithCompletedAt_RejectedByBR1Check`,
`Update_ClearCompletedAtOfCompletedTask_RejectedByBR1Check`, `ChangeTaskStatusAsync_NewToCompleted_PersistsStatusAndCompletedAt`.

Red evidence at FA: remove the `CompletedAt = …` assignment in `ChangeStatus`.
