// <copyright file="FrameworkProvider.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Datadog.RuntimeMetrics
{
    // Get metrics from .NET Framework runtime
    internal class FrameworkProvider : IMetricsProvider
    {
        public IReadOnlyList<(string Name, string Value)> GetMetrics()
        {
            return new List<(string, string)>()
            {
                (MetricsNames.Gen0CollectionsCount, GC.CollectionCount(0).ToString()),
                (MetricsNames.Gen1CollectionsCount, GC.CollectionCount(1).ToString()),
                (MetricsNames.Gen2CollectionsCount, GC.CollectionCount(2).ToString()),
                (MetricsNames.ProcessorTime, Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds.ToString()),
                (MetricsNames.PrivateBytes, Process.GetCurrentProcess().PrivateMemorySize64.ToString()),
            };
        }

        public void Stop()
        {
        }
    }
}
