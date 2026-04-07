---
name: roslyn-analyzer
description: This skill should be used when the user asks to "create an analyzer", "scaffold a Roslyn analyzer", "add a diagnostic", "add a code fix", "add a build-time warning", "create a linting rule", "enforce a coding convention", "review an analyzer", "check analyzer conventions", or mentions DiagnosticAnalyzer or CodeFixProvider in the Datadog.Trace.Tools.Analyzers projects.
argument-hint: [create <name>|review <name-or-path>]
allowed-tools: Read Grep Glob Bash(dotnet *) Write Edit
---

# Roslyn Analyzer Creator & Reviewer

Create new Roslyn analyzers/code fixes or review existing ones in the dd-trace-dotnet repository.

**Not for:** Source generators (`Datadog.Trace.SourceGenerators`), MSBuild tasks (`Datadog.Trace.MSBuild`), or other non-analyzer tooling.

## Project Layout

| Project | Path | Purpose |
|---------|------|---------|
| Analyzers | `tracer/src/Datadog.Trace.Tools.Analyzers/` | `DiagnosticAnalyzer` implementations |
| Code Fixes | `tracer/src/Datadog.Trace.Tools.Analyzers.CodeFixes/` | `CodeFixProvider` implementations |
| Tests | `tracer/test/Datadog.Trace.Tools.Analyzers.Tests/` | Unit tests for both |

Both target `netstandard2.0` with `Microsoft.CodeAnalysis.CSharp` v5.0.0. The code fixes project **links** diagnostic ID files from the analyzers project rather than duplicating them — this ensures diagnostic ID constants stay in sync between the two projects.

**Real-world examples:** Browse the existing analyzers at `tracer/src/Datadog.Trace.Tools.Analyzers/` and their code fixes at `tracer/src/Datadog.Trace.Tools.Analyzers.CodeFixes/` for complete, working implementations. `ThreadAbortAnalyzer` is the simplest; `SealedAnalyzer` demonstrates cross-file analysis; `ConfigurationBuilderWithKeysAnalyzer` shows Datadog.Trace type guards.

## Commands

### `create <name>`

Scaffold a new analyzer end-to-end. Follow the Create Workflow below.

### `review <name-or-path>`

Review an existing analyzer for correctness and repo convention adherence. Skip to the Review Checklist section.

### No arguments

Prompt the user to choose between creating or reviewing.

---

## Create Workflow

### Step 1: Gather Requirements

Gather the following (if not already provided):
1. **What should the analyzer detect?** (the code pattern or violation)
2. **Should it have a code fix?** (auto-fix the violation)
3. **Category**: Reliability, CodeQuality, Performance, Usage, or Maintainability
4. **Severity**: Error, Warning, or Info

### Step 2: Assign a Diagnostic ID

Read existing `Diagnostics.cs` files to find the next available ID:

```
tracer/src/Datadog.Trace.Tools.Analyzers/*/Diagnostics.cs
```

**ID conventions:**
- `DD00xx` — General/system diagnostics
- `DDLOGxxx` — Logging diagnostics
- `DDDUCKxxx` — Duck typing diagnostics
- `DDSEALxxx` — Sealing diagnostics
- New prefix — For a new category, propose a short prefix

### Step 3: Create the Files

**Folder naming:** Existing folders vary — some use singular (`DuckTypeAnalyzer`, `SealedAnalyzer`), some use plural for grouped analyzers (`AspectAnalyzers`, `ConfigurationAnalyzers`). Match the existing folder style when adding to an existing category. For a new standalone category, use `<Name>Analyzer` (singular).

#### 3a. Diagnostics.cs

```
tracer/src/Datadog.Trace.Tools.Analyzers/<Name>Analyzer/Diagnostics.cs
```

```csharp
namespace Datadog.Trace.Tools.Analyzers.<Name>Analyzer;

public class Diagnostics
{
    public const string <Name>DiagnosticId = "<ID>";
}
```

#### 3b. Analyzer

```
tracer/src/Datadog.Trace.Tools.Analyzers/<Name>Analyzer/<Name>Analyzer.cs
```

Choose the appropriate analysis pattern using this decision table, then see `references/analysis-patterns.md` for the full code template:

| Pattern | When to use |
|---------|-------------|
| 1. Syntax Node | Inspecting specific code structures; no type resolution needed |
| 2. Compilation Start | Need one-time type lookups before analyzing nodes/symbols |
| 3. Compilation Start + End | Collecting state across multiple files, reporting at compilation end |
| 4. Datadog.Trace Type Guard | Analyzer depends on internal `Datadog.Trace` types that may be renamed |

Every analyzer requires:
- `[DiagnosticAnalyzer(LanguageNames.CSharp)]` attribute
- `context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None)` — generated code (from source generators) should not be flagged for human-authored patterns
- `context.EnableConcurrentExecution()` — Roslyn runs analysis callbacks on multiple threads for IDE responsiveness, so all state must be thread-safe
- `#pragma warning disable RS2008` around the `DiagnosticDescriptor` — Roslyn's release tracking expects IDs registered in a shipped analyzer; our internal IDs don't follow that model

#### 3c. Code Fix Provider (if applicable)

See `references/codefix-template.md` for the full template.

Create at:
```
tracer/src/Datadog.Trace.Tools.Analyzers.CodeFixes/<Name>Analyzer/<Name>CodeFixProvider.cs
```

Then link the Diagnostics.cs in `Datadog.Trace.Tools.Analyzers.CodeFixes.csproj` — linking (instead of copying) ensures the diagnostic ID constant is defined in exactly one place:

```xml
<Compile Include="..\Datadog.Trace.Tools.Analyzers\<Name>Analyzer\Diagnostics.cs"
         Link="<Name>Analyzer\Diagnostics.cs" />
```

#### 3d. Tests

See `references/test-template.md` for the full template.

Create at:
```
tracer/test/Datadog.Trace.Tools.Analyzers.Tests/<Name>Analyzer/<Name>AnalyzerTests.cs
```

Must include at minimum:
- Empty source (no diagnostics)
- Valid code (no diagnostics)
- Invalid code (diagnostic reported at correct location)
- Code fix application (if code fix exists)

### Step 4: Build & Verify

```
dotnet build tracer/src/Datadog.Trace.Tools.Analyzers/ -c Release
dotnet build tracer/src/Datadog.Trace.Tools.Analyzers.CodeFixes/ -c Release
dotnet test tracer/test/Datadog.Trace.Tools.Analyzers.Tests/
```

If the build fails, check for:
- Missing `using` directives (especially `Microsoft.CodeAnalysis.CSharp` and `Microsoft.CodeAnalysis.Diagnostics`)
- Diagnostics.cs not linked in the CodeFixes `.csproj`
- Namespace mismatches between analyzer and code fix projects (CodeFixes uses `RootNamespace: Datadog.Trace.Tools.Analyzers`)

---

## Review Checklist

When reviewing an analyzer, check all of the following:

### Analyzer Structure
- `[DiagnosticAnalyzer(LanguageNames.CSharp)]` attribute present
- `ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None)` called
- `EnableConcurrentExecution()` called
- `#pragma warning disable RS2008` around `DiagnosticDescriptor`
- Diagnostic ID follows naming convention
- Category is one of: Reliability, CodeQuality, Performance, Usage, Maintainability
- Severity is appropriate for the violation

### Code Fix Provider (if present)
- `[ExportCodeFixProvider(LanguageNames.CSharp)]` and `[Shared]` attributes
- `GetFixAllProvider()` returns `WellKnownFixAllProviders.BatchFixer`
- `FixableDiagnosticIds` matches the analyzer's diagnostic ID(s)
- Diagnostics.cs linked (not duplicated) in CodeFixes `.csproj`
- Trivia (whitespace, comments) preserved in syntax transformations

### Thread Safety
- No shared mutable state in syntax/symbol action callbacks
- Uses `PooledConcurrentSet`/`PooledConcurrentDictionary` for cross-file state (see Pattern 3 in `references/analysis-patterns.md`)
- Pooled collections disposed in `CompilationEndAction`

### Tests
- Empty source test (no diagnostics)
- Valid code test (no false positives)
- Invalid code test (diagnostic at correct location)
- Code fix test (if applicable)
- Edge cases covered
- Uses `extern alias AnalyzerCodeFixes` for code fix verifier
- Uses `[Theory]` with `[MemberData]`/`[InlineData]` for variations instead of duplicated `[Fact]` methods

### Datadog.Trace Type Dependencies
- Uses `Diagnostics.IsTypeNullAndReportForDatadogTrace` when depending on internal types (see Pattern 4 in `references/analysis-patterns.md`)
- Uses `WellKnownTypeProvider` for repeated type lookups

---

## References

- **`references/analysis-patterns.md`** — Four Roslyn analysis patterns with full code examples (syntax node, compilation start, cross-file, Datadog.Trace type guards)
- **`references/codefix-template.md`** — Code fix provider template and conventions
- **`references/test-template.md`** — Test scaffolding, markup syntax, and tips
- **`references/shared-helpers.md`** — Reusable helper classes in the Helpers directory
