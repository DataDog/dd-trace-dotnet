// <copyright file="DatadogTraceObsoleteUsageAnalyzerCodeFixProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;

namespace Datadog.Trace.Analyzers;

/// <summary>
/// Provides a code fix for <see cref="DatadogTraceObsoleteUsageAnalyzer"/>
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DatadogTraceObsoleteUsageAnalyzerCodeFixProvider))]
[Shared]
public class DatadogTraceObsoleteUsageAnalyzerCodeFixProvider : CodeFixProvider
{
    private const string Title = "Use Tracer.Instance";

    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(DatadogTraceObsoleteUsageAnalyzer.ObsoleteConstructorDiagnosticId);

    /// <inheritdoc />
    public sealed override FixAllProvider GetFixAllProvider()
    {
        // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        return WellKnownFixAllProviders.BatchFixer;
    }

    /// <inheritdoc />
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        context.CancellationToken.ThrowIfCancellationRequested();

        var diagnostic = context.Diagnostics.First();
        var node = root.FindNode(diagnostic.Location.SourceSpan);
        if (node is not ObjectCreationExpressionSyntax creationNode)
        {
            return;
        }

        var getTracerInstance = GetTracerInstance(creationNode);

        context.RegisterCodeFix(
            CodeAction.Create(
                Title,
                createChangedDocument: c => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(creationNode, getTracerInstance))),
                equivalenceKey: Title),
            diagnostic);

        // // Find the type declaration identified by the diagnostic.
        // var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().First();
        //
        // // Register a code action that will invoke the fix.
        // context.RegisterCodeFix(
        //     CodeAction.Create(
        //         title: CodeFixResources.CodeFixTitle,
        //         createChangedSolution: c => MakeUppercaseAsync(context.Document, declaration, c),
        //         equivalenceKey: nameof(CodeFixResources.CodeFixTitle)),
        //     diagnostic);
    }

    // private async Task<Solution> MakeUppercaseAsync(Document document, TypeDeclarationSyntax typeDecl, CancellationToken cancellationToken)
    // {
    //     // Compute new uppercase name.
    //     var identifierToken = typeDecl.Identifier;
    //     var newName = identifierToken.Text.ToUpperInvariant();
    //
    //     // Get the symbol representing the type to be renamed.
    //     var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
    //     var typeSymbol = semanticModel.GetDeclaredSymbol(typeDecl, cancellationToken);
    //
    //     // Produce a new solution that has all references to that type renamed, including the declaration.
    //     var originalSolution = document.Project.Solution;
    //     var optionSet = originalSolution.Workspace.Options;
    //     var newSolution = await Renamer.RenameSymbolAsync(document.Project.Solution, typeSymbol, newName, optionSet, cancellationToken).ConfigureAwait(false);
    //
    //     // Return the new solution with the now-uppercase type name.
    //     return newSolution;
    // }

    private MemberAccessExpressionSyntax? GetTracerInstance(ObjectCreationExpressionSyntax objectCreationExpressionSyntax)
    {
        // Replace
        //   new Tracer()
        // with
        //   Tracer.Instance

        // assuming it's a NameSyntax that we can handler, as it should be Tracer or Datadog.Trace.Tracer etc
        var identifier = GetNameAsSimpleMemberAccessExpression(objectCreationExpressionSyntax.Type);

        if (identifier is null)
        {
            // Weird, shouldn't happen
            return null;
        }

        return SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            identifier,
            SyntaxFactory.IdentifierName("Instance"));
    }

    private ExpressionSyntax? GetNameAsSimpleMemberAccessExpression(TypeSyntax syntax)
    {
        // Need to convert qualified names Datadog.Trace.Tracer (for example) from
        //   QualifiedName
        // To
        //   SimpleMemberAccess -> SimpleMemberAccess -> IdentifierName
        return syntax switch
        {
            null => null,
            AliasQualifiedNameSyntax name => name,
            IdentifierNameSyntax name => name,
            QualifiedNameSyntax qualified => GetNameAsSimpleMemberAccessExpression(qualified.Left) is { } left
                                                 ? SyntaxFactory.MemberAccessExpression(
                                                     SyntaxKind.SimpleMemberAccessExpression,
                                                     left,
                                                     qualified.Right)
                                                 : null,
            _ => null
        };
    }
}
