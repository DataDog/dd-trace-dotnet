// <copyright file="BeforeAfterAspectCodeFixProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Tools.Analyzers.ThreadAbortAnalyzer;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace Datadog.Trace.Tools.Analyzers.AspectAnalyzers;

/// <summary>
/// A CodeFixProvider for the <see cref="ThreadAbortAnalyzer"/>
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(BeforeAfterAspectCodeFixProvider))]
[Shared]
public class BeforeAfterAspectCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds
    {
        get => ImmutableArray.Create(BeforeAfterAspectAnalyzer.DiagnosticId);
    }

    /// <inheritdoc />
    public sealed override FixAllProvider GetFixAllProvider()
    {
        // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
        return WellKnownFixAllProviders.BatchFixer;
    }

    /// <inheritdoc />
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        // Find the methodDeclaration identified by the diagnostic.
        var methodDeclaration = root?.FindToken(diagnosticSpan.Start)
                                    .Parent
                                    ?.AncestorsAndSelf()
                                    .OfType<MethodDeclarationSyntax>()
                                    .First();

        if (methodDeclaration is null)
        {
            return;
        }

        // Register a code action that will invoke the fix.
        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Wrap aspect with exception handler",
                createChangedDocument: c => AddTryCatch(context.Document, methodDeclaration, c),
                equivalenceKey: nameof(BeforeAfterAspectCodeFixProvider)),
            diagnostic);
    }

    private async Task<Document> AddTryCatch(Document document, MethodDeclarationSyntax methodDeclaration, CancellationToken cancellationToken)
    {
        var paramName = methodDeclaration.ParameterList.Parameters.FirstOrDefault()?.Identifier.Text;
        var blockSyntax = methodDeclaration.Body ?? CreateBasicBlock(methodDeclaration.ExpressionBody) ?? null;
        if (blockSyntax is null)
        {
            // weirdness, bail out
            return document;
        }

        var parentType = methodDeclaration.AncestorsAndSelf()
                                        .FirstOrDefault(x => x is TypeDeclarationSyntax or RecordDeclarationSyntax or StructDeclarationSyntax);

        var typeName = parentType switch
        {
            StructDeclarationSyntax t => t.Identifier.Text,
            RecordDeclarationSyntax t => t.Identifier.Text,
            TypeDeclarationSyntax t => t.Identifier.Text,
            _ => "UNKNOWN",
        };

        var methodName = methodDeclaration.Identifier.Text;

        // check if we already have a try catch
        TryStatementSyntax tryCatch;
        if (blockSyntax.Statements.Count == 1 && blockSyntax.Statements[0] is TryStatementSyntax tryStatementSyntax)
        {
            tryCatch = tryStatementSyntax;
        }
        else
        {
            tryCatch = SyntaxFactory.TryStatement().WithBlock(blockSyntax);
        }

        // create the trystatementsyntax with the internals of the method declaration
        var catchDeclaration = SyntaxFactory.CatchDeclaration(SyntaxFactory.IdentifierName("Exception"), SyntaxFactory.Identifier("ex"));
        var logExpression = SyntaxFactory.ExpressionStatement(
            SyntaxFactory.ParseExpression($$"""IastModule.Log.Error(ex, $"Error invoking {nameof({{typeName}})}.{nameof({{methodName}})}")"""));
        var returnStatement = paramName is not null
                                  ? SyntaxFactory.ReturnStatement(SyntaxFactory.IdentifierName(paramName))
                                  : SyntaxFactory.ReturnStatement();
        var syntaxList = SyntaxFactory.List(new StatementSyntax[] { logExpression, returnStatement });

        var catchSyntax = SyntaxFactory.CatchClause()
                                       .WithDeclaration(catchDeclaration)
                                       .WithBlock(SyntaxFactory.Block(syntaxList));

        var updatedTryCatch = tryCatch.AddCatches(catchSyntax);
        var newBlock = SyntaxFactory.Block(updatedTryCatch)
                                    .WithAdditionalAnnotations(Formatter.Annotation);

        // remove the expression body if we have one
        var newMethodDeclaration = methodDeclaration.ExpressionBody is not null
                                       ? methodDeclaration
                                        .WithExpressionBody(null)
                                        .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.None))
                                       : methodDeclaration;

        newMethodDeclaration = newMethodDeclaration.WithBody(newBlock);

        // replace the syntax and return updated document
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        root = root!.ReplaceNode(methodDeclaration, newMethodDeclaration);
        return document.WithSyntaxRoot(root);
    }

    private BlockSyntax? CreateBasicBlock(ArrowExpressionClauseSyntax? expressionBodyExpression)
    {
        if (expressionBodyExpression is null)
        {
            return null;
        }

        return SyntaxFactory.Block()
                            .WithStatements(
                                 SyntaxFactory.SingletonList<StatementSyntax>(
                                     SyntaxFactory.ReturnStatement(expressionBodyExpression.Expression)));
    }
}
