// <copyright file="FileCoverageInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Ci.Coverage.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci.Coverage.Models.Global;

internal sealed class FileCoverageInfo(string? path) : CoverageInfo
{
    [JsonProperty("path")]
    public string? Path { get; set; } = path;

    [JsonProperty("executableBitmap")]
    public byte[]? ExecutableBitmap { get; set; }

    [JsonProperty("executedBitmap")]
    public byte[]? ExecutedBitmap { get; set; }

    public static FileCoverageInfo? operator +(FileCoverageInfo? a, FileCoverageInfo? b)
    {
        if (a is null && b is null)
        {
            return null;
        }

        if (b is null)
        {
            return a;
        }

        if (a is null)
        {
            return b;
        }

        if (a.Path == b.Path)
        {
            var fcInfo = new FileCoverageInfo(a.Path);
            if (a.ExecutableBitmap is { } aExecutableBitmap)
            {
                fcInfo.AggregateExecutableBitmap(aExecutableBitmap);
            }

            if (b.ExecutableBitmap is { } bExecutableBitmap)
            {
                fcInfo.AggregateExecutableBitmap(bExecutableBitmap);
            }

            if (a.ExecutedBitmap is { } aExecutedBitmap)
            {
                fcInfo.AggregateExecutedBitmap(aExecutedBitmap);
            }

            if (b.ExecutedBitmap is { } bExecutedBitmap)
            {
                fcInfo.AggregateExecutedBitmap(bExecutedBitmap);
            }

            return fcInfo;
        }

        throw new InvalidOperationException("The operation cannot be executed. Instances are incompatibles.");
    }

    public void AggregateExecutableBitmap(byte[] bitmapBytes)
    {
        if (ExecutableBitmap is { } currentBitmapBytes)
        {
            using var currentBitmap = new FileBitmap(currentBitmapBytes);
            using var bitmap = new FileBitmap(bitmapBytes);
            using var newBitmap = currentBitmap | bitmap;
            ExecutableBitmap = newBitmap.GetInternalArrayOrToArrayAndDispose();
        }
        else
        {
            ExecutableBitmap = bitmapBytes;
        }
    }

    public void AggregateExecutedBitmap(byte[] bitmapBytes)
    {
        if (ExecutedBitmap is { } currentBitmapBytes)
        {
            using var currentBitmap = new FileBitmap(currentBitmapBytes);
            using var bitmap = new FileBitmap(bitmapBytes);
            using var newBitmap = currentBitmap | bitmap;
            ExecutedBitmap = newBitmap.GetInternalArrayOrToArrayAndDispose();
        }
        else
        {
            ExecutedBitmap = bitmapBytes;
        }
    }

    public void IncrementCounts(ref double total, ref double executed)
    {
        if (ExecutableBitmap is { } executableBitmap)
        {
            using var fileBitmap = new FileBitmap(executableBitmap);
            total += fileBitmap.CountActiveBits();
        }

        if (ExecutedBitmap is { } executedBitmap)
        {
            using var fileBitmap = new FileBitmap(executedBitmap);
            executed += fileBitmap.CountActiveBits();
        }
    }
}
