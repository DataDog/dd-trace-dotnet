# Analysis Patterns

## Pattern 1: Syntax Node Analysis (simplest)

Use when inspecting specific code structures. See `ThreadAbortAnalyzer` for a complete example.

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MyAnalyzer : DiagnosticAnalyzer
{
#pragma warning disable RS2008
    private static readonly DiagnosticDescriptor Rule = new(
        id: Diagnostics.MyDiagnosticId,
        title: "Short title",
        messageFormat: "Message with {0} placeholders",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Why this matters.");
#pragma warning restore RS2008

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; }
        = ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeSyntax, SyntaxKind.WhileStatement);
    }

    private static void AnalyzeSyntax(SyntaxNodeAnalysisContext context)
    {
        var node = (WhileStatementSyntax)context.Node;
        // ... analysis ...
        if (hasIssue)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, node.GetLocation()));
        }
    }
}
```

**`SyntaxNodeAnalysisContext` provides:**
- `context.Node` — the syntax node (cast to expected type)
- `context.SemanticModel` — type lookups, constant evaluation, data flow
- `context.CancellationToken` — cooperative cancellation

## Pattern 2: Compilation Start + Symbol/Syntax Analysis

Use when one-time type lookups are needed before analyzing nodes/symbols. See `SealedAnalyzer`, `ConfigurationBuilderWithKeysAnalyzer`.

```csharp
public override void Initialize(AnalysisContext context)
{
    context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
    context.EnableConcurrentExecution();
    context.RegisterCompilationStartAction(OnCompilationStart);
}

private static void OnCompilationStart(CompilationStartAnalysisContext context)
{
    var importantType = context.Compilation.GetTypeByMetadataName("Some.Type");
    if (importantType is null) return;

    context.RegisterSymbolAction(
        ctx => AnalyzeSymbol(ctx, importantType),
        SymbolKind.NamedType);
}
```

## Pattern 3: Compilation Start + End (cross-file)

Use when collecting state across the entire compilation and reporting at the end. See `SealedAnalyzer`.

```csharp
private static void OnCompilationStart(CompilationStartAnalysisContext context)
{
    var candidates = PooledConcurrentSet<INamedTypeSymbol>.GetInstance(SymbolEqualityComparer.Default);

    context.RegisterSymbolAction(ctx =>
    {
        candidates.Add((INamedTypeSymbol)ctx.Symbol);
    }, SymbolKind.NamedType);

    context.RegisterCompilationEndAction(ctx =>
    {
        foreach (var candidate in candidates)
        {
            ctx.ReportDiagnostic(Diagnostic.Create(Rule, candidate.Locations.First(), candidate.Name));
        }
        candidates.Dispose();
    });
}
```

Use `PooledConcurrentSet<T>` and `PooledConcurrentDictionary<K,V>` from `Helpers/` for memory-efficient, thread-safe collection during concurrent analysis. Always dispose pooled collections in the `CompilationEndAction`.

## Pattern 4: Checking Datadog.Trace Types

When depending on internal types from `Datadog.Trace`, use the `IsTypeNullAndReportForDatadogTrace` guard. It reports `DD0009` if the type is missing (renamed/removed) — but only when compiling `Datadog.Trace` itself:

```csharp
private static void OnCompilationStart(CompilationStartAnalysisContext context)
{
    var requiredType = context.Compilation.GetTypeByMetadataName("Datadog.Trace.Some.Type");

    if (Helpers.Diagnostics.IsTypeNullAndReportForDatadogTrace(
            context, requiredType, nameof(MyAnalyzer), "Datadog.Trace.Some.Type"))
    {
        return;
    }

    // requiredType guaranteed non-null here (via [NotNullWhen(false)])
    context.RegisterSyntaxNodeAction(ctx => Analyze(ctx, requiredType), SyntaxKind.InvocationExpression);
}
```
