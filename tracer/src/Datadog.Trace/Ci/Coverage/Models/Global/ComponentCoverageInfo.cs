// <copyright file="ComponentCoverageInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci.Coverage.Models.Global;

internal sealed class ComponentCoverageInfo(string? name) : CoverageInfo
{
    [JsonProperty("name")]
    public string? Name { get; set; } = name;

    [JsonProperty("files")]
    public List<FileCoverageInfo> Files { get; } = new();

    public static ComponentCoverageInfo? operator +(ComponentCoverageInfo? a, ComponentCoverageInfo? b)
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

        if (a.Name == b.Name)
        {
            var componentCoverageInfo = new ComponentCoverageInfo(a.Name);

            var aFiles = a.Files ?? Enumerable.Empty<FileCoverageInfo>();
            var bFiles = b.Files ?? Enumerable.Empty<FileCoverageInfo>();
            foreach (var filesGroup in aFiles.Concat(bFiles).GroupBy(f => f.Path))
            {
                var filesGroupArray = filesGroup.ToArray();
                if (filesGroupArray.Length == 1)
                {
                    componentCoverageInfo.Files.Add(filesGroupArray[0]);
                }
                else
                {
                    var res = filesGroupArray[0];
                    for (var i = 1; i < filesGroupArray.Length; i++)
                    {
                        res += filesGroupArray[i];
                    }

                    if (res is not null)
                    {
                        componentCoverageInfo.Files.Add(res);
                    }
                }
            }

            return componentCoverageInfo;
        }

        throw new InvalidOperationException("The operation cannot be executed. Instances are incompatibles.");
    }

    public void Add(FileCoverageInfo fileCoverageInfo)
    {
        if (fileCoverageInfo is null)
        {
            return;
        }

        var previous = Files.SingleOrDefault(m => m.Path == fileCoverageInfo.Path);
        if (previous is not null)
        {
            Files.Remove(previous);
            var res = previous + fileCoverageInfo;
            if (res is not null)
            {
                Files.Add(res);
                ClearData();
            }
        }
        else
        {
            Files.Add(fileCoverageInfo);
            ClearData();
        }
    }
}
