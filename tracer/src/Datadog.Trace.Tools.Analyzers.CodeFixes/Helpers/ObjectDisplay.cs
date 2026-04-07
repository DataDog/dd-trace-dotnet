// <copyright file="ObjectDisplay.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

// This is essentially a vendoring of the ObjectDisplay helper (seeing as we can't reference the library directly

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace Datadog.Trace.Tools.Analyzers.Helpers;

#pragma warning disable CA1200 // Avoid using cref tags with a prefix
/// <summary>
/// Displays a value in the C# style.
/// </summary>
/// <remarks>
/// Separate from <see cref="T:Microsoft.CodeAnalysis.CSharp.SymbolDisplay"/> because we want to link this functionality into
/// the Formatter project and we don't want it to be public there.
/// </remarks>
/// <seealso cref="T:Microsoft.CodeAnalysis.VisualBasic.ObjectDisplay.ObjectDisplay"/>
#pragma warning restore CA1200 // Avoid using cref tags with a prefix
internal static class ObjectDisplay
{
    /// <summary>
    /// Returns a C# string literal with the given value.
    /// </summary>
    public static string FormatLiteral(string value, bool useQuotes, bool escapeNonPrintable)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        const char quote = '"';

        var builder = new StringBuilder();

        var isVerbatim = useQuotes && !escapeNonPrintable && ContainsNewLine(value);

        if (useQuotes)
        {
            if (isVerbatim)
            {
                builder.Append('@');
            }

            builder.Append(quote);
        }

        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (escapeNonPrintable && CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.Surrogate)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(value, i);
                if (category == UnicodeCategory.Surrogate)
                {
                    // an unpaired surrogate
                    builder.Append("\\u" + ((int)c).ToString("x4"));
                }
                else if (NeedsEscaping(category))
                {
                    // a surrogate pair that needs to be escaped
                    var unicode = char.ConvertToUtf32(value, i);
                    builder.Append("\\U" + unicode.ToString("x8"));
                    i++; // skip the already-encoded second surrogate of the pair
                }
                else
                {
                    // copy a printable surrogate pair directly
                    builder.Append(c);
                    builder.Append(value[++i]);
                }
            }
            else if (escapeNonPrintable && TryReplaceChar(c, out var replaceWith))
            {
                builder.Append(replaceWith);
            }
            else if (useQuotes && c == quote)
            {
                if (isVerbatim)
                {
                    builder.Append(quote);
                    builder.Append(quote);
                }
                else
                {
                    builder.Append('\\');
                    builder.Append(quote);
                }
            }
            else
            {
                builder.Append(c);
            }
        }

        if (useQuotes)
        {
            builder.Append(quote);
        }

        return builder.ToString();
    }

    private static bool ContainsNewLine(string s)
    {
        foreach (char c in s)
        {
            if (SyntaxFacts.IsNewLine(c))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns true if the character should be replaced and sets
    /// <paramref name="replaceWith"/> to the replacement text.
    /// </summary>
    private static bool TryReplaceChar(char c, [NotNullWhen(returnValue: true)] out string? replaceWith)
    {
        replaceWith = null;
        switch (c)
        {
            case '\\':
                replaceWith = "\\\\";
                break;
            case '\0':
                replaceWith = "\\0";
                break;
            case '\a':
                replaceWith = "\\a";
                break;
            case '\b':
                replaceWith = "\\b";
                break;
            case '\f':
                replaceWith = "\\f";
                break;
            case '\n':
                replaceWith = "\\n";
                break;
            case '\r':
                replaceWith = "\\r";
                break;
            case '\t':
                replaceWith = "\\t";
                break;
            case '\v':
                replaceWith = "\\v";
                break;
        }

        if (replaceWith != null)
        {
            return true;
        }

        if (NeedsEscaping(CharUnicodeInfo.GetUnicodeCategory(c)))
        {
            replaceWith = "\\u" + ((int)c).ToString("x4");
            return true;
        }

        return false;
    }

    private static bool NeedsEscaping(UnicodeCategory category)
    {
        switch (category)
        {
            case UnicodeCategory.Control:
            case UnicodeCategory.OtherNotAssigned:
            case UnicodeCategory.ParagraphSeparator:
            case UnicodeCategory.LineSeparator:
            case UnicodeCategory.Surrogate:
                return true;
            default:
                return false;
        }
    }
}
