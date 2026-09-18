---
title: "orch → N2: plan review task"
type: bus-message
status: sent
updated: 2026-09-18
related: ["[[spec]]", "[[N1-to-orch-001]]", "[[index]]"]
from: orch
to: N2
seq: 1
---

# N2 Plan review: task

You are node **N2 (Plan review, opus)** in `.wiki/plan/graph.yaml`. You have a fresh context on purpose: you did not
write the spec, and your job is to check it against the brief with the skepticism of someone who will be blamed
if the fan-in fails.

Write `[uncertain]` instead of guessing. Report "not found" instead of inventing a path or API.

## Inputs

- `ORCHESTRATOR_PROMPT.md`: the brief, the source of truth.
- `AGENTS.md`: rules and the verified stack.
- `.wiki/domain/spec.md`, `.wiki/domain/*.md`, `docs/adr/*.md`, `.wiki/decisions/index.md`: N1's output under review.
- `.wiki/agents/bus/N1-to-orch-001.md`: N1's own report. Read it last, and treat its claims as claims, not evidence.
- `.wiki/plan/graph.yaml`: the execution graph. Check that its node outputs match spec §1.
- `.wiki/log.md`: N0 findings and the CodeGraph correction.

Already resolved by orch after N1, with evidence (re-check them if you doubt them):
- `dotnet ef migrations list --project <p> --no-connect` from a directory with no project exits 0 without
  `--startup-project` (dotnet-ef falls back to `--project`). Resolves N1's `[uncertain]` #2.
- `dotnet test --filter Category=Unit` at solution level exits 0 when one test project matches zero tests (VSTest
  prints "No test matches"). Resolves N1's `[uncertain]` #1.

## What to check

1. **Brief coverage, line by line.** Go through THE TASK, LOCKED DEFAULTS, DOCUMENTATION STANDARD and the N1/B1/B2/B3
   items of the EXECUTION GRAPH. For each requirement, name where the spec meets it, or flag it as **missing**.
   At minimum: employees, tasks, assignment, deadlines, statuses; BR1–BR5 each with a domain check, a DB constraint
   where SQL can express it, and a test; the three use cases; indexes (assignee + status, due date, unique email);
   check constraints (BR1, BR2, BR5); FK delete behaviours; concurrency token; seed data of 2–3 rows per table;
   the assignment ADR; the enum and transition table; `DateTimeOffset`/`timestamptz`/UTC; async + `CancellationToken`;
   BR3 checked in both the application and the domain.
2. **Invented.** Any package, type, method, overload, CLI flag, SQL behaviour or file path in the spec that does not
   exist. Verify the risky ones yourself: restored package XML docs under `~/.nuget/packages/<id>/<version>/`, NuGet
   (api.nuget.org), Microsoft Learn, Npgsql docs, `dotnet ef ... --help`. Minimum sample: `TableBuilder.HasCheckConstraint`,
   `IsRowVersion` → `xmin`, `HasConversion<string>()`, `HasConstraintName`, `HasDatabaseName`, `Guid.CreateVersion7`,
   `TimeProvider.GetUtcNow`, `PostgreSqlBuilder(string)`, `PostgresException.SqlState`/`ConstraintName`,
   `Database.SqlQueryRaw<T>`.
3. **Out of scope.** Anything the brief did not ask for: web API, UI, host, extra features or layers, and abstractions
   with a single implementation.
4. **Internal consistency.** The same name spelled the same way everywhere: spec vs entity pages vs BR pages vs ADRs
   vs graph.yaml outputs. The transition table vs the `ChangeStatus` steps vs the A2 theory rows. The seed data vs
   every CHECK/FK/unique rule. The BR→test mapping vs the test tables.
5. **Parallel-work hazards.** Can A1 and A2 each finish independently from the spec alone and still compile together
   at fan-in? Same question for B1, B2 and B3. List every name a worker would have to guess.
6. **Analyzer/build hazards** under `Directory.Build.props` (TreatWarningsAsErrors, latest-recommended, CS1591 as error,
   EnforceCodeStyleInBuild): anything the spec prescribes that would fail the build.
7. **Decisions the human should see.** Choices N1 made that the brief leaves open, for example allowing
   `New → Completed` and `InProgress → New`, no reassignment, and lowercase e-mail normalisation. List them neutrally.

## Files you may write

Only `.wiki/agents/bus/N2-to-orch-001.md`. Everything else is read-only for you: no edits to the spec, the ADRs or
any other file, and no commits.

## Report format (`.wiki/agents/bus/N2-to-orch-001.md`)

Frontmatter: `title`, `type: bus-message`, `status: sent`, `updated: 2026-09-18`, `related`, `from: N2`, `to: orch`,
`seq: 1`. Then:

- **Verdict**: `approve` | `approve with fixes` | `reject`.
- **Findings table**: id, severity (`blocker` | `major` | `minor`), category (missing | invented | out-of-scope |
  inconsistent | hazard), location (file + section), what is wrong, and the evidence (command + output, or doc URL
  + quote). A finding without evidence is marked `[uncertain]`.
- **Coverage matrix**: each brief requirement → spec location, or MISSING.
- **Verified identifiers**: identifier → where you checked it.
- **Decisions for the human**: a neutral list.
