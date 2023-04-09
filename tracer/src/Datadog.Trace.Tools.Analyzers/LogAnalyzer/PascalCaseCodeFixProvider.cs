// <copyright file="PascalCaseCodeFixProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

//------------------------------------------------------------------------------
// This file is based on https://github.com/Suchiman/SerilogAnalyzer/blob/bf62860f502db19bc45fd0f46541f383ef3a4455/SerilogAnalyzer/SerilogAnalyzer/SerilogAnalyzerPascalCaseCodeFixProvider.cs
//------------------------------------------------------------------------------

// Copyright 2013-2015 Serilog Contributors
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

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Datadog.Trace.Tools.Analyzers.LogAnalyzer;

/// <summary>
/// Fixes errors produced due to incorrect pascal case
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(PascalCaseCodeFixProvider))]
public class PascalCaseCodeFixProvider : CodeFixProvider
{
    private const char StringificationPrefix = '$';
    private const char DestructuringPrefix = '@';
    private const string Title = "Pascal case the property";

    /// <inheritdoc />
    public sealed override ImmutableArray<string> FixableDiagnosticIds
        => ImmutableArray.Create(Diagnostics.PascalPropertyNameDiagnosticId);

    /// <inheritdoc />
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null || context.Diagnostics.IsDefaultOrEmpty)
        {
            return;
        }

        var diagnostic = context.Diagnostics[0];
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var declaration = root.FindNode(diagnosticSpan);

        context.RegisterCodeFix(
            CodeAction.Create(
                title: Title,
                createChangedSolution: c => PascalCaseTheProperties(context.Document, declaration.DescendantNodesAndSelf().OfType<LiteralExpressionSyntax>().First(), c),
                equivalenceKey: Title),
            diagnostic);
    }

    /// <inheritdoc />
    public override FixAllProvider? GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer; // might work, needs testing!

    private static async Task<Solution> PascalCaseTheProperties(Document document, LiteralExpressionSyntax node, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is not null)
        {
            var oldToken = node.Token;

            var sb = new StringBuilder();
            if (oldToken.Text.StartsWith("@", StringComparison.Ordinal))
            {
                sb.Append('@');
            }

            sb.Append('"');

            var interpolatedString = (InterpolatedStringExpressionSyntax)SyntaxFactory.ParseExpression("$" + oldToken.ToString());
            foreach (var child in interpolatedString.Contents)
            {
                switch (child)
                {
                    case InterpolatedStringTextSyntax text:
                        sb.Append(text.TextToken.ToString());
                        break;
                    case InterpolationSyntax interpolation:
                        AppendAsPascalCase(sb, interpolation.ToString());
                        break;
                }
            }

            sb.Append('"');

            var newToken = SyntaxFactory.ParseToken(sb.ToString());
            root = root.ReplaceToken(oldToken, newToken);

            document = document.WithSyntaxRoot(root);
        }

        return document.Project.Solution;
    }

    private static void AppendAsPascalCase(StringBuilder sb, string input)
    {
        bool uppercaseChar = true;
        bool skipTheRest = false;
        for (int i = 0; i < input.Length; i++)
        {
            char current = input[i];
            if ((i < 2 && current == '{') || current == StringificationPrefix || current == DestructuringPrefix)
            {
                sb.Append(current);
                continue;
            }
            else if (skipTheRest || current == ',' || current == ':' || current == '}')
            {
                skipTheRest = true;
                sb.Append(current);
                continue;
            }
            else if (current == '_')
            {
                uppercaseChar = true;
                continue;
            }

            sb.Append(uppercaseChar ? char.ToUpper(current) : current);
            uppercaseChar = false;
        }
    }
}
