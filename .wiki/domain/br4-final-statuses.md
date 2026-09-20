---
okf_version: "0.2"
id: rule-br4
title: "BR4: Completed та Cancelled є фінальними"
type: BusinessRule
description: Бізнес-правило BR4 - незмінність фінальних статусів Completed та Cancelled, захищена доменом та тригером БД
status: approved
updated: 2026-09-20
tags: [okf, domain, business-rule, br4, state-machine, trigger, immutability]
related: ["spec.md", "task-item.md", "br1-completed-at.md"]
---

# BR4: `Completed` та `Cancelled` є фінальними

[ **Українська** ] · [ English ](br4-final-statuses.en.md)

Перехід із фінального статусу заборонений, включно з переходом у той самий статус (`Completed → Completed` також відхиляється, тому `CompletedAt` ніколи не може бути перезаписаний). Зі статусів `New` та `InProgress` дозволено будь-яку зміну. Повна таблиця на 16 рядків: [[spec]] §5.

| Рівень | Де (реалізовано: домен на fan-in A, база даних та сервіс на fan-in B) | Як |
|---|---|---|
| Домен | `TaskItem.ChangeStatus` (крок 2) у `src/TaskManagement.Domain/TaskItem.cs` | поточний статус фінальний → `BusinessRuleViolationException` (`RuleId == "BR4"`); стан не змінюється |
| Прикладний сервіс | `TaskService.ChangeTaskStatusAsync` | викликає домен; `DbUpdateConcurrencyException` прокидається нагору |
| Конкурентність | `TaskItem.Version` ↔ `xmin` | два автори, які прочитали нефінальне завдання, не можуть зберегти зміни одночасно: другий отримує `DbUpdateConcurrencyException`, тому фінальний статус ніколи не перезаписується мовчки |
| База даних | тригер `trg_tasks_br3_br4` на `UPDATE OF status` (у `src/TaskManagement.Infrastructure/Migrations/20260918164447_InitialCreate.cs`) | зміна статусу з `Completed`/`Cancelled` → `23514` / `trg_tasks_br4_final_status` для будь-якого клієнта, включно з прямим SQL ([ADR 0009](../../docs/adr/0009-br3-br4-enforced-by-trigger.md)) |

Тести ([[spec]] §15): `ChangeStatus_TransitionTableRow_BehavesAsSpecified` (8 заборонених переходів),
`ChangeStatus_FromCompleted_KeepsStatusAndCompletedAt`, `ChangeTaskStatusAsync_CompletedTask_ThrowsBR4AndLeavesRowUnchanged`,
`ConcurrentStatusChange_SecondSave_ThrowsDbUpdateConcurrencyException`, `Update_StatusOfCompletedTask_RejectedByBR4Trigger`,
`Update_StatusOfCancelledTask_RejectedByBR4Trigger`.
