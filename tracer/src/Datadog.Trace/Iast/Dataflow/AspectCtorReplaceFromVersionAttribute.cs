// <copyright file="AspectCtorReplaceFromVersionAttribute.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;

namespace Datadog.Trace.Iast.Dataflow;

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal class AspectCtorReplaceFromVersionAttribute : AspectCtorReplaceAttribute
{
    public AspectCtorReplaceFromVersionAttribute(string version, string targetMethod)
        : base(targetMethod)
    {
    }

    public AspectCtorReplaceFromVersionAttribute(string version, string targetMethod, params AspectFilter[] filters)
        : base(targetMethod, filters)
    {
    }
}
