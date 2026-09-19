---
title: "orch → B2: application service task"
type: bus-message
status: sent
updated: 2026-09-18
related: ["[[spec]]", "[[task-item]]", "[[br3-inactive-assignee]]", "[[index]]"]
from: orch
to: B2
seq: 1
---

# B2 Application service: task

You are node **B2 (Application service, sonnet)** in `.wiki/plan/graph.yaml`. You implement the three use cases of
the module (assigning a task, executing it through its statuses, and controlling who has what) in one class, `TaskService`.
You run in your own worktree in parallel with B1, which writes `TaskManagementDbContext`, and B3, which writes the
integration tests. The DbContext does **not** exist in your worktree: code against its contract in spec §13.

Write `[uncertain]` instead of guessing. Report "not found" instead of inventing a path or API.

## First

1. Load the `ponytail:ponytail` skill with the Skill tool (human instruction for development nodes). What the spec
   requires is required.
2. Read `AGENTS.md`, then `.wiki/domain/spec.md` §2, **§2a**, §3 (the domain API, already merged in
   `src/TaskManagement.Domain/`), §6, §8, §13 (the DbContext contract) and **§14 (your contract, including step 6 of
   `CreateTaskAsync` and the status filter of `ListTasksByAssigneeAsync`)**, then `docs/adr/0009` and the
   `0008` amendment.

## File you create (only this)

- `src/TaskManagement.Application/TaskService.cs`: `public sealed class TaskService`, namespace
  `TaskManagement.Application`, constructor `(TaskManagementDbContext dbContext, TimeProvider timeProvider)`, the
  three methods with the exact §14 signatures and steps.
- `.wiki/agents/bus/B2-to-orch-001.md`: your report.

XML docs per §2a on the class, constructor and every method: `<summary>`, `<param>`, `<returns>`, one `<exception>`
per exception type that §14 lists, and `<remarks>` naming the BRs each method enforces. `ListTasksByAssigneeAsync`'s
docs say the returned tasks are untracked (status changes go through `ChangeTaskStatusAsync`). Validate
`ArgumentNullException.ThrowIfNull` only where the spec implies it (the domain validates the title). Forward the
`CancellationToken` to every async EF call (CA2016).

## Checking your work

Your worktree can't compile `TaskService` because `TaskManagementDbContext` is missing. Compile it anyway, **outside
the repo**: in a temporary folder, create a class library that references the same packages as
`src/TaskManagement.Infrastructure/TaskManagement.Infrastructure.csproj`, includes copies of the four domain files,
a **stub** `TaskManagementDbContext` built from spec §13 (the constructor and the two `DbSet` properties only), your
`TaskService.cs`, and copies of `Directory.Build.props` and `.editorconfig`. Run `dotnet build -warnaserror` there and
report the output. Never commit the stub.

Confirm, and report, that `Npgsql.PostgresException` and `Npgsql.PostgresErrorCodes.CheckViolation` exist in the
restored `npgsql` 10.0.3 package (its XML docs or the stub build).

## Do not touch

Everything else: `src/TaskManagement.Domain/**`, `src/TaskManagement.Infrastructure/**`, `tests/**`, every `.csproj`,
and all docs outside your report. No new packages. No interface for the service.

## Acceptance

After your commit, `git diff --stat HEAD~1` touches only `TaskService.cs` and your report, and the out-of-repo stub
build exits 0. Compilation against the real DbContext is checked at fan-in B. Commit on your worktree branch:
`feat(application): task service for the three use cases`, with the trailer
`Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>`.

## Questions

Send them to orch with SendMessage (`to: "main"`) and also write them to `.wiki/agents/bus/B2-to-orch-<seq>.md`. Keep
working on the rest.

## Report (`.wiki/agents/bus/B2-to-orch-001.md`, committed with your code)

Frontmatter like this file's (`from: B2`, `to: orch`). Then: status; the public API (copied from your code); a table
**§14 step → code line** for all three methods; the stub build command and output; the Npgsql check; every command
you ran with its real output; `git diff --stat HEAD~1`; the worktree branch name and commit SHA; `[uncertain]` items;
open questions.
