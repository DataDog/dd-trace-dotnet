// <copyright file="RunSummaryUnsafeStructV3V4.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.XUnit.V3V4;

/// <summary>
/// Mirrors the sequential memory layout of Xunit.v3.RunSummary in xunit.v3 4.x.
/// The framework stores time as integral milliseconds and exposes it as decimal seconds.
/// </summary>
internal struct RunSummaryUnsafeStructV3V4
{
    private long _timeInMilliseconds;

    public int Total;

    public int Failed;

    public int Skipped;

    public int NotRun;

    public decimal Time
    {
        readonly get => _timeInMilliseconds / 1000m;
        set => _timeInMilliseconds = (long)(value * 1000m);
    }
}
