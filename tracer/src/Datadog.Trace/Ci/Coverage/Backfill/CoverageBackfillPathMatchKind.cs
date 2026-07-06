// <copyright file="CoverageBackfillPathMatchKind.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Ci.Coverage.Backfill;

/// <summary>
/// Identifies how a local coverage source path matched a backend coverage key.
/// </summary>
internal enum CoverageBackfillPathMatchKind
{
    None,
    ExactOrdinal,
    CaseInsensitiveExact,
    Suffix
}
