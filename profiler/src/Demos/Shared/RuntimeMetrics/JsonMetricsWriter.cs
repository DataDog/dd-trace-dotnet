// <copyright file="JsonMetricsWriter.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using System.IO;

namespace Datadog.RuntimeMetrics
{
    public class JsonMetricsWriter
    {
        private readonly string _filename;

        public JsonMetricsWriter(string filename)
        {
            _filename = filename;
        }

        public void Write(IReadOnlyList<(string Name, string Value)> metrics)
        {
            using (var writer = new StreamWriter(_filename))
            {
                writer.WriteLine("[");
                var count = metrics.Count;
                for (int i = 0; i < count; i++)
                {
                    var metric = metrics[i];
                    writer.Write("   {");
                    writer.Write($"\"{metric.Name}\":\"{metric.Value}\"");

                    if (i == count - 1)
                    {
                        writer.WriteLine("}");
                    }
                    else
                    {
                        writer.WriteLine("},");
                    }
                }

                writer.WriteLine("]");
            }
        }
    }
}
