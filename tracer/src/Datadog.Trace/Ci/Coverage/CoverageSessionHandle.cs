// <copyright file="CoverageSessionHandle.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Ci.Coverage;

internal sealed class CoverageSessionHandle
{
    public static readonly CoverageSessionHandle Invalid = new();

    private CoverageSessionHandle()
    {
        Owner = null;
        Context = null;
        Admission = CoverageContextAdmission.Noop;
    }

    public CoverageSessionHandle(CoverageEventHandler owner, CoverageContextContainer context, CoverageContextAdmission admission)
    {
        Owner = owner;
        Context = context;
        Admission = admission;
    }

    public CoverageEventHandler? Owner { get; }

    public CoverageContextContainer? Context { get; }

    public CoverageContextAdmission Admission { get; }

    public bool IsValid => Owner is not null && Context is not null;

    public void AbortIncomplete(GlobalCoverageFailureReason reason)
    {
        Owner?.AbortSession(this, reason);
    }
}
