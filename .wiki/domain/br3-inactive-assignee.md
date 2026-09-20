---
okf_version: "0.2"
id: rule-br3
title: "BR3: неактивному співробітнику не можна призначити нове завдання"
type: BusinessRule
description: Бізнес-правило BR3 - заборона призначення нових завдань неактивним співробітникам із захистом через тригер БД
status: approved
updated: 2026-09-20
tags: [okf, domain, business-rule, br3, validation, trigger, race-condition]
related: ["spec.md", "employee.md", "task-item.md"]
---

# BR3: неактивному співробітнику не можна призначити нове завдання

[ **Українська** ] · [ English ](br3-inactive-assignee.en.md)

"Призначити нове завдання" означає: створюється `TaskItem`, де цей співробітник є `AssigneeId`. Створення — це єдиний момент, коли встановлюється `AssigneeId` (перепризначення відсутнє), тому BR3 перевіряється при створенні у двох місцях, як того вимагає технічне завдання.
Статус автора не перевіряється, а завдання, які співробітник уже мав до деактивації, залишаються дійсними.

| Рівень | Де (реалізовано: домен на fan-in A, база даних та сервіс на fan-in B) | Як |
|---|---|---|
| Прикладний сервіс | `TaskService.CreateTaskAsync` у `src/TaskManagement.Application/TaskService.cs` | завантажує рядок виконавця, `!IsActive` → `BusinessRuleViolationException` (`RuleId == "BR3"`) перед викликом домену |
| Домен | `TaskItem.Create` (guard 6) | `!assignee.IsActive` → той самий виняток, тому жоден клієнт не може оминути перевірку |
| База даних | тригер `trg_tasks_br3_br4` на insert (у `src/TaskManagement.Infrastructure/Migrations/20260918164447_InitialCreate.cs`) | читає `is_active` виконавця під `FOR SHARE`; неактивний виконавець → `23514` / `trg_tasks_br3_assignee_active`; сервіс перехоплює і повторно викидає як `BusinessRuleViolationException("BR3")` ([ADR 0009](../../docs/adr/0009-br3-br4-enforced-by-trigger.md)) |

Блокування тригера `FOR SHARE` закриває стан гонитви між читанням сервісу та його вставкою: паралельна деактивація чекає завершення вставки, а деактивація, зафіксована до вставки, призводить до помилки BR3.

Тести ([[spec]] §15): `Create_InactiveAssignee_ThrowsBusinessRuleViolationBR3`, `Create_InactiveCreatorActiveAssignee_Succeeds`,
`Insert_TaskForInactiveAssignee_RejectedByBR3Trigger`, `Insert_TaskForUnknownAssignee_RejectedByForeignKeyNotBR3Trigger`,
`CreateTaskAsync_AssigneeDeactivatedAfterRead_ThrowsBR3FromTrigger`, `CreateTaskAsync_InactiveAssignee_ThrowsBR3AndPersistsNothing`,
`CreateTaskAsync_InactiveEmployeeAsCreatorAndAssignee_ThrowsBR3FromServiceCheck` (доводить перевірку сервісу). Передумова (не є тестом BR3): `Deactivate_ActiveEmployee_SetsIsActiveFalse`. Співробітниця Carol, `10000000-0000-0000-0000-000000000003`, є неактивною.
