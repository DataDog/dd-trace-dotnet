// <copyright file="FileCoverageInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci.Coverage.Models.Global;

internal sealed class FileCoverageInfo : CoverageInfo
{
    public FileCoverageInfo(string? path)
    {
        Path = path;
        Segments = new List<uint[]>();
    }

    [JsonProperty("path")]
    public string? Path { get; set; }

    [JsonProperty("segments")]
    public List<uint[]> Segments { get; set; }

    public static FileCoverageInfo? operator +(FileCoverageInfo? a, FileCoverageInfo? b)
    {
        if (a is null && b is null)
        {
            return null;
        }
        else if (b is null)
        {
            return a;
        }
        else if (a is null)
        {
            return b;
        }
        else if (a.Path == b.Path)
        {
            var fcInfo = new FileCoverageInfo(a.Path);
            var aSegments = a.Segments ?? Enumerable.Empty<uint[]>();
            var bSegments = b.Segments ?? Enumerable.Empty<uint[]>();

            fcInfo.Segments.AddRange(aSegments);

            foreach (var segment in bSegments)
            {
                fcInfo.Add(segment);
            }

            return fcInfo;
        }

        throw new InvalidOperationException("The operation cannot be executed. Instances are incompatibles.");
    }

    public void Add(uint[] segment)
    {
        if (segment?.Length == 5)
        {
            foreach (var eSegment in Segments)
            {
                if (eSegment[0] == segment[0] &&
                    eSegment[1] == segment[1] &&
                    eSegment[2] == segment[2] &&
                    eSegment[3] == segment[3])
                {
                    eSegment[4] += segment[4];
                    return;
                }
            }

            Segments.Add(segment);
        }
    }
}
