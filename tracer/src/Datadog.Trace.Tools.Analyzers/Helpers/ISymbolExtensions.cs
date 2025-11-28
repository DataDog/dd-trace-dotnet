// <copyright file="ISymbolExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

#nullable disable warnings

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Datadog.Trace.Tools.Analyzers.Helpers;

internal static class ISymbolExtensions
{
    /// <summary>
    /// True if the symbol is externally visible outside this assembly.
    /// </summary>
    public static bool IsExternallyVisible(this ISymbol symbol) =>
        symbol.GetResultantVisibility() == SymbolVisibility.Public;

    public static SymbolVisibility GetResultantVisibility(this ISymbol symbol)
    {
        // Start by assuming it's visible.
        SymbolVisibility visibility = SymbolVisibility.Public;

        switch (symbol.Kind)
        {
            case SymbolKind.Alias:
                // Aliases are uber private.  They're only visible in the same file that they
                // were declared in.
                return SymbolVisibility.Private;

            case SymbolKind.Parameter:
                // Parameters are only as visible as their containing symbol
                return GetResultantVisibility(symbol.ContainingSymbol);

            case SymbolKind.TypeParameter:
                // Type Parameters are private.
                return SymbolVisibility.Private;
        }

        while (symbol != null && symbol.Kind != SymbolKind.Namespace)
        {
            switch (symbol.DeclaredAccessibility)
            {
                // If we see anything private, then the symbol is private.
                case Accessibility.NotApplicable:
                case Accessibility.Private:
                    return SymbolVisibility.Private;

                // If we see anything internal, then knock it down from public to
                // internal.
                case Accessibility.Internal:
                case Accessibility.ProtectedAndInternal:
                    visibility = SymbolVisibility.Internal;
                    break;

                // For anything else (Public, Protected, ProtectedOrInternal), the
                // symbol stays at the level we've gotten so far.
            }

            symbol = symbol.ContainingSymbol;
        }

        return visibility;
    }

    public static AttributeData? GetAttribute(this ISymbol symbol, [NotNullWhen(true)] INamedTypeSymbol? attributeType)
    {
        return symbol.GetAttributes(attributeType).FirstOrDefault();
    }

    public static IEnumerable<AttributeData> GetAttributes(this ISymbol symbol, IEnumerable<INamedTypeSymbol?> attributesToMatch)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass == null)
            {
                continue;
            }

            foreach (var attributeToMatch in attributesToMatch)
            {
                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeToMatch))
                {
                    yield return attribute;
                    break;
                }
            }
        }
    }

    public static IEnumerable<AttributeData> GetAttributes(this ISymbol symbol, params INamedTypeSymbol?[] attributeTypesToMatch)
    {
        return symbol.GetAttributes(attributesToMatch: attributeTypesToMatch);
    }

    public static IEnumerable<AttributeData> GetAttributes(this ISymbol symbol, INamedTypeSymbol? attributeTypeToMatch1)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeTypeToMatch1))
            {
                yield return attribute;
            }
        }
    }

    public static IEnumerable<AttributeData> GetAttributes(this ISymbol symbol, INamedTypeSymbol? attributeTypeToMatch1, INamedTypeSymbol? attributeTypeToMatch2)
    {
        foreach (var attribute in symbol.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeTypeToMatch1) ||
                SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeTypeToMatch2))
            {
                yield return attribute;
            }
        }
    }

    public static bool HasAnyAttribute(this ISymbol symbol, params INamedTypeSymbol?[] attributeTypesToMatch)
    {
        return symbol.GetAttributes(attributeTypesToMatch).Any();
    }

    /// <summary>
    /// Check if the given <paramref name="methodSymbol"/> is an implicitly generated method for top level statements.
    /// </summary>
    public static bool IsTopLevelStatementsEntryPointMethod([NotNullWhen(true)] this IMethodSymbol? methodSymbol)
        => methodSymbol?.IsStatic == true && methodSymbol.Name switch
        {
            "$Main" => true,
            "<Main>$" => true,
            _ => false
        };

    /// <summary>
    /// Check if the given <paramref name="typeSymbol"/> is an implicitly generated type for top level statements.
    /// </summary>
    public static bool IsTopLevelStatementsEntryPointType([NotNullWhen(true)] this INamedTypeSymbol? typeSymbol)
        => typeSymbol is not null &&
           typeSymbol.GetMembers().OfType<IMethodSymbol>().Any(m => m.IsTopLevelStatementsEntryPointMethod());
}
