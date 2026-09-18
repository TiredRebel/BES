---
title: "orch → A1: domain task"
type: bus-message
status: sent
updated: 2026-09-18
related: ["[[spec]]", "[[employee]]", "[[task-item]]", "[[index]]"]
from: orch
to: A1
seq: 1
---

# A1 Domain: task

You are node **A1 (Domain, sonnet)** in `.wiki/plan/graph.yaml`. You run in your own git worktree, in parallel with
A2, which writes the unit tests from the same spec without seeing your code. Your code and A2's tests first meet at
fan-in A, so every name, signature, exception type and guard order must match the spec **character for character**.

Write `[uncertain]` instead of guessing. Report "not found" instead of inventing a path or API.

## First

1. Load the `ponytail:ponytail` skill with the Skill tool (human instruction for every development node): write the
   smallest code that meets the spec. What the spec requires (XML docs, guards, their order, exception types) is
   required, not optional.
2. Read `AGENTS.md`, then `.wiki/domain/spec.md` §1, §2, **§2a**, §3, §5, §6, §7, §8 (the domain part), §9, then
   `.wiki/domain/employee.md` and `.wiki/domain/task-item.md`.

## Files you create (only these)

- `src/TaskManagement.Domain/Employee.cs`
- `src/TaskManagement.Domain/TaskItem.cs`
- `src/TaskManagement.Domain/TaskItemStatus.cs`
- `src/TaskManagement.Domain/BusinessRuleViolationException.cs`
- `.wiki/agents/bus/A1-to-orch-001.md`: your report.

Namespace `TaskManagement.Domain` (file-scoped). Note §3.4: `BusinessRuleViolationException` has **two**
constructors, `(string ruleId, string message)` and `(string ruleId, string message, Exception innerException)`.

## Do not touch

Everything else: every `.csproj`, `Directory.Build.props`, `.editorconfig`, `tests/**`, `src/TaskManagement.Infrastructure/**`,
`src/TaskManagement.Application/**`, and all docs outside your report. No data-annotation attributes. No navigation
properties. No packages. If the spec looks wrong or incomplete, write it in your report as an open question and follow
the spec as written. Do not "improve" it.

## Acceptance (run it yourself, from the worktree root)

```bash
dotnet build src/TaskManagement.Domain -warnaserror
! grep -rnE '\[(Key|Required|Table|Column|MaxLength|StringLength|ForeignKey|Index|NotMapped|DatabaseGenerated|ConcurrencyCheck|Timestamp|InverseProperty|Owned|Precision|Unicode)\b' src/TaskManagement.Domain --include=*.cs
```

Both must exit 0. Then commit your files on your worktree branch with the message
`feat(domain): entities, status enum and business-rule exception` and the trailer
`Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>`.

## Questions

If you need something from A2 or from orch, send it to orch with SendMessage (`to: "main"`) and also write it to
`.wiki/agents/bus/A1-to-orch-<seq>.md`. Orch answers from the spec. Don't wait idle: continue with the rest.

## Report (`.wiki/agents/bus/A1-to-orch-001.md`, committed with your code)

Frontmatter like this file's (`from: A1`, `to: orch`). Then: status (done | blocked); files changed; the **public API
you implemented** (every public type and member signature, copied from your code); the commands you ran with their
real output (build tail, grep result, `git log --oneline -1`, `git diff --stat HEAD~1`); the worktree branch name
(`git rev-parse --abbrev-ref HEAD`); `[uncertain]` items; open questions.
