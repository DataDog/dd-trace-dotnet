// <copyright file="WindowsPipesConfig.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Datadog.Trace.TestHelpers
{
    public class WindowsPipesConfig
    {
        public WindowsPipesConfig(string traces, string metrics)
        {
            Traces = traces;
            Metrics = metrics;
        }

        public string Traces { get; }

        public string Metrics { get; }
    }
}
