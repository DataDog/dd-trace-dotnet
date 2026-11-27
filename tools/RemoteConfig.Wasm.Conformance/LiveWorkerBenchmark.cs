#if VERIFY_SHARED_WORKER
using System.Diagnostics;
using System.Text.Json;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RemoteConfigurationManagement.Shared;

namespace Datadog.Trace.Tools.RemoteConfigConformance;

internal static class LiveWorkerBenchmark
{
    private const string Product = "APM_TRACING";

    internal static void WriteFixtures()
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            single = Variants(1, 1024),
            multi = Variants(24, 4096),
        }));

        static byte[][] Variants(int count, int padding) =>
            [AgentComparison.CreateFixture(count, padding, 1).Response, AgentComparison.CreateFixture(count, padding, 2).Response];
    }

    internal static async Task Run(string agentUrl, string backend, string library)
    {
        var nativeLibrary = backend switch
        {
            "legacy" => null,
            "native" or "watch" => library,
            _ => throw new ArgumentException("Unknown Remote Config benchmark backend", nameof(backend)),
        };
        AppDomain.CurrentDomain.SetData(NativeAgentPoller.LibraryPathSetting, nativeLibrary);
        var delivered = 0;
        var subscriptions = new RcmSubscriptionManager();
        subscriptions.SetCapability(RcmCapabilitiesIndices.ApmTracingSampleRate, true);
        subscriptions.SubscribeToChanges(new Subscription(
            (upserts, _) =>
            {
                if (!upserts.TryGetValue(Product, out var files))
                {
                    return [];
                }

                Interlocked.Add(ref delivered, files.Count);
                var receipts = new ApplyDetails[files.Count];
                for (var index = 0; index < files.Count; index++)
                {
                    receipts[index] = ApplyDetails.FromOk(files[index].Path.Path);
                }

                return receipts;
            },
            Product)
        {
            InstallSnapshot = backend == "watch"
                                  ? (files, _, _, tryCommit) => tryCommit(() => Interlocked.Add(ref delivered, files.Count))
                                  : null,
        });
        var discovery = new NativeWorkerProof.Discovery();
        var manager = NativeWorkerProof.CreateManager(agentUrl, "0.0001", discovery, subscriptions, out var tracerSettings);
        discovery.Publish("/v0.7/config", tracerSettings.Manager.InitialExporterSettings);

        using var process = Process.GetCurrentProcess();
        var activationStart = Stopwatch.GetTimestamp();
        manager.Start();
        Task? stopping = null;
        try
        {
            await ReadSignal("first");
            var activationMs = ToMilliseconds(Stopwatch.GetTimestamp() - activationStart);
            Console.WriteLine("active");

            await ReadSignal("measure");
            var before = Capture();
            Console.WriteLine("measuring");

            await ReadSignal("stop");
            var after = Capture();
            var liveManagedBytes = GC.GetTotalMemory(forceFullCollection: true);
            stopping = manager.StopAsync();
            Console.WriteLine("stopping");
            await stopping;

            Console.WriteLine(JsonSerializer.Serialize(new
            {
                activationMs,
                wallMs = ToMilliseconds(after.WallTicks - before.WallTicks),
                cpuMs = (after.Cpu - before.Cpu).TotalMilliseconds,
                allocatedBytes = after.Allocated - before.Allocated,
                workingSetStartBytes = before.WorkingSet,
                workingSetEndBytes = after.WorkingSet,
                liveManagedBytes,
                delivered = after.Delivered - before.Delivered,
            }));
        }
        finally
        {
            await (stopping ?? manager.StopAsync());
        }

        static double ToMilliseconds(long ticks) => ticks * (1000d / Stopwatch.Frequency);

        Sample Capture()
        {
            process.Refresh();
            return new Sample(Stopwatch.GetTimestamp(), process.TotalProcessorTime, GC.GetTotalAllocatedBytes(precise: true), process.WorkingSet64, Volatile.Read(ref delivered));
        }
    }

    private static async Task ReadSignal(string expected)
    {
        if (await Console.In.ReadLineAsync() != expected)
        {
            throw new InvalidOperationException($"Expected benchmark signal {expected}");
        }
    }

    private readonly record struct Sample(long WallTicks, TimeSpan Cpu, long Allocated, long WorkingSet, int Delivered);
}
#endif
