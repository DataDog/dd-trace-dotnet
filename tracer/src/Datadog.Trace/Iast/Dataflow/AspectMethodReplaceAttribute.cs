// <copyright file="AspectMethodReplaceAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Iast.Dataflow;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal class AspectMethodReplaceAttribute : AspectAttribute
{
    public AspectMethodReplaceAttribute(string targetMethod)
        : base(targetMethod, string.Empty, new int[0], new bool[0], new AspectFilter[0], AspectType.Default)
    {
    }

    public AspectMethodReplaceAttribute(string targetMethod, params AspectFilter[] filters)
        : base(targetMethod, string.Empty, new int[0], new bool[0], filters, AspectType.Default)
    {
    }

    public AspectMethodReplaceAttribute(string targetMethod, string targetType, params AspectFilter[] filters)
        : base(targetMethod, targetType, new int[0], new bool[0], filters, AspectType.Default)
    {
    }

    public AspectMethodReplaceAttribute(string targetMethod, int[] paramShift, bool[] boxParam)
       : base(targetMethod, string.Empty, paramShift, boxParam, new AspectFilter[0], AspectType.Default)
    {
    }
}
