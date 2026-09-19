---
title: "orch → D3: review task (five reviewers)"
type: bus-message
status: sent
updated: 2026-09-18
related: ["[[spec]]", "[[index]]", "[[log]]"]
from: orch
to: D3
seq: 1
---

# D3 Review: task

Node **D3** in `.wiki/plan/graph.yaml`. Five reviewers run in parallel, each with one lens. You review the **code
and the evidence**. You never read the workers' own summaries (`.wiki/agents/bus/A1-*`, `A2-*`, `B1-*`, `B2-*`,
`B3-*`): they were written by the thing being checked.

Write `[uncertain]` instead of guessing. Report "not found" instead of inventing a path or API.

## Inputs (read these)

- The diff: `git diff main...feature/task-management -- src tests .claude` (run from `E:\BSS TT`). Code stat:
  24 files, +2769 in `src/` and `tests/`.
- The brief `ORCHESTRATOR_PROMPT.md` (HARD RULES, THE TASK, DOCUMENTATION STANDARD), `AGENTS.md`, and the approved
  spec `.wiki/domain/spec.md` (the contract), plus `docs/adr/0009-br3-br4-enforced-by-trigger.md`.
- Evidence, captured by orch from real runs (outside the repo):
  `C:\Users\mcgun\AppData\Local\Temp\claude\E--BSS-TT\47f39304-3fe3-4f6d-a1a7-717e3bd77983\scratchpad\d3\`
  - `build.txt`: `dotnet build -warnaserror` (exit 0)
  - `test.txt`: `dotnet test` normal verbosity (54 unit + 34 integration, all passed)
  - `fa_red_evidence.txt` / `fb_red_evidence.txt`: which tests went red when each domain guard, CHECK, the trigger and
    each service BR3 half was removed.

## Lenses (you get exactly one, named in your prompt)

| Reviewer | Lens | Scope |
|---|---|---|
| code-reviewer | bugs, spec conformance, `AGENTS.md` rules (Fluent only, no annotations, UTC, async + CancellationToken, no invented APIs), anything outside the brief's scope | whole code diff |
| comment-analyzer | XML docs: accurate against the code, complete per the DOCUMENTATION STANDARD (`<summary>`, `<param>`, `<returns>`, `<exception>`, `<remarks>` naming the BR), Fluent config docs name their index/constraint and why, the migration header lists the real schema changes | `src/**`, `tests/**` |
| type-design-analyzer | encapsulation and invariant expression of the domain types; can any public path create or reach a state that breaks BR1–BR5 | `src/TaskManagement.Domain/*.cs` |
| silent-failure-hunter | swallowed or mistranslated errors, catch filters, fallbacks that hide failures | `src/TaskManagement.Application/TaskService.cs` (plus the persistence layer where it matters) |
| pr-test-analyzer | does each BR have a test that fails when its enforcement is removed (use the red evidence files); gaps against spec §15; brittle or tautological tests | `tests/**` |

## Output

Write **only** your report file (no code edits, no commits):
`.wiki/agents/bus/D3-to-orch-00N.md`, where N is 1 code-reviewer, 2 comment-analyzer, 3 type-design-analyzer,
4 silent-failure-hunter, 5 pr-test-analyzer. Frontmatter: `title`, `type: bus-message`, `status: sent`,
`updated: 2026-09-18`, `related`, `from: D3-<reviewer>`, `to: orch`, `seq: N`.

Body: a findings table: id, severity (`critical` | `major` | `minor` | `nit`), confidence (0–100), `file:line`,
what is wrong, **evidence** (the code line, a command and its output, or the spec section it breaks), and the
smallest fix. A finding without evidence is marked `[uncertain]`. Then list what you checked and found clean. Report
only real issues against the brief and spec. Style preferences the spec doesn't require are `nit` at most.
