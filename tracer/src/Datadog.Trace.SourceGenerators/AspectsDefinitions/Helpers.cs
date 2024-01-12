// <copyright file="Helpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Iast;
using Datadog.Trace.Iast.Dataflow;

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

        internal static object? CreateAttributeInstance(Type attributeType, object?[] arguments)
        {
            int x = 0;
            if (attributeType == typeof(AspectClassAttribute))
            {
                if (arguments == null || arguments.Length == 0) { return new AspectClassAttribute(); }
                else if (arguments.Length == 1) { return new AspectClassAttribute((string)arguments[x++]!); }
                else if (arguments.Length == 3) { return new AspectClassAttribute((string)arguments[x++]!, (AspectType)arguments[x++]!, (VulnerabilityType[])arguments[x++]!); }
                else if (arguments.Length == 4 && arguments[1]?.GetType() == typeof(AspectFilter)) { return new AspectClassAttribute((string)arguments[x++]!, (AspectFilter)arguments[x++]!, (AspectType)arguments[x++]!, (VulnerabilityType[])arguments[x++]!); }
                else if (arguments.Length == 4) { return new AspectClassAttribute((string)arguments[x++]!, (AspectFilter[])arguments[x++]!, (AspectType)arguments[x++]!, (VulnerabilityType[])arguments[x++]!); }
            }
            else if (attributeType == typeof(AspectMethodReplaceAttribute))
            {
                if (arguments.Length == 1) { return new AspectMethodReplaceAttribute((string)arguments[x++]!); }
                else if (arguments.Length == 2) { return new AspectMethodReplaceAttribute((string)arguments[x++]!, (AspectFilter[])arguments[x++]!); }
                else if (arguments.Length == 3 && arguments[1]?.GetType() == typeof(string)) { return new AspectMethodReplaceAttribute((string)arguments[x++]!, (string)arguments[x++]!, (AspectFilter[])arguments[x++]!); }
                else if (arguments.Length == 3) { return new AspectMethodReplaceAttribute((string)arguments[x++]!, (AspectType)arguments[x++]!, (VulnerabilityType[])arguments[x++]!); }
            }
            else if (attributeType == typeof(AspectCtorReplaceAttribute))
            {
                if (arguments.Length == 1) { return new AspectCtorReplaceAttribute((string)arguments[x++]!); }
                else if (arguments.Length == 2) { return new AspectCtorReplaceAttribute((string)arguments[x++]!, (AspectFilter[])arguments[x++]!); }
                else if (arguments.Length == 3) { return new AspectCtorReplaceAttribute((string)arguments[x++]!, (AspectType)arguments[x++]!, (VulnerabilityType[])arguments[x++]!); }
            }
            else if (attributeType == typeof(AspectMethodInsertAfterAttribute))
            {
                if (arguments.Length == 1) { return new AspectMethodInsertAfterAttribute((string)arguments[x++]!); }
                else if (arguments.Length == 3) { return new AspectMethodInsertAfterAttribute((string)arguments[x++]!, (AspectType)arguments[x++]!, (VulnerabilityType[])arguments[x++]!); }
            }
            else if (attributeType == typeof(AspectMethodInsertBeforeAttribute))
            {
                if (arguments.Length == 2) { return new AspectMethodInsertBeforeAttribute((string)arguments[x++]!, (int[])arguments[x++]!); }
                else if (arguments.Length == 3 && arguments[1]?.GetType() == typeof(int)) { return new AspectMethodInsertBeforeAttribute((string)arguments[x++]!, (int)arguments[x++]!, (bool)arguments[x++]!); }
                else if (arguments.Length == 3) { return new AspectMethodInsertBeforeAttribute((string)arguments[x++]!, (int[])arguments[x++]!, (bool[])arguments[x++]!); }
            }

            throw new ArgumentException($"No constructor helper for {attributeType} and {arguments.Length} arguments", "attributeType");
        }
    }
}
