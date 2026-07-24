// <copyright file="GlobalCoverageMarkerRecord.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;

namespace Datadog.Trace.Ci.Coverage;

internal sealed class GlobalCoverageMarkerRecord
{
    public int Version { get; set; }

    public string? Status { get; set; }

    public string? RunToken { get; set; }

    public int ProcessId { get; set; }

    public string? Nonce { get; set; }

    public string? Directory { get; set; }

    public int RequiredMask { get; set; }

    public long CommittedGenerations { get; set; }

    public long Started { get; set; }

    public long Closed { get; set; }

    public long Disposed { get; set; }

    public bool Coordinator { get; set; }

    public List<string> Directories { get; } = new(2);
}
