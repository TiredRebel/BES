---
title: Wiki index
type: index
status: active
updated: 2026-09-19
related: ["[[log]]", "[[codemap]]", "[[spec]]", "[[decisions/index]]"]
---

# Task Management data layer: wiki index

[ Українська ](index.md) · [ **English** ]

Task Management is the internal CRM module for assigning, executing and controlling employees' tasks. This repo is
its data layer (PostgreSQL + EF Core).

Read this page first in every session. Instructions for agents live in `AGENTS.md`; this wiki holds the
project's state and knowledge. Plain Markdown with YAML frontmatter and `[[wikilinks]]`, so Obsidian can open it.

## Current state (updated 2026-09-19)

- **All execution-graph nodes and architectural enhancements are done.** Branch `feature/task-management`.
  `dotnet build -warnaserror` 0/0; `dotnet test` 101/101 (56 unit, 45 integration on PostgreSQL 17 via Testcontainers).
- **Implemented Enhancements (Session 2):**
  1. **Cyrillic Seed Data:** "Олена Коваленко", "Богдан Шевченко", "Оксана Мельник" and Ukrainian task titles; verified string length validation and PostgreSQL UTF-8 behavior.
  2. **Recommendation 1.1 (Query Object):** `TaskListQuery` and `ListTasksAsync` method supporting filtering by `AssigneeId`, `Status`, `DueFrom`, `DueTo`, `CreatorId`, `PageSize`, `Cursor`. Method `ListTasksByAssigneeAsync` preserved as a backwards-compatible wrapper.
  3. **Recommendation 2.1 (Keyset Pagination):** Implemented cursor pagination `TaskCursor(DueAt, Id)` and `PagedResult<T> : IReadOnlyList<T>` with `due_at ASC NULLS LAST, id ASC` ordering and page size clamping $1 \le \text{PageSize} \le 100$.
  4. **State Pattern / FSM:** Status transitions refactored to immutable transition matrix `FrozenDictionary<TaskItemStatus, FrozenSet<TaskItemStatus>> AllowedTransitions` in `TaskItem`.
  5. **Architectural Documentation:** Added comprehensive report and technical justification of decisions in `README.md` (Sections 2 and 3).
  6. **Bilingual Documentation:** Full parallel Ukrainian/English versions for all documentation (`README.md` / `README.en.md`, `.wiki/index.md` / `index.en.md`, `spec.md` / `spec.en.md`, BR1–BR5 pages) with language switchers and clickable links.
- **Resume here:** the newest checkpoint in `checkpoints/`, then the last two [[log]] entries, then
  `agents/bus/orch-to-D4-001.md`.
- **Waiting on the human (3 decisions):**
  1. Ratify D4's edits to the approved spec: §14 (failed-save cleanup), §15 (new tests), BR4/BR5 pages. Schema
     sections §10–§12 are unchanged.
  2. Extend the trigger to check BR3 on reassignment too (`UPDATE OF status, assignee_id` + the BR3 lookup on such
     updates)? A schema change (ADR 0009).
  3. Restrict the BR3 error translation to this call's own task (review finding SF3), or keep the documented behaviour?
- **Closed by documentation, not code:** SF3 (see decision 3), N1/SF6 (after a failed save the task is detached, so
  EF's "client wins" recovery saves nothing: retry by calling the service again), F2/T1 (see decision 2).
- **Still `[uncertain]`:** Antigravity reading a root `AGENTS.md` and its MCP config; the checkpoint hook has only
  been run by hand (it loads from the next Claude Code session); the "error using the connection" message on the first
  `database update` (exit 0); the `xmin` token cannot be ablated in isolation; the PostgreSQL 18 `pg_constraint`
  reason in the schema test; ADR 0005's non-ASCII lower-casing.
- **Next steps:** prune the 5 finished agent worktrees under `.claude/worktrees/` and their `worktree-agent-*`
  branches (needs the human's OK: it deletes files); optional test hardening (FOR SHARE timing, cancellation mid-save,
  same-status update allowed by the trigger) and the skipped nits listed in `agents/bus/orch-to-D4-001.md`.
- **History:** the human approved the spec at the N2 gate and four more decisions at the pre-implementation grill
  (BR3/BR4 trigger, status filter, demo seed in `InitialCreate`, BR3 trigger error translated to the domain
  exception); see [[log]].

## Pages

| Page | What it holds |
|---|---|
| [[log]] | Append-only session and commit log. Its newest entries show where work stopped. |
| `plan/graph.yaml` | Execution graph (DAG): per node its tier, inputs, exact outputs, acceptance command and status. |
| [[codemap]] | Code map built from the CodeGraph index: projects → namespaces → types → dependencies. |
| [[spec]] | The data model and every contract the workers code against: layout, entities, statuses and transitions, time, keys, mapping, constraints, seed, DbContext, service, test plan. |
| [[employee]] | Entity page: `Employee`. |
| [[task-item]] | Entity page: `TaskItem` and `TaskItemStatus`. |
| [[br1-completed-at]] | BR1: `CompletedAt` is set iff status = `Completed`. |
| [[br2-due-not-before-start]] | BR2: `DueAt` is not earlier than `PlannedStartAt`. |
| [[br3-inactive-assignee]] | BR3: an inactive employee cannot be given a new task. |
| [[br4-final-statuses]] | BR4: `Completed` and `Cancelled` are final. |
| [[br5-no-self-assignment]] | BR5: assignee ≠ creator. |
| [[decisions/index]] | Links to the ADRs in `docs/adr/` (0001–0009; 0009 supersedes 0004). |
| `checkpoints/` | Automatic progress checkpoints written by Claude Code hooks and agents before compaction and on API stops. |
| `agents/bus/` | Message bus: one file per agent message, `<from>-to-<to>-<seq>.md` (task specs, reports, reviews). |

## Solution layout (approved at N2; see [[spec]] §1)

`src/TaskManagement.Domain`, `src/TaskManagement.Infrastructure`, `src/TaskManagement.Application`,
`tests/TaskManagement.UnitTests`, `tests/TaskManagement.IntegrationTests`, solution file `TaskManagement.slnx`.

## Business rules (source: the brief)

- BR1 `CompletedAt` is required when, and only when, status = `Completed`.
- BR2 `DueAt` can't be earlier than `PlannedStartAt`.
- BR3 An inactive employee can't be given a new task.
- BR4 `Completed` and `Cancelled` are final. No transition out of them.
- BR5 A task can't be assigned to its creator (assignee ≠ creator).
