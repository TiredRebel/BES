---
title: Employee (entity)
type: entity
status: draft
updated: 2026-09-18
related: ["[[spec]]", "[[task-item]]", "[[br3-inactive-assignee]]", "[[br5-no-self-assignment]]"]
---

# Employee

A person who creates tasks and is assigned tasks. Full contract: [[spec]] §3.1, §10, §12.

- Code (planned): `src/TaskManagement.Domain/Employee.cs`, mapping in
  `src/TaskManagement.Infrastructure/Configurations/EmployeeConfiguration.cs`.
- Table: `employees` (`id uuid`, `full_name varchar(200)`, `email varchar(254)`, `is_active boolean`), PK
  `pk_employees`, unique index `ux_employees_email`.

| Property | CLR type | Rules |
|---|---|---|
| `Id` | `Guid` | `Guid.CreateVersion7()` in `Create` |
| `FullName` | `string` | required, trimmed, max `FullNameMaxLength` = 200 |
| `Email` | `string` | required, trimmed + lowercase, max `EmailMaxLength` = 254, unique |
| `IsActive` | `bool` | `true` on create; `Deactivate()` sets `false` |

- `Employee.Create(string fullName, string email)`: `ArgumentNullException` / `ArgumentException` on bad input.
- `Deactivate()`: idempotent. An inactive employee cannot be *given a new task* ([[br3-inactive-assignee]]); tasks
  they already have are untouched.
- An employee cannot be the assignee of a task they created ([[br5-no-self-assignment]]).
- Deleting an employee who is referenced by any task fails (`ON DELETE RESTRICT`). Employees are deactivated, not
  deleted.
- No concurrency token (its only mutation is idempotent and unused by the use cases).
- No use case creates employees: they come from the seed (Alice, Bob active; Carol inactive).
