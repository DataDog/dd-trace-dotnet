// <copyright file="SpanIndexOfCodeFixProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Datadog.Trace.Tools.Analyzers.SpanIndexOfAnalyzer;

/// <summary>
/// Replaces a <c>span.IndexOf(x) &gt;= 0</c>-style comparison with <c>span.Contains(x)</c> (or its negation).
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SpanIndexOfCodeFixProvider))]
[Shared]
public class SpanIndexOfCodeFixProvider : CodeFixProvider
{
    private const string Title = "Use Contains instead";

    /// <inheritdoc/>
    public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(Diagnostics.DiagnosticId);

    /// <inheritdoc/>
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        if (root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true) is not BinaryExpressionSyntax binary)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                Title,
                c => FixAsync(context.Document, binary, c),
                equivalenceKey: Title),
            diagnostic);
    }

    private static async Task<Document> FixAsync(Document document, BinaryExpressionSyntax binary, System.Threading.CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var invocation = binary.Left as InvocationExpressionSyntax ?? binary.Right as InvocationExpressionSyntax;
        if (invocation is not { Expression: MemberAccessExpressionSyntax memberAccess })
        {
            return document;
        }

        var containsName = SyntaxFactory.IdentifierName("Contains").WithTriviaFrom(memberAccess.Name);
        var containsInvocation = invocation.WithExpression(memberAccess.WithName(containsName));

        var negate = IsNegatedComparison(binary);
        ExpressionSyntax replacement = negate
            ? SyntaxFactory.PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, SyntaxFactory.ParenthesizedExpression(containsInvocation))
            : containsInvocation;

        replacement = replacement
            .WithLeadingTrivia(binary.GetLeadingTrivia())
            .WithTrailingTrivia(binary.GetTrailingTrivia());

        var newRoot = root.ReplaceNode(binary, replacement);
        return document.WithSyntaxRoot(newRoot);
    }

    private static bool IsNegatedComparison(BinaryExpressionSyntax binary)
    {
        // "not found" shapes (indexOf < 0, 0 > indexOf, indexOf == -1, -1 == indexOf) need !Contains(...).
        return binary.Kind() switch
        {
            SyntaxKind.LessThanExpression or SyntaxKind.GreaterThanExpression or SyntaxKind.EqualsExpression => IsSentinelNegated(binary),
            _ => false,
        };
    }

    private static bool IsSentinelNegated(BinaryExpressionSyntax binary)
    {
        // EqualsExpression (== -1) is always "not found".
        if (binary.IsKind(SyntaxKind.EqualsExpression))
        {
            return true;
        }

        // For < / > we need to know which side is the IndexOf call to know whether this is "< 0" (not found)
        // or "> -1" (found), vs. "0 >" / "-1 <" the other way around.
        var indexOfIsLeft = binary.Left is InvocationExpressionSyntax;
        if (binary.IsKind(SyntaxKind.LessThanExpression))
        {
            // indexOf < 0  => not found (negate)
            // 0 > indexOf (written as "indexOf < 0" cannot happen here since this branch is LessThan)
            // -1 < indexOf => found (do not negate)
            return indexOfIsLeft;
        }

        // GreaterThanExpression
        // indexOf > -1 => found (do not negate)
        // 0 > indexOf => not found (negate)
        return !indexOfIsLeft;
    }
}
