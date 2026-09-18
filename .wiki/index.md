---
title: Wiki index
type: index
status: active
updated: 2026-09-18
related: ["[[log]]", "[[codemap]]", "[[spec]]", "[[decisions/index]]"]
---

# Task Management data layer: wiki index

Read this page first in every session. Instructions for agents live in `AGENTS.md`; this wiki holds the
project's state and knowledge. Plain Markdown with YAML frontmatter and `[[wikilinks]]`, so Obsidian can open it.

## Current state

- Phase: **N2 human gate**. N0 and N1 are done; N2 reviewed the spec (approve with fixes) and orch applied the fixes.
  Waiting for human approval before S0 scaffolds the solution.
- Resume here: read the newest entries of [[log]], then the node statuses in `plan/graph.yaml`.

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
| [[decisions/index]] | Links to the ADRs in `docs/adr/` (0001–0008). |
| `agents/bus/` | Message bus: one file per agent message, `<from>-to-<to>-<seq>.md` (task specs, reports, reviews). |

## Solution layout (pending N2 approval; see [[spec]] §1)

`src/TaskManagement.Domain`, `src/TaskManagement.Infrastructure`, `src/TaskManagement.Application`,
`tests/TaskManagement.UnitTests`, `tests/TaskManagement.IntegrationTests`, solution file `TaskManagement.slnx`.

## Business rules (source: the brief)

- BR1 `CompletedAt` is required when, and only when, status = `Completed`.
- BR2 `DueAt` can't be earlier than `PlannedStartAt`.
- BR3 An inactive employee can't be given a new task.
- BR4 `Completed` and `Cancelled` are final. No transition out of them.
- BR5 A task can't be assigned to its creator (assignee ≠ creator).
