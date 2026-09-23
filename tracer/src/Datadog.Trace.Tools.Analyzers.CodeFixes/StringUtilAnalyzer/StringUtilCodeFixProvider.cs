// <copyright file="StringUtilCodeFixProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

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

namespace Datadog.Trace.Tools.Analyzers.StringUtilAnalyzer;

/// <summary>
/// Code fix provider for <see cref="StringUtilAnalyzer"/>. Replaces
/// <c>string.IsNullOrEmpty(...)</c> / <c>string.IsNullOrWhiteSpace(...)</c> with the equivalent
/// <c>StringUtil</c> call.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp)]
[Shared]
public sealed class StringUtilCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(Diagnostics.DiagnosticId);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var memberAccess = root.FindNode(diagnosticSpan).FirstAncestorOrSelf<MemberAccessExpressionSyntax>();
        if (memberAccess is null)
        {
            return;
        }

        var methodName = memberAccess.Name.Identifier.Text;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Use StringUtil.{methodName}()",
                createChangedDocument: c => ReplaceWithStringUtilAsync(context.Document, memberAccess, methodName, c),
                equivalenceKey: nameof(StringUtilCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> ReplaceWithStringUtilAsync(Document document, MemberAccessExpressionSyntax memberAccess, string methodName, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var newMemberAccess = memberAccess
                              .WithExpression(SyntaxFactory.IdentifierName("StringUtil"))
                              .WithTriviaFrom(memberAccess);

        var newRoot = root.ReplaceNode(memberAccess, newMemberAccess);
        return document.WithSyntaxRoot(newRoot);
    }
}
