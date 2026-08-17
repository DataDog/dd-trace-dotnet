// <copyright file="ModuleCoverageData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Reflection;
using Datadog.Trace.Ci.Coverage.Metadata;
using Datadog.Trace.Ci.Coverage.Util;

namespace Datadog.Trace.Ci.Coverage;

/// <summary>
/// Contains the compact executed-line bitmaps captured from one module buffer.
/// </summary>
internal readonly struct ModuleCoverageData
{
    public ModuleCoverageData(Module module, ModuleCoverageMetadata metadata, byte[]?[] executedBitmaps)
    {
        Module = module;
        Metadata = metadata;
        ExecutedBitmaps = executedBitmaps;
    }

    public Module Module { get; }

    public ModuleCoverageMetadata Metadata { get; }

    public byte[]?[] ExecutedBitmaps { get; }

    /// <summary>
    /// Scans the native counters once, outside the accumulator lock, and retains only executed bits.
    /// Counter values are intentionally read without synchronization because coverage only depends on
    /// whether a counter is non-zero.
    /// </summary>
    public static unsafe ModuleCoverageData Capture(ModuleValue moduleValue)
    {
        var metadata = moduleValue.Metadata;
        var expectedRawByteLength = CoverageMetadataValidator.ValidateAndGetRawByteLength(metadata);
        if (moduleValue.AllocatedByteLength != expectedRawByteLength)
        {
            throw new GlobalCoverageMetadataException("A coverage buffer length does not match its metadata.");
        }

        var rawPointer = moduleValue.FilesLines;
        if (rawPointer == IntPtr.Zero)
        {
            throw new GlobalCoverageMetadataException("A coverage buffer was disposed before aggregation.");
        }

        var executedBitmaps = new byte[]?[metadata.Files.Length];
        for (var fileIndex = 0; fileIndex < metadata.Files.Length; fileIndex++)
        {
            var file = metadata.Files[fileIndex];
            byte[]? executedBitmap = null;
            if (metadata.CoverageMode == 0)
            {
                var counters = (byte*)rawPointer + file.Offset;
                for (var lineIndex = 0; lineIndex < file.LastExecutableLine; lineIndex++)
                {
                    if (counters[lineIndex] != 0)
                    {
                        executedBitmap ??= new byte[FileBitmap.GetSize(file.LastExecutableLine)];
                        SetBit(executedBitmap, lineIndex);
                    }
                }
            }
            else
            {
                var counters = (int*)rawPointer + file.Offset;
                for (var lineIndex = 0; lineIndex < file.LastExecutableLine; lineIndex++)
                {
                    if (counters[lineIndex] != 0)
                    {
                        executedBitmap ??= new byte[FileBitmap.GetSize(file.LastExecutableLine)];
                        SetBit(executedBitmap, lineIndex);
                    }
                }
            }

            executedBitmaps[fileIndex] = executedBitmap;
        }

        return new ModuleCoverageData(moduleValue.Module, metadata, executedBitmaps);
    }

    private static void SetBit(byte[] bitmap, int zeroBasedLine)
    {
        var byteIndex = zeroBasedLine >> 3;
        bitmap[byteIndex] |= (byte)(128 >> (zeroBasedLine & 7));
    }
}
