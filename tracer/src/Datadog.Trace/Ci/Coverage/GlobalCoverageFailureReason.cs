// <copyright file="GlobalCoverageFailureReason.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Ci.Coverage;

internal enum GlobalCoverageFailureReason
{
    None,
    StartFailed,
    TestConstructionFailed,
    TestCloseBeforeCoverage,
    ProbeDataIncomplete,
    PerTestProcessingFailed,
    MergeFailed,
    SnapshotFailed,
    AggregateLimitExceeded,
    MetadataMismatch,
    OutputCommitFailed,
    ReconciliationAuthorityFailed,
}
