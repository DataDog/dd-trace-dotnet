// <copyright file="NmSymbolParser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;

namespace NativeValidation;

internal static class NmSymbolParser
{
    public static IReadOnlyList<NmSymbol> Parse(IEnumerable<string> nmOutput)
    {
        var symbols = new List<NmSymbol>();

        foreach (var line in nmOutput)
        {
            if (TryParse(line, out var symbol))
            {
                symbols.Add(symbol);
            }
        }

        return symbols;
    }

    private static bool TryParse(string line, out NmSymbol symbol)
    {
        symbol = null;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var trimmed = line.Trim();
        var symbolSeparator = FindLastWhitespace(trimmed);
        if (symbolSeparator < 0 || symbolSeparator == trimmed.Length - 1)
        {
            return false;
        }

        var symbolMetadata = trimmed.Substring(0, symbolSeparator).TrimEnd();
        var typeSeparator = FindLastWhitespace(symbolMetadata);
        var typeText = symbolMetadata.Substring(typeSeparator + 1);
        if (typeText.Length != 1)
        {
            return false;
        }

        var versionedName = trimmed.Substring(symbolSeparator + 1).TrimStart();
        if (versionedName.Length == 0)
        {
            return false;
        }

        var versionSeparator = versionedName.IndexOf('@');
        if (versionSeparator < 0)
        {
            symbol = new NmSymbol(typeText[0], versionedName, versionedName, versionName: null);
            return true;
        }

        var versionStart = versionSeparator;
        while (versionStart < versionedName.Length && versionedName[versionStart] == '@')
        {
            versionStart++;
        }

        var name = versionedName.Substring(0, versionSeparator);
        var versionName = versionStart < versionedName.Length
                              ? versionedName.Substring(versionStart)
                              : string.Empty;

        symbol = new NmSymbol(typeText[0], name, versionedName, versionName);
        return true;
    }

    private static int FindLastWhitespace(string value)
    {
        for (var i = value.Length - 1; i >= 0; i--)
        {
            if (char.IsWhiteSpace(value[i]))
            {
                return i;
            }
        }

        return -1;
    }
}
