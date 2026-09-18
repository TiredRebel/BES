---
title: Wiki index
type: index
status: active
updated: 2026-09-18
related: ["[[log]]", "[[graph]]", "[[codemap]]"]
---

# Task Management data layer: wiki index

Read this page first in every session. Instructions for agents live in `AGENTS.md`; this wiki holds the
project's state and knowledge. Plain Markdown with YAML frontmatter and `[[wikilinks]]`, so Obsidian can open it.

## Where things are

| Page | What it holds |
|---|---|
| [[log]] | Append-only session and commit log. Read its last entries to see where work stopped. |
| `plan/graph.yaml` | Execution graph (DAG) with each node's status, inputs, outputs and acceptance command. |
| [[codemap]] | Generated code map: projects → namespaces → types → dependencies. |
| `domain/` | Data model spec, one page per entity, one page per business rule (BR1–BR5). Written in N1. |
| `decisions/` | Links to the ADRs in `docs/adr/`. Written in N1. |
| `agents/bus/` | Message bus: one file per agent message, `<from>-to-<to>-<seq>.md`. |

## Current state

- Phase: N0 bootstrap. Next: N1 architecture spec, then the N2 review and the human approval gate.
- Solution layout: not decided yet (N1).

## Business rules (source: the brief)

- BR1 `CompletedAt` is required when, and only when, status = `Completed`.
- BR2 `DueAt` can't be earlier than `PlannedStartAt`.
- BR3 An inactive employee can't be given a new task.
- BR4 `Completed` and `Cancelled` are final. No transition out of them.
- BR5 A task can't be assigned to its creator (assignee ≠ creator).
