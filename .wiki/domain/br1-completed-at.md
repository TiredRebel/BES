---
okf_version: "0.2"
id: rule-br1
title: "BR1: CompletedAt встановлюється тоді й тільки тоді, коли статус = Completed"
type: BusinessRule
description: Бізнес-правило BR1 - часова мітка CompletedAt фіксується виключно при переході в статус Completed
status: approved
updated: 2026-09-20
tags: [okf, domain, business-rule, br1, validation, database-check]
related: ["spec.md", "task-item.md", "br4-final-statuses.md"]
---

# BR1: `CompletedAt` обов'язковий тоді й тільки тоді, коли статус = `Completed`

[ **Українська** ] · [ English ](br1-completed-at.en.md)

| Рівень | Де (реалізовано: домен на fan-in A, база даних та сервіс на fan-in B) | Як |
|---|---|---|
| Домен | `TaskItem.Create`, `TaskItem.ChangeStatus` у `src/TaskManagement.Domain/TaskItem.cs` | Структурно: `Create` встановлює `CompletedAt = null`; `ChangeStatus` встановлює `CompletedAt = changedAt.ToUniversalTime()`, коли новий статус `Completed`, та `null` в інших випадках. Публічні сетери відсутні, тому API не може виразити порушення і нічого не викидає. BR4 зберігає значення фінального завдання незмінним. |
| База даних | `ck_tasks_br1_completed_at_iff_completed` на `tasks` | `(status = 'Completed' AND completed_at IS NOT NULL) OR (status <> 'Completed' AND completed_at IS NULL)` → SqlState `23514` |
| Прикладний сервіс | `TaskService.ChangeTaskStatusAsync` | передає `timeProvider.GetUtcNow()` як `changedAt` |

Тести ([[spec]] §15): `Create_ValidInput_StartsAsNewWithNullCompletedAt`, `ChangeStatus_TransitionTableRow_BehavesAsSpecified`,
`ChangeStatus_ToCompleted_SetsCompletedAtToChangedAt`, `ChangeStatus_ToCompletedWithNonUtcOffset_StoresUtcInstant`,
`Insert_CompletedWithoutCompletedAt_RejectedByBR1Check`, `Insert_NewWithCompletedAt_RejectedByBR1Check`,
`Update_ClearCompletedAtOfCompletedTask_RejectedByBR1Check`, `ChangeTaskStatusAsync_NewToCompleted_PersistsStatusAndCompletedAt`.

Red evidence на FA: видалення присвоєння `CompletedAt = …` у `ChangeStatus`.
