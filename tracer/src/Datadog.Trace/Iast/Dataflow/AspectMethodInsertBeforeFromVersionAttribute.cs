// <copyright file="AspectMethodInsertBeforeFromVersionAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Iast.Dataflow;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal sealed class AspectMethodInsertBeforeFromVersionAttribute : AspectMethodInsertBeforeAttribute
{
    public AspectMethodInsertBeforeFromVersionAttribute(string version, string targetMethod, params int[] paramShift)
        : base(targetMethod, paramShift)
    {
    }

    public AspectMethodInsertBeforeFromVersionAttribute(string version, string targetMethod, int[] paramShift, bool[] boxParam)
        : base(targetMethod, paramShift, boxParam)
    {
    }
}
