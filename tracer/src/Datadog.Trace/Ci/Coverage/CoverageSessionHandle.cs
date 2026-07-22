// <copyright file="CoverageSessionHandle.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Ci.Coverage;

internal sealed class CoverageSessionHandle
{
    internal static readonly CoverageSessionHandle Invalid = new();

    private CoverageSessionHandle()
    {
        Owner = null;
        Context = null;
        Admission = CoverageContextAdmission.Noop;
    }

    internal CoverageSessionHandle(CoverageEventHandler owner, CoverageContextContainer context, CoverageContextAdmission admission)
    {
        Owner = owner;
        Context = context;
        Admission = admission;
    }

    internal CoverageEventHandler? Owner { get; }

    internal CoverageContextContainer? Context { get; }

    internal CoverageContextAdmission Admission { get; }

    internal bool IsValid => Owner is not null && Context is not null;

    internal void AbortIncomplete(GlobalCoverageFailureReason reason)
    {
        Owner?.AbortSession(this, reason);
    }
}
