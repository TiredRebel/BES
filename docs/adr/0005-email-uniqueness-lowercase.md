# 0005. E-mail uniqueness is case-insensitive through lowercase normalisation

Status: proposed (N1, 2026-09-18). Awaiting the N2 human gate (open question 2).

## Context

The brief requires a unique index on e-mail. `Alice@Example.com` and `alice@example.com` reach the same mailbox in
practice, so uniqueness should ignore case. Options:

1. Normalise to lowercase in the domain; plain unique index on `email`.
2. ICU nondeterministic collation (`HasCollation(..., provider: "icu", deterministic: false)` + `UseCollation`), the
   approach Npgsql's docs recommend for case-insensitive columns; the unique index then compares case-insensitively.
3. The `citext` extension column type.
4. A unique index on `lower(email)`: EF Core cannot model expression indexes, so it would be hand-written SQL that
   the model snapshot does not know about.

## Decision

Option 1. `Employee.Create` stores `email.Trim().ToLowerInvariant()`; B1 creates `ux_employees_email` as a plain
unique B-tree index on `email` (`character varying(254)`).

Reasons: no collation or extension object in the migration, nothing that depends on how the PostgreSQL server was
built, seed values are plain lowercase strings, and the domain rule is one line with one unit test. No use case
creates employees today, so the only writers are the seed and tests.

## Consequences

- Case-insensitive uniqueness holds for every write that goes through `Employee.Create`. The database itself only
  guarantees exact-match uniqueness: raw SQL could insert `ALICE.MORGAN@example.com` next to
  `alice.morgan@example.com`.
- A DB CHECK `email = lower(email)` was rejected: PostgreSQL `lower()` and .NET `ToLowerInvariant()` may disagree on
  non-ASCII characters `[uncertain]`, which would make valid domain writes fail.
- If DB-level case-insensitivity is required, switch to option 2 in a new migration; the column and index names stay.
