# Test Template

## With code fix

```csharp
extern alias AnalyzerCodeFixes;

using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;

using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpCodeFixVerifier<
    Datadog.Trace.Tools.Analyzers.<Name>Analyzer.<Name>Analyzer,
    AnalyzerCodeFixes::Datadog.Trace.Tools.Analyzers.<Name>Analyzer.<Name>CodeFixProvider,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Datadog.Trace.Tools.Analyzers.Tests;

public class <Name>AnalyzerTests
{
    private const string DiagnosticId = <Name>Analyzer.Diagnostics.<Name>DiagnosticId;

    [Fact]
    public async Task EmptySource_NoDiagnostics()
    {
        await Verifier.VerifyAnalyzerAsync(string.Empty);
    }

    [Fact]
    public async Task ValidCode_NoDiagnostics()
    {
        var code = GetTestCode(@"/* valid code that should NOT trigger */");
        await Verifier.VerifyAnalyzerAsync(code);
    }

    [Theory]
    [InlineData("variation1")]
    [InlineData("variation2")]
    public async Task InvalidCode_ReportsDiagnosticAndAppliesFix(string variation)
    {
        // {|#0:...|} marks where diagnostic #0 is expected
        var code = GetTestCode(@"/* code with {|#0:violation|} using " + variation + " */");
        var fix = GetTestCode(@"/* code with fix applied using " + variation + " */");

        var expected = new DiagnosticResult(DiagnosticId, DiagnosticSeverity.Error)
            .WithLocation(0);

        await Verifier.VerifyCodeFixAsync(code, expected, fix);
    }

    private static string GetTestCode(string testFragment)
    {
        return @"
using System;

namespace TestNamespace
{
    class TestClass
    {
        public void TestMethod()
        {" + testFragment + @"
        }
    }
}";
    }
}
```

## Without code fix

Use `CSharpAnalyzerVerifier` instead:

```csharp
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<
    Datadog.Trace.Tools.Analyzers.<Name>Analyzer.<Name>Analyzer,
    Microsoft.CodeAnalysis.Testing.DefaultVerifier>;
```

## Markup syntax

- `{|#0:code|}` — span where diagnostic `#0` is expected (referenced by `.WithLocation(0)`)
- `{|DiagnosticId:code|}` — span for a specific diagnostic ID
- `[|code|]` — span for the default diagnostic (single-diagnostic analyzers)

## Tips

- Prefer `[Theory]` with `[MemberData]`/`[InlineData]` for variations over duplicated `[Fact]` methods.
- Wrap code fragments in a `GetTestCode` helper that provides `using` directives and class scaffolding.
- For analyzers depending on Datadog.Trace types, provide stub type definitions so the analyzer can resolve them (see `ConfigurationAnalyzers/AnalyzerTestHelper.cs`).
- **Stub types must exactly match real production signatures.** If the test stub includes methods/overloads that don't exist in the real code, tests will pass but the code fix will generate uncompilable code in practice. Read the actual production type (e.g., `ThrowHelper.cs`, `StringBuilderCache.cs`) and mirror its signatures exactly.
- The `extern alias AnalyzerCodeFixes` is required because the analyzer and code fix projects share the same root namespace. The alias is configured in the test `.csproj`:

```xml
<ProjectReference Include="..\..\src\Datadog.Trace.Tools.Analyzers.CodeFixes\..."
                  Aliases="AnalyzerCodeFixes"/>
```
