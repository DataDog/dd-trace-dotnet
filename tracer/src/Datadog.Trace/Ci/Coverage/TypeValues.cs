// <copyright file="TypeValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.Ci.Coverage;

internal class TypeValues
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TypeValues(int maxMethods)
    {
        Methods = maxMethods == 0 ? Array.Empty<MethodValues>() : new MethodValues[maxMethods];
    }

    public MethodValues?[] Methods { get; }
}
