// <copyright file="EnumerablePolyfills.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NET7_0_OR_GREATER
#nullable enable

using System.Collections.Generic;

// ReSharper disable once CheckNamespace - In System.Linq so the linked Resources.cs finds it with its existing using
namespace System.Linq;

/// <summary>
/// The linked dd-dotnet Checks/Resources.cs uses Enumerable.Order(), which was added in .NET 7. dd-dotnet only
/// targets .NET 10+, but this test project also targets older frameworks.
/// </summary>
internal static class EnumerablePolyfills
{
    public static IOrderedEnumerable<T> Order<T>(this IEnumerable<T> source) => source.OrderBy(static x => x);
}
#endif
