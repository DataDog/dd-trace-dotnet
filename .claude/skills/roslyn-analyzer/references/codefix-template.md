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
- The code fix project **links** `Diagnostics.cs` from the analyzer project — add to `Datadog.Trace.Tools.Analyzers.CodeFixes.csproj`:

```xml
<Compile Include="..\Datadog.Trace.Tools.Analyzers\<Name>Analyzer\Diagnostics.cs"
         Link="<Name>Analyzer\Diagnostics.cs" />
```
