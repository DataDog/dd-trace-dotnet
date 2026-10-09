using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation.OpenFeature;
using Datadog.Trace.FeatureFlags.FlagEvaluation;
using Datadog.Trace.Telemetry;

namespace Benchmarks.Trace;

// Component measurement only: provider hooks and native CallTarget are measured separately
// with the instrumented OpenFeature sample. No network or fixture construction is timed here.
[MemoryDiagnoser]
public class FlagEvaluationBenchmarks
{
    private const int BatchSize = 256;
    private FlagEvaluationWriter _writer;
    private Dictionary<string, object> _attributes;
    private string[] _subjects;
    private bool _consent;
    private TaskCompletionSource<bool> _release;

    [Params("Disabled", "ProtectedRepeated", "ProtectedUnique", "FullRepeated", "FullHostile", "Saturated")]
    public string Scenario { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _subjects = new string[BatchSize];
        for (var i = 0; i < _subjects.Length; i++)
        {
            _subjects[i] = "subject-" + i;
        }

        _consent = Scenario.StartsWith("Full", StringComparison.Ordinal);
        _attributes = new Dictionary<string, object>();
        var fields = Scenario == "FullHostile" ? 1024 : 8;
        for (var i = 0; i < fields; i++)
        {
            _attributes["field-" + i] = "value";
        }

        if (Scenario == "Disabled")
        {
            return;
        }

        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var context = new Dictionary<string, string> { ["service"] = "ffe-benchmark" };
        _writer = new FlagEvaluationWriter(
            _ =>
            {
                if (Scenario != "Saturated")
                {
                    return Task.CompletedTask;
                }

                entered.TrySetResult(true);
                return _release.Task;
            },
            context,
            queueCap: Scenario == "Saturated" ? 1 : FlagEvaluationWriter.DefaultQueueCapacity,
            metrics: NullMetricsTelemetryCollector.Instance);

        if (Scenario == "Saturated")
        {
            var observation = new FlagEvalEvent("flag", "on", "allocation", "subject", 1, null);
            _writer.TryEnqueue(observation);
            _ = _writer.FlushAsync();
            if (!entered.Task.Wait(TimeSpan.FromSeconds(5)))
            {
                throw new InvalidOperationException("Benchmark did not reach blocked-send setup");
            }

            _writer.TryEnqueue(observation);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _release?.TrySetResult(true);
        _writer?.CloseAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
    }

    [Benchmark(OperationsPerInvoke = BatchSize)]
    public Task ObserveAndFlush()
    {
        for (var i = 0; i < BatchSize; i++)
        {
            if (_writer is not null && _writer.HasCapacity())
            {
                OpenFeatureSdkEnqueueEVPIntegration.Enqueue(
                    _writer, "flag", "on", "allocation", _subjects[Scenario == "ProtectedUnique" ? i : 0],
                    DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), null, _attributes, _consent ? FlagEvaluationBridge.ObserveFullEvaluationData : 0);
            }
        }

        return _writer is null || Scenario == "Saturated" ? Task.CompletedTask : _writer.FlushAsync();
    }
}
