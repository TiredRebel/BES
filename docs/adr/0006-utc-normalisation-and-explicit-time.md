# 0006. Timestamps: normalise to UTC in the domain; time comes from TimeProvider

Status: proposed (N1, 2026-09-18). Awaiting the N2 human gate.

## Context

The brief fixes timestamps as `DateTimeOffset` stored as `timestamptz`, in UTC. Npgsql (6.0 and later; checked in
10.0.3) refuses to write a `DateTimeOffset` with a non-zero offset to `timestamp with time zone`: the DLL carries the
message "Cannot write DateTimeOffset with Offset=… to PostgreSQL type 'timestamp with time zone', only offset 0 (UTC)
is supported.", and https://www.npgsql.org/doc/types/datetime.html says the same ("only with Offset=0"). Tests need
deterministic completion times.

## Decision

- The domain **normalises** every incoming `DateTimeOffset` with `ToUniversalTime()` (`PlannedStartAt`, `DueAt`,
  `changedAt`). It does not reject non-UTC offsets: the instant is preserved and PostgreSQL does not store the
  offset anyway.
- The domain never reads a clock. `TaskItem.ChangeStatus(TaskItemStatus newStatus, DateTimeOffset changedAt)` takes
  the time as a parameter.
- `TaskService` receives `System.TimeProvider` in its constructor and uses `GetUtcNow()`. Production code passes
  `TimeProvider.System`; tests pass a subclass that overrides the virtual `GetUtcNow()`.

## Consequences

- Npgsql's offset exception cannot happen through the domain.
- BR2 compares instants, so normalisation never changes a BR2 result; a test covers dates whose local clock readings
  are ordered differently from their instants.
- No extra package (Microsoft.Extensions.TimeProvider.Testing is not needed).
- PostgreSQL stores microseconds, .NET ticks are 100 ns: integration tests use whole-second times.
