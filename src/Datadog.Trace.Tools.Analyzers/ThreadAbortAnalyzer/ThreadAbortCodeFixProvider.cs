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

namespace Datadog.Trace.Tools.Analyzers.ThreadAbortAnalyzer
{
    /// <summary>
    /// A CodeFixProvider for the <see cref="ThreadAbortAnalyzer"/>
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ThreadAbortCodeFixProvider))]
    [Shared]
    public class ThreadAbortCodeFixProvider : CodeFixProvider
    {
        /// <inheritdoc />
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(ThreadAbortAnalyzer.DiagnosticId); }
        }

        /// <inheritdoc />
        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        /// <inheritdoc />
        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the while block catch declaration identified by the diagnostic.
            var whileStatement = root.FindToken(diagnosticSpan.Start)
                .Parent
                .AncestorsAndSelf()
                .OfType<WhileStatementSyntax>().First();

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Resources.CodeFixTitle,
                    createChangedDocument: c => AddThrowStatement(context.Document, whileStatement, c),
                    equivalenceKey: nameof(ThreadAbortCodeFixProvider)),
                diagnostic);
        }

        private async Task<Document> AddThrowStatement(Document document, WhileStatementSyntax whileStatement, CancellationToken cancellationToken)
        {
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            var catchBlock = ThreadAbortSyntaxHelper.FindProblematicCatchClause(whileStatement, semanticModel);

            // This messes with the whitespace, but it's a PITA to get that right
            var throwStatement = SyntaxFactory.ThrowStatement();
            var statements = catchBlock.Block.Statements.Add(throwStatement);
            var newCatchBlock = catchBlock.Block.WithStatements(statements);

            // replace the syntax and return updated document
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            root = root.ReplaceNode(catchBlock.Block, newCatchBlock);
            return document.WithSyntaxRoot(root);
        }
    }
}
