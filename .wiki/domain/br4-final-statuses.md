---
title: "BR4: Completed and Cancelled are final"
type: rule
status: draft
updated: 2026-09-18
related: ["[[spec]]", "[[task-item]]", "[[br1-completed-at]]"]
---

# BR4: `Completed` and `Cancelled` are final

No transition out of a final status, including to the same status (`Completed → Completed` is rejected too, so
`CompletedAt` can never be overwritten). From `New` and `InProgress` every change is allowed. Full 16-row table:
[[spec]] §5.

| Layer | Where (planned) | How |
|---|---|---|
| Domain | `TaskItem.ChangeStatus` (step 2) in `src/TaskManagement.Domain/TaskItem.cs` | current status final → `BusinessRuleViolationException` (`RuleId == "BR4"`); nothing changes |
| Application | `TaskService.ChangeTaskStatusAsync` | calls the domain; `DbUpdateConcurrencyException` propagates |
| Concurrency | `TaskItem.Version` ↔ `xmin` | two writers that both read a non-final task cannot both save: the second gets `DbUpdateConcurrencyException`, so a final status is never silently overwritten |
| Database | none | a row-local CHECK cannot see the previous status ([ADR 0004](../../docs/adr/0004-no-db-constraint-for-br3-br4.md)) |

Tests ([[spec]] §15): `ChangeStatus_TransitionTableRow_BehavesAsSpecified` (8 disallowed rows),
`ChangeStatus_FromCompleted_KeepsStatusAndCompletedAt`, `ChangeTaskStatusAsync_CompletedTask_ThrowsBR4AndLeavesRowUnchanged`,
`ConcurrentStatusChange_SecondSave_ThrowsDbUpdateConcurrencyException`.
