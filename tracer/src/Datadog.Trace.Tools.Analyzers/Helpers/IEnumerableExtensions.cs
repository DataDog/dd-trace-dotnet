// <copyright file="IEnumerableExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

// Based on https://github.com/dotnet/roslyn-analyzers/blob/4512de66bb6e21c548ab0d5a83242b70969ba576/src/Utilities/Compiler/Extensions/IEnumerableExtensions.cs
// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Analyzer.Utilities.Extensions;

internal static class IEnumerableExtensions
{
    public static ImmutableArray<TSource> WhereAsArray<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> selector)
    {
        var builder = ImmutableArray.CreateBuilder<TSource>();
        bool any = false;
        foreach (var element in source)
        {
            if (selector(element))
            {
                any = true;
                builder.Add(element);
            }
        }

        if (any)
        {
            return builder.ToImmutable();
        }
        else
        {
            return ImmutableArray<TSource>.Empty;
        }
    }
}
