# Microsoft C#/.NET coding standards and tooling

> Source: project guideline document "ms coding standards and tools". Normative for this
> repository unless `AGENTS.md` or an ADR says otherwise.

Microsoft's practical approach for C#/.NET: document a small set of team conventions, encode
them in `.editorconfig`, enforce correctness with Roslyn/.NET analyzers, and run formatting plus
analysis in CI. Microsoft's published conventions are a strong baseline, not an inflexible
universal standard.

## C# baseline

| Area | Standard |
|---|---|
| Indentation | 4 spaces; never tabs |
| Braces | Allman style: opening and closing braces on their own lines |
| Naming | PascalCase for types, public/protected members, methods, properties, events, constants, enum values; camelCase for parameters and locals; interfaces start with `I` |
| Fields | Private fields use `_camelCase`; make the rule explicit in `.editorconfig` |
| Types | Prefer C# aliases (`string`, `int`, `bool`) over `String`, `Int32`, `Boolean` |
| Accessibility | Specify access modifiers deliberately, especially on externally visible APIs |
| Structure | One statement/declaration per line; blank line between logical blocks; keep methods focused |
| Comments | Explain why, constraints, and non-obvious trade-offs — not line-by-line mechanics |
| Resources | `using` / `await using` for disposables |
| Async | Async all the way; accept `CancellationToken` in I/O-bound application and service methods; never block on `.Result` / `.Wait()` |
| Exceptions | Do not catch merely to rethrow; preserve context; use domain-appropriate exceptions and structured logging |
| APIs | Clear contracts, nullable reference types enabled, validation at boundaries, predictable error behavior |

## Core tools

- **`.editorconfig`** — the source-controlled policy file for whitespace, naming, code style,
  severity levels and analyzer configuration. Visual Studio applies applicable files by scope, so a
  root policy can carry more specific per-project overrides.
- **Roslyn/.NET analyzers** — compiler-integrated diagnostics for code quality, design,
  reliability, globalization and style. Configure severity so important rules fail the build instead
  of becoming optional IDE hints.
- **`dotnet format`** — command-line formatting and analyzer-driven code fixes; ideal for
  pre-commit checks and CI.
- **Visual Studio / Rider inspections** — fast feedback while editing, plus refactorings that comply
  with the repository's rules.
- **SonarQube / SonarCloud** — pull-request quality gates, code smells, duplication, reliability
  findings, trend reporting.
- **StyleCop.Analyzers** — optional, when stricter prescriptive documentation/style rules are
  needed. Do not layer it blindly over Roslyn rules; resolve overlaps explicitly.
- **Security scanning** — `dotnet list package --vulnerable --include-transitive`, Dependabot or
  Renovate, secret scanning, and a SAST tool such as CodeQL where available.
- **Tests and coverage** — xUnit/NUnit/MSTest, optionally FluentAssertions; Coverlet and
  ReportGenerator when coverage reporting is useful. Coverage percentage is never a substitute for
  meaningful test cases.

The benefit is eliminating subjective review churn: IDEs and CI report violations automatically,
instead of reviewers debating spacing, naming or minor syntax.

## Minimal repository setup

`Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnableNETAnalyzers>true</EnableNETAnalyzers>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>
</Project>
```

Start with `latest-recommended`, not the most aggressive rule set. Promote individual diagnostics
to errors only after the codebase is clean or an intentional baseline/suppression strategy exists.

`.editorconfig` starting point:

```ini
root = true

[*]
charset = utf-8
end_of_line = lf
insert_final_newline = true
indent_style = space
indent_size = 4
trim_trailing_whitespace = true

[*.cs]
indent_size = 4

# Braces and layout
csharp_new_line_before_open_brace = all
csharp_preserve_single_line_statements = false
csharp_preserve_single_line_blocks = true

# Prefer explicit, readable code
csharp_style_var_for_built_in_types = false:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_elsewhere = false:suggestion
dotnet_style_qualification_for_field = false:suggestion
dotnet_style_qualification_for_property = false:suggestion
dotnet_style_qualification_for_method = false:suggestion

# Modern idioms
csharp_style_namespace_declarations = file_scoped:suggestion
csharp_style_prefer_primary_constructors = false:suggestion
dotnet_style_readonly_field = true:warning

# Naming
dotnet_naming_rule.interfaces_start_with_i.severity = warning
dotnet_naming_rule.interfaces_start_with_i.symbols = interfaces
dotnet_naming_rule.interfaces_start_with_i.style = starts_with_i
dotnet_naming_symbols.interfaces.applicable_kinds = interface
dotnet_naming_symbols.interfaces.applicable_accessibilities = public, internal
dotnet_naming_style.starts_with_i.required_prefix = I
dotnet_naming_style.starts_with_i.capitalization = pascal_case

dotnet_naming_rule.private_fields_use_underscore.severity = warning
dotnet_naming_rule.private_fields_use_underscore.symbols = private_fields
dotnet_naming_rule.private_fields_use_underscore.style = underscore_camel
dotnet_naming_symbols.private_fields.applicable_kinds = field
dotnet_naming_symbols.private_fields.applicable_accessibilities = private
dotnet_naming_style.underscore_camel.required_prefix = _
dotnet_naming_style.underscore_camel.capitalization = camel_case
```

Whether private fields use `_camelCase` or `camelCase` matters less than making one convention
machine-enforced.

## CI quality gate

```bash
dotnet restore
dotnet format --verify-no-changes --no-restore
dotnet build --configuration Release --no-restore
dotnet test --configuration Release --no-build
dotnet list package --vulnerable --include-transitive
```

A stricter pipeline can add:

```bash
dotnet format analyzers --verify-no-changes --no-restore
dotnet test --configuration Release --collect:"XPlat Code Coverage"
```

Keep the pull-request gate fast enough that developers do not bypass it. Run slower tasks — full
integration tests, deep SAST, dependency/license audits, mutation tests — on merge, nightly or
release branches.

## Review standard

Review what automation cannot judge:

- Does the change meet the intended behavior, including failure paths?
- Are public contracts, validation rules and nullability correct?
- Is cancellation, timeout, disposal and retry behavior appropriate?
- Is the design easy to test without excessive mocking?
- Are logs structured, safe and useful in production?
- Does the code introduce unnecessary coupling or premature abstraction?
- Do tests check behavior and edge cases rather than implementation details?
- Are security and privacy implications considered for external input, authentication, secrets and
  authorization?

Do not spend review time on formatting and trivial naming that the editor and CI can enforce.

## Adoption path

1. Add `.editorconfig`, nullable reference types, `dotnet format` and built-in analyzers.
2. Make formatting mandatory in pre-commit or CI.
3. Turn clear correctness/reliability diagnostics into build errors.
4. Improve legacy code gradually rather than attempting a style-only rewrite.
5. Add SonarQube/CodeQL/dependency scanning when the workflow justifies the overhead.
6. Keep a short `CONTRIBUTING.md` for the rules that need human judgment: architecture, test
   expectations, logging, API design, review process.

Sources: Microsoft Learn C# coding conventions; Visual Studio coding-standards guidance.
