// <copyright file="ConstantMessageTemplateCodeFixProvider.StringInterpolation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

//------------------------------------------------------------------------------
// This file is based on https://github.com/Suchiman/SerilogAnalyzer/blob/bf62860f502db19bc45fd0f46541f383ef3a4455/SerilogAnalyzer/SerilogAnalyzer/ConvertToMessageTemplateCodeRefactoringProvider.StringInterpolation.cs
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Datadog.Trace.Tools.Analyzers.LogAnalyzer;

/// <summary>
/// Converts a non-constant message template into a constant template
/// </summary>
public partial class ConstantMessageTemplateCodeFixProvider
{
    private static void GetFormatStringAndExpressionsFromInterpolation(InterpolatedStringExpressionSyntax interpolatedString, out InterpolatedStringExpressionSyntax format, out List<ExpressionSyntax> expressions)
    {
        var sb = new StringBuilder();
        var replacements = new List<string>();
        var interpolations = new List<ExpressionSyntax>();
        foreach (var child in interpolatedString.Contents)
        {
            switch (child)
            {
                case InterpolatedStringTextSyntax text:
                    sb.Append(text.TextToken.ToString());
                    break;
                case InterpolationSyntax interpolation:
                    int argumentPosition = interpolations.Count;
                    interpolations.Add(interpolation.Expression);

                    sb.Append("{");
                    sb.Append(replacements.Count);
                    sb.Append("}");

                    replacements.Add($"{{{ConversionName}{argumentPosition}{interpolation.AlignmentClause}{interpolation.FormatClause}}}");

                    break;
            }
        }

        format = (InterpolatedStringExpressionSyntax)SyntaxFactory.ParseExpression("$\"" + string.Format(sb.ToString(), replacements.ToArray()) + "\"");
        expressions = interpolations;
    }

    private async Task<Document> ConvertInterpolationToMessageTemplateAsync(Document document, InterpolatedStringExpressionSyntax interpolatedString, InvocationExpressionSyntax logger, CancellationToken cancellationToken)
    {
        GetFormatStringAndExpressionsFromInterpolation(interpolatedString, out var format, out var expressions);

        return await InlineFormatAndArgumentsIntoLoggerStatementAsync(document, interpolatedString, logger, format, expressions, cancellationToken).ConfigureAwait(false);
    }
}
