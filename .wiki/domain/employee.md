---
okf_version: "0.2"
id: entity-employee
title: Employee (сутність)
type: Entity
description: Доменна сутність співробітника (Employee), правила валідації, життєвий цикл та обмеження зв'язків
status: approved
updated: 2026-09-20
tags: [okf, domain, entity, employee, crm]
related: ["spec.md", "task-item.md", "br3-inactive-assignee.md", "br5-no-self-assignment.md"]
---

# Співробітник (Employee)

[ **Українська** ] · [ English ](employee.en.md)

Особа, яка створює завдання та якій призначаються завдання. Повний контракт: [[spec]] §3.1, §10, §12.

- **Код**: `src/TaskManagement.Domain/Employee.cs` (реалізовано на fan-in A; тести в `tests/TaskManagement.UnitTests/EmployeeTests.cs`). Мапінг (реалізовано на fan-in B): `src/TaskManagement.Infrastructure/Configurations/EmployeeConfiguration.cs`.
- **Таблиця**: `employees` (`id uuid`, `full_name varchar(200)`, `email varchar(254)`, `is_active boolean`), PK `pk_employees`, унікальний індекс `ux_employees_email`.

| Властивість | CLR-тип | Правила |
|---|---|---|
| `Id` | `Guid` | `Guid.CreateVersion7()` у `Create` |
| `FullName` | `string` | обов'язкове, обрізаються пробіли, максимум `FullNameMaxLength` = 200 |
| `Email` | `string` | обов'язкове, обрізаються пробіли + нижній регістр, максимум `EmailMaxLength` = 254, унікальне |
| `IsActive` | `bool` | `true` при створенні; `Deactivate()` встановлює `false` |

- `Employee.Create(string fullName, string email)`: `ArgumentNullException` / `ArgumentException` на некоректних вхідних даних.
- `Deactivate()`: ідемпотентний метод. Неактивному співробітнику не можна *призначити нове завдання* ([[br3-inactive-assignee]]); завдання, які він уже має, залишаються дійсними.
- Співробітник не може бути виконавцем завдання, яке він сам створив ([[br5-no-self-assignment]]).
- Видалення співробітника, на якого посилається будь-яке завдання, завершується помилкою (`ON DELETE RESTRICT`). Співробітники деактивуються, а не видаляються.
- Немає токена конкурентності (єдина мутація є ідемпотентною і не викликається основними use cases).
- Жоден use case не створює співробітників: вони надходять із початкових даних seed (Alice, Bob активні; Carol неактивна).
