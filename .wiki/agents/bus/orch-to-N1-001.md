---
title: "orch → N1: architecture spec task"
type: bus-message
status: sent
updated: 2026-09-18
related: ["[[index]]", "[[log]]"]
from: orch
to: N1
seq: 1
---

# N1 Architect: task

You are node **N1 (Architect, opus)** in `.wiki/plan/graph.yaml`. You write the spec. You write no code.

Write `[uncertain]` instead of guessing. Report "not found" instead of inventing a path or API.

## Read first (in this order)

1. `ORCHESTRATOR_PROMPT.md`: the brief and the source of truth. Pay particular attention to THE TASK, LOCKED DEFAULTS,
   DOCUMENTATION STANDARD and EXECUTION GRAPH.
2. `AGENTS.md`: rules, verified stack versions, conventions.
3. `.wiki/log.md`: N0 findings (package versions, what the probe proved, the pending Relational package question).
4. `.wiki/plan/graph.yaml`: your node (N1) and the nodes that consume your spec (S0, A1, A2, B1, B2, B3).

## Files you may create

- `.wiki/domain/spec.md`: the full contract (see "What the spec must pin down").
- `.wiki/domain/<entity>.md`: one page per entity.
- `.wiki/domain/br1-<slug>.md` … `.wiki/domain/br5-<slug>.md`: one page per business rule.
- `docs/adr/NNNN-<title>.md`: one ADR per non-trivial decision, numbered from 0001, sections Context / Decision /
  Consequences. Keep them short. If the `engineering:architecture` skill is available, you may use it for the ADR format.
- `.wiki/decisions/index.md`: one line per ADR with a link to it.
- `.wiki/agents/bus/N1-to-orch-001.md`: your report.

Every wiki page gets YAML frontmatter with `title`, `type` (spec | entity | rule | decision-index), `status: draft`,
`updated: 2026-09-18`, `related` (a list of `[[wikilinks]]`). Link entity pages and rule pages to each other. Mark code
paths that don't exist yet as "planned".

## Do not touch

Everything else. In particular: no `.cs`, `.csproj` or solution files, and no edits to `AGENTS.md`,
`.wiki/index.md`, `.wiki/log.md`, `.wiki/plan/graph.yaml`, `.wiki/codemap.md`, or `ORCHESTRATOR_PROMPT.md`
(orch owns these). Leave every `.gitkeep` in place. Do not commit; orch commits after the human gate.

## Why the spec must be exact

Workers code against your spec in parallel, without seeing each other's code:
A1 (domain) and A2 (unit tests) run side by side, and so do B1 (persistence), B2 (application service) and
B3 (integration tests). Each name they share must be spelled out character for character, or the fan-in fails to compile.
That covers namespaces, type names, full member signatures, constructor/factory parameters, exception types and when
each is thrown, enum members, DbContext and DbSet names, and table, column, index and constraint names.

## What the spec must pin down

1. **Solution layout.** The brief suggests Domain / Infrastructure / Application / Tests: justify each project or
   merge them. Give exact `.csproj` paths, the project references, and the package references per project. Packages
   come only from the `AGENTS.md` stack table; the explicit `Microsoft.EntityFrameworkCore.Relational` 10.0.12 is
   pending approval. Say whether there are one or two test projects. Tests carry
   `[Trait("Category", "Unit")]` or `[Trait("Category", "Integration")]`.
2. **Naming traps.** `ImplicitUsings` imports `System.Threading.Tasks`, so an entity named `Task` collides with
   `System.Threading.Tasks.Task`, and an enum named `TaskStatus` collides with `System.Threading.Tasks.TaskStatus`.
   Pick names that avoid both.
3. **Entities.** Cover only what the brief requires: employees, tasks, assignment, deadlines, statuses, plus what the
   invariants need. For each: properties (CLR type, nullability, max length), how instances are created (constructor
   or factory with exact signature), the mutating methods, and which BR each guard enforces. Name the exception type
   each guard throws, with an example message. Use existing BCL exceptions unless a domain exception type earns its
   place; if you define one, give its exact name, namespace and constructors.
4. **Assignment model (ADR).** Choose between an assignee column on the task and a `TaskAssignment` history table.
   Hard input: the brief requires BR5 as a DB check constraint (B1). A PostgreSQL CHECK constraint is row-local and
   cannot contain a subquery, so BR5 as a CHECK needs the assignee and the creator on the same row. Decide, and write
   that reasoning into the ADR. Say whether an assign/reassign operation exists at all. The minimum use cases are:
   create a task, change a task's status, list tasks by assignee.
5. **Statuses.** The enum members, how the enum is stored (text or int, and whether a CHECK restricts the values),
   and the **full transition table as literal rows `(from, to, allowed)` covering every ordered pair, self-transitions
   included**. Add what happens to `CompletedAt` on each allowed transition (BR1) and how BR4 finality shows up.
6. **Time.** `DateTimeOffset` in UTC, stored as `timestamptz`. State how the domain treats a non-UTC offset: reject it,
   or normalise it. Check Npgsql's documented behaviour for writing a non-zero offset to `timestamptz`, or mark it
   `[uncertain]`. State where "now" comes from (for example `TimeProvider`, which is in the BCL, or explicit
   parameters) so tests are deterministic.
7. **BR2 and deadlines.** Nullability of `PlannedStartAt` and `DueAt`, and how BR2 behaves when either is null.
8. **BR3.** It is checked in the domain **and** in the application service (the brief says both). Define exactly what
   "given a new task" means for this model.
9. **Keys.** PK type and generation strategy. Known gotcha, relevant to the seed: seeding with `HasData` into a
   PostgreSQL identity column does not advance the identity sequence, so the first application insert can collide
   with a seeded id. Pick a strategy that avoids this and justify it in an ADR.
10. **Relational mapping.** Table and column names (snake_case is the PostgreSQL convention, but it must be achieved
    with the Fluent API and no extra package), FKs and their delete behaviours (with a reason), and the concurrency
    token. The N0 probe verified Npgsql's `xmin` mapping: a `uint` property configured with `.IsRowVersion()` builds,
    migrates and round-trips. Say whether it is a CLR property or a shadow property, and on which entities.
11. **Indexes and constraints: the full list.** Exact names, columns or SQL expression text, and the BR each one
    enforces. The brief requires: an index on (assignee + status), an index on due date, a unique index on email, and
    check constraints for BR1, BR2 and BR5. Decide whether email uniqueness is case-insensitive, and how. Add an explicit
    statement (in an ADR) on why BR3 and BR4 have no DB constraint: a row-local CHECK sees neither the prior state nor
    another row, and triggers were not requested.
12. **Seed data.** 2–3 rows per table with exact values (fixed ids, fixed UTC timestamps) that satisfy every
    constraint and every BR.
13. **DbContext contract.** Class name, namespace, constructor, DbSet property names, and how `dotnet ef` builds it at
    design time when no host exists (for example `IDesignTimeDbContextFactory<T>`), including where the design-time
    connection string comes from.
14. **Application service contract.** Class name, namespace, constructor dependencies, and the exact async method
    signatures with `CancellationToken` for the three use cases, plus return types and not-found behaviour. Three use
    cases only. No interface for a single implementation unless you justify it.
15. **Test plan.** A2 unit tests: test class names, and the test cases per BR and per transition-table row.
    B3 integration tests: the raw SQL each test issues to prove the DB itself rejects BR1, BR2 and BR5 violations
    (expected SqlState and constraint name), plus: the migration applies to an empty DB, the seed rows load, and the
    three use cases work end to end. BR→test mapping table.
16. **Verification.** For every package, API, type, method and Npgsql behaviour you name, record where you verified it:
    restored package XML docs under `~/.nuget/packages/<id>/<version>/`, api.nuget.org, or Microsoft Learn / Npgsql
    docs (the URL). If you can't verify something, mark it `[uncertain]`.

Stay inside the brief. Anything outside it (priorities, comments, tags, soft delete, audit tables, pagination,
repositories, CQRS, MediatR) is out unless the brief needs it; flag it as a question instead.

## Report (write to `.wiki/agents/bus/N1-to-orch-001.md`)

- status: done | blocked
- files written (paths)
- decisions, at most 15 lines
- verification table: identifier → source
- `[uncertain]` items
- at most 3 open questions for the human, each with your recommended default
