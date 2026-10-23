// <copyright file="StripInjectedContextBenchmark.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if NET6_0_OR_GREATER
using BenchmarkDotNet.Attributes;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Lambda;

namespace Benchmarks.Trace;

/// <summary>
/// Measures the cost of StripInjectedContext.StripInternal for the three Lambda event shapes.
/// Mirrors the payload shapes of the Java JMH benchmark.
/// Run with: dotnet run -c Release -f net11.0 -- --filter '*StripInjectedContextBenchmark*' --inProcess
/// </summary>
[MemoryDiagnoser]
[BenchmarkCategory(Constants.TracerCategory)]
public class StripInjectedContextBenchmark
{
    private const string Carrier = "{\"x-datadog-trace-id\":\"123\",\"x-datadog-parent-id\":\"456\"}";
    private string _payload;

    [Params(true, false)]
    public bool ContainsDatadogCarrier { get; set; }

    // "eventbridge" | "sqs" | "sns"
    [Params("eventbridge", "sqs", "sns")]
    public string PayloadShape { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _payload = PayloadShape switch
        {
            "sqs" => ContainsDatadogCarrier
                ? $"{{\"orderId\":42,\"customer\":\"acme\",\"_datadog\":{Carrier}}}"
                : "{\"orderId\":42,\"customer\":\"acme\"}",

            "sns" => BuildSnsPayload(),

            _ => // eventbridge
                ContainsDatadogCarrier
                ? $"{{\"detail-type\":\"order.created\",\"detail\":{{\"orderId\":42,\"customer\":\"acme\",\"_datadog\":{Carrier}}}}}"
                : "{\"detail-type\":\"order.created\",\"detail\":{\"orderId\":42,\"customer\":\"acme\"}}",
        };
    }

    [Benchmark]
    public string StripInternal() => StripInjectedContext.StripInternal(_payload);

    private static string EscapeForJsonString(string s)
    => s.Replace("\"", "\\\"");

    private string BuildSnsPayload()
    {
        // SNS: _datadog is inside a string-encoded Message field
        var inner = ContainsDatadogCarrier
            ? $"{{\\\"orderId\\\":42,\\\"_datadog\\\":{EscapeForJsonString(Carrier)}}}"
            : "{\\\"orderId\\\":42}";
        return $"{{\"Type\":\"Notification\",\"Message\":\"{inner}\"}}";
    }

}
#endif
