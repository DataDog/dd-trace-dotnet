// <copyright file="DestructuringHintCodeFixProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

//------------------------------------------------------------------------------
// This file is based on https://github.com/Suchiman/SerilogAnalyzer/blob/bf62860f502db19bc45fd0f46541f383ef3a4455/SerilogAnalyzer/SerilogAnalyzer/DestructuringHintCodeFixProvider.cs
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
using Microsoft.CodeAnalysis.Text;

namespace Datadog.Trace.Tools.Analyzers.LogAnalyzer;

/// <summary>
/// Fixes errors produced due to not destructing anonymous objects
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(DestructuringHintCodeFixProvider))]
public class DestructuringHintCodeFixProvider : CodeFixProvider
{
    private const string Title = "Add destructuring hint for anonymous object";

    /// <inheritdoc/>
    public sealed override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArray.Create(Diagnostics.DestructureAnonymousObjectsDiagnosticId);

    /// <inheritdoc/>
    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    /// <inheritdoc/>
    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: Title,
                createChangedSolution: c => AddDestructuringHint(context.Document, diagnosticSpan, c),
                equivalenceKey: Title),
            diagnostic);
    }

    private async Task<Solution> AddDestructuringHint(Document document, TextSpan textSpan, CancellationToken cancellationToken)
    {
        var text = await document.GetTextAsync(cancellationToken).ConfigureAwait(false);
        text = text.Replace(start: textSpan.Start + 1, length: 0, newText: "@"); // textSpan: "{Name}" -> "{@Name}"
        document = document.WithText(text);

        return document.Project.Solution;
    }
}
