// <copyright file="CoverageInfo.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.Ci.Coverage.Models.Global;

internal abstract class CoverageInfo
{
    private double[]? _data;

    [JsonProperty("data")]
    public double[] Data
    {
        get
        {
            if (_data is null)
            {
                RefreshData();
            }

            return _data!;
        }
    }

    protected void RefreshData()
    {
        ClearData();

        double total = 0L;
        double executed = 0L;

        if (this is FileCoverageInfo fCovInfo)
        {
            fCovInfo.IncrementCounts(ref total, ref executed);
        }
        else if (this is ComponentCoverageInfo { Files.Count: > 0 } cCovInfo)
        {
            foreach (var file in cCovInfo.Files)
            {
                var data = file.Data;
                total += data[1];
                executed += data[2];
            }
        }
        else if (this is GlobalCoverageInfo { Components.Count: > 0 } gCovInfo)
        {
            foreach (var component in gCovInfo.Components)
            {
                var data = component.Data;
                total += data[1];
                executed += data[2];
            }
        }

        _data = [Math.Round((executed / total) * 100, 2).ToValidPercentage(), total, executed];
    }

    protected void ClearData()
    {
        _data = null;

        if (this is ComponentCoverageInfo { Files.Count: > 0 } cCovInfo)
        {
            foreach (var file in cCovInfo.Files)
            {
                file.ClearData();
            }
        }
        else if (this is GlobalCoverageInfo { Components.Count: > 0 } gCovInfo)
        {
            foreach (var component in gCovInfo.Components)
            {
                component.ClearData();
            }
        }
    }

    public double GetTotalPercentage()
    {
        return Data[0].ToValidPercentage();
    }
}
