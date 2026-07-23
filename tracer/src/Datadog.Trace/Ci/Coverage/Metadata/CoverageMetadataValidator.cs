// <copyright file="CoverageMetadataValidator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;

namespace Datadog.Trace.Ci.Coverage.Metadata;

internal static class CoverageMetadataValidator
{
    internal static int ValidateAndGetRawByteLength(ModuleCoverageMetadata metadata)
    {
        if (metadata.CoverageMode is not 0 and not 1)
        {
            throw new InvalidOperationException($"Unsupported coverage mode '{metadata.CoverageMode}'.");
        }

        if (metadata.TotalLines < 0)
        {
            throw new InvalidOperationException("Coverage metadata contains a negative total line count.");
        }

        var bytesPerLine = metadata.CoverageMode == 0 ? sizeof(byte) : sizeof(int);
        if (metadata.TotalLines > int.MaxValue / bytesPerLine)
        {
            throw new InvalidOperationException("Coverage metadata requires a raw counter buffer larger than the supported size.");
        }

        var rawByteLength = metadata.TotalLines * bytesPerLine;

        foreach (var file in metadata.Files)
        {
            if (file.Offset < 0 || file.LastExecutableLine < 0)
            {
                throw new InvalidOperationException("Coverage metadata contains a negative file offset or line count.");
            }

            var end = (long)file.Offset + file.LastExecutableLine;
            if (end > int.MaxValue)
            {
                throw new InvalidOperationException("Coverage metadata contains an overflowing file range.");
            }

            if (end > metadata.TotalLines)
            {
                throw new InvalidOperationException("Coverage metadata contains a file range outside the module counter buffer.");
            }

            var expectedBitmapLength = ((long)file.LastExecutableLine + 7) / 8;
            if (file.Bitmap is null || file.Bitmap.Length != expectedBitmapLength)
            {
                throw new InvalidOperationException("Coverage metadata contains an executable bitmap with an invalid length.");
            }
        }

        return rawByteLength;
    }
}
