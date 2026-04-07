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
    /// throw statements and throw expressions with ThrowHelper method calls.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ThrowInInlinedMethodCodeFixProvider))]
    [Shared]
    public class ThrowInInlinedMethodCodeFixProvider : CodeFixProvider
    {
        private const string ThrowHelperUsingNamespace = "Datadog.Trace.Util";

        private static readonly Dictionary<string, (string MethodName, int MaxArgs)> SupportedExceptions
            = new Dictionary<string, (string, int)>
            {
                ["System.ArgumentNullException"] = ("ThrowArgumentNullException", 1),
                ["System.ArgumentOutOfRangeException"] = ("ThrowArgumentOutOfRangeException", 3),
                ["System.ArgumentException"] = ("ThrowArgumentException", 2),
                ["System.InvalidOperationException"] = ("ThrowInvalidOperationException", 1),
                ["System.Exception"] = ("ThrowException", 1),
                ["System.InvalidCastException"] = ("ThrowInvalidCastException", 1),
                ["System.IndexOutOfRangeException"] = ("ThrowIndexOutOfRangeException", 1),
                ["System.NotSupportedException"] = ("ThrowNotSupportedException", 1),
                ["System.Collections.Generic.KeyNotFoundException"] = ("ThrowKeyNotFoundException", 1),
                ["System.NullReferenceException"] = ("ThrowNullReferenceException", 1),
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

            var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            if (semanticModel is null)
            {
                return;
            }

            if (node is ThrowStatementSyntax throwStatement
                && throwStatement.Expression is ObjectCreationExpressionSyntax creation1
                && TryGetThrowHelperMethod(semanticModel, creation1, context.CancellationToken, out var methodName1))
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: $"Replace with ThrowHelper.{methodName1}",
                        createChangedDocument: c => ReplaceThrowStatementAsync(context.Document, throwStatement, methodName1, c),
                        equivalenceKey: nameof(ThrowInInlinedMethodCodeFixProvider)),
                    diagnostic);
            }
            else if (node is ThrowExpressionSyntax throwExpression
                     && throwExpression.Expression is ObjectCreationExpressionSyntax creation2
                     && TryGetThrowHelperMethod(semanticModel, creation2, context.CancellationToken, out var methodName2)
                     && CanConvertThrowExpression(throwExpression))
            {
                context.RegisterCodeFix(
                    CodeAction.Create(
                        title: $"Replace with ThrowHelper.{methodName2}",
                        createChangedDocument: c => ReplaceThrowExpressionAsync(context.Document, throwExpression, methodName2, c),
                        equivalenceKey: nameof(ThrowInInlinedMethodCodeFixProvider)),
                    diagnostic);
            }
        }

        private static bool TryGetThrowHelperMethod(
            SemanticModel semanticModel,
            ObjectCreationExpressionSyntax creation,
            CancellationToken cancellationToken,
            out string methodName)
        {
            methodName = string.Empty;
            var typeInfo = semanticModel.GetTypeInfo(creation, cancellationToken);
            var typeName = typeInfo.Type?.ToDisplayString();

            if (typeName is null || !SupportedExceptions.TryGetValue(typeName, out var entry))
            {
                return false;
            }

            var argCount = creation.ArgumentList?.Arguments.Count ?? 0;
            if (argCount > entry.MaxArgs)
            {
                return false;
            }

            methodName = entry.MethodName;
            return true;
        }

        private static bool CanConvertThrowExpression(ThrowExpressionSyntax throwExpression)
        {
            var parent = throwExpression.Parent;

            if (parent is BinaryExpressionSyntax coalesce && coalesce.IsKind(SyntaxKind.CoalesceExpression))
            {
                // Only offer fix when left side has no side effects (won't be evaluated twice)
                if (!IsSimpleExpression(coalesce.Left))
                {
                    return false;
                }

                // Must be a direct expression of a statement (not nested in another expression)
                if (!IsDirectStatementExpression(coalesce))
                {
                    return false;
                }
            }
            else if (parent is ConditionalExpressionSyntax conditional)
            {
                if (!IsDirectStatementExpression(conditional))
                {
                    return false;
                }
            }
            else
            {
                return false;
            }

            // Must be inside a statement within a block (so we can insert the if-statement)
            var statement = parent.FirstAncestorOrSelf<StatementSyntax>();
            return statement?.Parent is BlockSyntax;
        }

        private static bool IsSimpleExpression(ExpressionSyntax expression)
        {
            return expression switch
            {
                IdentifierNameSyntax => true,
                ThisExpressionSyntax => true,
                MemberAccessExpressionSyntax ma => IsSimpleExpression(ma.Expression),
                _ => false,
            };
        }

        private static bool IsDirectStatementExpression(ExpressionSyntax expression)
        {
            var parent = expression.Parent;

            // return expr;
            if (parent is ReturnStatementSyntax)
            {
                return true;
            }

            // var x = expr; or T x = expr;
            if (parent is EqualsValueClauseSyntax)
            {
                return true;
            }

            // x = expr; (where the assignment is the entire expression statement)
            if (parent is AssignmentExpressionSyntax assignment
                && assignment.Right == expression
                && assignment.Parent is ExpressionStatementSyntax)
            {
                return true;
            }

            return false;
        }

        private static async Task<Document> ReplaceThrowStatementAsync(
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

            var invocation = BuildThrowHelperInvocation(methodName, creation.ArgumentList);
            var expressionStatement = SyntaxFactory.ExpressionStatement(invocation)
                .WithLeadingTrivia(throwStatement.GetLeadingTrivia())
                .WithTrailingTrivia(throwStatement.GetTrailingTrivia());

            root = root.ReplaceNode(throwStatement, expressionStatement);
            root = AddUsingDirectiveIfNeeded(root);
            return document.WithSyntaxRoot(root);
        }

        private static async Task<Document> ReplaceThrowExpressionAsync(
            Document document,
            ThrowExpressionSyntax throwExpression,
            string methodName,
            CancellationToken cancellationToken)
        {
            var creation = (ObjectCreationExpressionSyntax)throwExpression.Expression;
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root is null)
            {
                return document;
            }

            var parent = throwExpression.Parent!;
            var containingStatement = parent.FirstAncestorOrSelf<StatementSyntax>()!;
            var block = (BlockSyntax)containingStatement.Parent!;

            var throwHelperInvocation = BuildThrowHelperInvocation(methodName, creation.ArgumentList);

            ExpressionSyntax condition;
            ExpressionSyntax valueExpression;

            if (parent is BinaryExpressionSyntax coalesce && coalesce.IsKind(SyntaxKind.CoalesceExpression))
            {
                // x ?? throw new Ex(args)  →  if (x is null) { ThrowHelper.ThrowEx(args); }
                condition = SyntaxFactory.IsPatternExpression(
                    coalesce.Left.WithoutTrivia(),
                    SyntaxFactory.ConstantPattern(
                        SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)));
                valueExpression = coalesce.Left.WithoutTrivia();
            }
            else if (parent is ConditionalExpressionSyntax conditional)
            {
                if (conditional.WhenFalse == throwExpression)
                {
                    // cond ? val : throw  →  if (negated-cond) { ThrowHelper; }; use val
                    condition = NegateExpression(conditional.Condition.WithoutTrivia());
                    valueExpression = conditional.WhenTrue.WithoutTrivia();
                }
                else
                {
                    // cond ? throw : val  →  if (cond) { ThrowHelper; }; use val
                    condition = conditional.Condition.WithoutTrivia();
                    valueExpression = conditional.WhenFalse.WithoutTrivia();
                }
            }
            else
            {
                return document;
            }

            // Build the if statement with proper formatting
            var indentation = GetIndentation(containingStatement);
            var eol = GetEndOfLine(containingStatement);
            var ifStatement = BuildIfStatement(indentation, eol, condition, throwHelperInvocation);

            // Replace the parent expression (coalesce/conditional) with just the value expression
            var modifiedStatement = containingStatement.ReplaceNode(parent, valueExpression);

            // Replace the single statement with if + modified statement in the block
            var newStatements = new List<StatementSyntax>(block.Statements.Count + 1);
            foreach (var stmt in block.Statements)
            {
                if (stmt == containingStatement)
                {
                    newStatements.Add(ifStatement);
                    newStatements.Add(modifiedStatement);
                }
                else
                {
                    newStatements.Add(stmt);
                }
            }

            root = root.ReplaceNode(block, block.WithStatements(SyntaxFactory.List(newStatements)));
            root = AddUsingDirectiveIfNeeded(root);
            return document.WithSyntaxRoot(root);
        }

        private static StatementSyntax BuildIfStatement(
            string indentation,
            string eol,
            ExpressionSyntax condition,
            InvocationExpressionSyntax throwHelperInvocation)
        {
            var conditionText = condition.NormalizeWhitespace().ToFullString();
            var invocationText = throwHelperInvocation.NormalizeWhitespace().ToFullString();
            var innerIndent = indentation + "    ";

            var text = $"if ({conditionText}){eol}{indentation}{{{eol}{innerIndent}{invocationText};{eol}{indentation}}}";
            return SyntaxFactory.ParseStatement(text)
                .WithLeadingTrivia(SyntaxFactory.Whitespace(indentation))
                .WithTrailingTrivia(SyntaxFactory.EndOfLine(eol));
        }

        private static ExpressionSyntax NegateExpression(ExpressionSyntax expression)
        {
            // Already negated with !: unwrap
            if (expression is PrefixUnaryExpressionSyntax prefix
                && prefix.IsKind(SyntaxKind.LogicalNotExpression))
            {
                return prefix.Operand is ParenthesizedExpressionSyntax paren
                    ? paren.Expression
                    : prefix.Operand;
            }

            // Simple expressions don't need parentheses: !expr
            if (expression is IdentifierNameSyntax or MemberAccessExpressionSyntax
                or InvocationExpressionSyntax or ParenthesizedExpressionSyntax)
            {
                return SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, expression);
            }

            // Complex expressions: !(expr)
            return SyntaxFactory.PrefixUnaryExpression(
                SyntaxKind.LogicalNotExpression,
                SyntaxFactory.ParenthesizedExpression(expression));
        }

        private static InvocationExpressionSyntax BuildThrowHelperInvocation(
            string methodName,
            ArgumentListSyntax? argumentList)
        {
            return SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.IdentifierName("ThrowHelper"),
                    SyntaxFactory.IdentifierName(methodName)),
                argumentList ?? SyntaxFactory.ArgumentList());
        }

        private static SyntaxNode AddUsingDirectiveIfNeeded(SyntaxNode root)
        {
            if (root is CompilationUnitSyntax compilationUnit && !HasUsingDirective(compilationUnit))
            {
                var eolTrivia = compilationUnit.Usings.Count > 0
                    ? GetTrailingEndOfLine(compilationUnit.Usings.Last())
                    : SyntaxFactory.ElasticCarriageReturnLineFeed;

                var usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(ThrowHelperUsingNamespace))
                    .WithTrailingTrivia(eolTrivia);
                return compilationUnit.AddUsings(usingDirective);
            }

            return root;
        }

        private static string GetIndentation(SyntaxNode node)
        {
            foreach (var trivia in node.GetLeadingTrivia())
            {
                if (trivia.IsKind(SyntaxKind.WhitespaceTrivia))
                {
                    return trivia.ToString();
                }
            }

            return string.Empty;
        }

        private static string GetEndOfLine(SyntaxNode node)
        {
            foreach (var trivia in node.DescendantTrivia())
            {
                if (trivia.IsKind(SyntaxKind.EndOfLineTrivia))
                {
                    return trivia.ToString();
                }
            }

            return "\r\n";
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
            if (compilationUnit.Usings.Any(u => u.Name?.ToString() == ThrowHelperUsingNamespace))
            {
                return true;
            }

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
