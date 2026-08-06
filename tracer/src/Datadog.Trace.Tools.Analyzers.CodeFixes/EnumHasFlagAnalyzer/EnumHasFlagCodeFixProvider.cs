// <copyright file="EnumHasFlagCodeFixProvider.cs" company="Datadog">
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

namespace Datadog.Trace.Tools.Analyzers.EnumHasFlagAnalyzer;

/// <summary>
/// Replaces Enum.HasFlag() with HasFlagFast() when the extension method is available.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp)]
public sealed class EnumHasFlagCodeFixProvider : CodeFixProvider
{
    private const string Title = "Replace with HasFlagFast()";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(Diagnostics.DiagnosticId);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider()
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics[0];

        // Only offer the code fix when HasFlagFast is available
        if (!diagnostic.Properties.TryGetValue(Diagnostics.HasFlagFastAvailableKey, out var value)
            || value != "true")
        {
            return Task.CompletedTask;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: Title,
                createChangedDocument: ct => ReplaceWithHasFlagFast(context.Document, diagnostic, ct),
                equivalenceKey: Title),
            diagnostic);

        return Task.CompletedTask;
    }

    private static async Task<Document> ReplaceWithHasFlagFast(
        Document document,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var node = root.FindNode(diagnostic.Location.SourceSpan);
        var invocation = node.AncestorsAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault();
        if (invocation?.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return document;
        }

        // Replace .HasFlag with .HasFlagFast
        var newName = SyntaxFactory.IdentifierName("HasFlagFast");
        var newMemberAccess = memberAccess.WithName(newName);
        var newInvocation = invocation.WithExpression(newMemberAccess);

        root = root.ReplaceNode(invocation, newInvocation);
        return document.WithSyntaxRoot(root);
    }
}
