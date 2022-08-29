// <copyright file="MethodValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Runtime.CompilerServices;

namespace Datadog.Trace.Ci.Coverage;

internal class MethodValues
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MethodValues(int maxSequencePoints)
    {
        SequencePoints = maxSequencePoints == 0 ? Array.Empty<int>() : new int[maxSequencePoints];
    }

    public int[] SequencePoints { get; }
}
