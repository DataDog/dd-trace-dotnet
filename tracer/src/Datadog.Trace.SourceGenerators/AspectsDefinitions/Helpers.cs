// <copyright file="Helpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Datadog.Trace.SourceGenerators.AspectsDefinitions
{
    internal static class Helpers
    {
        public static object? ToArray<TSource>(this IEnumerable<TSource> source, Type? elementType)
        {
            if (elementType == null) { return null; }
#pragma warning disable CS8604 // Possible null reference argument.
            var array = elementType.IsEnum ? source.Select(o => Enum.ToObject(elementType, o)).Cast<TSource>().ToArray() : source.ToArray();
#pragma warning restore CS8604 // Possible null reference argument.
            Array destinationArray = Array.CreateInstance(elementType, array.Length);
            Array.Copy(array, destinationArray, array.Length);
            return destinationArray;
        }
    }
}
