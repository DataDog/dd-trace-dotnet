# Code Fix Provider Template

```csharp
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Datadog.Trace.Tools.Analyzers.<Name>Analyzer;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(<Name>CodeFixProvider))]
[Shared]
public class <Name>CodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(Diagnostics.<Name>DiagnosticId);

    public sealed override FixAllProvider GetFixAllProvider()
        => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        // Navigate from the diagnostic span to the target node
        var node = root.FindToken(diagnosticSpan.Start)
            .Parent
            .AncestorsAndSelf()
            .OfType<TargetSyntaxType>()
            .First();

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Description of the fix",
                createChangedDocument: c => ApplyFix(context.Document, node, c),
                equivalenceKey: nameof(<Name>CodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> ApplyFix(
        Document document, TargetSyntaxType node, CancellationToken cancellationToken)
    {
        // Build the replacement syntax.
        // Syntax trees are immutable — create new nodes, don't mutate.
        // Preserve trivia (whitespace, comments) when adding/removing tokens.
        var newNode = /* transform */;

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        return document.WithSyntaxRoot(root.ReplaceNode(node, newNode));
    }
}
```

## Key conventions

- Always use `WellKnownFixAllProviders.BatchFixer` for the FixAll provider.
- Use `nameof(MyCodeFixProvider)` as the `equivalenceKey` — this groups fix actions for "Fix All".
- Navigate from diagnostic span to target node using `FindToken().Parent.AncestorsAndSelf().OfType<T>()`.
- Link `Diagnostics.cs` from the analyzer project into the CodeFixes `.csproj` (see SKILL.md Step 3c for the XML snippet and rationale).
- Apply `Formatter.Annotation` to modified nodes so the formatter cleans up spacing.
- When generating type names, use `Simplifier.Annotation` to reduce fully-qualified names.

## Critical: Code fixes must never generate uncompilable code

A code fix that produces broken code is worse than no code fix. Apply these guards:

- **Verify target methods/overloads exist**: Before generating a call like `ThrowHelper.ThrowFoo(args)`, use the semantic model to confirm the exact overload exists in the compilation with matching parameter count and types. Don't assume a method exists based on naming conventions alone.
- **Preserve existing syntax where possible**: When modifying a list (e.g., generic type arguments), keep the original syntax nodes and only replace the one being changed. Rebuilding from `ISymbol` data can lose `using` context and produce unqualified names that don't compile.
- **Handle nullable value types in rewrites**: When rewriting `x ?? throw ...` into `if (x is null) ...; use x;`, the result drops the nullable unwrapping. Use `.Value` or a cast for nullable value types.
- **Guard against unsupported constructor signatures**: If the fix rewrites `new Type(args)` into `Helper(args)`, verify the helper accepts those parameter types. Only offer the fix for supported overloads.
- **Scope isolation**: When collecting nodes to rewrite (e.g., all `.ToString()` calls on a variable), stay within the current scope — skip nested lambdas and local functions, and match the exact symbol, not just the identifier text.
