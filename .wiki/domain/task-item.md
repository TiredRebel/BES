---
okf_version: "0.2"
id: entity-task-item
title: TaskItem (сутність) та TaskItemStatus (перелік)
type: Entity
description: Доменна сутність завдання (TaskItem), життєвий цикл статусів, зв'язки з автором і виконавцем, оптимістичне блокування
status: approved
updated: 2026-09-20
tags: [okf, domain, entity, task-item, status-machine, xmin]
related: ["spec.md", "employee.md", "br1-completed-at.md", "br2-due-not-before-start.md", "br3-inactive-assignee.md", "br4-final-statuses.md", "br5-no-self-assignment.md"]
---

# Завдання (TaskItem)

[ **Українська** ] · [ English ](task-item.en.md)

Одиниця роботи, створена одним співробітником і призначена іншому. Названа `TaskItem` (а не `Task`), а її перелік статусів — `TaskItemStatus` (а не `TaskStatus`), щоб уникнути колізій з `System.Threading.Tasks`. Повний контракт: [[spec]] §3.2–§3.4, §5, §10–§12.

- **Код**: `src/TaskManagement.Domain/TaskItem.cs`, `TaskItemStatus.cs`, `BusinessRuleViolationException.cs` (реалізовано на fan-in A; тести в `tests/TaskManagement.UnitTests/TaskItemCreateTests.cs` та `TaskItemChangeStatusTests.cs`). Мапінг: `src/TaskManagement.Infrastructure/Configurations/TaskItemConfiguration.cs`; сервіс: `src/TaskManagement.Application/TaskService.cs`.
- **Таблиця**: `tasks`, PK `pk_tasks`.

| Властивість | CLR-тип | Колонка | Правила |
|---|---|---|---|
| `Id` | `Guid` | `id uuid` | `Guid.CreateVersion7()` у `Create` |
| `Title` | `string` | `title varchar(200)` | обов'язкове, обрізаються пробіли, максимум `TitleMaxLength` = 200 |
| `Status` | `TaskItemStatus` | `status varchar(20)` | `New` при створенні; зберігається як текст; `ck_tasks_status_valid` |
| `CreatorId` | `Guid` | `creator_id uuid` | FK `fk_tasks_employees_creator_id`, RESTRICT |
| `AssigneeId` | `Guid` | `assignee_id uuid` | FK `fk_tasks_employees_assignee_id`, RESTRICT; ≠ `CreatorId` ([[br5-no-self-assignment]]); встановлюється один раз |
| `PlannedStartAt` | `DateTimeOffset?` | `planned_start_at timestamptz` | UTC ([[br2-due-not-before-start]]) |
| `DueAt` | `DateTimeOffset?` | `due_at timestamptz` | UTC, дедлайн, ≥ `PlannedStartAt`, коли обидва встановлені |
| `CompletedAt` | `DateTimeOffset?` | `completed_at timestamptz` | встановлюється тоді й тільки тоді, коли статус = `Completed` ([[br1-completed-at]]) |
| `Version` | `uint` | `xmin` (system) | токен конкурентності; ніколи не записується доменом |

## Операції

- `TaskItem.Create(string title, Employee creator, Employee assignee, DateTimeOffset? plannedStartAt, DateTimeOffset? dueAt)`:
  перевірки за порядком: заголовок, ненульові співробітники, нормалізація до UTC, BR5, BR3, BR2.
- `ChangeStatus(TaskItemStatus newStatus, DateTimeOffset changedAt)`:
  перевірка значення переліку, потім BR4, потім no-op якщо без змін, потім застосування (встановлює або очищує `CompletedAt`, BR1).
- Операції перепризначення, перейменування, перенесення термінів або видалення відсутні: сценарії завдання цього не вимагають.

## Статуси

`New = 0`, `InProgress = 1`, `Completed = 2`, `Cancelled = 3`. `Completed` та `Cancelled` є фінальними ([[br4-final-statuses]]). Будь-яка зміна зі статусів `New` або `InProgress` дозволена, включно з переходом у той самий статус (no-op). Таблиця на 16 переходів наведена в [[spec]] §5.

## Індекси

`ix_tasks_assignee_id_status` (виконавець + статус), `ix_tasks_due_at` (дедлайн), `ix_tasks_creator_id` (FK автора).
