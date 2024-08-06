// <copyright file="ReplaceAspectCodeFixProvider.cs" company="Datadog">
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
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ReplaceAspectCodeFixProvider))]
[Shared]
public class ReplaceAspectCodeFixProvider : CodeFixProvider
{
    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds
    {
        get => ImmutableArray.Create(ReplaceAspectAnalyzer.DiagnosticId);
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

        if (methodDeclaration?.Body is { Statements.Count: >2 } body
         && body.Statements[0] is LocalDeclarationStatementSyntax localDeclaration
         && body.Statements[body.Statements.Count - 1] is ReturnStatementSyntax { Expression: IdentifierNameSyntax identifierName }
         && localDeclaration.Declaration.Variables.Count == 1
         && localDeclaration.Declaration.Variables[0] is { } variable
         && variable.Identifier.ToString() == identifierName.Identifier.ToString())
        {
            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Wrap internals with exception handler",
                    createChangedDocument: c => AddTryCatch(context.Document, methodDeclaration, c),
                    equivalenceKey: nameof(ReplaceAspectCodeFixProvider)),
                diagnostic);
        }
    }

    private async Task<Document> AddTryCatch(Document document, MethodDeclarationSyntax methodDeclaration, CancellationToken cancellationToken)
    {
        // we know we're calling this with something we can fix,
        // we just need to work out if we need to wrap the internals in a try-catch
        // or add a catch statement
        var body = methodDeclaration.Body!;
        var localDeclaration = (LocalDeclarationStatementSyntax)body.Statements[0];
        var returnSyntax = (ReturnStatementSyntax)body.Statements[body.Statements.Count - 1];
        TryStatementSyntax tryCatch;

        if (body.Statements.Count == 3 && body.Statements[1] is TryStatementSyntax tryStatementSyntax)
        {
            tryCatch = tryStatementSyntax;
        }
        else
        {
            var block = SyntaxFactory.Block(body.Statements.Skip(1).Take(body.Statements.Count - 2));
            tryCatch = SyntaxFactory.TryStatement().WithBlock(block);
        }

        // Add the catch statement to the try-catch block
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

        var catchDeclaration = SyntaxFactory.CatchDeclaration(SyntaxFactory.IdentifierName("Exception"), SyntaxFactory.Identifier("ex"));
        var logExpression = SyntaxFactory.ExpressionStatement(
            SyntaxFactory.ParseExpression($$"""IastModule.Log.Error(ex, $"Error invoking {nameof({{typeName}})}.{nameof({{methodName}})}")"""));

        var catchSyntax = SyntaxFactory.CatchClause()
                                       .WithDeclaration(catchDeclaration)
                                       .WithBlock(SyntaxFactory.Block(logExpression));

        var updatedTryCatch = tryCatch.AddCatches(catchSyntax);
        var newBody = SyntaxFactory.Block(localDeclaration, updatedTryCatch, returnSyntax)
                                   .WithAdditionalAnnotations(Formatter.Annotation);

        var newMethodDeclaration = methodDeclaration.WithBody(newBody);

        // replace the syntax and return updated document
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        root = root!.ReplaceNode(methodDeclaration, newMethodDeclaration);
        return document.WithSyntaxRoot(root);
    }
}
