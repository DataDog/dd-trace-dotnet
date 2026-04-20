// <copyright file="GlobalCoverageInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci.Coverage.Models.Global;

internal sealed class GlobalCoverageInfo : CoverageInfo
{
    [JsonProperty("components")]
    public List<ComponentCoverageInfo> Components { get; } = new();

    public static GlobalCoverageInfo? operator +(GlobalCoverageInfo? a, GlobalCoverageInfo? b)
    {
        if (a is null)
        {
            return b;
        }

        if (b is null)
        {
            return a;
        }

        var globalCovInfo = new GlobalCoverageInfo();
        var aComponents = a?.Components ?? Enumerable.Empty<ComponentCoverageInfo>();
        var bComponents = b?.Components ?? Enumerable.Empty<ComponentCoverageInfo>();
        foreach (var componentGroup in aComponents.Concat(bComponents).GroupBy(m => m.Name))
        {
            var componentGroupArray = componentGroup.ToArray();
            if (componentGroupArray.Length == 1)
            {
                globalCovInfo.Components.Add(componentGroupArray[0]);
            }
            else
            {
                var res = componentGroupArray[0];
                for (var i = 1; i < componentGroupArray.Length; i++)
                {
                    res += componentGroupArray[i];
                }

                if (res is not null)
                {
                    globalCovInfo.Components.Add(res);
                }
            }
        }

        return globalCovInfo;
    }

    public static GlobalCoverageInfo? Combine(params GlobalCoverageInfo?[] coverages)
    {
        GlobalCoverageInfo? res = null;
        foreach (var coverage in coverages)
        {
            res += coverage;
        }

        return res;
    }

    public void Add(ComponentCoverageInfo componentCoverageInfo)
    {
        if (componentCoverageInfo is null)
        {
            return;
        }

        var previous = Components.SingleOrDefault(m => m.Name == componentCoverageInfo.Name);
        if (previous is not null)
        {
            Components.Remove(previous);
            var res = previous + componentCoverageInfo;
            if (res is not null)
            {
                Components.Add(res);
                ClearData();
            }
        }
        else
        {
            Components.Add(componentCoverageInfo);
            ClearData();
        }
    }
}
