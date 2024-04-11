// <copyright file="ContinuationsHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.ClrProfiler.CallTarget.Handlers.Continuations;

internal static class ContinuationsHelper
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Type GetResultType(Type? parentType)
    {
        var currentType = parentType;
        while (currentType != null)
        {
            var typeArguments = currentType.GenericTypeArguments ?? Type.EmptyTypes;
            switch (typeArguments.Length)
            {
                case 0:
                    return typeof(object);
                case 1:
                    return typeArguments[0];
                default:
                    currentType = currentType.BaseType;
                    break;
            }
        }

        return typeof(object);
    }
}
