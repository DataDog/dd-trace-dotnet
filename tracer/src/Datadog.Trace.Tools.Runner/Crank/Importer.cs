// <copyright file="Importer.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Sampling;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Spectre.Console;

#pragma warning disable SA1201 // Elements should appear in the correct order

namespace Datadog.Trace.Tools.Runner.Crank
{
    internal class Importer
    {
        public const string RateLimit = "rate-limit";
        public const string PayloadSize = "payload-size";

        private static readonly IResultConverter[] Converters = new IResultConverter[]
        {
            new MsTimeResultConverter("benchmarks/start-time"),
            new MsTimeResultConverter("benchmarks/build-time"),
            new KbSizeResultConverter("benchmarks/published-size"),
            new MbSizeResultConverter("benchmarks/working-set"),
            new PercentageResultConverter("benchmarks/cpu"),

            new PercentageResultConverter("runtime-counter/cpu-usage"),
            new MbSizeResultConverter("runtime-counter/working-set"),
            new MbSizeResultConverter("runtime-counter/gc-heap-size"),
            new NumberResultConverter("runtime-counter/gen-0-gc-count"),
            new NumberResultConverter("runtime-counter/gen-1-gc-count"),
            new NumberResultConverter("runtime-counter/gen-2-gc-count"),
            new NumberResultConverter("runtime-counter/exception-count"),
            new NumberResultConverter("runtime-counter/threadpool-thread-count"),
            new NumberResultConverter("runtime-counter/monitor-lock-contention-count"),
            new NumberResultConverter("runtime-counter/threadpool-queue-length"),
            new NumberResultConverter("runtime-counter/threadpool-completed-items-count"),
            new PercentageResultConverter("runtime-counter/time-in-gc"),
            new ByteSizeResultConverter("runtime-counter/gen-0-size"),
            new ByteSizeResultConverter("runtime-counter/gen-1-size"),
            new ByteSizeResultConverter("runtime-counter/gen-2-size"),
            new ByteSizeResultConverter("runtime-counter/loh-size"),
            new NumberResultConverter("runtime-counter/alloc-rate"),
            new NumberResultConverter("runtime-counter/assembly-count"),
            new NumberResultConverter("runtime-counter/active-timer-count"),

            new NumberResultConverter("aspnet-counter/requests-per-second"),
            new NumberResultConverter("aspnet-counter/total-requests"),
            new NumberResultConverter("aspnet-counter/current-requests"),
            new NumberResultConverter("aspnet-counter/failed-requests"),

            new MsTimeResultConverter("http/firstrequest"),

            new NumberResultConverter("bombardier/requests"),
            new NumberResultConverter("bombardier/badresponses"),
            new UsTimeResultConverter("bombardier/latency/mean"),
            new UsTimeResultConverter("bombardier/latency/max"),
            new NumberResultConverter("bombardier/rps/max"),
            new NumberResultConverter("bombardier/rps/mean"),
            new NumberResultConverter("bombardier/throughput"),
            new BombardierRawConverter("bombardier/raw"),
        };

        public static int Process(string jsonFilePath)
        {
            AnsiConsole.WriteLine("Importing Crank json result file...");
            try
            {
                string jsonContent = File.ReadAllText(jsonFilePath);
                var result = JsonConvert.DeserializeObject<Models.ExecutionResult>(jsonContent);

                if (result?.JobResults?.Jobs?.Count > 0)
                {
                    var fileName = Path.GetFileName(jsonFilePath);

                    CIVisibility.Initialize();
                    Tracer tracer = Tracer.Instance;

                    foreach (var jobItem in result.JobResults.Jobs)
                    {
                        var jobResult = jobItem.Value;
                        if (jobResult is null)
                        {
                            continue;
                        }

                        DateTimeOffset minTimeStamp = DateTimeOffset.UtcNow;
                        DateTimeOffset maxTimeStamp = minTimeStamp;
                        var measurements = jobResult.Measurements?.SelectMany(i => i).ToList() ?? new List<Models.Measurement>();
                        if (measurements.Count > 0)
                        {
                            maxTimeStamp = measurements.Max(i => i.Timestamp).ToUniversalTime();
                            minTimeStamp = measurements.Min(i => i.Timestamp).ToUniversalTime();
                        }

                        var duration = (maxTimeStamp - minTimeStamp);

                        Span span = tracer.StartSpan("crank.test", startTime: minTimeStamp, serviceName: "crank");

                        span.Type = SpanTypes.Test;
                        span.ResourceName = $"{fileName}/{jobItem.Key}";
                        CIEnvironmentValues.Instance.DecorateSpan(span);

                        span.SetTag(TestTags.Name, jobItem.Key);
                        span.SetTag(TestTags.Type, TestTags.TypeBenchmark);
                        span.SetTag(TestTags.Suite, $"Crank.{fileName}");
                        span.SetTag(TestTags.Framework, "Crank");
                        span.SetTag(TestTags.Status, result.ReturnCode == 0 ? TestTags.StatusPass : TestTags.StatusFail);

                        if (result.JobResults.Properties?.Count > 0)
                        {
                            string scenario = string.Empty;
                            string profile = string.Empty;
                            string arch = string.Empty;
                            string rateLimit = string.Empty;
                            string payloadSize = string.Empty;
                            string testName = jobItem.Key;
                            foreach (var propItem in result.JobResults.Properties)
                            {
                                span.SetTag("test.properties." + propItem.Key, propItem.Value);

                                if (propItem.Key == "name")
                                {
                                    testName = propItem.Value + "." + jobItem.Key;
                                }
                                else if (propItem.Key == "scenario")
                                {
                                    scenario = propItem.Value;
                                }
                                else if (propItem.Key == "profile")
                                {
                                    profile = propItem.Value;
                                }
                                else if (propItem.Key == "arch")
                                {
                                    arch = propItem.Value;
                                }
                                else if (propItem.Key == RateLimit)
                                {
                                    rateLimit = propItem.Value;
                                }
                                else if (propItem.Key == PayloadSize)
                                {
                                    payloadSize = propItem.Value;
                                }
                            }

                            string suite = fileName;
                            if (!string.IsNullOrEmpty(scenario))
                            {
                                suite = scenario;

                                if (!string.IsNullOrEmpty(profile))
                                {
                                    suite += "." + profile;
                                }

                                if (!string.IsNullOrEmpty(arch))
                                {
                                    suite += "." + arch;
                                }
                            }

                            span.SetTag(TestTags.Suite, $"Crank.{suite}");
                            span.SetTag(TestTags.Name, testName);

                            if (rateLimit != string.Empty)
                            {
                                span.SetTag(RateLimit, rateLimit);
                            }

                            if (payloadSize != string.Empty)
                            {
                                span.SetTag(PayloadSize, payloadSize);
                            }

                            span.ResourceName = $"{suite}/{testName}";
                        }

                        try
                        {
                            if (jobResult.Results?.Count > 0)
                            {
                                foreach (var resultItem in jobResult.Results)
                                {
                                    if (string.IsNullOrEmpty(resultItem.Key))
                                    {
                                        continue;
                                    }

                                    if (resultItem.Value is string valueString)
                                    {
                                        span.SetTag("test.results." + resultItem.Key.Replace("/", ".").Replace("-", "_"), valueString);
                                    }
                                    else
                                    {
                                        NumberResultConverter numberConverter = default;

                                        bool converted = false;
                                        foreach (var converter in Converters)
                                        {
                                            if (converter.CanConvert(resultItem.Key))
                                            {
                                                converter.SetToSpan(span, "test.results." + resultItem.Key.Replace("/", ".").Replace("-", "_"), resultItem.Value);
                                                converted = true;
                                                break;
                                            }
                                        }

                                        if (!converted)
                                        {
                                            numberConverter.SetToSpan(span, "test.results." + resultItem.Key.Replace("/", ".").Replace("-", "_"), resultItem.Value);
                                        }
                                    }
                                }
                            }

                            if (jobResult.Environment?.Count > 0)
                            {
                                foreach (var envItem in jobResult.Environment)
                                {
                                    span.SetTag("environment." + envItem.Key, envItem.Value?.ToString() ?? "(null)");
                                }
                            }
                        }
                        finally
                        {
                            if (duration == TimeSpan.Zero)
                            {
                                span.Finish();
                            }
                            else
                            {
                                span.Finish(span.StartTime.Add(duration));
                            }
                        }
                    }

                    // Ensure all the spans gets flushed before we report the success.
                    // In some cases the process finishes without sending the traces in the buffer.
                    CIVisibility.FlushSpans();
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.WriteException(ex);
                return 1;
            }

            AnsiConsole.WriteLine("The result file was imported successfully.");
            return 0;
        }
    }
}
