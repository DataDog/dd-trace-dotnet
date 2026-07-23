// <copyright file="CoverageContextAdmission.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Ci.Coverage;

internal abstract class CoverageContextAdmission
{
    public static readonly CoverageContextAdmission Noop = new NoopCoverageContextAdmission();

    public abstract void CommitInstalled();

    public abstract void FailStart(GlobalCoverageFailureReason reason);

    public abstract void Release();

    private sealed class NoopCoverageContextAdmission : CoverageContextAdmission
    {
        public override void CommitInstalled()
        {
        }

        public override void FailStart(GlobalCoverageFailureReason reason)
        {
        }

        public override void Release()
        {
        }
    }
}
