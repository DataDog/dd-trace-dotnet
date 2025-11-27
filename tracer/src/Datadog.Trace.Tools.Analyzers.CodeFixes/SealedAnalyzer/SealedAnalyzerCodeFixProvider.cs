// <copyright file="SealedAnalyzerCodeFixProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Composition;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Editing;

namespace Datadog.Trace.Tools.Analyzers.SealedAnalyzer;

/// <summary>
/// Code fix provider for the SealedAnalyzer
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp)]
public sealed class SealedAnalyzerCodeFixProvider : CodeFixProvider
{
    private const string Title = "Seal class";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds { get; } = ImmutableArray.Create(Diagnostics.DiagnosticId);

    /// <inheritdoc/>
    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var codeAction = CodeAction.Create(
            Title,
            SealClassDeclarationsAsync,
            Title);
        context.RegisterCodeFix(codeAction, context.Diagnostics);
        return Task.CompletedTask;

        async Task<Solution> SealClassDeclarationsAsync(CancellationToken token)
        {
            var solutionEditor = new SolutionEditor(context.Document.Project.Solution);
            await SealDeclarationAt(solutionEditor, context.Diagnostics[0].Location, token).ConfigureAwait(false);

            foreach (var location in context.Diagnostics[0].AdditionalLocations)
            {
                await SealDeclarationAt(solutionEditor, location, token).ConfigureAwait(false);
            }

            return solutionEditor.GetChangedSolution();
        }

        static async Task SealDeclarationAt(SolutionEditor solutionEditor, Location location, CancellationToken token)
        {
            var solution = solutionEditor.OriginalSolution;
            var document = solution.GetDocument(location.SourceTree);

            if (document is null)
            {
                return;
            }

            var documentEditor = await solutionEditor.GetDocumentEditorAsync(document.Id, token).ConfigureAwait(false);

            var root = await GetRequiredSyntaxRootAsync(document, token).ConfigureAwait(false);
            var declaration = root.FindNode(location.SourceSpan);
            var newModifiers = documentEditor.Generator.GetModifiers(declaration).WithIsSealed(true);
            var newDeclaration = documentEditor.Generator.WithModifiers(declaration, newModifiers);
            documentEditor.ReplaceNode(declaration, newDeclaration);
        }

        static async ValueTask<SyntaxNode> GetRequiredSyntaxRootAsync(Document document, CancellationToken cancellationToken)
        {
            if (document.TryGetSyntaxRoot(out var root))
            {
                return root;
            }

            root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            return root ?? throw new InvalidOperationException("SyntaxTree is required to accomplish the task but is not supported by document");
        }
    }
}
