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
            if (!request.ContentType.StartsWith("multipart/form-data"))
            {
                return false;
            }

            var mpReader = new MultiPartReader(request);
            if (!mpReader.Parse())
            {
                return false;
            }

            var files = mpReader.Files;
            var metricsFileInfo = files.FirstOrDefault(f => f.FileName == "metrics.json");
            if (metricsFileInfo == null)
            {
                return false;
            }

            // TODO: when the file will be generated the right way, parse the json content to ensure that at least 1 metrics is sent
            var metricsFileContent = mpReader.GetStringFile(metricsFileInfo.BytesPos, metricsFileInfo.BytesSize);
            // Today, the content is not correct and with a binary format

            return true;
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
