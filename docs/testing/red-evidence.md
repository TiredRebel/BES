# Протокол верифікації бізнес-правил: Red Evidence (Failing-then-Passing)

[ **Українська** ] · [ English ](red-evidence.en.md)

Цей документ містить повний детальний звіт про мутаційне тестування та верифікацію надійності захисту бізнес-правил (BR1-BR5) у модулі **Task Management**.

---

## Методологія перевірки

Кожне вилучення захисту нижче було виконано на фінальному робочому коді:
1. Вилучався конкретний механізм захисту (guard-перевірка в C# коді, CHECK-констрейнт або тригер у PostgreSQL).
2. Запускався тестовий набір (`dotnet test`).
3. Фіксувалися тести, що переходили у стан "червоний" (fail), підтверджуючи, що саме цей механізм є ефективним і незамінним.
4. Файл відновлювався байт-у-байт, після чого всі 101 тест знову ставали "зеленими" (pass).

Прогони зафіксовані у fan-in A, fan-in B та D4 у [`.wiki/log.md`](../../.wiki/log.md).

---

## Таблиця Red Evidence для правил BR1-BR5

| Правило | Тести | Red evidence (вилучення захисту → тести, що падають) |
|---|---|---|
| **BR1** | `Create_ValidInput_StartsAsNewWithNullCompletedAt`<br/>`ChangeStatus_TransitionTableRow_BehavesAsSpecified`<br/>`ChangeStatus_ToCompleted_SetsCompletedAtToChangedAt`<br/>`ChangeStatus_ToCompletedWithNonUtcOffset_StoresUtcInstant`<br/>`Insert_CompletedWithoutCompletedAt_RejectedByBR1Check`<br/>`Insert_NewWithCompletedAt_RejectedByBR1Check`<br/>`Update_ClearCompletedAtOfCompletedTask_RejectedByBR1Check`<br/>`ChangeTaskStatusAsync_NewToCompleted_PersistsStatusAndCompletedAt` | **Доменний рівень:** видалення присвоєння `CompletedAt` → 5 червоних тестів (`ChangeStatus_TransitionTableRow_BehavesAsSpecified` рядки, `ChangeStatus_ToCompleted_SetsCompletedAtToChangedAt`, `ChangeStatus_ToCompletedWithNonUtcOffset_StoresUtcInstant`, `ChangeStatus_FromCompleted_KeepsStatusAndCompletedAt`).<br/>**Рівень БД:** видалення `ck_tasks_br1_completed_at_iff_completed` → 3 тести БД для BR1 + тест схеми червоні. |
| **BR2** | `Create_DueAtBeforePlannedStartAt_ThrowsBusinessRuleViolationBR2`<br/>`Create_DueAtEqualsPlannedStartAt_Succeeds`<br/>`Create_PlannedStartAtOrDueAtMissing_Succeeds`<br/>`Create_DueAtBeforePlannedStartAtAfterUtcConversion_ThrowsBusinessRuleViolationBR2`<br/>`Insert_DueAtBeforePlannedStartAt_RejectedByBR2Check`<br/>`Insert_DueAtEqualsPlannedStartAt_Accepted`<br/>`CreateTaskAsync_DueAtBeforePlannedStartAt_ThrowsBR2` | **Доменний рівень:** видалення guard-перевірки BR2 → 2 червоні тести (`Create_DueAtBeforePlannedStartAt_…BR2`, `…AfterUtcConversion_…BR2`).<br/>**Рівень БД:** видалення `ck_tasks_br2_due_at_not_before_planned_start_at` → тест БД для BR2 + тест схеми червоні. |
| **BR3** | `Create_InactiveAssignee_ThrowsBusinessRuleViolationBR3`<br/>`Create_InactiveCreatorActiveAssignee_Succeeds`<br/>`Insert_TaskForInactiveAssignee_RejectedByBR3Trigger`<br/>`Insert_TaskForUnknownAssignee_RejectedByForeignKeyNotBR3Trigger`<br/>`CreateTaskAsync_InactiveAssignee_ThrowsBR3AndPersistsNothing`<br/>`CreateTaskAsync_InactiveEmployeeAsCreatorAndAssignee_ThrowsBR3FromServiceCheck`<br/>`CreateTaskAsync_AssigneeDeactivatedAfterRead_ThrowsBR3FromTrigger` | **Доменний рівень:** видалення guard-перевірки BR3 → 1 червоний тест (`Create_InactiveAssignee_ThrowsBusinessRuleViolationBR3`).<br/>**Рівень БД:** тригер не створено → 6 червоних тестів (`Insert_TaskForInactiveAssignee_RejectedByBR3Trigger`, обидва тести тригера BR4, тест схеми, `CreateTaskAsync_AssigneeDeactivatedAfterRead_ThrowsBR3FromTrigger`, `CreateTaskAsync_AfterTriggerRejection_NextCallOnSameContextSucceeds`).<br/>**Сервісний рівень:** видалення власної перевірки BR3 → тільки `CreateTaskAsync_InactiveEmployeeAsCreatorAndAssignee_ThrowsBR3FromServiceCheck` червоний; видалення трансляції помилки тригера → `CreateTaskAsync_AssigneeDeactivatedAfterRead_ThrowsBR3FromTrigger` та `CreateTaskAsync_AfterTriggerRejection_NextCallOnSameContextSucceeds` червоні. |
| **BR4** | `ChangeStatus_TransitionTableRow_BehavesAsSpecified` (8 заборонених переходів)<br/>`ChangeStatus_FromCompleted_KeepsStatusAndCompletedAt`<br/>`ChangeTaskStatusAsync_CompletedTask_ThrowsBR4AndLeavesRowUnchanged`<br/>`ConcurrentStatusChange_SecondSave_ThrowsDbUpdateConcurrencyException`<br/>`Update_StatusOfCompletedTask_RejectedByBR4Trigger`<br/>`Update_StatusOfCancelledTask_RejectedByBR4Trigger` | **Доменний рівень:** видалення guard-перевірки BR4 → 9 червоних тестів (8 заборонених переходів + `ChangeStatus_FromCompleted_KeepsStatusAndCompletedAt`).<br/>**Рівень БД:** тригер не створено → `Update_StatusOfCompletedTask_RejectedByBR4Trigger` та `Update_StatusOfCancelledTask_RejectedByBR4Trigger` червоні; вилучення `'Cancelled'` із гілки тригера BR4 → тільки `Update_StatusOfCancelledTask_RejectedByBR4Trigger` червоний. Токен `xmin` (`ConcurrentStatusChange_SecondSave_ThrowsDbUpdateConcurrencyException`) не може бути вилучений ізольовано: його видалення ламає всі 38 інтеграційних тестів. |
| **BR5** | `Create_AssigneeIsCreator_ThrowsBusinessRuleViolationBR5`<br/>`Create_InactiveEmployeeAsCreatorAndAssignee_ThrowsBusinessRuleViolationBR5`<br/>`Insert_AssigneeEqualsCreator_RejectedByBR5Check`<br/>`CreateTaskAsync_AssigneeIsCreator_ThrowsBR5` | **Доменний рівень:** видалення guard-перевірки BR5 → 2 червоні тести (`Create_AssigneeIsCreator_ThrowsBusinessRuleViolationBR5`, `Create_InactiveEmployeeAsCreatorAndAssignee_ThrowsBusinessRuleViolationBR5`).<br/>**Рівень БД:** видалення `ck_tasks_br5_assignee_not_creator` → тест БД для BR5 + тест схеми червоні. |

---

## Верифікація очищення контексту (Failed-Save Cleanup)

Крім безпосередньо правил BR1-BR5, механізм очищення сутності після невдалого збереження (`Detach`) перевірено аналогічним чином:
1. **Видалення `Detach` у `CreateTaskAsync`:** ламає тести `CreateTaskAsync_AfterTriggerRejection_NextCallOnSameContextSucceeds` та `CreateTaskAsync_AssigneeDeletedBeforeInsert_ThrowsForeignKeyDbUpdateException`.
2. **Видалення `Detach` у `ChangeTaskStatusAsync`:** ламає тест `ChangeTaskStatusAsync_AfterConcurrencyConflict_RetryOnSameContextSucceeds`.
3. **Розширення catch-фільтра BR3 до будь-якого `DbUpdateException`:** ламає тест зовнішнього ключа (`ForeignKeyDbUpdateException`).

---

## Пов'язана документація

- [README.md - Головний процес прийняття рішень](../../README.md)
- [README.uk.md - Технічний опис (Українська)](../../README.uk.md)
- [README.en.md - Technical Overview (English)](../../README.en.md)
- [.wiki/domain/spec.md - Специфікація рівня даних](../../.wiki/domain/spec.md)
- [.wiki/log.md - Журнал розробки та прогонів тестів](../../.wiki/log.md)
