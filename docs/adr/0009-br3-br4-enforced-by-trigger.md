# 0009. BR3 and BR4 are enforced in the database by a trigger

Status: accepted (human, pre-implementation grill, 2026-09-18). Supersedes [ADR 0004](0004-no-db-constraint-for-br3-br4.md)
(its `xmin` concurrency-token decision stays in force).

## Context

The brief asks for a DB constraint for each business rule "where SQL can express it". A CHECK constraint is row-local
and cannot express BR3 (it needs `employees.is_active`, another table's row) or BR4 (it needs the row's previous
status). ADR 0004 therefore left both rules to application code. The consequences were that raw SQL or any other
writer could reopen a final task or give a task to an inactive employee, and that a deactivation committed between
the service's read and its insert went undetected (the BR3 race).

A PL/pgSQL trigger can express both rules. At the pre-implementation grill the human chose option (b): enforce BR3
and BR4 with a trigger, and translate the trigger's BR3 rejection into the domain exception (Q4 b).

## Decision

- One function `tasks_enforce_br3_br4()` and one trigger `trg_tasks_br3_br4`,
  `BEFORE INSERT OR UPDATE OF status ON tasks FOR EACH ROW`. The exact SQL is in [spec §11](../../.wiki/domain/spec.md).
  - **BR3, on insert**: reads the assignee's `is_active` under `FOR SHARE` and rejects an inactive assignee.
    An unknown assignee is left to the FK (`23503`), so the trigger never masks a referential error.
  - **BR4, on a status update**: rejects a status change out of `Completed` or `Cancelled`. `UPDATE OF status` means
    updates that don't touch `status` skip the trigger.
  - Both raise SqlState `23514` (`check_violation`) with a constraint name: `trg_tasks_br3_assignee_active` or
    `trg_tasks_br4_final_status`. Callers and tests handle them exactly like the CHECK constraints.
- Created in `InitialCreate` with `migrationBuilder.Sql` at the end of `Up()`, and dropped at the start of `Down()`.
  This is still an EF Core migration, not a SQL dump. EF's model and snapshot do not know about the trigger, so future
  `migrations add` runs leave it alone.
- `TaskService.CreateTaskAsync` catches the `DbUpdateException` whose inner `PostgresException` names
  `trg_tasks_br3_assignee_active`, and rethrows it as `BusinessRuleViolationException("BR3", …, inner)`. Callers see
  one exception type for BR3 whichever layer caught it. `ChangeTaskStatusAsync` needs no translation: an EF update
  carries the `xmin` token, so a row finalised by another writer fails the concurrency check first.

## Consequences

- Every writer is held to BR4 and to BR3 **on insert**, raw SQL included, and the BR3 race is closed. BR3 is
  checked only when a task is inserted, because creation is the only moment the model sets an assignee (spec §8).
  A raw-SQL `UPDATE` of `assignee_id` to an inactive employee is not rejected (D3 findings F2/T1); covering it
  would mean extending the trigger to `UPDATE OF status, assignee_id`, a schema change for the human to approve.
  About the race: while a task insert's transaction is open, a concurrent update of that employee row
  (deactivation, but also a name or e-mail change) waits for it. With EF's short `SaveChanges` transactions, that
  wait is milliseconds.
- Measured cost, in a throwaway `postgres:17-alpine` container with the spec's schema and 1,000 employees
  (server-side loop, no network, so only the differences carry over to production):
  - single-row insert: +21 µs (+11 µs for the lookup, +10 µs for the `FOR SHARE` lock);
  - single-row status update: +4 µs (in-memory comparison, no extra reads or locks);
  - reads: no effect (triggers fire only on writes);
  - a 200,000-row bulk insert: 7.5 s → 12.2 s (+61%), because PL/pgSQL runs once per row. A trusted bulk load can
    disable the trigger (`ALTER TABLE … DISABLE TRIGGER`), and the rows loaded that way are not checked.
- Concurrent task inserts for the same assignee don't block each other (share locks are compatible). The existing
  FKs already take `FOR KEY SHARE` on the same row. `FOR SHARE` is the weakest mode that also blocks an update of
  `is_active`.
- Rule logic now also lives in SQL. The named errors, this ADR, the migration header and the B3 tests keep it visible.
- New B3 tests: `Insert_TaskForInactiveAssignee_RejectedByBR3Trigger`,
  `Insert_TaskForUnknownAssignee_RejectedByForeignKeyNotBR3Trigger`, `Update_StatusOfCompletedTask_RejectedByBR4Trigger`,
  `Update_StatusOfCancelledTask_RejectedByBR4Trigger` (D4) and `CreateTaskAsync_AssigneeDeactivatedAfterRead_ThrowsBR3FromTrigger`.
