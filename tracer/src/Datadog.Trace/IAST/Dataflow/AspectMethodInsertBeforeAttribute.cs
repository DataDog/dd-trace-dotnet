// <copyright file="AspectMethodInsertBeforeAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Iast.Dataflow;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal sealed class AspectMethodInsertBeforeAttribute : AspectAttribute
{
    public AspectMethodInsertBeforeAttribute(string targetMethod, int paramShift = 0, bool boxParam = false)
        : base(targetMethod, string.Empty, paramShift, boxParam)
    {
    }

    public AspectMethodInsertBeforeAttribute(string targetMethod, params int[] paramShift)
        : base(targetMethod, string.Empty, paramShift, new bool[0], new AspectFilter[0])
    {
    }

    public AspectMethodInsertBeforeAttribute(string targetMethod, int[] paramShift, bool[] boxParam)
        : base(targetMethod, string.Empty, paramShift, boxParam, new AspectFilter[0])
    {
    }
}
