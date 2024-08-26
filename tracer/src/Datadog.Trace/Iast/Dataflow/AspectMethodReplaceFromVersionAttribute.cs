// <copyright file="AspectMethodReplaceFromVersionAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Iast.Dataflow;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal sealed class AspectMethodReplaceFromVersionAttribute : AspectMethodReplaceAttribute
{
    public AspectMethodReplaceFromVersionAttribute(string version, string targetMethod)
        : base(targetMethod)
    {
    }

    public AspectMethodReplaceFromVersionAttribute(string version, string targetMethod, params AspectFilter[] filters)
        : base(targetMethod, filters)
    {
    }

    public AspectMethodReplaceFromVersionAttribute(string version, string targetMethod, string targetType, params AspectFilter[] filters)
        : base(targetMethod, targetType, filters)
    {
    }

    public AspectMethodReplaceFromVersionAttribute(string version, string targetMethod, int[] paramShift, bool[] boxParam)
       : base(targetMethod, paramShift, boxParam)
    {
    }
}
