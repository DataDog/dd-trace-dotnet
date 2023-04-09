// <copyright file="ExceptionPositionCodeFixProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

//------------------------------------------------------------------------------
// This file is based on https://github.com/Suchiman/SerilogAnalyzer/blob/bf62860f502db19bc45fd0f46541f383ef3a4455/SerilogAnalyzer/SerilogAnalyzer/CodeFixProvider.cs
//------------------------------------------------------------------------------

// Copyright 2016 Robin Sue
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

#nullable enable
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Datadog.Trace.Tools.Analyzers.LogAnalyzer;

/// <summary>
/// A code fix that moves an exception to the correct location
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ExceptionPositionCodeFixProvider))]
public class ExceptionPositionCodeFixProvider : CodeFixProvider
{
    private const string Title = "Make exception the first argument";

    /// <inheritdoc/>
    public sealed override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArray.Create(Diagnostics.ExceptionDiagnosticId);

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

        var diagnostic = context.Diagnostics[0];
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var declaration = root.FindNode(diagnosticSpan) as ArgumentSyntax;

        if (declaration is null)
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: Title,
                createChangedSolution: c => MoveExceptionFirstAsync(context.Document, declaration, c),
                equivalenceKey: Title),
            diagnostic);
    }

    private async Task<Solution> MoveExceptionFirstAsync(Document document, ArgumentSyntax argument, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var argumentList = argument.AncestorsAndSelf().OfType<ArgumentListSyntax>().First();

        var newList = argumentList.Arguments.Remove(argument);

        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is not null && semanticModel is not null && argumentList.Parent is not null)
        {
            var symbolInfo = semanticModel.GetSymbolInfo(argumentList.Parent);

            if (symbolInfo.Symbol is IMethodSymbol methodSymbol && methodSymbol is { IsExtensionMethod: true, IsStatic: true })
            {
                // This is a static method invocation of an extension method, so the first parameter is the
                // extended type itself; hence we insert at the second position
                newList = newList.Insert(1, argument);
            }
            else
            {
                newList = newList.Insert(0, argument);
            }

            root = root.ReplaceNode(argumentList, argumentList.WithArguments(newList));
            document = document.WithSyntaxRoot(root);
        }

        return document.Project.Solution;
    }
}
