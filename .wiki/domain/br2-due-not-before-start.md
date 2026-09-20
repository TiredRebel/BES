---
okf_version: "0.2"
id: rule-br2
title: "BR2: DueAt не може бути раніше за PlannedStartAt"
type: BusinessRule
description: Бізнес-правило BR2 - дедлайн завдання DueAt не може передувати запланованому початку PlannedStartAt
status: approved
updated: 2026-09-20
tags: [okf, domain, business-rule, br2, validation, database-check]
related: ["spec.md", "task-item.md"]
---

# BR2: `DueAt` не може бути раніше за `PlannedStartAt`

[ **Українська** ] · [ English ](br2-due-not-before-start.en.md)

Обидві дати є nullable. Правило застосовується лише тоді, коли обидві встановлені; рівність дозволена.

| Рівень | Де (реалізовано: домен на fan-in A, база даних та сервіс на fan-in B) | Як |
|---|---|---|
| Домен | `TaskItem.Create` (guard 7) у `src/TaskManagement.Domain/TaskItem.cs` | після нормалізації до UTC, якщо `dueAt < plannedStartAt` → `BusinessRuleViolationException` з `RuleId == "BR2"` |
| База даних | `ck_tasks_br2_due_at_not_before_planned_start_at` на `tasks` | `planned_start_at IS NULL OR due_at IS NULL OR due_at >= planned_start_at` → SqlState `23514` |
| Прикладний сервіс | `TaskService.CreateTaskAsync` | через домен |

Дати не можуть змінюватися після створення (сценарій перенесення термінів відсутній), тому `Create` є єдиною точкою входу в домені.

Тести ([[spec]] §15): `Create_DueAtBeforePlannedStartAt_ThrowsBusinessRuleViolationBR2`, `Create_DueAtEqualsPlannedStartAt_Succeeds`,
`Create_PlannedStartAtOrDueAtMissing_Succeeds`, `Create_DueAtBeforePlannedStartAtAfterUtcConversion_ThrowsBusinessRuleViolationBR2`,
`Insert_DueAtBeforePlannedStartAt_RejectedByBR2Check`, `Insert_DueAtEqualsPlannedStartAt_Accepted`,
`CreateTaskAsync_DueAtBeforePlannedStartAt_ThrowsBR2`.
