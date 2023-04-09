// <copyright file="StringHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;

namespace Datadog.Trace.Tools.Analyzers.LogAnalyzer.Helpers;

/// <summary>
/// Helper class for finding the "escaped" position in a string literal
/// </summary>
public class StringHelper
{
    /// <summary>
    /// Remaps a string position into the position in a string literal
    /// </summary>
    /// <param name="literal">The literal string as string</param>
    /// <param name="unescapedPosition">The position in the non literal string</param>
    /// <returns>The position in the literal</returns>
    public static int GetPositionInLiteral(string literal, int unescapedPosition)
    {
        if (literal[0] == '@')
        {
            for (var i = 2; i < literal.Length; i++)
            {
                var c = literal[i];

                // unescapedPosition != 0 is a hack added which wasn't in the original
                if (c == '"' && unescapedPosition != 0 && i + 1 < literal.Length && literal[i + 1] == '"')
                {
                    i++;
                }

                unescapedPosition--;

                if (unescapedPosition == -1)
                {
                    return i;
                }
            }
        }
        else
        {
            for (var i = 1; i < literal.Length; i++)
            {
                var c = literal[i];

                // unescapedPosition != 0 is a hack added which wasn't in the original
                if (c == '\\' && unescapedPosition != 0 && i + 1 < literal.Length)
                {
                    c = literal[++i];
                    if (c == 'x' || c == 'u' || c == 'U')
                    {
                        var max = Math.Min((c == 'U' ? 8 : 4) + i + 1, literal.Length);
                        for (i++; i < max; i++)
                        {
                            c = literal[i];
                            if (!IsHexDigit(c))
                            {
                                break;
                            }
                        }

                        i--;
                    }
                }

                unescapedPosition--;

                if (unescapedPosition == -1)
                {
                    return i;
                }
            }
        }

        return unescapedPosition;
    }

    private static bool IsHexDigit(char c)
        => c is >= '0' and <= '9'
               or >= 'A' and <= 'F'
               or >= 'a' and <= 'f';
}
