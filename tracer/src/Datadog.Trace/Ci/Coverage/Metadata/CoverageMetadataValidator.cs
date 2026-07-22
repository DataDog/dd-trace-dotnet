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

        int rawByteLength;
        try
        {
            rawByteLength = checked((int)((long)metadata.TotalLines * (metadata.CoverageMode == 0 ? sizeof(byte) : sizeof(int))));
        }
        catch (OverflowException ex)
        {
            throw new InvalidOperationException("Coverage metadata requires a raw counter buffer larger than the supported size.", ex);
        }

        foreach (var file in metadata.Files)
        {
            if (file.Offset < 0 || file.LastExecutableLine < 0)
            {
                throw new InvalidOperationException("Coverage metadata contains a negative file offset or line count.");
            }

            int end;
            int expectedBitmapLength;
            try
            {
                end = checked(file.Offset + file.LastExecutableLine);
                expectedBitmapLength = checked((int)(((long)file.LastExecutableLine + 7) / 8));
            }
            catch (OverflowException ex)
            {
                throw new InvalidOperationException("Coverage metadata contains an overflowing file range.", ex);
            }

            if (end > metadata.TotalLines)
            {
                throw new InvalidOperationException("Coverage metadata contains a file range outside the module counter buffer.");
            }

            if (file.Bitmap is null || file.Bitmap.Length != expectedBitmapLength)
            {
                throw new InvalidOperationException("Coverage metadata contains an executable bitmap with an invalid length.");
            }
        }

        return rawByteLength;
    }
}
