// <copyright file="GlobalCoverageAccumulatorLimits.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Ci.Coverage;

internal sealed class GlobalCoverageAccumulatorLimits
{
    public static readonly GlobalCoverageAccumulatorLimits Default = new(8 * 1024 * 1024, 64 * 1024 * 1024, 10_000, 100_000);

    public GlobalCoverageAccumulatorLimits(int maximumSingleBitmapBytes, int maximumRetainedBitmapBytes, int maximumModules, int maximumFileSlots)
    {
        MaximumSingleBitmapBytes = maximumSingleBitmapBytes;
        MaximumRetainedBitmapBytes = maximumRetainedBitmapBytes;
        MaximumModules = maximumModules;
        MaximumFileSlots = maximumFileSlots;
    }

    public int MaximumSingleBitmapBytes { get; }

    public int MaximumRetainedBitmapBytes { get; }

    public int MaximumModules { get; }

    public int MaximumFileSlots { get; }
}
