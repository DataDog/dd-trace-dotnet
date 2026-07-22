// <copyright file="CoverageContextAdmission.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Ci.Coverage;

internal abstract class CoverageContextAdmission
{
    internal static readonly CoverageContextAdmission Noop = new NoopCoverageContextAdmission();

    internal abstract void CommitInstalled();

    internal abstract void FailStart(GlobalCoverageFailureReason reason);

    internal abstract void Release();

    private sealed class NoopCoverageContextAdmission : CoverageContextAdmission
    {
        internal override void CommitInstalled()
        {
        }

        internal override void FailStart(GlobalCoverageFailureReason reason)
        {
        }

        internal override void Release()
        {
        }
    }
}
