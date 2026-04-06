// <copyright file="ThrowInInlinedMethodCodeFixProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
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

namespace Datadog.Trace.Tools.Analyzers.ThrowInInlinedMethodAnalyzer
{
    /// <summary>
    /// A CodeFixProvider for <see cref="ThrowInInlinedMethodAnalyzer"/> that replaces
    /// throw statements with ThrowHelper method calls.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ThrowInInlinedMethodCodeFixProvider))]
    [Shared]
    public class ThrowInInlinedMethodCodeFixProvider : CodeFixProvider
    {
        private const string ThrowHelperUsingNamespace = "Datadog.Trace.Util";

        private static readonly int[] OneArg = { 1 };
        private static readonly int[] OneOrTwoArgs = { 1, 2 };
        private static readonly int[] OneToThreeArgs = { 1, 2, 3 };

        private static readonly Dictionary<string, (string MethodName, int[] ValidArgCounts)> SupportedExceptions
            = new Dictionary<string, (string, int[])>
            {
                ["System.ArgumentNullException"] = ("ThrowArgumentNullException", OneArg),
                ["System.ArgumentOutOfRangeException"] = ("ThrowArgumentOutOfRangeException", OneToThreeArgs),
                ["System.ArgumentException"] = ("ThrowArgumentException", OneOrTwoArgs),
                ["System.InvalidOperationException"] = ("ThrowInvalidOperationException", OneArg),
                ["System.Exception"] = ("ThrowException", OneArg),
                ["System.InvalidCastException"] = ("ThrowInvalidCastException", OneArg),
                ["System.IndexOutOfRangeException"] = ("ThrowIndexOutOfRangeException", OneArg),
                ["System.NotSupportedException"] = ("ThrowNotSupportedException", OneArg),
                ["System.Collections.Generic.KeyNotFoundException"] = ("ThrowKeyNotFoundException", OneArg),
                ["System.NullReferenceException"] = ("ThrowNullReferenceException", OneArg),
            };

        /// <inheritdoc />
        public sealed override ImmutableArray<string> FixableDiagnosticIds
            => ImmutableArray.Create(Diagnostics.DiagnosticId);

        /// <inheritdoc />
        public sealed override FixAllProvider GetFixAllProvider()
            => WellKnownFixAllProviders.BatchFixer;

        /// <inheritdoc />
        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            if (root is null)
            {
                return;
            }

            var diagnostic = context.Diagnostics.First();
            var node = root.FindNode(diagnostic.Location.SourceSpan);

            // Only handle throw statements with object creation expressions (not throw expressions or bare throw;)
            if (node is not ThrowStatementSyntax throwStatement
                || throwStatement.Expression is not ObjectCreationExpressionSyntax creation)
            {
                return;
            }

            var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            if (semanticModel is null)
            {
                return;
            }

            var typeInfo = semanticModel.GetTypeInfo(creation, context.CancellationToken);
            var typeName = typeInfo.Type?.ToDisplayString();

            if (typeName is null || !SupportedExceptions.TryGetValue(typeName, out var entry))
            {
                return;
            }

            var argCount = creation.ArgumentList?.Arguments.Count ?? 0;
            if (!entry.ValidArgCounts.Contains(argCount))
            {
                return;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: $"Replace with ThrowHelper.{entry.MethodName}",
                    createChangedDocument: c => ReplaceWithThrowHelperAsync(context.Document, throwStatement, entry.MethodName, c),
                    equivalenceKey: nameof(ThrowInInlinedMethodCodeFixProvider)),
                diagnostic);
        }

        private static async Task<Document> ReplaceWithThrowHelperAsync(
            Document document,
            ThrowStatementSyntax throwStatement,
            string methodName,
            CancellationToken cancellationToken)
        {
            var creation = (ObjectCreationExpressionSyntax)throwStatement.Expression!;
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root is null)
            {
                return document;
            }

            // Build: ThrowHelper.MethodName(args)
            var invocation = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("ThrowHelper"),
                    SyntaxFactory.IdentifierName(methodName)),
                creation.ArgumentList ?? SyntaxFactory.ArgumentList());

            var expressionStatement = SyntaxFactory.ExpressionStatement(invocation)
                .WithLeadingTrivia(throwStatement.GetLeadingTrivia())
                .WithTrailingTrivia(throwStatement.GetTrailingTrivia());

            root = root.ReplaceNode(throwStatement, expressionStatement);

            // Add using directive if not already present
            if (root is CompilationUnitSyntax compilationUnit && !HasUsingDirective(compilationUnit))
            {
                // Match the document's existing line ending style
                var eolTrivia = compilationUnit.Usings.Count > 0
                    ? GetTrailingEndOfLine(compilationUnit.Usings.Last())
                    : SyntaxFactory.ElasticCarriageReturnLineFeed;

                var usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(ThrowHelperUsingNamespace))
                    .WithTrailingTrivia(eolTrivia);
                root = compilationUnit.AddUsings(usingDirective);
            }

            return document.WithSyntaxRoot(root);
        }

        private static SyntaxTrivia GetTrailingEndOfLine(UsingDirectiveSyntax usingDirective)
        {
            foreach (var trivia in usingDirective.GetTrailingTrivia())
            {
                if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
                {
                    return trivia;
                }
            }

            return SyntaxFactory.ElasticCarriageReturnLineFeed;
        }

        private static bool HasUsingDirective(CompilationUnitSyntax compilationUnit)
        {
            // Check top-level usings
            if (compilationUnit.Usings.Any(u => u.Name?.ToString() == ThrowHelperUsingNamespace))
            {
                return true;
            }

            // Check usings inside namespace declarations
            foreach (var member in compilationUnit.Members)
            {
                if (member is BaseNamespaceDeclarationSyntax ns
                    && ns.Usings.Any(u => u.Name?.ToString() == ThrowHelperUsingNamespace))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
