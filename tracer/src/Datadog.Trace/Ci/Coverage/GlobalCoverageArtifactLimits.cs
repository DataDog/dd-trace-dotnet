// <copyright file="GlobalCoverageArtifactLimits.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Ci.Coverage;

internal sealed class GlobalCoverageArtifactLimits
{
    internal static readonly GlobalCoverageArtifactLimits Default = new(
        maximumSerializedBytes: 256L * 1024 * 1024,
        maximumBitmapBytes: 8 * 1024 * 1024,
        maximumModelBitmapBytes: 128L * 1024 * 1024,
        maximumComponents: 10_000,
        maximumEntries: 100_000,
        maximumIdentityCharacters: 32_000_000,
        maximumPropertyCharacters: 1_024,
        maximumScalarCharacters: 16_777_216,
        maximumDepth: 64,
        scannerBufferCharacters: 16 * 1024);

    internal GlobalCoverageArtifactLimits(
        long maximumSerializedBytes,
        int maximumBitmapBytes,
        long maximumModelBitmapBytes,
        int maximumComponents,
        int maximumEntries,
        int maximumIdentityCharacters,
        int maximumPropertyCharacters,
        int maximumScalarCharacters,
        int maximumDepth,
        int scannerBufferCharacters)
    {
        MaximumSerializedBytes = maximumSerializedBytes;
        MaximumBitmapBytes = maximumBitmapBytes;
        MaximumModelBitmapBytes = maximumModelBitmapBytes;
        MaximumComponents = maximumComponents;
        MaximumEntries = maximumEntries;
        MaximumIdentityCharacters = maximumIdentityCharacters;
        MaximumPropertyCharacters = maximumPropertyCharacters;
        MaximumScalarCharacters = maximumScalarCharacters;
        MaximumDepth = maximumDepth;
        ScannerBufferCharacters = scannerBufferCharacters;
    }

    internal long MaximumSerializedBytes { get; }

    internal int MaximumBitmapBytes { get; }

    internal long MaximumModelBitmapBytes { get; }

    internal int MaximumComponents { get; }

    internal int MaximumEntries { get; }

    internal int MaximumIdentityCharacters { get; }

    internal int MaximumPropertyCharacters { get; }

    internal int MaximumScalarCharacters { get; }

    internal int MaximumDepth { get; }

    internal int ScannerBufferCharacters { get; }
}
