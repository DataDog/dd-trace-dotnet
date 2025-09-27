// <copyright file="DuckTypeNullCheckCodeFixProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Datadog.Trace.Tools.Analyzers.DuckTypeAnalyzer
{
    /// <summary>
    /// Fixes IDuckType null checks to check the .Instance property instead
    /// Note that this always does a conditional access to be as safe as possible
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DuckTypeNullCheckCodeFixProvider))]
    public class DuckTypeNullCheckCodeFixProvider : CodeFixProvider
    {
        private const string Title = "Check ?.Instance for null instead";

        /// <inheritdoc/>
        public sealed override ImmutableArray<string> FixableDiagnosticIds
            => ImmutableArray.Create(DuckDiagnostics.DuckTypeNullCheckDiagnosticId);

        /// <inheritdoc/>
        public sealed override FixAllProvider GetFixAllProvider()
            => WellKnownFixAllProviders.BatchFixer;

        /// <inheritdoc/>
        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            if (root == null)
            {
                return;
            }

            var span = context.Span;

            context.RegisterCodeFix(
                CodeAction.Create(
                    Title,
                    c => FixAsync(context.Document, span, c),
                    equivalenceKey: Title),
                context.Diagnostics);
        }

        private static async Task<Document> FixAsync(Document document, TextSpan span, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root == null)
            {
                return document;
            }

            // Get the node that contains the diagnostic span
            var node = root.FindNode(span, findInsideTrivia: false, getInnermostNodeForTie: false);

            // Find the expression we need to fix by walking up if necessary
            var targetNode = node.AncestorsAndSelf()
                .FirstOrDefault(n => n is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.EqualsExpression or (int)SyntaxKind.NotEqualsExpression }
                                  || n is IsPatternExpressionSyntax);

            if (targetNode == null)
            {
                return document;
            }

            switch (targetNode)
            {
                case BinaryExpressionSyntax binary:
                    {
                        var (duckExpression, isLeft) = GetDuckExpressionFromBinary(binary);
                        if (duckExpression == null)
                        {
                            return document;
                        }

                        var instance = CreateInstanceAccess(duckExpression);
                        var updated = isLeft ? binary.WithLeft(instance) : binary.WithRight(instance);
                        var newRoot = root.ReplaceNode(binary, updated);
                        return document.WithSyntaxRoot(newRoot);
                    }

                case IsPatternExpressionSyntax isPattern:
                    {
                        var instance = CreateInstanceAccess(isPattern.Expression);
                        var updated = isPattern.WithExpression(instance);
                        var newRoot = root.ReplaceNode(isPattern, updated);
                        return document.WithSyntaxRoot(newRoot);
                    }

                default:
                    return document;
            }
        }

        private static (ExpressionSyntax? DuckExpression, bool IsLeft) GetDuckExpressionFromBinary(BinaryExpressionSyntax binary)
        {
            var leftIsNull = IsNullLiteral(binary.Left);
            var rightIsNull = IsNullLiteral(binary.Right);

            if (!leftIsNull && !rightIsNull)
            {
                return (null, false);
            }

            return leftIsNull ? (binary.Right, false) : (binary.Left, true);
        }

        private static bool IsNullLiteral(ExpressionSyntax expression)
        {
            return expression is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression }
                   || (expression is ParenthesizedExpressionSyntax p && IsNullLiteral(p.Expression));
        }

        private static ExpressionSyntax CreateInstanceAccess(ExpressionSyntax expression)
        {
            expression = StripOuterParentheses(expression);

            // Preserve leading and trailing trivia
            var leadingTrivia = expression.GetLeadingTrivia();
            var trailingTrivia = expression.GetTrailingTrivia();

            if (expression is CastExpressionSyntax cast && IsObjectTypeSyntax(cast.Type))
            {
                var result = CreateInstanceAccessor(StripOuterParentheses(cast.Expression));
                return result.WithLeadingTrivia(leadingTrivia).WithTrailingTrivia(trailingTrivia);
            }

            var accessor = CreateInstanceAccessor(expression);
            return accessor.WithLeadingTrivia(leadingTrivia).WithTrailingTrivia(trailingTrivia);
        }

        private static ExpressionSyntax CreateInstanceAccessor(ExpressionSyntax expression)
        {
            return SyntaxFactory.ConditionalAccessExpression(ParenthesizeIfNeeded(expression), SyntaxFactory.MemberBindingExpression(SyntaxFactory.IdentifierName("Instance")));
        }

        private static ExpressionSyntax ParenthesizeIfNeeded(ExpressionSyntax expression)
        {
            return expression is IdentifierNameSyntax or MemberAccessExpressionSyntax or ParenthesizedExpressionSyntax ? expression : SyntaxFactory.ParenthesizedExpression(expression);
        }

        private static ExpressionSyntax StripOuterParentheses(ExpressionSyntax expression)
        {
            while (expression is ParenthesizedExpressionSyntax p)
            {
                expression = p.Expression;
            }

            return expression;
        }

        // this seems seems to be that in VS when you open a code file the operands get boxed(?) to "object"
        // if we don't account for it, you see the errors at compile time, in the error list, but then disappear
        // when you open the code file in the editor.
        private static bool IsObjectTypeSyntax(TypeSyntax t) =>
            t is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.ObjectKeyword }
            || (t is IdentifierNameSyntax id && id.Identifier.ValueText == "object");
    }
}
