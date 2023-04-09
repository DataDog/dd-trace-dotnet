// <copyright file="CorrectContextCodeFixProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

//------------------------------------------------------------------------------
// This file is based on https://github.com/Suchiman/SerilogAnalyzer/blob/bf62860f502db19bc45fd0f46541f383ef3a4455/SerilogAnalyzer/SerilogAnalyzer/CorrectLoggerContextCodeFixProvider.cs
//------------------------------------------------------------------------------

// Copyright 2017 Robin Sue
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Simplification;

namespace Datadog.Trace.Tools.Analyzers.LogAnalyzer;

/// <summary>
/// Fixes errors produced due to using the wrong context in GetLoggerFor
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(CorrectContextCodeFixProvider))]
public class CorrectContextCodeFixProvider : CodeFixProvider
{
    private const string Title = "Use correct context for logger";

    /// <inheritdoc/>
    public sealed override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArray.Create(Diagnostics.UseCorrectContextualLoggerDiagnosticId);

    /// <inheritdoc/>
    public sealed override FixAllProvider GetFixAllProvider()
        => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var declaration = root.FindNode(diagnosticSpan) as TypeSyntax;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: Title,
                createChangedSolution: c => UseCorrectType(context.Document, declaration, c),
                equivalenceKey: Title),
            diagnostic);
    }

    private async Task<Solution> UseCorrectType(Document document, TypeSyntax syntax, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is not null && semanticModel is not null)
        {
            var argumentList = syntax.Ancestors().OfType<TypeDeclarationSyntax>().First();
            var symbol = ModelExtensions.GetDeclaredSymbol(semanticModel, argumentList, cancellationToken);

            if (symbol is not null)
            {
                var identifier = SyntaxFactory.ParseName(symbol.ToString()).WithAdditionalAnnotations(Simplifier.Annotation);
                root = root.ReplaceNode(syntax, identifier);
                document = document.WithSyntaxRoot(root);
            }
        }

        return document.Project.Solution;
    }
}
