// <copyright file="JsonArrayPoolCodeFixProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Tools.Analyzers.AllocationAnalyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Datadog.Trace.Tools.Analyzers.AllocationAnalyzers;

/// <summary>
/// Code fix provider that adds <c>ArrayPool = JsonArrayPool.Shared</c> to Newtonsoft.Json reader/writer initializers.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp)]
public sealed class JsonArrayPoolCodeFixProvider : CodeFixProvider
{
    private const string Title = "Add ArrayPool = JsonArrayPool.Shared";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds
    { get; } = ImmutableArray.Create(Diagnostics.JsonArrayPoolDiagnosticId);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var diagnostic = context.Diagnostics[0];
        var codeAction = CodeAction.Create(
            Title,
            ct => AddArrayPoolAsync(context.Document, diagnostic, ct),
            Title);
        context.RegisterCodeFix(codeAction, diagnostic);
        return Task.CompletedTask;
    }

    private static async Task<Document> AddArrayPoolAsync(
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
        if (node is not ObjectCreationExpressionSyntax objectCreation)
        {
            return document;
        }

        var arrayPoolAssignment = SyntaxFactory.AssignmentExpression(
            SyntaxKind.SimpleAssignmentExpression,
            SyntaxFactory.IdentifierName("ArrayPool"),
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("JsonArrayPool"),
                SyntaxFactory.IdentifierName("Shared")));

        ObjectCreationExpressionSyntax newObjectCreation;

        if (objectCreation.Initializer is null)
        {
            // No initializer — create one: { ArrayPool = JsonArrayPool.Shared }
            var initializer = SyntaxFactory.InitializerExpression(
                SyntaxKind.ObjectInitializerExpression,
                SyntaxFactory.SingletonSeparatedList<ExpressionSyntax>(arrayPoolAssignment));

            // Add space before the initializer
            newObjectCreation = objectCreation.WithInitializer(
                initializer.WithLeadingTrivia(SyntaxFactory.Space));
        }
        else
        {
            // Existing initializer — append ArrayPool assignment
            var newExpressions = objectCreation.Initializer.Expressions.Add(arrayPoolAssignment);
            var newInitializer = objectCreation.Initializer.WithExpressions(newExpressions);
            newObjectCreation = objectCreation.WithInitializer(newInitializer);
        }

        var newRoot = root.ReplaceNode(objectCreation, newObjectCreation);

        // Add using directive for JsonArrayPool if not present
        if (newRoot is CompilationUnitSyntax compilationUnit)
        {
            const string jsonArrayPoolNamespace = "Datadog.Trace.Util.Json";
            var hasUsing = compilationUnit.Usings.Any(u => u.Name?.ToString() == jsonArrayPoolNamespace);
            if (!hasUsing)
            {
                // Detect the line ending style from the existing document
                var endOfLineTrivia = root.DescendantTrivia()
                    .Where(t => t.IsKind(SyntaxKind.EndOfLineTrivia))
                    .Select(t => t.ToString())
                    .FirstOrDefault() ?? "\n";

                var usingDirective = SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(jsonArrayPoolNamespace))
                    .WithTrailingTrivia(SyntaxFactory.EndOfLine(endOfLineTrivia));
                newRoot = compilationUnit.AddUsings(usingDirective);
            }
        }

        return document.WithSyntaxRoot(newRoot);
    }
}
