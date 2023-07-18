// <copyright file="SymbolsExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Vendors.dnlib.DotNet.Pdb.Symbols;

namespace Datadog.Trace.Debugger.Symbols;

internal static class SymbolsExtensions
{
    public static bool IsHidden(this SymbolSequencePoint sq)
    {
        return sq is { Line: 0xFEEFEE, EndLine: 0xFEEFEE };
    }
}
