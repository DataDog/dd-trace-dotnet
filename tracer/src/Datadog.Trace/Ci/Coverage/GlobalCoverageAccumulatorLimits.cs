// <copyright file="GlobalCoverageAccumulatorLimits.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Ci.Coverage;

internal sealed class GlobalCoverageAccumulatorLimits
{
    internal static readonly GlobalCoverageAccumulatorLimits Default = new(8 * 1024 * 1024, 64 * 1024 * 1024, 10_000, 100_000);

    internal GlobalCoverageAccumulatorLimits(int maximumSingleBitmapBytes, int maximumBitmapBytesPerGeneration, int maximumModules, int maximumFileSlots)
    {
        MaximumSingleBitmapBytes = maximumSingleBitmapBytes;
        MaximumBitmapBytesPerGeneration = maximumBitmapBytesPerGeneration;
        MaximumModules = maximumModules;
        MaximumFileSlots = maximumFileSlots;
    }

    internal int MaximumSingleBitmapBytes { get; }

    internal int MaximumBitmapBytesPerGeneration { get; }

    internal int MaximumModules { get; }

    internal int MaximumFileSlots { get; }
}
