# 0002. Assignment is a column on the task, not a TaskAssignment history table

Status: proposed (N1, 2026-09-18). Awaiting the N2 human gate.

## Context

The brief requires "task assignment" and BR5 (assignee ≠ creator) as a database check constraint. The minimum use
cases are: create a task, change a task's status, list tasks by assignee. None of them reassigns a task.

Two models were considered:

1. `tasks.assignee_id` next to `tasks.creator_id`.
2. A `task_assignments` table (task_id, employee_id, assigned_at, …) holding the assignment history.

A PostgreSQL CHECK constraint is evaluated on one row and cannot contain a subquery. With model 2 the assignee lives
in `task_assignments` and the creator in `tasks`, so no CHECK can compare them; BR5 would need a trigger (not
requested) or a denormalised `creator_id` copy on every assignment row kept in sync by hand.

## Decision

Model 1. `tasks.assignee_id uuid NOT NULL` with FK `fk_tasks_employees_assignee_id` (`ON DELETE RESTRICT`), set once
by `TaskItem.Create` and never changed. BR5 is the row-local CHECK
`ck_tasks_br5_assignee_not_creator`: `assignee_id <> creator_id`.

There is no assign or reassign operation. Every task has an assignee from the moment it exists, which is also the
only moment BR3 ("given a new task") applies.

## Consequences

- BR5 is enforced by the database on every write path, including raw SQL.
- "List tasks by assignee" is a single indexed filter (`ix_tasks_assignee_id_status`, leading column `assignee_id`).
- No assignment history. If reassignment is needed later, it becomes a new domain method with BR3 and BR5 checks;
  the CHECK keeps working because the assignee stays on the row. A history table can then be added beside the
  column without replacing it.
- Unassigned tasks cannot exist. If the business needs them, `assignee_id` becomes nullable; the BR5 CHECK already
  passes for NULL.
