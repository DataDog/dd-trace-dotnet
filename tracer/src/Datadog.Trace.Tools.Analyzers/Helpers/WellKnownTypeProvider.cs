// <copyright file="WellKnownTypeProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace Datadog.Trace.Tools.Analyzers.Helpers;

/// <summary>
/// Provides and caches well known types in a compilation.
/// </summary>
internal class WellKnownTypeProvider
{
    private static readonly BoundedCacheWithFactory<Compilation, WellKnownTypeProvider> ProviderCache = new();

    /// <summary>
    /// Static cache of full type names (with namespaces) to namespace name parts,
    /// so we can query <see cref="IAssemblySymbol.NamespaceNames"/>.
    /// </summary>
    /// <remarks>
    /// Example: "System.Collections.Generic.List`1" => [ "System", "Collections", "Generic" ]
    ///
    /// https://github.com/dotnet/roslyn/blob/9e786147b8cb884af454db081bb747a5bd36a086/src/Compilers/CSharp/Portable/Symbols/AssemblySymbol.cs#L455
    /// suggests the TypeNames collection can be checked to avoid expensive operations. But realizing TypeNames seems to be
    /// as memory intensive as unnecessary calls GetTypeByMetadataName() in some cases. So we'll go with namespace names.
    /// </remarks>
    private static readonly ConcurrentDictionary<string, ImmutableArray<string>> FullTypeNameToNamespaceNames =
        new(StringComparer.Ordinal);

    /// <summary>
    /// All the referenced assembly symbols.
    /// </summary>
    /// <remarks>
    /// Seems to be less memory intensive than:
    /// foreach (Compilation.Assembly.Modules)
    ///     foreach (Module.ReferencedAssemblySymbols)
    /// </remarks>
    private readonly Lazy<ImmutableArray<IAssemblySymbol>> _referencedAssemblies;

    /// <summary>
    /// Mapping of full name to <see cref="INamedTypeSymbol"/>.
    /// </summary>
    private readonly ConcurrentDictionary<string, INamedTypeSymbol?> _fullNameToTypeMap;

    private WellKnownTypeProvider(Compilation compilation)
    {
        Compilation = compilation;
        _fullNameToTypeMap = new ConcurrentDictionary<string, INamedTypeSymbol?>(StringComparer.Ordinal);
        _referencedAssemblies = new Lazy<ImmutableArray<IAssemblySymbol>>(
            () =>
            {
                return Compilation.Assembly.Modules
                                  .SelectMany(m => m.ReferencedAssemblySymbols)
                                  .Distinct<IAssemblySymbol>(SymbolEqualityComparer.Default)
                                  .ToImmutableArray();
            },
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public Compilation Compilation { get; }

    public static WellKnownTypeProvider GetOrCreate(Compilation compilation)
    {
        return ProviderCache.GetOrCreateValue(compilation, CreateWellKnownTypeProvider);

        // Local functions
        static WellKnownTypeProvider CreateWellKnownTypeProvider(Compilation compilation) => new(compilation);
    }

    /// <summary>
    /// Attempts to get the type by the full type name.
    /// </summary>
    /// <param name="fullTypeName">Namespace + type name, e.g. "System.Exception".</param>
    /// <param name="namedTypeSymbol">Named type symbol, if any.</param>
    /// <returns>True if found in the compilation, false otherwise.</returns>
    public bool TryGetOrCreateTypeByMetadataName(
        string fullTypeName,
        [NotNullWhen(returnValue: true)] out INamedTypeSymbol? namedTypeSymbol)
    {
        if (_fullNameToTypeMap.TryGetValue(fullTypeName, out namedTypeSymbol))
        {
            return namedTypeSymbol is not null;
        }

        return TryGetOrCreateTypeByMetadataNameSlow(fullTypeName, out namedTypeSymbol);
    }

    /// <summary>
    /// Gets a type by its full type name.
    /// </summary>
    /// <param name="fullTypeName">Namespace + type name, e.g. "System.Exception".</param>
    /// <returns>The <see cref="INamedTypeSymbol"/> if found, null otherwise.</returns>
    public INamedTypeSymbol? GetOrCreateTypeByMetadataName(string fullTypeName)
    {
        TryGetOrCreateTypeByMetadataName(fullTypeName, out INamedTypeSymbol? namedTypeSymbol);
        return namedTypeSymbol;
    }

    private static ImmutableArray<string> GetNamespaceNamesFromFullTypeName(string fullTypeName)
    {
        using ArrayBuilder<string> namespaceNamesBuilder = ArrayBuilder<string>.GetInstance();

        int prevStartIndex = 0;
        for (int i = 0; i < fullTypeName.Length; i++)
        {
            if (fullTypeName[i] == '.')
            {
                namespaceNamesBuilder.Add(fullTypeName.Substring(prevStartIndex, i - prevStartIndex));
                prevStartIndex = i + 1;
            }
            else if (!IsIdentifierPartCharacter(fullTypeName[i]))
            {
                break;
            }
        }

        return namespaceNamesBuilder.ToImmutable();
    }

    /// <summary>
    /// Returns true if the Unicode character can be a part of an identifier.
    /// </summary>
    /// <param name="ch">The Unicode character.</param>
    private static bool IsIdentifierPartCharacter(char ch)
    {
        // identifier-part-character:
        //   letter-character
        //   decimal-digit-character
        //   connecting-character
        //   combining-character
        //   formatting-character

#pragma warning disable SA1108 // Block statments should not containe embedded comments
        if (ch < 'a') // '\u0061'
        {
            if (ch < 'A') // '\u0041'
            {
                return ch is >= '0' // '\u0030'
                           and <= '9'; // '\u0039'
            }

            return ch is <= 'Z' // '\u005A'
                       or '_'; // '\u005F'
        }

        if (ch <= 'z') // '\u007A'
        {
            return true;
        }

        if (ch <= '\u007F') // max ASCII
        {
            return false;
        }
#pragma warning restore SA1108
        UnicodeCategory cat = CharUnicodeInfo.GetUnicodeCategory(ch);

        ////return IsLetterChar(cat)
        ////    || IsDecimalDigitChar(cat)
        ////    || IsConnectingChar(cat)
        ////    || IsCombiningChar(cat)
        ////    || IsFormattingChar(cat);

        return cat switch
        {
            // Letter
            UnicodeCategory.UppercaseLetter
                or UnicodeCategory.LowercaseLetter
                or UnicodeCategory.TitlecaseLetter
                or UnicodeCategory.ModifierLetter
                or UnicodeCategory.OtherLetter
                or UnicodeCategory.LetterNumber
                or UnicodeCategory.DecimalDigitNumber
                or UnicodeCategory.ConnectorPunctuation
                or UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.Format => true,
            _ => false,
        };
    }

    private static bool IsSubsetOfCollection<T>(ImmutableArray<T> set1, ICollection<T> set2)
    {
        if (set1.Length > set2.Count)
        {
            return false;
        }

        for (int i = 0; i < set1.Length; i++)
        {
            if (!set2.Contains(set1[i]))
            {
                return false;
            }
        }

        return true;
    }

    private bool TryGetOrCreateTypeByMetadataNameSlow(
        string fullTypeName,
        [NotNullWhen(returnValue: true)] out INamedTypeSymbol? namedTypeSymbol)
    {
        namedTypeSymbol = _fullNameToTypeMap.GetOrAdd(
            fullTypeName,
            fullyQualifiedMetadataName =>
            {
                // Caching null results is intended.

                // sharwell says: Suppose you reference assembly A with public API X.Y, and you reference assembly B with
                // internal API X.Y. Even though you can use X.Y from assembly A, compilation.GetTypeByMetadataName will
                // fail outright because it finds two types with the same name.

                INamedTypeSymbol? type = null;

                ImmutableArray<string> namespaceNames;
                if (string.IsInterned(fullTypeName) != null)
                {
                    namespaceNames = FullTypeNameToNamespaceNames.GetOrAdd(
                        fullTypeName,
                        GetNamespaceNamesFromFullTypeName);
                }
                else
                {
                    namespaceNames = GetNamespaceNamesFromFullTypeName(fullTypeName);
                }

                if (IsSubsetOfCollection(namespaceNames, Compilation.Assembly.NamespaceNames))
                {
                    type = Compilation.Assembly.GetTypeByMetadataName(fullyQualifiedMetadataName);
                }

                if (type is null)
                {
                    foreach (IAssemblySymbol? referencedAssembly in _referencedAssemblies.Value)
                    {
                        if (!IsSubsetOfCollection(namespaceNames, referencedAssembly.NamespaceNames))
                        {
                            continue;
                        }

                        var currentType = referencedAssembly.GetTypeByMetadataName(fullyQualifiedMetadataName);
                        if (currentType is null)
                        {
                            continue;
                        }

                        switch (currentType.GetResultantVisibility())
                        {
                            case SymbolVisibility.Public:
                            case SymbolVisibility.Internal when referencedAssembly.GivesAccessTo(Compilation.Assembly):
                                break;

                            default:
                                continue;
                        }

                        if (type is object)
                        {
                            // Multiple visible types with the same metadata name are present.
                            return null;
                        }

                        type = currentType;
                    }
                }

                return type;
            });

        return namedTypeSymbol != null;
    }
}
