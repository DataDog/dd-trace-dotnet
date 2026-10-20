// <copyright file="NativeExportChecker.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;

namespace NativeValidation;

internal static class NativeExportChecker
{
    public static IReadOnlyList<string> FindForbiddenExports(
        IEnumerable<string> nmDefinedSymbols,
        IEnumerable<string> forbiddenSymbols)
    {
        var forbidden = new HashSet<string>(forbiddenSymbols, StringComparer.Ordinal);
        var found = new HashSet<string>(StringComparer.Ordinal);

        foreach (var line in nmDefinedSymbols)
        {
            if (TryGetSymbolName(line, out var symbol)
             && forbidden.Contains(symbol))
            {
                found.Add(symbol);
            }
        }

        return found.OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    private static bool TryGetSymbolName(string line, out string symbol)
    {
        symbol = null;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var trimmed = line.Trim();
        var separator = trimmed.LastIndexOf(' ');
        if (separator < 0 || separator == trimmed.Length - 1)
        {
            return false;
        }

        var symbolMetadata = trimmed.Substring(0, separator).TrimEnd();
        var typeSeparator = symbolMetadata.LastIndexOf(' ');
        var symbolType = symbolMetadata.Substring(typeSeparator + 1);
        if (symbolType.Length != 1 || symbolType[0] is 'U' or 'w' or 'v')
        {
            return false;
        }

        symbol = trimmed.Substring(separator + 1);
        var versionSeparator = symbol.IndexOf('@');
        if (versionSeparator >= 0)
        {
            symbol = symbol.Substring(0, versionSeparator);
        }

        return symbol.Length > 0;
    }
}
