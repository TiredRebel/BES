# Business Rules Verification Protocol: Red Evidence (Failing-then-Passing)

[ Українська ](red-evidence.md) · [ **English** ]

This document contains the comprehensive report on mutation testing and verification of business rules (BR1-BR5) in the **Task Management** data layer.

---

## Verification Methodology

Each protection removal below was performed on the final working codebase:
1. A specific protection mechanism was temporarily removed (a domain guard in C#, a CHECK constraint, or a trigger in PostgreSQL).
2. The test suite was executed (`dotnet test`).
3. The resulting failing ("red") tests were recorded, proving that the protection is active, necessary, and effective.
4. The file was restored byte-for-byte, returning all 101 tests to the "green" (passing) state.

Test runs are recorded under fan-in A, fan-in B, and D4 entries in [`.wiki/log.md`](../../.wiki/log.md).

---

## Red Evidence Table for Rules BR1-BR5

| Rule | Tests | Red evidence (enforcement removed → tests that fail) |
|---|---|---|
| **BR1** | `Create_ValidInput_StartsAsNewWithNullCompletedAt`<br/>`ChangeStatus_TransitionTableRow_BehavesAsSpecified`<br/>`ChangeStatus_ToCompleted_SetsCompletedAtToChangedAt`<br/>`ChangeStatus_ToCompletedWithNonUtcOffset_StoresUtcInstant`<br/>`Insert_CompletedWithoutCompletedAt_RejectedByBR1Check`<br/>`Insert_NewWithCompletedAt_RejectedByBR1Check`<br/>`Update_ClearCompletedAtOfCompletedTask_RejectedByBR1Check`<br/>`ChangeTaskStatusAsync_NewToCompleted_PersistsStatusAndCompletedAt` | **Domain:** `CompletedAt` assignment removed → 5 red (`ChangeStatus_TransitionTableRow_BehavesAsSpecified` rows, `ChangeStatus_ToCompleted_SetsCompletedAtToChangedAt`, `ChangeStatus_ToCompletedWithNonUtcOffset_StoresUtcInstant`, `ChangeStatus_FromCompleted_KeepsStatusAndCompletedAt`).<br/>**Database:** `ck_tasks_br1_completed_at_iff_completed` dropped → the 3 BR1 DB tests + the schema test red. |
| **BR2** | `Create_DueAtBeforePlannedStartAt_ThrowsBusinessRuleViolationBR2`<br/>`Create_DueAtEqualsPlannedStartAt_Succeeds`<br/>`Create_PlannedStartAtOrDueAtMissing_Succeeds`<br/>`Create_DueAtBeforePlannedStartAtAfterUtcConversion_ThrowsBusinessRuleViolationBR2`<br/>`Insert_DueAtBeforePlannedStartAt_RejectedByBR2Check`<br/>`Insert_DueAtEqualsPlannedStartAt_Accepted`<br/>`CreateTaskAsync_DueAtBeforePlannedStartAt_ThrowsBR2` | **Domain:** BR2 guard removed → 2 red (`Create_DueAtBeforePlannedStartAt_…BR2`, `…AfterUtcConversion_…BR2`).<br/>**Database:** `ck_tasks_br2_due_at_not_before_planned_start_at` dropped → the BR2 DB test + the schema test red. |
| **BR3** | `Create_InactiveAssignee_ThrowsBusinessRuleViolationBR3`<br/>`Create_InactiveCreatorActiveAssignee_Succeeds`<br/>`Insert_TaskForInactiveAssignee_RejectedByBR3Trigger`<br/>`Insert_TaskForUnknownAssignee_RejectedByForeignKeyNotBR3Trigger`<br/>`CreateTaskAsync_InactiveAssignee_ThrowsBR3AndPersistsNothing`<br/>`CreateTaskAsync_InactiveEmployeeAsCreatorAndAssignee_ThrowsBR3FromServiceCheck`<br/>`CreateTaskAsync_AssigneeDeactivatedAfterRead_ThrowsBR3FromTrigger` | **Domain:** BR3 guard removed → 1 red (`Create_InactiveAssignee_ThrowsBusinessRuleViolationBR3`).<br/>**Database:** trigger not created → 6 red (`Insert_TaskForInactiveAssignee_RejectedByBR3Trigger`, both BR4 trigger tests, the schema test, `CreateTaskAsync_AssigneeDeactivatedAfterRead_ThrowsBR3FromTrigger`, `CreateTaskAsync_AfterTriggerRejection_NextCallOnSameContextSucceeds`).<br/>**Service:** own BR3 check removed → only `CreateTaskAsync_InactiveEmployeeAsCreatorAndAssignee_ThrowsBR3FromServiceCheck` red; trigger-error translation removed → `CreateTaskAsync_AssigneeDeactivatedAfterRead_ThrowsBR3FromTrigger` and `CreateTaskAsync_AfterTriggerRejection_NextCallOnSameContextSucceeds` red. |
| **BR4** | `ChangeStatus_TransitionTableRow_BehavesAsSpecified` (8 disallowed rows)<br/>`ChangeStatus_FromCompleted_KeepsStatusAndCompletedAt`<br/>`ChangeTaskStatusAsync_CompletedTask_ThrowsBR4AndLeavesRowUnchanged`<br/>`ConcurrentStatusChange_SecondSave_ThrowsDbUpdateConcurrencyException`<br/>`Update_StatusOfCompletedTask_RejectedByBR4Trigger`<br/>`Update_StatusOfCancelledTask_RejectedByBR4Trigger` | **Domain:** BR4 guard removed → 9 red (8 disallowed transition rows + `ChangeStatus_FromCompleted_KeepsStatusAndCompletedAt`).<br/>**Database:** trigger not created → `Update_StatusOfCompletedTask_RejectedByBR4Trigger` and `Update_StatusOfCancelledTask_RejectedByBR4Trigger` red; `'Cancelled'` dropped from the trigger's BR4 branch → only `Update_StatusOfCancelledTask_RejectedByBR4Trigger` red. The `xmin` token (`ConcurrentStatusChange_SecondSave_ThrowsDbUpdateConcurrencyException`) could not be removed in isolation: removing it fails all 38 integration tests. |
| **BR5** | `Create_AssigneeIsCreator_ThrowsBusinessRuleViolationBR5`<br/>`Create_InactiveEmployeeAsCreatorAndAssignee_ThrowsBusinessRuleViolationBR5`<br/>`Insert_AssigneeEqualsCreator_RejectedByBR5Check`<br/>`CreateTaskAsync_AssigneeIsCreator_ThrowsBR5` | **Domain:** BR5 guard removed → 2 red (`Create_AssigneeIsCreator_ThrowsBusinessRuleViolationBR5`, `Create_InactiveEmployeeAsCreatorAndAssignee_ThrowsBusinessRuleViolationBR5`).<br/>**Database:** `ck_tasks_br5_assignee_not_creator` dropped → the BR5 DB test + the schema test red. |

---

## Failed-Save Cleanup Verification

Beyond BR1-BR5, the service's failed-save cleanup is proven the same way:
1. **Removing `Detach` in `CreateTaskAsync`:** fails `CreateTaskAsync_AfterTriggerRejection_NextCallOnSameContextSucceeds` and `CreateTaskAsync_AssigneeDeletedBeforeInsert_ThrowsForeignKeyDbUpdateException`.
2. **Removing `Detach` in `ChangeTaskStatusAsync`:** fails `ChangeTaskStatusAsync_AfterConcurrencyConflict_RetryOnSameContextSucceeds`.
3. **Widening the BR3 catch filter to every `DbUpdateException`:** fails the FK test (`ForeignKeyDbUpdateException`).

---

## Related Documentation

- [README.md - Engineering Decision Process (Ukrainian)](../../README.md)
- [README.uk.md - Technical Overview (Ukrainian)](../../README.uk.md)
- [README.en.md - Technical Overview (English)](../../README.en.md)
- [.wiki/domain/spec.md - Data Layer Specification](../../.wiki/domain/spec.md)
- [.wiki/log.md - Session & Commit Log](../../.wiki/log.md)
