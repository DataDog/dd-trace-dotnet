// <copyright file="GlobalCoverageMarkerRecord.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Ci.Coverage;

internal sealed class GlobalCoverageMarkerRecord
{
    internal int Version { get; set; }

    internal string? Status { get; set; }

    internal string? RunToken { get; set; }

    internal int ProcessId { get; set; }

    internal string? Nonce { get; set; }

    internal string? Directory { get; set; }

    internal int RequiredMask { get; set; }

    internal long CommittedGenerations { get; set; }

    internal long Started { get; set; }

    internal long Closed { get; set; }

    internal long Disposed { get; set; }

    internal bool Coordinator { get; set; }

    internal List<string> Directories { get; } = new(2);
}
