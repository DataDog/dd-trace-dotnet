// <copyright file="ImmutableArrayExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Immutable;

namespace Datadog.Trace.SourceGenerators.Helpers;

internal static class ImmutableArrayExtensions
{
    public static bool HasValues<T>(this ImmutableArray<T> array) => !array.IsDefaultOrEmpty;
}
