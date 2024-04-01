// <copyright file="IastModuleResponse.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using Datadog.Trace.Vendors.dnlib.IO;

namespace Datadog.Trace.Iast;

internal readonly struct IastModuleResponse
{
    public static readonly IastModuleResponse Empty = new(false);
    public static readonly IastModuleResponse Vulnerable = new(true);

    public IastModuleResponse(bool vulnAdded = true)
    {
        SingleSpan = null;
        VulnerabilityAdded = vulnAdded;
    }

    public IastModuleResponse(Scope? singleSpan)
    {
        SingleSpan = singleSpan;
        VulnerabilityAdded = true;
    }

    public Scope? SingleSpan { get; }

    public bool VulnerabilityAdded { get; }
}
