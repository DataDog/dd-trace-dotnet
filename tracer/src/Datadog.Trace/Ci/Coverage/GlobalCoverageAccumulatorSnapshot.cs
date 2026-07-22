// <copyright file="GlobalCoverageAccumulatorSnapshot.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Ci.Coverage;

internal readonly struct GlobalCoverageAccumulatorSnapshot
{
    internal GlobalCoverageAccumulatorSnapshot(long generationId, int retainedBitmapBytes, int moduleCount, int fileSlotCount, long acceptedContextCount, bool isValid, GlobalCoverageFailureReason failureReason)
    {
        GenerationId = generationId;
        RetainedBitmapBytes = retainedBitmapBytes;
        ModuleCount = moduleCount;
        FileSlotCount = fileSlotCount;
        AcceptedContextCount = acceptedContextCount;
        IsValid = isValid;
        FailureReason = failureReason;
    }

    internal long GenerationId { get; }

    internal int RetainedBitmapBytes { get; }

    internal int ModuleCount { get; }

    internal int FileSlotCount { get; }

    internal long AcceptedContextCount { get; }

    internal bool IsValid { get; }

    internal GlobalCoverageFailureReason FailureReason { get; }
}
