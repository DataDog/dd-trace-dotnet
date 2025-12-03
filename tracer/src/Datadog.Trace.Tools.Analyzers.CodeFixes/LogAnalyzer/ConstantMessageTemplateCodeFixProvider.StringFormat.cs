// <copyright file="ConstantMessageTemplateCodeFixProvider.StringFormat.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

//------------------------------------------------------------------------------
// This file is based on https://github.com/Suchiman/SerilogAnalyzer/blob/bf62860f502db19bc45fd0f46541f383ef3a4455/SerilogAnalyzer/SerilogAnalyzer/ConvertToMessageTemplateCodeRefactoringProvider.StringFormat.cs
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
    private static void GetFormatStringAndExpressionsFromStringFormat(InvocationExpressionSyntax stringFormat, out InterpolatedStringExpressionSyntax format, out List<ExpressionSyntax> expressions)
    {
        var arguments = stringFormat.ArgumentList.Arguments;
        var formatString = ((LiteralExpressionSyntax)arguments[0].Expression).Token.ToString();
        var interpolatedString = (InterpolatedStringExpressionSyntax)SyntaxFactory.ParseExpression("$" + formatString);

        var sb = new StringBuilder();
        var replacements = new List<string>();
        foreach (var child in interpolatedString.Contents)
        {
            switch (child)
            {
                case InterpolatedStringTextSyntax text:
                    sb.Append(text.TextToken.ToString());
                    break;
                case InterpolationSyntax interpolation:
                    int argumentPosition;
                    if (interpolation.Expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.NumericLiteralExpression))
                    {
                        argumentPosition = (int)literal.Token.Value;
                    }
                    else
                    {
                        argumentPosition = -1;
                    }

                    sb.Append("{");
                    sb.Append(replacements.Count);
                    sb.Append("}");

                    replacements.Add($"{{{ConversionName}{argumentPosition}{interpolation.AlignmentClause}{interpolation.FormatClause}}}");

                    break;
            }
        }

        format = (InterpolatedStringExpressionSyntax)SyntaxFactory.ParseExpression("$\"" + string.Format(sb.ToString(), replacements.ToArray()) + "\"");
        expressions = arguments.Skip(1).Select(x => x.Expression).ToList();
    }

    private async Task<Document> ConvertStringFormatToMessageTemplateAsync(Document document, InvocationExpressionSyntax stringFormat, InvocationExpressionSyntax logger, CancellationToken cancellationToken)
    {
        GetFormatStringAndExpressionsFromStringFormat(stringFormat, out var format, out var expressions);

        return await InlineFormatAndArgumentsIntoLoggerStatementAsync(document, stringFormat, logger, format, expressions, cancellationToken).ConfigureAwait(false);
    }
}
