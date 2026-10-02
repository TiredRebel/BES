# Roslyn code analyzers: best practices and rulesets

> Source: project guideline document "roslyn code analyzers best practices and rulesets".
> Normative for analyzer policy in this repository.

Treat Roslyn analyzers as a versioned, source-controlled policy: use SDK-provided .NET analyzers as
the base, configure them in `.editorconfig`, fail CI only on a curated set of high-confidence
diagnostics, and avoid legacy `.ruleset` files for new work. .NET enables analyzers by default for
projects targeting .NET 5+; older targets opt in with `EnableNETAnalyzers`.

## Mental model

| Layer | Purpose | Examples |
|---|---|---|
| Compiler diagnostics | Language correctness | CS8602, CS8618, unreachable code, invalid conversions |
| Built-in .NET analyzers | Reliability, API design, performance, globalization, maintainability | CA1062, CA1822, CA2007, CA2016 |
| Code-style analyzers | Readability and consistency | IDE0005, IDE0055, naming rules |
| Third-party / custom | Organization- or domain-specific policy | SonarAnalyzer, Meziantou.Analyzer, async/API/architecture rules |

A diagnostic may have a matching code fix: the IDE applies the safe mechanical transformation, and
`dotnet format analyzers` applies many supported fixes non-interactively.

## Preferred configuration model

Use `.editorconfig` as the primary mechanism. Rule sets can coexist, but `.ruleset` was deprecated
in favor of EditorConfig starting with Visual Studio 2019 16.5, and `.editorconfig` takes precedence
where the two conflict.

```
repo/
├─ Directory.Build.props
├─ .editorconfig
├─ globalconfig/.globalconfig   # optional
└─ src/
```

`Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <!-- Make IDE-style diagnostics participate in dotnet build. -->
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <!-- Do not set this globally until the repository is clean. -->
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

`AnalysisLevel` controls the analyzer rule-set version the SDK selects. Pinning it makes behavior
predictable across machines and CI; a newer SDK otherwise brings new diagnostics with the update.
For an established codebase start at `latest-recommended`, establish a baseline, then elevate
selected rules deliberately.

## Severity configuration

```ini
root = true

[*.cs]
# Global default: analyzer findings visible but not build-breaking
dotnet_analyzer_diagnostic.severity = suggestion

# Reliability and correctness: build-breaking once clean
dotnet_diagnostic.CA2000.severity = error
dotnet_diagnostic.CA2016.severity = error
dotnet_diagnostic.CA2200.severity = error

# API and defensive-programming policy
dotnet_diagnostic.CA1062.severity = warning
dotnet_diagnostic.CA1031.severity = warning

# Performance: useful, normally not a PR blocker
dotnet_diagnostic.CA1822.severity = suggestion
dotnet_diagnostic.CA1825.severity = suggestion
dotnet_diagnostic.CA1848.severity = suggestion

# Readability / formatting
dotnet_diagnostic.IDE0005.severity = warning
dotnet_diagnostic.IDE0055.severity = warning

# Intentional team decision
dotnet_diagnostic.CA2007.severity = none

# Rule-specific options
dotnet_code_quality.CA1062.null_check_validation_methods = ThrowIfNull
dotnet_code_quality.CA1031.api_surface = public, internal
```

Per-rule syntax:

```ini
dotnet_diagnostic.<RULE-ID>.severity = error|warning|suggestion|silent|none|default
```

Severity can also be applied by analyzer category or globally. Do not enable every rule as an error:
severity expresses operational risk and remediation cost, not whether a rule is theoretically
valuable.

## Rules worth prioritizing

| Priority | Rule | Action | Why |
|---|---|---|---|
| Highest | Nullable compiler warnings (CS8600–CS8625) | Warning, then error after cleanup | Prevents null-reference bugs, improves API contracts |
| Highest | CA2000 — dispose objects before losing scope | Error for new/clean code | Detects resource leaks |
| Highest | CA2016 — forward `CancellationToken` | Error in service/application layers | Avoids work continuing after cancellation |
| Highest | CA2200 — rethrow to preserve stack details | Error | Preserves useful stack traces |
| High | CA1062 — validate public-method arguments | Warning or error for public libraries | Boundary validation; configure the API surface |
| High | CA1031 — do not catch general exception types | Warning | Prevents swallowing failures; permit narrow, documented boundaries |
| High | CA1848 — use `LoggerMessage` delegates | Warning in high-throughput services | Reduces logging overhead |
| High | CA2017 — parameter count matches logging template | Error | Prevents malformed structured logs |
| Medium | CA2007 — do not directly await a task | Policy-based | Useful in reusable libraries, commonly disabled in app code |
| Medium | CA1816 — call `GC.SuppressFinalize` correctly | Warning | Relevant to the dispose pattern |
| Medium | CA1852 — seal internal types | Suggestion | Minor performance/design gain; keep it quiet |
| Medium | CA1822 — mark members static | Suggestion | Helpful cleanup, not build-blocking |
| Medium | CA1825 — avoid empty-array allocations | Suggestion | Mechanical micro-allocation improvement |
| Low | IDE0005, IDE0055, naming, `var` preferences | Warning plus automated formatting | Automate consistency, do not debate it in review |

Keep style diagnostics separate from correctness diagnostics so a formatting dispute cannot hide a
production-risk finding.

## Policy by project type

| Project type | Emphasize | Relax or scope |
|---|---|---|
| Reusable NuGet library | Public API design, nullability, argument validation, XML docs, compatibility, cancellation | Framework-specific synchronization-context rules |
| ASP.NET Core service | Cancellation, disposal, logging, configuration/secret handling, async correctness, observability | CA2007 — no request `SynchronizationContext` concern |
| Console worker / background service | Cancellation, hosted-service lifecycle, disposal, logging, resilience | UI thread-affinity rules |
| Desktop UI app | UI-thread correctness, async behavior, disposable subscriptions/resources | Blanket `ConfigureAwait(false)` policies — evaluate per UI boundary |
| Domain model / test project | Simplicity, immutability, correctness | Documentation, test naming, some design rules |
| Generated code | Compile correctness only | Most style/design analyzers excluded |

Scope exceptions by directory or project instead of repeating in-source suppressions:

```ini
# Product code
[src/**/*.cs]
dotnet_diagnostic.CA1062.severity = warning
dotnet_diagnostic.CA2016.severity = error

# Tests need a lower-noise policy
[tests/**/*.cs]
dotnet_diagnostic.CA1707.severity = none
dotnet_diagnostic.CA1062.severity = none
dotnet_diagnostic.CA1822.severity = none

# Generated files should not create review noise
[**/*.g.cs]
generated_code = true
dotnet_analyzer_diagnostic.severity = none

[**/*.Designer.cs]
generated_code = true
dotnet_analyzer_diagnostic.severity = none
```

## Suppressions

Use the narrowest mechanism, in this order:

1. Fix the code if the analyzer found a real issue.
2. Configure the rule globally or by scope when it does not fit a project type, folder or test
   convention.
3. Use a local suppression only for a reviewed, intentional exception.
4. Use `NoWarn` sparingly — it is broad, less discoverable, and can conceal new violations.

```csharp
#pragma warning disable CA1031 // Boundary handler must isolate plug-in failures.
try
{
    plugin.Execute(context);
}
catch (Exception ex)
{
    logger.LogError(ex, "Plug-in {PluginName} failed", plugin.Name);
}
#pragma warning restore CA1031
```

Catching `Exception` is defensible only at a true fault-isolation boundary that records and handles
the failure. For a wider policy decision, make the exception auditable in `.editorconfig` rather than
scattering suppressions.

## Rulesets and migration

For new SDK-style code, do not start with `.ruleset`. Migrate legacy rules incrementally:

1. Keep the current `.ruleset` temporarily so behavior stays stable.
2. Create a root `.editorconfig`.
3. Convert a small, understood group of severities at a time.
4. Verify the diagnostic set in the IDE and with `dotnet build`.
5. Remove `CodeAnalysisRuleSet` from project files after parity is confirmed.
6. Delete the legacy `.ruleset` once nothing references it.

`Microsoft.CodeAnalysis.RulesetToEditorconfigConverter` converts rule-set files to equivalent
`.editorconfig` configuration. Avoid leaving contradictory settings in place long-term.

## CI workflow

Separate formatting from correctness, and pin the SDK with `global.json` so CI matches developers.

```bash
dotnet restore

# Deterministic whitespace/style and available analyzer code fixes
dotnet format whitespace --verify-no-changes --no-restore
dotnet format style --verify-no-changes --no-restore
dotnet format analyzers --verify-no-changes --no-restore

# Compiler diagnostics plus analyzers configured for build
dotnet build --configuration Release --no-restore

# Behavioral validation
dotnet test --configuration Release --no-build
```

`EnforceCodeStyleInBuild` matters because some style analyzers otherwise appear only in the IDE and
do not participate in command-line builds. The CI gate should enforce errors plus the warnings the
team explicitly treats as failures.

## Custom analyzers

Write one only when the rule is specific to your architecture or domain, hard to enforce with
existing analyzers or tests, stable enough to have a very low false-positive rate, and valuable
enough to maintain across Roslyn and SDK versions.

Good examples: enforce a company-standard `Result<T>` abstraction in endpoint handlers; prohibit
direct use of legacy service clients outside an adapter assembly; require tenant-context propagation
in data access; block unsafe/internal APIs across project boundaries; require certain commands to
publish an audit event.

Poor examples: personal formatting preferences already covered by `.editorconfig`; rules needing
runtime data flow static analysis cannot determine; broad architectural "smells" with frequent
legitimate exceptions; checks that are simpler and more accurate as tests.

Provide a code fix whenever a safe mechanical correction exists. Test each analyzer against valid
and invalid snippets, include regression cases for false positives, and distribute it as an internal
NuGet analyzer package rather than a loose DLL.

A disciplined policy — small error set, scoped configuration, CI verification, reviewed suppressions
— beats enabling hundreds of rules and training developers to ignore warnings.

Sources: Microsoft Learn Roslyn analyzers overview, code-analysis configuration options and files,
code-quality rule options, analyzers FAQ.
