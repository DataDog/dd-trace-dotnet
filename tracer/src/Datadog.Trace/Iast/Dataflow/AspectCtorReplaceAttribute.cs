// <copyright file="AspectCtorReplaceAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;

namespace Datadog.Trace.Iast.Dataflow;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal class AspectCtorReplaceAttribute : AspectAttribute
{
    public AspectCtorReplaceAttribute(string targetMethod)
        : base(targetMethod, string.Empty, new int[0], new bool[0], new AspectFilter[0], AspectType.Default)
    {
    }

    public AspectCtorReplaceAttribute(string targetMethod, params AspectFilter[] filters)
        : base(targetMethod, string.Empty, new int[0], new bool[0], filters, AspectType.Default)
    {
    }
}
