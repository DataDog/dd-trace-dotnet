// <copyright file="SymbolModel.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;

namespace Datadog.Trace.Debugger.Symbols
{
    internal enum SymbolType
    {
        Assembly,
        Class,
        Method,
        Field,
        Arg,
        Local
    }

    internal record struct SymbolModel
    {
        internal string Service { get; set; }

        internal string Env { get; set; }

        internal string Version { get; set; }

        internal string Language { get; set; }

        internal IReadOnlyList<Scope> Scopes { get; set; }
    }

    internal record struct LanguageSpecifics
    {
        internal IReadOnlyList<string> AccessModifiers { get; set; }

        internal IReadOnlyList<string> Annotations { get; set; }

        internal IReadOnlyList<string> SuperClasses { get; set; }

        internal IReadOnlyList<string> Interfaces { get; set; }

        internal IReadOnlyList<string> ReturnType { get; set; }
    }

    internal record struct Symbol
    {
        internal string Name { get; set; }

        internal string Type { get; set; }

        internal SymbolType SymbolType { get; set; }

        internal int Line { get; set; }
    }

    internal record struct Scope
    {
        internal SymbolType SymbolType { get; set; }

        internal string Name { get; set; }

        internal string Type { get; set; }

        internal string SourceFile { get; set; }

        internal int StartLine { get; set; }

        internal int EndLine { get; set; }

        internal LanguageSpecifics LanguageSpecifics { get; set; }

        internal IReadOnlyList<Symbol> Symbols { get; set; }

        internal IList<Scope> Scopes { get; set; }
    }
}
