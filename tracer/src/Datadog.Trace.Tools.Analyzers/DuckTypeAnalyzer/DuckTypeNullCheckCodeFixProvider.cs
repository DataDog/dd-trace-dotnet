// <copyright file="DuckTypeNullCheckCodeFixProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;

namespace Datadog.Trace.Tools.Analyzers.DuckTypeAnalyzer
{
    /// <summary>
    /// Fixes IDuckType null checks to check the .Instance property instead
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DuckTypeNullCheckCodeFixProvider))]
    public class DuckTypeNullCheckCodeFixProvider : CodeFixProvider
    {
        private const string Title = "Check .Instance for null instead";

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
            var diagnostic = context.Diagnostics.First();

            // crab the node that was flagged by the analyzer
            var span = diagnostic.Location.SourceSpan;
            var node = root?.FindNode(span);

            if (node == null)
            {
                return;
            }

            // we could be in either a binary expression (== or !=) or an is-pattern expression (is or is not)
            var binary = node.FirstAncestorOrSelf<BinaryExpressionSyntax>();
            var isPattern = node.FirstAncestorOrSelf<IsPatternExpressionSyntax>();

            if (binary is null && isPattern is null)
            {
                return;
            }

            // register a code action that will invoke the fix (squiggly thing lightbulb)
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Title,
                    createChangedDocument: c => FixAsync(context.Document, binary, isPattern, c),
                    equivalenceKey: Title),
                diagnostic);
        }

        private static async Task<Document> FixAsync(
            Document document,
            BinaryExpressionSyntax? binary,
            IsPatternExpressionSyntax? isPattern,
            CancellationToken ct)
        {
            var editor = await DocumentEditor.CreateAsync(document, ct).ConfigureAwait(false);
            var semanticModel = editor.SemanticModel;
            if (binary is not null)
            {
                // we have something like:  if (duckType == null)  or           if (duckType != null)
                // we want something like:  if (duckType.Instance == null)  or  if (duckType.Instance != null)
                var (duckExpr, isLeft) = GetDuckExpressionFromBinary(binary);
                if (duckExpr is null)
                {
                    return document;
                }

                var instance = CreateInstanceAccess(duckExpr, semanticModel);
                var newBinary = isLeft ? binary.WithLeft(instance) : binary.WithRight(instance);
                editor.ReplaceNode(binary, newBinary);
            }
            else if (isPattern is not null)
            {
                // we have something like:  if (duckType is null)  or           if (duckType is not null)
                // we want something like:  if (duckType.Instance is null)  or  if (duckType.Instance is not null)
                var instance = CreateInstanceAccess(isPattern.Expression, semanticModel);
                editor.ReplaceNode(isPattern, isPattern.WithExpression(instance));
            }

            return editor.GetChangedDocument();
        }

        private static (ExpressionSyntax? Expr, bool IsLeft) GetDuckExpressionFromBinary(BinaryExpressionSyntax binary)
        {
            // Find which side is the null literal
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
            if (expression is null)
            {
                return false;
            }

            if (expression is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression })
            {
                return true;
            }

            // handle parens around the (null) literal - edge case
            if (expression is ParenthesizedExpressionSyntax p && IsNullLiteral(p.Expression))
            {
                return true;
            }

            return false;
        }

        private static ExpressionSyntax CreateInstanceAccess(ExpressionSyntax baseExpression, SemanticModel semanticModel)
        {
            baseExpression = StripOuterParens(baseExpression);

            // this is a special case if we have a cast to object
            // I don't think I have seen this in the codebase, but I wrote a test for it and it failed so here we are
            // (object)duckType  -->  duckType.Instance
            // we need to remove the cast
            if (baseExpression is CastExpressionSyntax cast && IsObject(cast.Type))
            {
                return CreateInstanceAccessor(cast.Expression).WithTriviaFrom(baseExpression);
            }

            // if it is IDuckType?, use ?.Instance
            var useConditional = IsNullable(baseExpression, semanticModel);

            ExpressionSyntax? instanceExpression;
            if (useConditional)
            {
                // duckType?.Instance
                instanceExpression = SyntaxFactory.ConditionalAccessExpression(ParenthesizeIfNeeded(baseExpression), SyntaxFactory.MemberBindingExpression(SyntaxFactory.IdentifierName("Instance")));
            }
            else
            {
                // duckType.Instance
                instanceExpression = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, ParenthesizeIfNeeded(baseExpression), SyntaxFactory.IdentifierName("Instance"));
            }

            return instanceExpression.WithTriviaFrom(baseExpression);
        }

        private static bool IsNullable(ExpressionSyntax expression, SemanticModel semanticModel)
        {
            var symbol = semanticModel.GetSymbolInfo(expression).Symbol;

            return symbol switch
            {
                IParameterSymbol p => p.NullableAnnotation == NullableAnnotation.Annotated,
                ILocalSymbol l => l.NullableAnnotation == NullableAnnotation.Annotated,
                IFieldSymbol f => f.NullableAnnotation == NullableAnnotation.Annotated,
                IPropertySymbol pr => pr.NullableAnnotation == NullableAnnotation.Annotated,
                _ => false, // For expressions without a direct symbol, just don't add ?.
            };
        }

        private static bool IsObject(TypeSyntax t)
        {
            if (t is null)
            {
                return false;
            }

            if (t is PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.ObjectKeyword })
            {
                return true;
            }

            if (t is IdentifierNameSyntax id && (id.Identifier.ValueText == "object"))
            {
                return true;
            }

            return false;
        }

        private static ExpressionSyntax CreateInstanceAccessor(ExpressionSyntax receiver)
        {
            // duckType --> duckType.Instance
            return SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, ParenthesizeIfNeeded(receiver), SyntaxFactory.IdentifierName("Instance"));
        }

        private static ExpressionSyntax StripOuterParens(ExpressionSyntax expr)
        {
            // if we have casted we need to remove parentheses
            while (expr is ParenthesizedExpressionSyntax p)
            {
                expr = p.Expression.WithTriviaFrom(expr);
            }

            return expr;
        }

        private static ExpressionSyntax ParenthesizeIfNeeded(ExpressionSyntax expression)
        {
            // in case we have a more complex pattern add parens as necessary
            if (expression is IdentifierNameSyntax or MemberAccessExpressionSyntax or ParenthesizedExpressionSyntax)
            {
                return expression;
            }

            return SyntaxFactory.ParenthesizedExpression(expression);
        }
    }
}
