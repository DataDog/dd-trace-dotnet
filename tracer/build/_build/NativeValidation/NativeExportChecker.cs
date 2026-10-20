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
        IEnumerable<NmSymbol> symbols,
        IEnumerable<string> forbiddenSymbols)
    {
        var forbidden = new HashSet<string>(forbiddenSymbols, StringComparer.Ordinal);
        var found = new HashSet<string>(StringComparer.Ordinal);

        foreach (var symbol in symbols)
        {
            if (!symbol.IsUndefined
             && forbidden.Contains(symbol.Name))
            {
                found.Add(symbol.Name);
            }
        }

        return found.OrderBy(x => x, StringComparer.Ordinal).ToList();
    }
}
