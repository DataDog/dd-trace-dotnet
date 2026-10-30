using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OpenFeature.Constant;
using OpenFeature.Model;
using Samples;

namespace Samples.FeatureFlags;

// Uses only existing public OpenFeature/provider APIs, so the same driver can load
// either baseline or candidate assemblies. Fixture setup and flush are measured separately.
internal static class EvaluationPerformanceSample
{
    internal static async Task RunAsync(double initializationMs)
    {
        var iterations = int.Parse(Environment.GetEnvironmentVariable("FFE_TEST_ITERATIONS") ?? "10000");
        var subjects = int.Parse(Environment.GetEnvironmentVariable("FFE_TEST_SUBJECTS") ?? "64");
        var workload = Environment.GetEnvironmentVariable("FFE_TEST_WORKLOAD") ?? "burst";
        var client = global::OpenFeature.Api.Instance.GetClient();
        var setup = Stopwatch.StartNew();
        var contexts = new EvaluationContext[subjects];
        for (var i = 0; i < contexts.Length; i++)
        {
            var builder = EvaluationContext.Builder().Set("targetingKey", "evp-subject-" + i);
            var fields = workload == "hostile" ? 1024 : 8;
            for (var field = 0; field < fields; field++)
            {
                builder.Set("attribute-" + field, "evp-private-attribute-canary");
            }

            contexts[i] = builder.Build();
        }

        var fixtureMs = setup.Elapsed.TotalMilliseconds;
        var warmup = Stopwatch.StartNew();
        var warmupCount = 0;
        while (warmup.Elapsed.TotalSeconds < 3)
        {
            Evaluate(warmupCount++);
        }

        await Task.Delay(250); // allow background tiered compilation to finish before measurement
        await SampleHelpers.ForceTracerFlushAsync();
        var latencies = new double[iterations];
        var retainedBefore = GC.GetTotalMemory(forceFullCollection: true);
        var allocatedBefore = AllocatedBytes();
        using var process = Process.GetCurrentProcess();
        var cpuBefore = process.TotalProcessorTime;
        var elapsed = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
        {
            var start = Stopwatch.GetTimestamp();
            Evaluate(i);
            latencies[i] = (Stopwatch.GetTimestamp() - start) * 1_000_000.0 / Stopwatch.Frequency;
            if (workload == "sparse" || (workload == "typical" && i % 10 == 9))
            {
                Thread.Sleep(10);
            }
        }

        var evaluationMs = elapsed.Elapsed.TotalMilliseconds;
        var allocatedAfter = AllocatedBytes();
        var flush = Stopwatch.StartNew();
        await SampleHelpers.ForceTracerFlushAsync();
        var flushMs = flush.Elapsed.TotalMilliseconds;
        var allocatedIncludingFlush = AllocatedBytes() - allocatedBefore;
        var cpuMs = (process.TotalProcessorTime - cpuBefore).TotalMilliseconds;
        var retainedAfter = GC.GetTotalMemory(forceFullCollection: true);
        process.Refresh();
        Array.Sort(latencies);
        Console.WriteLine(FormattableString.Invariant(
            $"FFE_PERF {{\"iterations\":{iterations},\"warmup\":{warmupCount},\"subjects\":{subjects},\"workload\":\"{workload}\",\"initialization_ms\":{initializationMs},\"fixture_ms\":{fixtureMs},\"evaluation_ms\":{evaluationMs},\"flush_ms\":{flushMs},\"cpu_ms_including_flush\":{cpuMs},\"p50_us\":{latencies[iterations / 2]},\"p95_us\":{latencies[(int)(iterations * 0.95)]},\"p99_us\":{latencies[(int)(iterations * 0.99)]},\"allocated_bytes\":{allocatedAfter - allocatedBefore},\"allocated_bytes_including_flush\":{allocatedIncludingFlush},\"retained_before_bytes\":{retainedBefore},\"retained_after_bytes\":{retainedAfter},\"working_set_bytes\":{process.WorkingSet64},\"peak_working_set_bytes\":{process.PeakWorkingSet64}}}"));

        void Evaluate(int index)
        {
            var result = client.GetStringDetailsAsync("simple-string", "caller-default", contexts[index % contexts.Length]).GetAwaiter().GetResult();
            if (result.Value != "test-value" || result.ErrorType != ErrorType.None)
            {
                throw new InvalidOperationException("Performance driver observed an incorrect evaluation result");
            }
        }
    }

    private static long AllocatedBytes()
    {
#if NETCOREAPP3_0_OR_GREATER
        return GC.GetTotalAllocatedBytes(precise: true);
#else
        // Execution evidence uses net10.0; retain source compatibility with the older sample TFMs.
        return 0;
#endif
    }
}
