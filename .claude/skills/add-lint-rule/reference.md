# Lint rule reference

Load this after the rule is classified in [SKILL.md](SKILL.md).

## File layout

```
tracer/src/Datadog.Trace.Tools.Analyzers/<Area>/
  YourAnalyzer.cs
  Diagnostics.cs                 # share IDs with a code fix
tracer/src/Datadog.Trace.Tools.Analyzers.CodeFixes/<Area>/
  YourCodeFixProvider.cs         # optional
tracer/test/Datadog.Trace.Tools.Analyzers.Tests/<Area>/
  YourAnalyzerTests.cs
```

Wiring already in place:

- `tracer/src/Directory.Build.props` — project-references both analyzer projects as analyzers (**core** `tracer/src` only for now; not tests/helpers)
- `tracer/Directory.Build.props` — `StyleCop.Analyzers` + `BannedApiAnalyzers`
- Repo-root `BannedSymbols.txt` — `AdditionalFiles` on `Datadog.Trace.csproj` (core)
- `.editorconfig` — `dotnet_diagnostic.RS0030.severity = error`

Closest copies:

- Analyzer only: `PrimaryConstructorAnalyzer/DoNotCapturePrimaryConstructorParametersAnalyzer.cs` (`DD0003`)
- Analyzer + code fix + tests: `DuckTypeAnalyzer/` (`DDDUCK001`)
- Banned API with a real perf reason: `Environment.Version` in `BannedSymbols.txt`
- Legacy `Info` (ignored; do not copy): `SealedAnalyzer` (`DDSEAL001`)

## Diagnostic IDs

Grep `DiagnosticId` under `tracer/src/Datadog.Trace.Tools.Analyzers` before allocating.

Patterns in use:

- Sequential: `DD0001` … `DD0015` (gaps exist; do not reuse)
- Families: `DDLOG001`–`DDLOG009`, `DDDUCK001`, `DDSEAL001`
- Banned API: `RS0030`

If adding a code fix, put the `DiagnosticDescriptor` in `Diagnostics.cs` and
link that file from `Datadog.Trace.Tools.Analyzers.CodeFixes.csproj` (see
existing `<Compile Include="..\Datadog.Trace.Tools.Analyzers\...">` entries).

## Analyzer skeleton

```csharp
#nullable enable
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Datadog.Trace.Tools.Analyzers.YourArea;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class YourAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "DDxxxx";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        title: "Short title",
        messageFormat: "What is wrong and what to do instead",
        category: "Usage", // Usage | CodeQuality | Maintainability | Performance (rare)
        defaultSeverity: DiagnosticSeverity.Error, // new rules: Error only. Info is legacy and ignored.
        isEnabledByDefault: true,
        description: "One-sentence why. No speculative perf claims.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static compilationContext =>
        {
            // If the rule needs a Datadog.Trace type, resolve it here and
            // early-out when missing. For required types in Datadog.Trace,
            // use Helpers.Diagnostics.IsTypeNullAndReportForDatadogTrace
            // (reports DD0009 instead of silently doing nothing).

            compilationContext.RegisterOperationAction(
                ctx => { /* ReportDiagnostic */ },
                OperationKind.Invocation);
        });
    }
}
```

Habits:

- Prefer `IOperation` over raw syntax when the rule is semantic
- Skip generated code (`GeneratedCodeAnalysisFlags.None`)
- Enable concurrent execution
- Use the Apache 2 / Datadog copyright header used by neighboring files
- Look up types with `GetTypeByMetadataName` / `WellKnownTypeProvider`; do not
  load application assemblies from disk

## Tests

`Datadog.Trace.Tools.Analyzers.Tests` uses
`Microsoft.CodeAnalysis.CSharp.Testing`. The source snippet must compile
except for the diagnostic under test (`CSharpAnalyzerVerifier` fails on
CS* errors). Analyzer-only:

```csharp
using System.Threading.Tasks;
using Xunit;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Datadog.Trace.Tools.Analyzers.YourArea.YourAnalyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Datadog.Trace.Tools.Analyzers.Tests.YourArea;

public class YourAnalyzerTests
{
    [Fact]
    public async Task Flags_BadPattern()
    {
        var source = """
            class C
            {
                void BadCall() { }
                void M() => [|BadCall()|];
            }
            """;
        await Verifier.VerifyAnalyzerAsync(source);
    }

    [Fact]
    public async Task DoesNotFlag_GoodPattern()
    {
        var source = """
            class C
            {
                void GoodCall() { }
                void M() => GoodCall();
            }
            """;
        await Verifier.VerifyAnalyzerAsync(source);
    }
}
```

If you add a code fix, copy an existing code-fix test such as
`DuckTypeAnalyzers/DuckTypeNullCheckAnalyzerTests.cs`.

Run:

```bash
./tracer/build.sh BuildAndRunManagedUnitTests --filter FullyQualifiedName~YourAnalyzerTests
```

## BannedSymbols.txt

Repo root. Format: documentation ID + `;` + message.

```
M:Namespace.Type.Method(System.String);Use OtherType.OtherMethod instead — <real reason>
P:Namespace.Type.Property;Use cached copy instead — allocates on every get
T:Namespace.Type;Do not use this type
```

`RS0030` is error in `.editorconfig` (vendored code is excepted). The message
is shown in the IDE; keep it accurate.

## StyleCop / editorconfig

- Settings: `tracer/stylecop.json`
- Package: `StyleCop.Analyzers` from `tracer/Directory.Build.props`
- To fail an **existing** diagnostic: `.editorconfig`
  `dotnet_diagnostic.SA1300.severity = error`
- Shared projects also import `shared/CodeAnalysis.props` (`Microsoft.CodeAnalysis.CSharp.CodeStyle` 3.8.0; `EnableNETAnalyzers` is false)

Do not duplicate StyleCop with a custom analyzer.

## Native (do not expand without a user request)

| Surface | Command / config | Failure mode |
|---|---|---|
| Tracer native CppCheck | Nuke `CppCheckNativeSrc` in `Build.Steps.cs` | Stage `static_analysis_checks_tracer` |
| Native loader CppCheck | Nuke `CppCheckNativeLoader` in `Build.Shared.Steps.cs` | same |
| Profiler CppCheck | `profiler/cppcheck-suppressions.txt`; `--inline-suppr` | `CheckCppCheckResults` (severity=`error`) |
| Profiler Clang-Tidy | `profiler/.clang-tidy` plus appended `ClangTidyChecks` in `Build.Profiler.Steps.cs` | `CheckClangTidyResults` (line contains ` error:`) |

Profiler Clang-Tidy is the merge of both sources. `profiler/.clang-tidy`
enables `readability-*`, `clang-analyzer-*`, and `cppcoreguidelines-*`.
Command-line `-checks` is appended to that (it does not replace it). Do not
add style checks by default; read both files before changing the set.

`.clang-format` is not a CI gate.

## Do not

- Add GitHub Actions / Azure stages / Nuke targets for a C# diagnostic
- Turn on CodeQL (workflow jobs are `if: false`) or resurrect
  `static-analysis.datadog.yml` as a PR check
- Enable `EnableNETAnalyzers` repo-wide
- Leave existing violations for a follow-up PR (`TreatWarningsAsErrors`)
