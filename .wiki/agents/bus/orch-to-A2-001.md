---
title: "orch → A2: unit-test task"
type: bus-message
status: sent
updated: 2026-09-18
related: ["[[spec]]", "[[br1-completed-at]]", "[[br2-due-not-before-start]]", "[[br3-inactive-assignee]]", "[[br4-final-statuses]]", "[[br5-no-self-assignment]]", "[[index]]"]
from: orch
to: A2
seq: 1
---

# A2 Unit tests: task

You are node **A2 (Unit tests, sonnet)** in `.wiki/plan/graph.yaml`. You write the domain unit tests **from the spec
only**, in your own git worktree, in parallel with A1, which writes the domain code. `src/TaskManagement.Domain` is
empty in your worktree, and that's expected: your tests meet A1's code at fan-in A. So every type, member, exception
and `RuleId` you use must match the spec character for character.

Write `[uncertain]` instead of guessing. Report "not found" instead of inventing a path or API.

## First

1. Load the `ponytail:ponytail` skill with the Skill tool (human instruction for every development node): the
   smallest tests that prove each spec row. The spec's test list is required, not optional.
2. Read `AGENTS.md`, then `.wiki/domain/spec.md` §2, **§2a**, §3 (the API under test), §5 (the transition table), §6,
   §7, §8, and **§15 "A2 unit tests"** (your exact test list), then the BR pages `.wiki/domain/br1-*.md` … `br5-*.md`.

## Files you create (only these)

- `tests/TaskManagement.UnitTests/EmployeeTests.cs`
- `tests/TaskManagement.UnitTests/TaskItemCreateTests.cs`
- `tests/TaskManagement.UnitTests/TaskItemChangeStatusTests.cs`
- `.wiki/agents/bus/A2-to-orch-001.md`: your report.

Namespace `TaskManagement.UnitTests` (file-scoped). Implement **every** test named in spec §15 "A2 unit tests", with
exactly those method names. Every test method has `[Trait("Category", "Unit")]` and an XML doc whose `<summary>`
names the BR it proves (§2a). Test classes are `public sealed` with XML docs. Assert exception types exactly
(`Assert.Throws<T>`), and assert `RuleId`, never message text. The transition theory is
`ChangeStatus_TransitionTableRow_BehavesAsSpecified(TaskItemStatus from, TaskItemStatus to, bool allowed)` with the 16
`[InlineData]` rows of spec §5, with enum values written as `TaskItemStatus.New` (§2a).

## Do not touch

Everything else: every `.csproj`, `src/**`, `tests/TaskManagement.IntegrationTests/**`, and all docs outside your
report. Do not create domain code, not even stubs, anywhere in the repo.

## Checking your work

Your worktree can't compile the tests, because the domain types don't exist there. To catch syntax, analyzer and xUnit
analyzer errors anyway, you may write a **signatures-only stub** of the spec §3 API (bodies `throw new
NotImplementedException()`) in a temporary folder **outside the repo**, copy your test files next to it with copies of
`Directory.Build.props` and `.editorconfig`, and build that. Report the result. Never commit the stub. If you do not
do this, run `dotnet build tests/TaskManagement.UnitTests` in the worktree and report the histogram of error codes: it
should show only missing-type/member errors (CS0246, CS0103, CS1061, CS0117).

## Acceptance

`git diff --stat HEAD~1` after your commit touches only your 3 test files and your report. Commit on your worktree
branch with the message `test(domain): unit tests for BR1-BR5 and the transition table` and the trailer
`Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>`.

## Questions

If you need something from A1 (for example an exception type) or from orch, send it to orch with SendMessage
(`to: "main"`) and also write it to `.wiki/agents/bus/A2-to-orch-<seq>.md`. Orch answers from the spec. Don't wait
idle: continue with the rest.

## Report (`.wiki/agents/bus/A2-to-orch-001.md`, committed with your tests)

Frontmatter like this file's (`from: A2`, `to: orch`). Then: status; files changed; a table **test method → BR / spec
row** covering every §15 A2 test (and all 16 transition rows); the commands you ran with their real output (the stub
build or the error-code histogram, `git log --oneline -1`, `git diff --stat HEAD~1`); the worktree branch name;
`[uncertain]` items; open questions.
