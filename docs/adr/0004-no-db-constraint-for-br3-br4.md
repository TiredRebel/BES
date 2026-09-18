# 0004. BR3 and BR4 have no database constraint

Status: proposed (N1, 2026-09-18). Awaiting the N2 human gate.

## Context

The brief asks for a DB constraint for each business rule "where SQL can express it". BR1, BR2 and BR5 compare
columns of the same row and are CHECK constraints. BR3 and BR4 are different:

- **BR3** (an inactive employee can't be given a new task) depends on `employees.is_active`, a column of *another*
  row in *another* table.
- **BR4** (`Completed` and `Cancelled` are final) depends on the row's *previous* status, i.e. on a state
  transition, not on the row's current values.

A PostgreSQL CHECK constraint sees only the new version of the row being written and cannot contain a subquery.
It can express neither rule. Triggers could, but the brief did not ask for them and they would hide business logic
in the database.

## Decision

- No DB constraint for BR3 or BR4. They are enforced in the domain (`TaskItem.Create`, `TaskItem.ChangeStatus`);
  BR3 additionally in `TaskService.CreateTaskAsync`, as the brief requires.
- `tasks` carries an optimistic concurrency token: the CLR property `TaskItem.Version` (`uint`) mapped by Npgsql to
  the system column `xmin`. Two concurrent status changes on the same task cannot both save; the second gets
  `DbUpdateConcurrencyException`. Without it, a writer that read a task as `New` could overwrite another writer's
  `Completed` with `Cancelled`, and BR4 would be broken silently while BR1 still passed.
- `employees` has no token: `Deactivate()` is idempotent and no use case calls it.

## Consequences

- Raw SQL can still move a task out of a final status, or assign a task to an inactive employee. Only application
  code paths are protected.
- A deactivation committed between the service's read of the assignee and its insert of the task is not detected
  (BR3 race). Closing it would need a lock on the employee row or a trigger; out of scope.
- The B3 test `ConcurrentStatusChange_SecondSave_ThrowsDbUpdateConcurrencyException` proves the token works.
