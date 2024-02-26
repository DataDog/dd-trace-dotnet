// <copyright file="FileValue.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.Runtime.CompilerServices;

namespace Datadog.Trace.Ci.Coverage;

internal readonly struct FileValue
{
    public readonly int[]? Lines;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public FileValue(int maxExecutableLine)
    {
        Lines = maxExecutableLine == 0 ? [] : new int[maxExecutableLine];
    }
}
