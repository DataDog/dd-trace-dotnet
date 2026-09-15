// <copyright file="EnumToStringCodeFixProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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

namespace Datadog.Trace.Tools.Analyzers.EnumToStringAnalyzer;

/// <summary>
/// Code fix provider that replaces .ToString() with .ToStringFast() on enum expressions
/// when a ToStringFast extension method is available.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp)]
public sealed class EnumToStringCodeFixProvider : CodeFixProvider
{
    private const string Title = "Use ToStringFast()";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(Diagnostics.DiagnosticId);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider()
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document
            .GetSyntaxRootAsync(context.CancellationToken)
            .ConfigureAwait(false);

        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not InvocationExpressionSyntax invocation)
        {
            return;
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        // Check if ToStringFast extension method is available for this enum type
        var semanticModel = await context.Document
            .GetSemanticModelAsync(context.CancellationToken)
            .ConfigureAwait(false);

        if (semanticModel is null)
        {
            return;
        }

        var receiverType = semanticModel.GetTypeInfo(
            memberAccess.Expression,
            context.CancellationToken).Type;

        if (receiverType is null)
        {
            return;
        }

        // Look for a ToStringFast extension method accessible at this location
        var extensionMethods = semanticModel.LookupSymbols(
            invocation.SpanStart,
            receiverType,
            "ToStringFast",
            includeReducedExtensionMethods: true);

        if (!extensionMethods.Any(s => s is IMethodSymbol { Parameters.Length: 0 }))
        {
            return;
        }

        var codeAction = CodeAction.Create(
            Title,
            ct => ReplaceWithToStringFastAsync(context.Document, invocation, memberAccess, ct),
            Title);

        context.RegisterCodeFix(codeAction, diagnostic);
    }

    private static async Task<Document> ReplaceWithToStringFastAsync(
        Document document,
        InvocationExpressionSyntax invocation,
        MemberAccessExpressionSyntax memberAccess,
        CancellationToken cancellationToken)
    {
        var newName = SyntaxFactory.IdentifierName("ToStringFast");
        var newMemberAccess = memberAccess.WithName(newName);
        var newInvocation = invocation.WithExpression(newMemberAccess);

        var root = await document
            .GetSyntaxRootAsync(cancellationToken)
            .ConfigureAwait(false);

        if (root is null)
        {
            return document;
        }

        var newRoot = root.ReplaceNode(invocation, newInvocation);
        return document.WithSyntaxRoot(newRoot);
    }
}
