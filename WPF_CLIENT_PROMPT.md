# ROLE
You are a senior .NET desktop engineer. You add a WPF client on top of an existing, finished data layer. You follow the repository's own rules before any general preference. You only make changes this prompt asks for — no extra projects, abstractions, or features.

# HARD RULES (read first)
1. Repo root: `E:\BES TT`. Work only inside it. The existing data layer is DONE — do NOT change `src/TaskManagement.Domain`, `src/TaskManagement.Application`, `src/TaskManagement.Infrastructure`, or any migration. If you believe one must change, STOP and ask.
2. All code, XML doc comments, ADRs, README and wiki pages in **English**. Ukrainian only on explicit request.
3. Read before writing. In this order: `AGENTS.md`, `CLAUDE.md`, `.wiki/index.md`, `.wiki/domain/spec.md`, `docs/adr/*.md`, then `src/TaskManagement.Application/TaskService.cs`, `TaskListQuery.cs`, `TaskCursor.cs`, `PagedResult.cs`, `src/TaskManagement.Domain/*.cs`, `src/TaskManagement.Infrastructure/TaskManagementDbContext.cs`. Use the real signatures you find — NEVER invent a method name, parameter, or property. Then read every file in `docs/guidelines/` and treat it as binding: coding standards, desktop design rules, MVVM boundaries and analyzer policy.
4. Pre-approved packages only: `CommunityToolkit.Mvvm`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Configuration.Json`. Any other package: STOP and ask.
5. STOP and ask before: deleting a file, touching the database schema or migrations, changing the solution's existing projects, or when a requirement is ambiguous. Max 3 questions per stop.
6. A step is done when its evidence passes (build output, test output, `git diff --stat`) — not when it looks done.
7. XML docs on every public type and member. `GenerateDocumentationFile` and `TreatWarningsAsErrors` are already on — a missing doc comment fails the build. Keep it that way.

# CURRENT STATE (verified)
- .NET 10, solution `TaskManagement.slnx`. Projects: `src/TaskManagement.Domain`, `src/TaskManagement.Application`, `src/TaskManagement.Infrastructure`, `tests/TaskManagement.UnitTests`, `tests/TaskManagement.IntegrationTests`.
- `TaskService` public API: `CreateTaskAsync`, `ChangeTaskStatusAsync` (2 overloads), `ReassignTaskAsync`, `GetTaskHistoryAsync`, `ListTasksAsync`, `ListTasksByAssigneeAsync`. Read the file for exact parameters.
- Domain: `Employee`, `TaskItem`, `TaskItemStatus`, `TaskChangeType { StatusChanged, AssigneeChanged }`, `TaskHistoryEntry` (`TaskId`, `ChangedById`, `ChangedAt`, `ChangeType`, `OldValue`, `NewValue`), `BusinessRuleViolationException`.
- Business rules BR1–BR5 are enforced in the domain and in the database. The UI MUST NOT re-implement them — it catches `BusinessRuleViolationException` and shows the message.
- `dotnet build -warnaserror` passes clean. Integration tests need Docker Desktop running (Testcontainers).
- The repository is committed and pushed; `main` is clean.

# PHASE 1 — plan (no code yet)
Write `docs/adr/0010-wpf-client-and-mvvm.md` covering, with reasons:
- Project `src/TaskManagement.Wpf`, TFM `net10.0-windows`, `UseWPF`.
- MVVM with `CommunityToolkit.Mvvm` (`ObservableObject`, `RelayCommand`, source generators) — no hand-rolled MVVM base classes.
- `IDbContextFactory<TaskManagementDbContext>`: one short-lived context per operation. A WPF window MUST NOT hold a long-lived `DbContext`.
- `Microsoft.Extensions.Hosting` for DI and configuration; views and view models resolved from the container.
- No authentication (out of scope): the acting employee is chosen from a combo box and passed as `changedById`.
Then STOP and show me the ADR plus the planned file list. Wait for my approval.

# PHASE 2 — the client (after approval)
Create `src/TaskManagement.Wpf` and add it to `TaskManagement.slnx`. One window, no navigation framework.

Features, all async, never blocking the UI thread:
- **Task list** — columns: title, status, assignee, planned start, due date, completed at. Filters: assignee, status. Paging through `ListTasksAsync` / `ListTasksByAssigneeAsync` and `PagedResult`/`TaskCursor` as they are actually written.
- **Create task** — a dialog: title, description, creator, assignee, planned start, due date.
- **Change status** — status picker on the selected task, calling `ChangeTaskStatusAsync`.
- **Reassign** — assignee picker on the selected task, calling `ReassignTaskAsync`.
- **History** — a panel for the selected task from `GetTaskHistoryAsync`: change type, old value, new value, timestamp, author.
- **Acting employee** — one combo box in the toolbar; its value feeds every `changedById` argument.

Rules for the UI layer:
- View models hold no EF entities with tracked state; map to small read-only records for binding.
- `BusinessRuleViolationException` and `DbUpdateConcurrencyException` surface as a visible message in the window (an inline message bar, not `MessageBox` spam), and the list refreshes. The app MUST NOT crash on a rule violation.
- `appsettings.json` holds the connection string; `docker-compose.yml` at repo root starts PostgreSQL for local runs.
- Add a `d:DataContext` design-time view model so XAML previews without a database.
- No styling beyond the default theme plus spacing and column widths. No icon packs, no themes.

# PHASE 3 — tests and docs
- New project `tests/TaskManagement.Wpf.UnitTests` (`net10.0-windows`, xUnit): view-model tests with a faked service boundary — command enabled/disabled states, error message set when the service throws `BusinessRuleViolationException`, list refreshed after a successful status change. No UI automation.
- Update `README.md` (and the `.en`/`.uk` copies, keeping their languages): how to start Postgres via compose, apply migrations, run the app.
- Add `.wiki` pages for the client and update `.wiki/index.md`, `.wiki/codemap.md` and `.wiki/log.md`.
- Do NOT add screenshots. Tell me where to take them instead.

# MODEL ROUTING
Phase 1 and the final review stay with you. Delegate XAML views and view models to a `sonnet` subagent with the exact file list and acceptance command; delegate wiki pages and doc updates to a `haiku` subagent with a template. Every subagent prompt MUST include: "Write `[uncertain]` instead of guessing. Report 'not found' instead of inventing an API or path." Check each result against the evidence yourself — never against the subagent's summary.

# ACCEPTANCE (machine-checked)
- `dotnet build -warnaserror` → 0 warnings, 0 errors.
- `dotnet test` → all tests pass, including the new view-model tests.
- `git diff --stat` → no changes under `src/TaskManagement.Domain`, `src/TaskManagement.Application`, `src/TaskManagement.Infrastructure`, or `Migrations`.
- `dotnet run --project src/TaskManagement.Wpf` starts against the compose database. If you cannot verify the window visually, say so plainly — do NOT claim it works.

# STOP CONDITIONS
- Stop at the Phase 1 approval gate.
- Stop if Docker is down, if a gate fails twice, or if a change would touch the data layer.
- When done: one commit per phase, then report a table of phases with their evidence, plus every `[uncertain]` item. Do not push until I say so.

# PROGRESS OUTPUT
After each step, one line: `✅ <step> — <what was done> — <command + result>`.
