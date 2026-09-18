---
title: Session and commit log
type: log
status: active
updated: 2026-09-18
related: ["[[index]]"]
---

# Log

Append-only. One entry per session and per commit: date, agent, what changed, commit SHA.
Newest entry at the bottom.

## 2026-09-18 · orch (claude-opus-5) · session 1 · N0 bootstrap

- Verified environment: `dotnet --list-sdks` → 8.0.425, 9.0.318, **10.0.401** (latest LTS, `net10.0`).
  git 2.55.0, Docker server 29.8.0, dotnet-ef 10.0.12, graphify 0.9.46 (C# via tree-sitter-c-sharp 0.23.5).
- NuGet latest stable, checked on api.nuget.org: EF Core / Design 10.0.12, Npgsql EF 10.0.3,
  Testcontainers.PostgreSql 4.15.0. xUnit trio taken from the SDK 10.0.401 `xunit` template
  (xunit 2.9.3, xunit.runner.visualstudio 3.1.4, Microsoft.NET.Test.Sdk 17.14.1); `coverlet.collector` dropped.
- Throwaway probe (scratchpad, outside the repo) under the same `Directory.Build.props` + `.editorconfig`:
  - CA1707 fired on underscore test names → relaxed for `tests/**.cs` only.
  - CA1861 fired in the EF migration → `src/**/Migrations/*.cs` marked `generated_code = true`. CS1591 did not fire
    (EF emits `<inheritdoc />`).
  - MSB3277: Npgsql 10.0.3 pins Relational ≥ 10.0.4 while Design 10.0.12 compiles Infrastructure against 10.0.12.
    An explicit `Microsoft.EntityFrameworkCore.Relational` 10.0.12 reference fixes it. **[uncertain] unlisted package,
    awaiting human approval at N2.**
  - Result: `dotnet build -warnaserror` 0 errors; `dotnet test` 2/2 passed including Testcontainers
    (`postgres:17-alpine`) + `MigrateAsync` + `HasData`; `--filter Category=Unit` selects 1/1.
  - `graphify update` on the probe produced 64 nodes / 76 edges with C# namespaces, types, members, NuGet refs.
- Pointer files, formats checked against current docs:
  - `CLAUDE.md` = `@AGENTS.md` (code.claude.com/docs/en/memory, "AGENTS.md" section).
  - `.cursor/rules/agents.mdc` with `description` + `alwaysApply: true` (cursor.com/docs/context/rules). Cursor also
    reads `AGENTS.md` natively.
  - `.agents/rules/agents.md` for Antigravity (antigravity.google/docs/rules-workflows: workspace rules in
    `.agents/rules/`). **[uncertain]** the docs describe no frontmatter for activation mode; it is set in the UI
    ("Always On"). The official page does not say whether Antigravity reads a root `AGENTS.md`; third-party guides
    say it does.
- Pre-existing files: `ORCHESTRATOR_PROMPT.md` (the brief; committed as the source of truth) and
  `archive-kZFEsd/gk_3.1.75_windows_amd64.zip` (GitKraken CLI download; gitignored, not deleted).
- Note: the graphify CLI warns that its installed skill (0.9.32) is older than its package (0.9.46). Left alone;
  fixing it would change user config outside the repo.
- Commit: see next entry.
