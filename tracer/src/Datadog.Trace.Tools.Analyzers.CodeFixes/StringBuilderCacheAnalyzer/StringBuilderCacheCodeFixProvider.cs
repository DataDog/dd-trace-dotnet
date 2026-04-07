// <copyright file="StringBuilderCacheCodeFixProvider.cs" company="Datadog">
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
using Microsoft.CodeAnalysis.Formatting;

namespace Datadog.Trace.Tools.Analyzers.StringBuilderCacheAnalyzer;

/// <summary>
/// Code fix provider that replaces <c>new StringBuilder()</c> with
/// <c>StringBuilderCache.Acquire()</c> and rewrites <c>.ToString()</c>
/// to <c>StringBuilderCache.GetStringAndRelease()</c>.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(StringBuilderCacheCodeFixProvider))]
[Shared]
public sealed class StringBuilderCacheCodeFixProvider : CodeFixProvider
{
    private const string Title = "Use StringBuilderCache";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds { get; }
        = ImmutableArray.Create(Diagnostics.DiagnosticId);

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
        var node = root.FindNode(diagnostic.Location.SourceSpan);

        if (node is not (ObjectCreationExpressionSyntax or ImplicitObjectCreationExpressionSyntax))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: Title,
                createChangedDocument: ct => ApplyFixAsync(context.Document, node, ct),
                equivalenceKey: Title),
            diagnostic);
    }

    private static async Task<Document> ApplyFixAsync(Document document, SyntaxNode creationNode, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }

        var variableName = GetAssignedVariableName(creationNode);

        // Collect all .ToString() invocations on the variable in the enclosing scope
        var toStringInvocations = ImmutableArray<InvocationExpressionSyntax>.Empty;
        if (variableName is not null)
        {
            var enclosingFunction = GetEnclosingFunction(creationNode);
            if (enclosingFunction is not null)
            {
                toStringInvocations = enclosingFunction
                    .DescendantNodes()
                    .OfType<InvocationExpressionSyntax>()
                    .Where(inv =>
                        inv.Expression is MemberAccessExpressionSyntax memberAccess
                        && memberAccess.Name.Identifier.Text == "ToString"
                        && memberAccess.Expression is IdentifierNameSyntax id
                        && id.Identifier.Text == variableName
                        && inv.ArgumentList.Arguments.Count == 0)
                    .ToImmutableArray();
            }
        }

        // Replace all nodes in a single pass to avoid span invalidation
        var newRoot = root.ReplaceNodes(
            toStringInvocations.Cast<SyntaxNode>().Append(creationNode),
            (original, _) =>
            {
                if (original == creationNode)
                {
                    return BuildAcquireCall(original);
                }

                // Must be a .ToString() invocation — replace with GetStringAndRelease
                return SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName("StringBuilderCache"),
                        SyntaxFactory.IdentifierName("GetStringAndRelease")),
                    SyntaxFactory.ArgumentList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.Argument(SyntaxFactory.IdentifierName(variableName!)))))
                    .WithTriviaFrom(original);
            });

        // Add using directive for Datadog.Trace.Util
        newRoot = AddUsingDirectiveIfMissing(newRoot);

        return document.WithSyntaxRoot(newRoot);
    }

    private static InvocationExpressionSyntax BuildAcquireCall(SyntaxNode creationNode)
    {
        var memberAccess = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.IdentifierName("StringBuilderCache"),
            SyntaxFactory.IdentifierName("Acquire"));

        // Forward constructor arguments (e.g., capacity) to Acquire()
        ArgumentListSyntax? originalArgs = creationNode switch
        {
            ObjectCreationExpressionSyntax objectCreation => objectCreation.ArgumentList,
            ImplicitObjectCreationExpressionSyntax implicitCreation => implicitCreation.ArgumentList,
            _ => null,
        };

        var args = originalArgs is not null && originalArgs.Arguments.Count > 0
            ? originalArgs
            : SyntaxFactory.ArgumentList();

        return SyntaxFactory.InvocationExpression(memberAccess, args)
            .WithTriviaFrom(creationNode);
    }

    private static string? GetAssignedVariableName(SyntaxNode creationNode)
    {
        var parent = creationNode.Parent;

        // var sb = new StringBuilder();
        if (parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator })
        {
            return declarator.Identifier.Text;
        }

        // sb = new StringBuilder();
        if (parent is AssignmentExpressionSyntax assignment
            && assignment.Left is IdentifierNameSyntax identifier)
        {
            return identifier.Identifier.Text;
        }

        return null;
    }

    private static SyntaxNode? GetEnclosingFunction(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case MethodDeclarationSyntax:
                case LocalFunctionStatementSyntax:
                case AnonymousFunctionExpressionSyntax:
                case AccessorDeclarationSyntax:
                    return current;
            }
        }

        return null;
    }

    private static SyntaxNode AddUsingDirectiveIfMissing(SyntaxNode root)
    {
        if (root is not CompilationUnitSyntax compilationUnit)
        {
            return root;
        }

        const string targetNamespace = "Datadog.Trace.Util";

        // Check if the using already exists
        var hasUsing = compilationUnit.Usings
            .Any(u => u.Name?.ToString() == targetNamespace);

        if (hasUsing)
        {
            return root;
        }

        // Match the line ending style of the existing using directives
        var existingTrailingTrivia = compilationUnit.Usings.LastOrDefault()?.GetTrailingTrivia();
        var trailingTrivia = existingTrailingTrivia?.Count > 0
            ? existingTrailingTrivia.Value
            : SyntaxFactory.TriviaList(SyntaxFactory.ElasticLineFeed);

        var usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(targetNamespace))
            .WithTrailingTrivia(trailingTrivia)
            .WithAdditionalAnnotations(Formatter.Annotation);

        return compilationUnit.AddUsings(usingDirective);
    }
}
