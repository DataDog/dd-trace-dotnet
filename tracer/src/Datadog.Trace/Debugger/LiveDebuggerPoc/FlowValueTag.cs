// <copyright file="FlowValueTag.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Debugger.LiveDebuggerPoc
{
    internal enum FlowValueTag : byte
    {
        Null = 1,
        Boolean = 2,
        Int64 = 3,
        UInt64 = 4,
        Double = 5,
        Decimal = 6,
        String = 7,
        TypeSummary = 8,
        CollectionSummary = 9,
        NotCaptured = 10
    }
}
