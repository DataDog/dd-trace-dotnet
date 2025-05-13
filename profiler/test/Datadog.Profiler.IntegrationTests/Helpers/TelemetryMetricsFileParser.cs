// <copyright file="TelemetryMetricsFileParser.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using static Datadog.Profiler.IntegrationTests.Helpers.TelemetryMetricsFileParser;

namespace Datadog.Profiler.IntegrationTests.Helpers
{
    public class TelemetryMetricsFileParser
    {
        private TelemetryMetricsFileParser()
        {
            Profiles = new List<TelemetryMetric>();
            RuntimeIds = new List<TelemetryMetric>();
        }

        public List<TelemetryMetric> Profiles { get; private set; }
        public List<TelemetryMetric> RuntimeIds { get; private set; }

        public static TelemetryMetricsFileParser LoadFromDirectory(string directory)
        {
            // folder might not be created (i.e. no pprof/telemetry metrics are generated)
            try
            {
                // only one telemetry*.json file is expected in the directory
                var jsonFilename = Directory.EnumerateFiles(directory, "telemetry*.json").FirstOrDefault();
                if (jsonFilename == null)
                {
                    return null;
                }

                var parser = new TelemetryMetricsFileParser();
                if (parser.LoadFile(jsonFilename))
                {
                    return parser;
                }
            }
            catch (Exception)
            {
            }

            return null;
        }

        // the .json file is not correct: it is the concatenation of lines of json objects
        public static TelemetryMetricsFileParser LoadFromFile(string jsonFileName)
        {
            var parser = new TelemetryMetricsFileParser();
            if (parser.LoadFile(jsonFileName))
            {
                return parser;
            }

            return null;
        }

        public static TelemetryMetricsFileParser LoadFromString(string content)
        {
            var parser = new TelemetryMetricsFileParser();

            if (parser.ParseString(content))
            {
                return parser;
            }

            return null;
        }

        private bool LoadFile(string jsonFilename)
        {
            try
            {
                var content = File.ReadAllText(jsonFilename);
                return ParseString(content);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool ParseString(string content)
        {
            try
            {
                var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    ParseJsonLine(line);
                }

                return true;
            }
            catch (Exception x)
            {
                Console.WriteLine(x.Message);
                return false;
            }
        }

        private bool ParseJsonLine(string line)
        {
            try
            {
                // this could throw an exception because some lines do no correspond to the metrics format (i.e. payload is an object and not an array)
                var jObject = JsonConvert.DeserializeObject<TelemetryMetricObject>(line);

                if ((jObject.payload == null) || (jObject.request_type == null) || (jObject.request_type != "message-batch"))
                {
                    return false;
                }

                var request = jObject.payload.FirstOrDefault();
                if ((request == null) || (request.request_type != "generate-metrics"))
                {
                    return false;
                }

                var series = request.payload.series;
                if ((series == null) || (series.Count == 0))
                {
                    return false;
                }

                foreach (var serie in series)
                {
                    var metric = new TelemetryMetric(
                        serie.metric,
                        serie.tags.Select(t => new Tuple<string, string>(t.Split(':')[0], t.Split(':')[1])).ToList(),
                        serie.points.Select(p => new Tuple<double, double>(p[0], p[1])).ToList());

                    if (serie.metric == "ssi_heuristic.number_of_profiles")
                    {
                        Profiles.Add(metric);
                    }
                    else if (serie.metric == "ssi_heuristic.number_of_runtime_id")
                    {
                        RuntimeIds.Add(metric);
                    }
                }

                return (Profiles.Count + RuntimeIds.Count > 0);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // remove warnings for deserialization types
#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
#pragma warning disable SA1300 // Element should begin with upper-case letter
#pragma warning disable SA1310 // Field names should not contain underscore
#pragma warning disable SA1401 // Fields should be private
#pragma warning disable CS0649
        // types used to deserialize the json file
        internal class TelemetryMetricObject
        {
            public string request_type;
            public List<Request> payload { get; set; }
        }

        internal class Request
        {
            public string request_type;
            public Series payload { get; set; }
        }

        internal class Series
        {
            public List<Serie> series { get; set; }
        }

        internal class Serie
        {
            public string metric { get; set; }
            public List<string> tags { get; set; }
            public List<List<double>> points { get; set; }
        }
#pragma warning restore CS0649
#pragma warning restore SA1401 // Fields should be private
#pragma warning restore SA1310 // Field names should not contain underscore
#pragma warning restore SA1300 // Element should begin with upper-case letter
#pragma warning restore SA1307 // Accessible fields should begin with upper-case letter
    }
}
