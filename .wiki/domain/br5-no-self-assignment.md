---
okf_version: "0.2"
id: rule-br5
title: "BR5: завдання не може бути призначене своєму автору"
type: BusinessRule
description: Бізнес-правило BR5 - виконавець завдання не може збігатися з автором (assignee != creator)
status: approved
updated: 2026-09-20
tags: [okf, domain, business-rule, br5, validation, database-check]
related: ["spec.md", "task-item.md", "employee.md"]
---

# BR5: завдання не може бути призначене своєму автору (виконавець ≠ автор)

[ **Українська** ] · [ English ](br5-no-self-assignment.en.md)

Визначено в технічному завданні: це є значенням формулювання "призначення самому собі".

| Рівень | Де (реалізовано: домен на fan-in A, база даних та сервіс на fan-in B) | Як |
|---|---|---|
| Домен | `TaskItem.Create` (guard 5) у `src/TaskManagement.Domain/TaskItem.cs` | `creator.Id == assignee.Id` → `BusinessRuleViolationException` (`RuleId == "BR5"`) |
| База даних | `ck_tasks_br5_assignee_not_creator` на `tasks` | `assignee_id <> creator_id` → SqlState `23514` |
| Прикладний сервіс | `TaskService.CreateTaskAsync` | через домен |

Обмеження `CHECK` можливе лише тому, що виконавець і автор знаходяться в одному рядку ([ADR 0002](../../docs/adr/0002-assignee-column-on-task.md)). Обидві колонки є non-null, тому `CHECK` ніколи не бачить `NULL`.

Тести ([[spec]] §15): `Create_AssigneeIsCreator_ThrowsBusinessRuleViolationBR5`, `Create_InactiveEmployeeAsCreatorAndAssignee_ThrowsBusinessRuleViolationBR5` (порядок guard-перевірок), `Insert_AssigneeEqualsCreator_RejectedByBR5Check`,
`CreateTaskAsync_AssigneeIsCreator_ThrowsBR5`.
