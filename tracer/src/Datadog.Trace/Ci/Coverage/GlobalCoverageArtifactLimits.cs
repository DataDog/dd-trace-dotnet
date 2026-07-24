// <copyright file="GlobalCoverageArtifactLimits.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

namespace Datadog.Trace.Ci.Coverage;

internal sealed class GlobalCoverageArtifactLimits
{
    public static readonly GlobalCoverageArtifactLimits Default = new(
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

    public GlobalCoverageArtifactLimits(
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

    public long MaximumSerializedBytes { get; }

    public int MaximumBitmapBytes { get; }

    public long MaximumModelBitmapBytes { get; }

    public int MaximumComponents { get; }

    public int MaximumEntries { get; }

    public int MaximumIdentityCharacters { get; }

    public int MaximumPropertyCharacters { get; }

    public int MaximumScalarCharacters { get; }

    public int MaximumDepth { get; }

    public int ScannerBufferCharacters { get; }
}
