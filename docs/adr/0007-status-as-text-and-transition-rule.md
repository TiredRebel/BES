# 0007. Task status: four values, stored as text, one transition rule

Status: proposed (N1, 2026-09-18). Awaiting the N2 human gate.

## Context

The brief names two statuses (`Completed`, `Cancelled`) and one transition rule (BR4: both are final). BR1 needs a
CHECK that compares the status with `completed_at`. The enum cannot be called `TaskStatus`
(`System.Threading.Tasks.TaskStatus` is imported by `ImplicitUsings`).

## Decision

- `TaskItemStatus { New = 0, InProgress = 1, Completed = 2, Cancelled = 3 }`. `New` and `InProgress` are the
  minimum non-final states a task needs; no others.
- Stored as text with `HasConversion<string>()` in `status character varying(20)`, restricted by
  `ck_tasks_status_valid`: `status IN ('New', 'InProgress', 'Completed', 'Cancelled')`. Text keeps the BR1 CHECK and
  raw SQL readable (`status = 'Completed'`) and survives enum reordering. A PostgreSQL enum type was rejected: it
  needs `MapEnum` on the data source and makes changes to the value list harder.
- Transition rule: a change is allowed iff the current status is not final. A change to the same non-final status
  is a no-op. The brief states no other restriction, so none is invented.

## Consequences

- 16-row table in the spec: 8 allowed (from `New`/`InProgress`), 8 rejected with BR4.
- Status values in the database are exactly the C# member names; renaming a member needs a migration that updates
  the CHECK and the data.
- `InProgress → New` is allowed. If the business wants a stricter workflow, it is a domain change plus table rows.
