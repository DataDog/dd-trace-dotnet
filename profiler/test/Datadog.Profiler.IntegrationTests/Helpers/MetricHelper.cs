// <copyright file="MetricHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    public class MetricHelper
    {
        public static bool GetMetrics(HttpListenerRequest request)
        {
            // The on-wire metrics part is still a binary payload, so only its presence is checked.
            return MultiPartReader.TryParse(request, out var upload)
                && upload.ContainsFile("metrics.json");
        }

        public static List<Tuple<string, double>> GetMetrics(string metricsFile)
        {
            var metrics = new List<Tuple<string, double>>();
            var jsonContent = System.IO.File.ReadAllText(metricsFile);

            var doc = JsonDocument.Parse(jsonContent);
            _ = doc.RootElement.EnumerateArray().All(element =>
            {
                var kvp = element.EnumerateArray().ToArray();
                metrics.Add(new Tuple<string, double>(kvp[0].ToString(), kvp[1].GetDouble()));
                return true;
            });
            return metrics;
        }
    }
}
