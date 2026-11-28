#if VERIFY_SHARED_WORKER
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipes;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.ConfigurationSources;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.Expressions;
using Datadog.Trace.Debugger.RateLimiting;
using Datadog.Trace.Debugger.Snapshots;
using Datadog.Trace.Debugger.Upload;
using Datadog.Trace.HttpOverStreams;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RemoteConfigurationManagement.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Datadog.Trace.Tools.RemoteConfigConformance;

internal static class NativeWorkerProof
{
    private const string Product = "APM_TRACING";
    private const int FileCount = 257;

    public static async Task Run(string artifact)
    {
        AppDomain.CurrentDomain.SetData(NativeAgentPoller.LibraryPathSetting, artifact);
        await VerifyPipePeerDisconnect();
        await Verify(socketPath: null);
        await Verify(socketPath: null, pipeName: $"dd-rc-{Guid.NewGuid():N}");
        if (Socket.OSSupportsUnixDomainSockets)
        {
            var socketPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            try
            {
                await Verify(socketPath);
            }
            finally
            {
                File.Delete(socketPath);
            }
        }

        await Verify(socketPath: null, watch: true);
        await VerifyRetainedRawPaths(artifact, artifact);
        await VerifyRetainedRawPaths(null, artifact);
        await VerifyRetainedRawPaths(artifact, null);
        await VerifyRetainedRawPaths(null, null);
    }

    private static async Task VerifyRetainedRawPaths(string? before, string? after)
    {
        const string endpoint = "/v0.7/config";
        const string product = RcmProducts.LiveDebugging;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var requests = Channel.CreateUnbounded<Exchange>();
        var withdrawn = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var newSubscriberCalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = CreateAgent(endpoint, socketPath: null, requests.Writer, timeout.Token);
        await server.StartAsync(timeout.Token);

        var subscriptions = new RcmSubscriptionManager();
        var deliveryOwner = new object();
        var surviving = new Subscription(
            (upserts, removals) =>
            {
                if (removals?.TryGetValue(product, out var paths) == true && paths.Exists(path => path.Id == "retained"))
                {
                    withdrawn.TrySetResult();
                }

                return upserts.TryGetValue(product, out var files)
                           ? files.Select(file => ApplyDetails.FromOk(file.Path.Path)).ToArray()
                           : [];
            },
            product)
        {
            DeliveryOwner = deliveryOwner,
        };
        subscriptions.SubscribeToChanges(surviving);
        var previous = StartWorker(before);
        Task? retired = null;
        string? previousClientId = null;
        try
        {
            var initial = await requests.Reader.ReadAsync(timeout.Token);
            previousClientId = initial.Request.GetProperty("client").GetProperty("id").GetString();
            initial.Completion.SetResult(new Reply(200, Assignment(product, 1, ("retained", 1, "{}"))));
            var acknowledged = await requests.Reader.ReadAsync(timeout.Token);
            VerifyReceipt(acknowledged, "retained", 2, version: 1);
            retired = previous.StopAsync();
            acknowledged.Completion.TrySetResult(new Reply(204, []));
        }
        finally
        {
            await (retired ?? previous.StopAsync()).WaitAsync(timeout.Token);
        }

        if (before is null)
        {
            subscriptions.Replace(surviving, new Subscription(surviving.Invoke, product, Product) { DeliveryOwner = deliveryOwner });
        }

        subscriptions.SubscribeToChanges(new Subscription(
            (_, _) =>
            {
                newSubscriberCalled.TrySetResult();
                return Array.Empty<ApplyDetails>();
            },
            product));
        var successor = StartWorker(after);
        Task? stopped = null;
        try
        {
            var noChange = await requests.Reader.ReadAsync(timeout.Token);
            Require(ConfigStates(noChange).GetArrayLength() == 0, "The successor sent a receipt from the previous backend");
            var successorClient = noChange.Request.GetProperty("client");
            Require(successorClient.GetProperty("id").GetString() != previousClientId, "The successor reused the retired client ID");
            Require(successorClient.GetProperty("state").GetProperty("targets_version").GetInt64() == 0, "The successor reused the retired targets version");
            noChange.Completion.SetResult(new Reply(204, []));
            var noAssignment = await requests.Reader.ReadAsync(timeout.Token);
            Require(!withdrawn.Task.IsCompleted, "A 204 withdrew a retained assignment");
            noAssignment.Completion.SetResult(new Reply(200, Encoding.UTF8.GetBytes("{}")));
            var empty = await requests.Reader.ReadAsync(timeout.Token);
            Require(!withdrawn.Task.IsCompleted, "A response without an assignment withdrew a retained file");
            empty.Completion.SetResult(new Reply(200, Assignment(product, 2)));
            var settled = await requests.Reader.ReadAsync(timeout.Token);
            Require(withdrawn.Task.IsCompleted, "The verified empty assignment did not withdraw the retained file");
            Require(!newSubscriberCalled.Task.IsCompleted, "A fresh subscriber received a withdrawal for an old file");
            Require(ConfigStates(settled).GetArrayLength() == 0, "A retained path introduced an old receipt");
            stopped = successor.StopAsync();
            settled.Completion.TrySetResult(new Reply(204, []));
        }
        finally
        {
            await (stopped ?? successor.StopAsync()).WaitAsync(timeout.Token);
            AppDomain.CurrentDomain.SetData(NativeAgentPoller.LibraryPathSetting, before ?? after);
        }

        if (before is null)
        {
            var returned = StartWorker(null);
            Exchange? request = null;
            try
            {
                request = await requests.Reader.ReadAsync(timeout.Token);
                Require(ConfigStates(request).GetArrayLength() == 0, "Returning to managed polling sent receipts from its retired session");
                var client = request.Request.GetProperty("client");
                Require(client.GetProperty("id").GetString() != previousClientId, "Returning to managed polling reused the retired client ID");
                Require(client.GetProperty("state").GetProperty("targets_version").GetInt64() == 0, "Returning to managed polling reused the retired targets version");
            }
            finally
            {
                var retirement = returned.StopAsync();
                request?.Completion.TrySetResult(new Reply(204, []));
                await retirement.WaitAsync(timeout.Token);
                AppDomain.CurrentDomain.SetData(NativeAgentPoller.LibraryPathSetting, after);
            }
        }

        Console.WriteLine($"Raw retention passed from {(before is null ? "managed" : "native")} to {(after is null ? "managed" : "native")}");

        RemoteConfigurationManager StartWorker(string? path)
        {
            AppDomain.CurrentDomain.SetData(NativeAgentPoller.LibraryPathSetting, path);
            var discovery = new Discovery();
            var manager = CreateManager(server.Urls.Single(), "0.05", discovery, subscriptions, out var settings);
            discovery.Publish(endpoint, settings.Manager.InitialExporterSettings);
            manager.Start();
            return manager;
        }
    }

    public static async Task VerifyTracerShutdown(string artifact, bool replaceBeforeShutdown)
    {
        const string endpoint = "/v0.7/config";
        AppDomain.CurrentDomain.SetData(NativeAgentPoller.LibraryPathSetting, artifact);
        Environment.SetEnvironmentVariable("DD_REMOTE_CONFIG_POLL_INTERVAL_SECONDS", "0.05");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var requests = Channel.CreateUnbounded<Exchange>();
        await using var agent = CreateAgent(endpoint, socketPath: null, requests.Writer, timeout.Token);
        agent.MapGet("/info", () => Results.Json(new { endpoints = new[] { endpoint } }));
        await agent.StartAsync(timeout.Token);
        var agentUrl = agent.Urls.Single();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscriber = new Subscription(async (upserts, _) =>
        {
            if (!upserts.TryGetValue(Product, out var files) || !files.Any(file => file.Path.Id == "shutdown"))
            {
                return [];
            }

            entered.SetResult();
            await release.Task.WaitAsync(timeout.Token);
            return files.Select(file => ApplyDetails.FromOk(file.Path.Path)).ToArray();
        }, Product);

        RemoteConfigurationManager? worker = null;
        Task? shutdown = null;
        var subscriptions = (RcmSubscriptionManager)RcmSubscriptionManager.Instance;
        subscriptions.SubscribeToChanges(subscriber);
        try
        {
            Tracer.Configure(ReplacementSettings(agentUrl, "shutdown-current", remoteConfigEnabled: true));
            worker = (RemoteConfigurationManager)Tracer.Instance.TracerManager.RemoteConfigurationManager;
            var poll = await requests.Reader.ReadAsync(timeout.Token);
            const string config = """{"lib_config":{"tracing_sampling_rate":0.37}}""";
            poll.Completion.SetResult(new Reply(200, WatchAssignment(1, ("shutdown", 1, config))));
            await entered.Task.WaitAsync(timeout.Token);
            Require(subscriptions.ProductKeys.Contains(RcmProducts.TracerFlareInitiated),
                    "The native tracer did not subscribe to tracer flare");

            if (replaceBeforeShutdown)
            {
                Tracer.Configure(ReplacementSettings(agentUrl, "shutdown-successor", remoteConfigEnabled: false));
                Require(Tracer.Instance.TracerManager.RemoteConfigurationManager is NullRemoteConfigurationManager,
                        "The successor should have no polling worker");
            }

            shutdown = Tracer.Instance.TracerManager.ShutdownAsync();
            var observation = Task.Delay(TimeSpan.FromSeconds(1), timeout.Token);
            Require(await Task.WhenAny(shutdown, observation) == observation,
                    "Tracer shutdown returned while a Remote Config callback was still running");
            timeout.Token.ThrowIfCancellationRequested();
            Require(subscriptions.ProductKeys.Contains(RcmProducts.TracerFlareInitiated),
                    "Tracer shutdown removed the flare subscriber while a native callback was still running");
            release.SetResult();
            await shutdown.WaitAsync(timeout.Token);
            Require(!subscriptions.ProductKeys.Contains(RcmProducts.TracerFlareInitiated),
                    "The flare subscriber outlived tracer shutdown");
            Require(worker.StopAsync().IsCompletedSuccessfully, "Tracer shutdown did not await the Remote Config worker");
        }
        finally
        {
            release.TrySetResult();
            subscriptions.Unsubscribe(subscriber);
            if (worker is not null)
            {
                await (shutdown ?? Tracer.Instance.TracerManager.ShutdownAsync()).WaitAsync(timeout.Token);
                await worker.StopAsync().WaitAsync(timeout.Token);
            }
        }

        var owner = replaceBeforeShutdown ? "retired" : "active";
        Console.WriteLine($"Tracer shutdown awaited the {owner} Remote Config worker");
    }

    public static async Task VerifyLateManagedStart(string artifact)
    {
        const string endpoint = "/v0.7/config";
        AppDomain.CurrentDomain.SetData(NativeAgentPoller.LibraryPathSetting, null);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var requests = Channel.CreateUnbounded<Exchange>();
        await using var agent = CreateAgent(endpoint, socketPath: null, requests.Writer, timeout.Token);
        agent.MapGet("/info", () => Results.Json(new { endpoints = new[] { endpoint } }));
        await agent.StartAsync(timeout.Token);

        var agentUrl = agent.Urls.Single();
        TracerManager.ReplaceGlobalManager(ReplacementSettings(agentUrl, "delayed-managed-start", remoteConfigEnabled: true), TracerManagerFactory.Instance);
        var previous = TracerManager.Instance;
        Require(previous.RemoteConfigurationManager is RemoteConfigurationManager, "The delayed manager did not register Remote Config");

        AppDomain.CurrentDomain.SetData(NativeAgentPoller.LibraryPathSetting, artifact);
        Tracer.Configure(ReplacementSettings(agentUrl, "native-successor", remoteConfigEnabled: true));
        var successor = TracerManager.Instance;

        var poll = await requests.Reader.ReadAsync(timeout.Token);
        try
        {
            VerifyPollBackend(poll, native: true);
            Require(poll.Request.GetProperty("client").GetProperty("client_tracer").GetProperty("service").GetString() == "native-successor",
                    "The first poll did not come from the native successor");
            var subscriptions = (RcmSubscriptionManager)RcmSubscriptionManager.Instance;
            var watcher = subscriptions.GetSnapshotSubscription(Product);
            Require(watcher is not null, "The native successor did not own the APM snapshot watch");

            previous.Start();

            Require(ReferenceEquals(watcher, subscriptions.GetSnapshotSubscription(Product)), "The retired manager registered a competing APM subscription on first start");
        }
        finally
        {
            poll.Completion.TrySetResult(new Reply(204, []));
            await successor.ShutdownAsync().WaitAsync(timeout.Token);
        }

        Console.WriteLine("The retired manager left the native APM snapshot subscription intact");
    }

    public static async Task VerifyManagedSuccessorShutdown(string artifact)
    {
        const string endpoint = "/v0.7/config";
        const string managedService = "shutdown-managed-successor";
        AppDomain.CurrentDomain.SetData(NativeAgentPoller.LibraryPathSetting, artifact);
        Environment.SetEnvironmentVariable("DD_REMOTE_CONFIG_POLL_INTERVAL_SECONDS", "0.05");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var requests = Channel.CreateUnbounded<Exchange>();
        await using var agent = CreateAgent(endpoint, socketPath: null, requests.Writer, timeout.Token);
        agent.MapGet("/info", () => Results.Json(new { endpoints = new[] { endpoint } }));
        await agent.StartAsync(timeout.Token);
        var agentUrl = agent.Urls.Single();

        Tracer.Configure(ReplacementSettings(agentUrl, "shutdown-native-predecessor", remoteConfigEnabled: true));
        var nativePoll = await requests.Reader.ReadAsync(timeout.Token);
        VerifyPollBackend(nativePoll, native: true);
        AppDomain.CurrentDomain.SetData(NativeAgentPoller.LibraryPathSetting, null);
        Tracer.Configure(ReplacementSettings(agentUrl, managedService, remoteConfigEnabled: true));
        var manager = Tracer.Instance.TracerManager;

        await nativePoll.Disconnected.WaitAsync(timeout.Token);
        var managedPoll = await requests.Reader.ReadAsync(timeout.Token);
        VerifyPollBackend(managedPoll, native: false);
        var service = managedPoll.Request.GetProperty("client").GetProperty("client_tracer").GetProperty("service").GetString();
        Require(service == managedService, "The in-flight poll did not belong to the managed successor");
        var shutdown = manager.ShutdownAsync();
        try
        {
            await shutdown.WaitAsync(TimeSpan.FromSeconds(2), timeout.Token);
            Require(!managedPoll.Disconnected.IsCompleted, "Shutdown changed the managed poll's transport lifetime");
        }
        finally
        {
            managedPoll.Completion.TrySetResult(new Reply(204, []));
            await shutdown.WaitAsync(timeout.Token);
        }

        Console.WriteLine("Tracer shutdown completed with a managed successor poll in flight");
    }

    public static async Task VerifyManagedRetiredResponse(
        string? nativeArtifact,
        bool pauseDuringDelivery,
        bool cancelInFlight = false,
        bool shutdownDuringRetirement = false,
        string? intermediateNativeArtifact = null)
    {
        const string endpoint = "/v0.7/config";
        Environment.SetEnvironmentVariable("DD_REMOTE_CONFIG_POLL_INTERVAL_SECONDS", "0.05");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var requests = Channel.CreateUnbounded<Exchange>();
        await using var agent = CreateAgent(endpoint, socketPath: null, requests.Writer, timeout.Token);
        agent.MapGet("/info", () => Results.Json(new { endpoints = new[] { endpoint } }));
        await agent.StartAsync(timeout.Token);
        var address = agent.Urls.Single();

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscriptions = RcmSubscriptionManager.Instance;
        var blocker = new Subscription(
            async (_, _) =>
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(timeout.Token);
                return [];
            },
            Product);
        if (pauseDuringDelivery)
        {
            subscriptions.SubscribeToChanges(blocker);
        }

        Tracer.Configure(ReplacementSettings(address, "managed-predecessor", remoteConfigEnabled: true));
        var previous = Tracer.Instance.TracerManager;
        var worker = (RemoteConfigurationManager)previous.RemoteConfigurationManager;
        var pending = await requests.Reader.ReadAsync(timeout.Token);
        VerifyPollBackend(pending, native: false);
        const string configuration = """{"lib_config":{"tracing_sampling_rate":0.81}}""";
        var reply = new Reply(200, WatchAssignment(1, ("retired-managed", 1, configuration)));
        if (pauseDuringDelivery)
        {
            pending.Completion.SetResult(reply);
            await entered.Task.WaitAsync(timeout.Token);
        }

        if (intermediateNativeArtifact is not null)
        {
            AppDomain.CurrentDomain.SetData(NativeAgentPoller.LibraryPathSetting, intermediateNativeArtifact);
            TracerManager.ReplaceGlobalManager(
                ReplacementSettings(address, "unstarted-native", remoteConfigEnabled: true),
                TracerManagerFactory.Instance);
            Tracer.Instance.TracerManager.Start();
        }

        AppDomain.CurrentDomain.SetData(NativeAgentPoller.LibraryPathSetting, nativeArtifact);
        TracerManager.ReplaceGlobalManager(
            ReplacementSettings(address, "managed-retired-successor", remoteConfigEnabled: nativeArtifact is not null || cancelInFlight || shutdownDuringRetirement),
            TracerManagerFactory.Instance);
        var successor = Tracer.Instance.TracerManager;
        Task? shutdown = null;
        try
        {
            await worker.StopRequested.WaitAsync(timeout.Token);
            if (shutdownDuringRetirement)
            {
                successor.Start();
                shutdown = successor.ShutdownAsync();
                await shutdown.WaitAsync(TimeSpan.FromSeconds(2), timeout.Token);
                Console.WriteLine("Shutdown finished while the retired managed worker's callback was still held");
                return;
            }

            release.TrySetResult();
            if (cancelInFlight)
            {
                await pending.Disconnected.WaitAsync(TimeSpan.FromSeconds(5), timeout.Token);
            }

            pending.Completion.TrySetResult(reply);
            await worker.StopAsync().WaitAsync(timeout.Token);
            Require(successor.PerTraceSettings.Settings.GlobalSamplingRate == 1.0, "A retired managed response applied to an incompatible successor");

            if (nativeArtifact is not null || cancelInFlight)
            {
                successor.Start();
                var first = await requests.Reader.ReadAsync(timeout.Token);
                first.Completion.SetResult(new Reply(204, []));
                VerifyPollBackend(first, native: nativeArtifact is not null);
                Require(ConfigStates(first).GetArrayLength() == 0, "The successor inherited the retired managed receipt");
            }
            else
            {
                Require(successor.RemoteConfigurationManager is NullRemoteConfigurationManager, "The successor enabled Remote Config");
                if (pauseDuringDelivery)
                {
                    subscriptions.Unsubscribe(blocker);
                    Tracer.Configure(ReplacementSettings(address, "managed-after-disabled", remoteConfigEnabled: true));
                    successor = Tracer.Instance.TracerManager;
                    var first = await requests.Reader.ReadAsync(timeout.Token);
                    Require(ConfigStates(first).GetArrayLength() == 0, "The next managed worker inherited receipts from interrupted delivery");
                    Require(first.Request.GetProperty("cached_target_files").GetArrayLength() == 0, "The next managed worker inherited files from interrupted delivery");
                    first.Completion.SetResult(new Reply(204, []));
                }
            }
        }
        finally
        {
            subscriptions.Unsubscribe(blocker);
            release.TrySetResult();
            pending.Completion.TrySetResult(new Reply(204, []));
            await (shutdown ?? successor.ShutdownAsync()).WaitAsync(timeout.Token);
        }

        Console.WriteLine(cancelInFlight
                              ? "Managed retirement canceled the predecessor request and the successor resumed polling"
                              : "The incompatible successor rejected the late managed response");
    }

    public static async Task VerifyRawProductHandoff(string artifact)
    {
        const string endpoint = "/v0.7/config";
        const string siblingProduct = RcmProducts.LiveDebugging;
        AppDomain.CurrentDomain.SetData(NativeAgentPoller.LibraryPathSetting, artifact);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var requests = Channel.CreateUnbounded<Exchange>();
        await using var agent = CreateAgent(endpoint, socketPath: null, requests.Writer, timeout.Token);
        await agent.StartAsync(timeout.Token);

        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var installed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var replayEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseReplay = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var siblingCalls = 0;
        var original = new Subscription(
            async (upserts, _) =>
            {
                if (upserts[Product].Any(file => file.Version == 2))
                {
                    entered.TrySetResult();
                    await release.Task.WaitAsync(timeout.Token);
                }

                return Acknowledge(upserts, Product);
            },
            Product);
        var replacement = new Subscription(
            (upserts, _) =>
            {
                Require(upserts[Product].Single().Version == 2, "The replacement received the wrong current assignment");
                installed.TrySetResult();
                return Acknowledge(upserts, Product);
            },
            Product);
        var sibling = new Subscription(
            (upserts, _) =>
            {
                Interlocked.Increment(ref siblingCalls);
                return Acknowledge(upserts, siblingProduct);
            },
            siblingProduct);
        var subscriptions = new RcmSubscriptionManager();
        subscriptions.SubscribeToChanges(original);
        subscriptions.SubscribeToChanges(sibling);
        var discovery = new Discovery();
        var manager = CreateManager(agent.Urls.Single(), "0.05", discovery, subscriptions, out var settings);
        discovery.Publish(endpoint, settings.Manager.InitialExporterSettings);
        manager.Start();
        try
        {
            var initial = await requests.Reader.ReadAsync(timeout.Token);
            initial.Completion.SetResult(new Reply(200, Assignment(1, [(Product, "primary", 1, "{}"), (siblingProduct, "sibling", 1, "{}")])));

            var update = await requests.Reader.ReadAsync(timeout.Token);
            VerifyReceipt(update, "primary", 2, version: 1);
            VerifyReceipt(update, "sibling", 2, version: 1);
            update.Completion.SetResult(new Reply(200, Assignment(2, [(Product, "primary", 2, """{"revision":2}"""), (siblingProduct, "sibling", 1, "{}")])));
            await entered.Task.WaitAsync(timeout.Token);
            subscriptions.Replace(original, replacement);
            release.TrySetResult();

            var replayed = await requests.Reader.ReadAsync(timeout.Token);
            Require(ConfigStates(replayed).EnumerateArray().Any(state => state.GetProperty("id").GetString() == "sibling"), "The raw subscription change dropped the unrelated receipt");
            VerifyReceipt(replayed, "sibling", 2, version: 1);
            VerifyReceipt(replayed, "primary", 2, version: 2);
            await installed.Task.WaitAsync(timeout.Token);
            var installedWithoutChange = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var nextOwner = new Subscription(
                async (upserts, _) =>
                {
                    Require(upserts[Product].Single().Version == 2, "Replay delivered an obsolete assignment");
                    replayEntered.TrySetResult();
                    await releaseReplay.Task.WaitAsync(timeout.Token);
                    return Acknowledge(upserts, Product);
                },
                Product);
            var finalOwner = new Subscription(
                (upserts, _) =>
                {
                    Require(upserts[Product].Single().Version == 2, "The unchanged poll replayed an obsolete assignment");
                    installedWithoutChange.TrySetResult();
                    return Acknowledge(upserts, Product);
                },
                Product);
            subscriptions.Replace(replacement, nextOwner);
            replayed.Completion.SetResult(new Reply(204, []));
            await replayEntered.Task.WaitAsync(timeout.Token);
            subscriptions.Replace(nextOwner, finalOwner);
            releaseReplay.TrySetResult();

            var duringHandoff = await requests.Reader.ReadAsync(timeout.Token);
            Require(!installedWithoutChange.Task.IsCompleted, "The poll waited for another replay after the registration changed");
            VerifyReceipt(duringHandoff, "sibling", 2, version: 1);
            duringHandoff.Completion.SetResult(new Reply(204, []));
            var unchanged = await requests.Reader.ReadAsync(timeout.Token);
            await installedWithoutChange.Task.WaitAsync(timeout.Token);
            VerifyReceipt(unchanged, "sibling", 2, version: 1);
            Require(Volatile.Read(ref siblingCalls) == 1, "The unchanged sibling was delivered again");
            unchanged.Completion.SetResult(new Reply(204, []));
        }
        finally
        {
            release.TrySetResult();
            releaseReplay.TrySetResult();
            await manager.StopAsync().WaitAsync(timeout.Token);
        }

        Console.WriteLine("Only the replaced raw product replayed its current assignment");

        static ApplyDetails[] Acknowledge(Dictionary<string, List<RemoteConfiguration>> upserts, string product)
            => upserts[product].Select(file => ApplyDetails.FromOk(file.Path.Path)).ToArray();
    }

    public static async Task VerifyFeatureFlagsLifetime(string artifact, bool replace, bool managedPredecessors)
    {
        const string endpoint = "/v0.7/config";
        AppDomain.CurrentDomain.SetData(NativeAgentPoller.LibraryPathSetting, managedPredecessors ? null : artifact);
        Environment.SetEnvironmentVariable("DD_REMOTE_CONFIG_POLL_INTERVAL_SECONDS", "0.05");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var requests = Channel.CreateUnbounded<Exchange>();
        await using var agent = CreateAgent(endpoint, socketPath: null, requests.Writer, timeout.Token);
        agent.MapGet("/info", () => Results.Json(new { endpoints = new[] { endpoint } }));
        await agent.StartAsync(timeout.Token);

        Tracer.Configure(ReplacementSettings(agent.Urls.Single(), "shutdown-feature-flags", remoteConfigEnabled: true, featureFlagsEnabled: true));
        var manager = Tracer.Instance.TracerManager;
        var featureFlags = manager.FeatureFlags ?? throw new InvalidOperationException("Feature Flags is not configured");
        var subscriptions = (RcmSubscriptionManager)RcmSubscriptionManager.Instance;
        Require(subscriptions.ProductKeys.Contains(RcmProducts.FfeFlags), "Feature Flags did not subscribe to Remote Config");
        var originalSubscription = FeatureFlagsSubscriptions().Single();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        featureFlags.RegisterOnNewConfigEventHandler(() =>
        {
            entered.SetResult();
            release.Task.WaitAsync(timeout.Token).GetAwaiter().GetResult();
        });

        Task? shutdown = null;
        TracerManager? successor = null;
        try
        {
            const string configuration = """{"format":"SERVER","createdAt":"2025-01-01T00:00:00Z","environment":{"name":"test"},"flags":{}}""";
            var poll = await requests.Reader.ReadAsync(timeout.Token);
            poll.Completion.SetResult(new Reply(200, Assignment(RcmProducts.FfeFlags, 1, ("shutdown-flags", 1, configuration))));
            await entered.Task.WaitAsync(timeout.Token);
            Require(featureFlags.HasConfiguration, "The Feature Flags callback ran without installing a configuration");

            if (replace)
            {
                if (managedPredecessors)
                {
                    Tracer.Configure(ReplacementSettings(agent.Urls.Single(), "intermediate-feature-flags", remoteConfigEnabled: true, featureFlagsEnabled: true));
                    successor = Tracer.Instance.TracerManager;
                    var intermediate = FeatureFlagsSubscriptions();
                    Require(intermediate.Length == 1 && ReferenceEquals(intermediate[0], originalSubscription),
                            "A managed replacement registered Feature Flags while its predecessor was still delivering");
                    AppDomain.CurrentDomain.SetData(NativeAgentPoller.LibraryPathSetting, artifact);
                }

                Tracer.Configure(ReplacementSettings(agent.Urls.Single(), "replacement-feature-flags", remoteConfigEnabled: true, featureFlagsEnabled: true));
                successor = Tracer.Instance.TracerManager;
                var duringHandoff = FeatureFlagsSubscriptions();
                Require(duringHandoff.Length == 1 && ReferenceEquals(duringHandoff[0], originalSubscription),
                        "The successor registered Feature Flags before the predecessor finished delivery");
                release.TrySetResult();

                var first = await requests.Reader.ReadAsync(timeout.Token);
                var active = FeatureFlagsSubscriptions();
                first.Completion.SetResult(new Reply(204, []));
                Require(first.Request.GetProperty("client").GetProperty("client_tracer").GetProperty("service").GetString() == "replacement-feature-flags",
                        "The first poll after handoff did not come from the successor");
                Require(AdvertisesProduct(first, RcmProducts.FfeFlags), "The successor omitted Feature Flags from its first poll");
                Require(active.Length == 1 && !ReferenceEquals(active[0], originalSubscription), "Feature Flags was not handed to the successor");
            }
            else
            {
                shutdown = manager.ShutdownAsync();
                var observation = Task.Delay(TimeSpan.FromSeconds(1), timeout.Token);
                Require(await Task.WhenAny(shutdown, observation) == observation,
                        "Tracer shutdown returned while a Feature Flags callback was still running");
                timeout.Token.ThrowIfCancellationRequested();
                Require(subscriptions.ProductKeys.Contains(RcmProducts.FfeFlags),
                        "Tracer shutdown disposed Feature Flags before its callback finished");
            }
        }
        finally
        {
            release.TrySetResult();
            await (shutdown ?? (successor ?? manager).ShutdownAsync()).WaitAsync(timeout.Token);
        }

        Require(!subscriptions.ProductKeys.Contains(RcmProducts.FfeFlags), "Feature Flags outlived tracer shutdown");
        Console.WriteLine(replace
                              ? "Feature Flags registered after predecessor delivery drained and before the successor polled"
                              : "Tracer shutdown drained the Feature Flags callback before disposal");

        ISubscription[] FeatureFlagsSubscriptions()
            => subscriptions.CapturePollInterest().Subscriptions.Where(subscription => subscription.ProductKeys.Contains(RcmProducts.FfeFlags)).ToArray();
    }

    public static async Task VerifyTracerReplacement(string? artifact, bool retireDuringDelivery)
    {
        const string endpoint = "/v0.7/config";
        const string traces = "/v0.4/traces";
        const string oldService = "replacement-old";
        const string newService = "replacement-new";
        const string tagged = """{"lib_config":{"tracing_sampling_rate":0.37,"tracing_tags":["proof:enabled"]}}""";
        const string late = """{"lib_config":{"tracing_sampling_rate":0.81}}""";
        const string updated = """{"lib_config":{"tracing_sampling_rate":0.54}}""";
        AppDomain.CurrentDomain.SetData(NativeAgentPoller.LibraryPathSetting, artifact);
        Environment.SetEnvironmentVariable("DD_REMOTE_CONFIG_POLL_INTERVAL_SECONDS", "0.05");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var requests = Channel.CreateUnbounded<Exchange>();
        var traceReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumeTrace = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = CreateAgent(endpoint, socketPath: null, requests.Writer, timeout.Token);
        server.MapGet("/info", () => Results.Json(new { endpoints = new[] { endpoint, traces } }));
        server.MapMethods(traces, ["PUT", "POST"], async (HttpContext context) =>
        {
            await context.Request.Body.CopyToAsync(Stream.Null, timeout.Token);
            traceReceived.TrySetResult();
            await resumeTrace.Task.WaitAsync(timeout.Token);
            return Results.Json(new { rate_by_service = new { } });
        });
        await server.StartAsync(timeout.Token);
        var agentUrl = server.Urls.Single();

        if (artifact is null)
        {
            await VerifyManagedDelivery();
            return;
        }

        if (retireDuringDelivery)
        {
            Environment.SetEnvironmentVariable("DD_DYNAMIC_INSTRUMENTATION_ENABLED", null);
            await VerifyRetiredDelivery();
            return;
        }

        Tracer.Configure(ReplacementSettings(agentUrl, oldService, remoteConfigEnabled: true, featureFlagsEnabled: true));
        var first = await Next(oldService);
        Require(AdvertisesProduct(first, RcmProducts.FfeFlags), "The original tracer did not advertise Feature Flags");
        first.Completion.SetResult(new Reply(200, WatchAssignment(1, ("old", 1, tagged))));

        var pending = await Next(oldService);
        Require(Tracer.Instance.CurrentTraceSettings.Settings.GlobalSamplingRate == 0.37, "The tracer did not apply the tagged configuration");
        var tags = pending.Request.GetProperty("client").GetProperty("client_tracer").GetProperty("tags");
        Require(tags.EnumerateArray().Any(tag => tag.GetString() == "proof:enabled"), "The next poll omitted the tracing tag");
        VerifyReceipt(pending, "old", 2, version: 1);

        var headers = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        pending.Completion.SetResult(new Reply(200, WatchAssignment(2, ("late", 1, late)), headers));
        await headers.Task.WaitAsync(timeout.Token);
        using (var trace = Tracer.Instance.StartActive("replacement.flush"))
        {
            trace.Span.SetTag(Tags.ManualKeep, "true");
        }

        var flushing = Tracer.Instance.FlushAsync();
        try
        {
            await traceReceived.Task.WaitAsync(timeout.Token);
            Require(!flushing.IsCompleted, "The Agent did not hold the old trace flush");
            Tracer.Configure(ReplacementSettings(agentUrl, "replacement-disabled", remoteConfigEnabled: false));
            var settings = ReplacementSettings(agentUrl, newService, remoteConfigEnabled: true, featureFlagsEnabled: false);
            var beforeRetarget = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var releaseRetarget = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var holdRetarget = settings.Manager.SubscribeToChanges(changes =>
            {
                if (changes.UpdatedMutable?.DefaultServiceName == "retargeted")
                {
                    beforeRetarget.TrySetResult();
                    releaseRetarget.Task.WaitAsync(timeout.Token).GetAwaiter().GetResult();
                }
            });
            Tracer.Configure(settings);

            await pending.Disconnected.WaitAsync(timeout.Token);
            Require(!flushing.IsCompleted, "The old config worker waited for the trace flush");
            var replacement = await Next(newService);
            Require(!AdvertisesProduct(replacement, RcmProducts.FfeFlags), "The replacement advertised the retired Feature Flags subscription");
            Require(ConfigStates(replacement).GetArrayLength() == 0, "The replacement inherited the old client's receipt");
            Require(Tracer.Instance.CurrentTraceSettings.Settings.GlobalSamplingRate == 1.0, "The old configuration applied to the replacement");
            replacement.Completion.SetResult(new Reply(200, WatchAssignment(1, ("new", 1, updated))));
            var acknowledged = await Next(newService);
            VerifyReceipt(acknowledged, "new", 2, version: 1);
            Require(Tracer.Instance.CurrentTraceSettings.Settings.GlobalSamplingRate == 0.54, "The replacement did not apply its configuration");

            var subscriptions = (RcmSubscriptionManager)RcmSubscriptionManager.Instance;
            if (subscriptions.GetSnapshotSubscription(Product) is not { InstallSnapshot: { } install } original)
            {
                throw new InvalidOperationException("The replacement did not register its APM watch");
            }

            var reachedInstall = new TaskCompletionSource<Thread>(TaskCreationOptions.RunContinuationsAsynchronously);
            var observing = new Subscription(original.Invoke, Product)
            {
                InstallSnapshot = (files, rejections, generation, tryCommit) =>
                {
                    if (files.Exists(file => file.Path.Id == "obsolete"))
                    {
                        reachedInstall.TrySetResult(Thread.CurrentThread);
                    }

                    return install(files, rejections, generation, tryCommit);
                },
            };
            subscriptions.Unsubscribe(original);
            subscriptions.SubscribeToChanges(observing);
            try
            {
                acknowledged.Completion.SetResult(new Reply(204, []));
                var obsolete = await Next(newService);
                var manual = new ManualInstrumentationConfigurationSource(
                    new Dictionary<string, object?> { ["DD_SERVICE"] = "retargeted" },
                    useDefaultSources: true);
                var retarget = Task.Run(() => settings.Manager.UpdateManualConfigurationSettings(manual, new ConfigurationTelemetry()));
                try
                {
                    await beforeRetarget.Task.WaitAsync(timeout.Token);
                    obsolete.Completion.SetResult(new Reply(200, WatchAssignment(2, ("obsolete", 1, late))));
                    var installerThread = await reachedInstall.Task.WaitAsync(timeout.Token);
                    Require(SpinWait.SpinUntil(() => (installerThread.ThreadState & ThreadState.WaitSleepJoin) != 0, TimeSpan.FromSeconds(5)),
                            "The installer did not reach the held settings lock");
                }
                finally
                {
                    releaseRetarget.TrySetResult();
                    Require(await retarget.WaitAsync(timeout.Token), "The fixture did not retarget the replacement");
                }

                var retargeted = await Next("retargeted");
                Require(Tracer.Instance.CurrentTraceSettings.Settings.GlobalSamplingRate == 0.54, "The obsolete watch changed the retargeted service's sample rate");
                Require(ConfigStates(retargeted).GetArrayLength() == 0, "The retargeted tracer advertised an obsolete receipt");
                retargeted.Completion.SetResult(new Reply(204, []));
                Console.WriteLine("Global tracer replacement passed with the previous flush held and rejected the obsolete watched assignment");
            }
            finally
            {
                subscriptions.Unsubscribe(observing);
            }
        }
        finally
        {
            resumeTrace.TrySetResult();
            await flushing.WaitAsync(TimeSpan.FromSeconds(5));
        }

        async Task<Exchange> Next(string service)
        {
            var exchange = await requests.Reader.ReadAsync(timeout.Token);
            var client = exchange.Request.GetProperty("client");
            var expectedCapabilities = artifact is null ? JsonValueKind.String : JsonValueKind.Array;
            Require(client.GetProperty("capabilities").ValueKind == expectedCapabilities, "The replacement proof used the wrong poller");
            var actual = client.GetProperty("client_tracer").GetProperty("service").GetString();
            Require(actual == service, $"Expected a poll for {service}, received {actual}");
            return exchange;
        }

        async Task VerifyManagedDelivery()
        {
            Tracer.Configure(ReplacementSettings(agentUrl, oldService, remoteConfigEnabled: true));
            var previous = Tracer.Instance.TracerManager;
            var worker = (RemoteConfigurationManager)previous.RemoteConfigurationManager;
            var pending = await Next(oldService);
            var reply = new Reply(200, WatchAssignment(1, ("managed-late", 1, late)));
            using (var trace = Tracer.Instance.StartActive("managed-replacement.flush"))
            {
                trace.Span.SetTag(Tags.ManualKeep, "true");
            }

            var flushing = Tracer.Instance.FlushAsync();
            try
            {
                await traceReceived.Task.WaitAsync(timeout.Token);
                Require(!flushing.IsCompleted, "The Agent did not hold the original trace flush");
                // Tracer.Configure publishes new settings before starting the successor's poller.
                TracerManager.ReplaceGlobalManager(ReplacementSettings(agentUrl, newService, remoteConfigEnabled: true), TracerManagerFactory.Instance);
                var successor = Tracer.Instance.TracerManager;
                Require(!ReferenceEquals(successor, previous), "The fixture did not publish a successor");

                pending.Completion.SetResult(reply);
                resumeTrace.TrySetResult();
                await worker.StopRequested.WaitAsync(timeout.Token);
                await worker.StopAsync().WaitAsync(timeout.Token);
                Require(previous.PerTraceSettings.Settings.GlobalSamplingRate == 1.0, "The retired tracer applied the managed update");
                Require(successor.PerTraceSettings.Settings.GlobalSamplingRate == 1.0, "The successor applied another worker's response");

                successor.Start();
                var first = await Next(newService);
                Require(ConfigStates(first).GetArrayLength() == 0, "The successor acknowledged another worker's response");
                Require(first.Request.GetProperty("cached_target_files").GetArrayLength() == 0, "The successor inherited files from a discarded response");
                first.Completion.SetResult(reply);
                var acknowledged = await Next(newService);
                VerifyReceipt(acknowledged, "managed-late", 2, version: 1);
                Require(successor.PerTraceSettings.Settings.GlobalSamplingRate == 0.81, "The successor did not install its own response");
                acknowledged.Completion.SetResult(new Reply(204, []));
                Console.WriteLine("The managed successor installed and acknowledged the assignment from its own poll");
            }
            finally
            {
                pending.Completion.TrySetResult(new Reply(204, []));
                resumeTrace.TrySetResult();
                await flushing.WaitAsync(timeout.Token);
            }
        }

        async Task VerifyRetiredDelivery()
        {
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var deliveredAfterRetirement = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var raw = new Subscription(async (upserts, _) =>
            {
                if (!upserts.TryGetValue(Product, out var files))
                {
                    return [];
                }

                if (files.Any(file => file.Path.Id == "retired"))
                {
                    entered.TrySetResult();
                    await release.Task.WaitAsync(timeout.Token);
                }

                return files.Select(file => ApplyDetails.FromOk(file.Path.Path)).ToArray();
            }, Product);
            var following = new Subscription((upserts, _) =>
            {
                if (upserts.TryGetValue(Product, out var files) && files.Any(file => file.Path.Id == "retired"))
                {
                    deliveredAfterRetirement.TrySetResult();
                }

                return Array.Empty<ApplyDetails>();
            }, Product);

            // Subscribe before the tracer so the old delivery pauses before its APM callback.
            RcmSubscriptionManager.Instance.SubscribeToChanges(raw);
            RcmSubscriptionManager.Instance.SubscribeToChanges(following);
            try
            {
                Tracer.Configure(ReplacementSettings(agentUrl, oldService, remoteConfigEnabled: true));
                var oldWorker = (RemoteConfigurationManager)Tracer.Instance.TracerManager.RemoteConfigurationManager;
                var initial = await Next(oldService);
                const string currentConfig = """{"lib_config":{"code_origin_enabled":true,"dynamic_instrumentation_enabled":true}}""";
                initial.Completion.SetResult(new Reply(200, WatchAssignment(1, ("old", 1, currentConfig))));
                var pending = await Next(oldService);
                VerifyReceipt(pending, "old", 2, version: 1);
                var debuggerSettings = DebuggerManager.Instance.DebuggerSettings.DynamicSettings;
                Require(debuggerSettings.CodeOriginEnabled == true, "The current tracer did not apply its debugger setting");
                const string retiredConfig = """{"lib_config":{"tracing_sampling_rate":0.37,"code_origin_enabled":false}}""";
                pending.Completion.SetResult(new Reply(200, WatchAssignment(2, ("retired", 1, retiredConfig))));
                await entered.Task.WaitAsync(timeout.Token);

                Tracer.Configure(ReplacementSettings(agentUrl, newService, remoteConfigEnabled: false));
                release.SetResult();
                await oldWorker.StopAsync().WaitAsync(timeout.Token);
                Require(!deliveredAfterRetirement.Task.IsCompleted, "The retired worker invoked another raw subscriber");
                Require(Tracer.Instance.CurrentTraceSettings.Settings.GlobalSamplingRate == 1.0, "A retired subscriber updated the replacement's sampling rate");
                Require(DebuggerManager.Instance.DebuggerSettings.DynamicSettings == debuggerSettings, "A retired subscriber updated the process debugger settings");

                // The Agent advertises no debugger endpoint, so activation cannot finish before replacement.
                Tracer.Configure(ReplacementSettings(agentUrl, newService, remoteConfigEnabled: true));
                var owner = Tracer.Instance.TracerManager;
                var debugger = DebuggerManager.Instance;
                Require(debugger.NeedsDynamicInstrumentationActivation(owner, debugger.DebuggerSettings, debuggerSettings.DynamicInstrumentationEnabled), "The retired activation satisfied the replacement");
                var first = await Next(newService);
                first.Completion.SetResult(new Reply(200, WatchAssignment(1, ("current", 1, currentConfig))));
                var acknowledged = await Next(newService);
                VerifyReceipt(acknowledged, "current", 2, version: 1);
                Require(!debugger.NeedsDynamicInstrumentationActivation(owner, debugger.DebuggerSettings, debuggerSettings.DynamicInstrumentationEnabled), "The replacement did not retry the same debugger settings");
                acknowledged.Completion.SetResult(new Reply(204, []));
                Tracer.Configure(ReplacementSettings(agentUrl, newService, remoteConfigEnabled: false));
                Console.WriteLine("The retired callback left settings intact and the successor retried activation");
            }
            finally
            {
                release.TrySetResult();
                RcmSubscriptionManager.Instance.Unsubscribe(raw);
                RcmSubscriptionManager.Instance.Unsubscribe(following);
            }
        }
    }

    public static async Task VerifyDebuggerExporterPublication()
    {
        const string configurationEndpoint = "/v0.7/config";
        const string debuggerEndpoint = "/debugger/v1/input";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var settings = ReplacementSettings("http://127.0.0.1:8126", "debugger-cutover", remoteConfigEnabled: true).Manager;
        var original = settings.InitialExporterSettings;
        var discovery = new DebuggerTakeoverDiscovery(debuggerEndpoint, original, configurationEndpoint);
        discovery.PublishReadyRoute();

        var published = new TaskCompletionSource<ExporterSettings>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var pause = settings.SubscribeToChanges(changes =>
        {
            if (changes.UpdatedExporter is { } exporter)
            {
                published.TrySetResult(exporter);
                resume.Task.WaitAsync(timeout.Token).GetAwaiter().GetResult();
            }
        });
        using var transport = new DebuggerAgentTransport(settings, discovery, CreateDebuggerRequestFactory);
        Require(transport.TryRunWithRemoteConfigRoute(static () => { }), "The initial Debugger route was not ready");

        var manual = new ManualInstrumentationConfigurationSource(
            new Dictionary<string, object?> { ["DD_TRACE_AGENT_URL"] = "http://127.0.0.1:18126" }, useDefaultSources: true);
        var updated = Task.Run(() => settings.UpdateManualConfigurationSettings(manual, new ConfigurationTelemetry()));
        try
        {
            var next = await published.Task.WaitAsync(timeout.Token);
            Require(ReferenceEquals(transport.Current?.Configuration.DiscoverySettings, original), "The Debugger transport was updated before the fixture paused notification");

            var applied = false;
            var accepted = transport.TryRunWithRemoteConfigRoute(() => applied = true);
            Require(!accepted && !applied, "Debugger takeover admitted the old exporter after the new exporter was published");
            var ready = transport.WaitForRemoteConfigRouteAsync();
            Require(!ready.IsCompleted, "A stale Debugger route satisfied readiness");

            discovery.PublishConfigurationOnlyRoute(next);
            resume.TrySetResult();
            Require(await updated.WaitAsync(timeout.Token), "The exporter settings did not change");
            Require(!ready.IsCompleted, "A config-only Agent satisfied Debugger readiness");

            discovery.PublishReadyRoute(next);
            Require(await ready.WaitAsync(timeout.Token), "The published exporter did not become ready");
            Require(transport.TryRunWithRemoteConfigRoute(() => applied = true) && applied, "The current ready route did not admit takeover");
            Console.WriteLine("Debugger takeover waited for discovery on the published exporter");
        }
        finally
        {
            resume.TrySetResult();
            await updated.WaitAsync(timeout.Token);
        }
    }

    public static async Task VerifyDebuggerDiscoveryPublication()
    {
        const string configurationEndpoint = "/v0.7/config";
        const string debuggerEndpoint = "/debugger/v1/input";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var readyEndpoints = new[] { configurationEndpoint, debuggerEndpoint };
        var configOnlyEndpoints = new[] { configurationEndpoint };
        var endpoints = new[] { debuggerEndpoint };
        await using var agent = CreateAgent(configurationEndpoint, socketPath: null, Channel.CreateUnbounded<Exchange>().Writer, timeout.Token);
        agent.MapGet("/info", () => Results.Json(new { endpoints = Volatile.Read(ref endpoints) }));
        await agent.StartAsync(timeout.Token);

        var settings = ReplacementSettings(agent.Urls.Single(), "debugger-discovery", remoteConfigEnabled: true).Manager;
        var exporter = settings.InitialExporterSettings;
        await using var discovery = CreateManualDiscovery(exporter);
        await discovery.RunOneIterationAsync(null).WaitAsync(timeout.Token);

        var published = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pauseWithdrawal = false;
        Action<AgentConfiguration> pause = configuration =>
        {
            if (Volatile.Read(ref pauseWithdrawal) && configuration.ConfigurationEndpoint is not null && configuration.DebuggerEndpoint is null)
            {
                published.TrySetResult();
                resume.Task.WaitAsync(timeout.Token).GetAwaiter().GetResult();
            }
        };
        discovery.SubscribeToChanges(pause);
        using var transport = new DebuggerAgentTransport(settings, discovery, CreateDebuggerRequestFactory);
        var initialReady = transport.WaitForRemoteConfigRouteAsync();
        Require(!initialReady.IsCompleted, "Debugger-only discovery satisfied takeover readiness");
        Volatile.Write(ref endpoints, configOnlyEndpoints);
        await discovery.RunOneIterationAsync(null).WaitAsync(timeout.Token);
        Require(!initialReady.IsCompleted, "Remote Configuration-only discovery satisfied takeover readiness");
        Volatile.Write(ref endpoints, readyEndpoints);
        await discovery.RunOneIterationAsync(null).WaitAsync(timeout.Token);
        Require(await initialReady.WaitAsync(timeout.Token), "Discovery of both endpoints did not satisfy takeover readiness");
        Require(transport.TryRunWithRemoteConfigRoute(static () => { }), "The initial Debugger route was not ready");
        var original = transport.Current?.Configuration;

        Volatile.Write(ref pauseWithdrawal, true);
        Volatile.Write(ref endpoints, configOnlyEndpoints);
        var refresh = Task.Run(() => discovery.RunOneIterationAsync(null));
        try
        {
            await published.Task.WaitAsync(timeout.Token);
            Require(ReferenceEquals(transport.Current?.Configuration, original), "The Debugger transport was updated before the fixture paused notification");

            var applied = false;
            var accepted = transport.TryRunWithRemoteConfigRoute(() => applied = true);
            Require(!accepted && !applied, "Debugger takeover admitted a withdrawn discovery snapshot");
            var ready = transport.WaitForRemoteConfigRouteAsync();
            Require(!ready.IsCompleted, "A withdrawn Debugger route satisfied readiness");

            resume.TrySetResult();
            await refresh.WaitAsync(timeout.Token);
            Require(!ready.IsCompleted, "A config-only Agent satisfied Debugger readiness");

            Volatile.Write(ref endpoints, readyEndpoints);
            await discovery.RunOneIterationAsync(null).WaitAsync(timeout.Token);
            Require(await ready.WaitAsync(timeout.Token), "The restored endpoints did not become ready");
            Require(transport.TryRunWithRemoteConfigRoute(() => applied = true) && applied, "The restored route did not admit takeover");
            Console.WriteLine("Debugger takeover waited for the current discovery endpoints");
        }
        finally
        {
            resume.TrySetResult();
            discovery.RemoveSubscription(pause);
            await refresh.WaitAsync(timeout.Token);
        }
    }

    public static async Task VerifyRawApmPublication(bool native, string artifact)
    {
        const string endpoint = "/v0.7/config";
        const string obsolete = """{"lib_config":{"tracing_sampling_rate":0.42}}""";
        const string current = """{"lib_config":{"tracing_enabled":true}}""";
        AppDomain.CurrentDomain.SetData(NativeAgentPoller.LibraryPathSetting, native ? artifact : null);
        Environment.SetEnvironmentVariable("DD_REMOTE_CONFIG_POLL_INTERVAL_SECONDS", "0.05");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var requests = Channel.CreateUnbounded<Exchange>();
        var endpoints = new[] { endpoint };
        await using var agent = CreateAgent(endpoint, null, requests.Writer, timeout.Token);
        agent.MapGet("/info", () => Results.Json(new { endpoints = Volatile.Read(ref endpoints) }));
        await agent.StartAsync(timeout.Token);

        var tracerSettings = ReplacementSettings(agent.Urls.Single(), "raw-apm-publication", remoteConfigEnabled: true);
        await using var discovery = CreateManualDiscovery(tracerSettings.Manager.InitialExporterSettings);
        await discovery.RunOneIterationAsync(null).WaitAsync(timeout.Token);
        TracerManager.ReplaceGlobalManager(tracerSettings, new DiscoveryTracerManagerFactory(discovery));
        var owner = Tracer.Instance.TracerManager;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            owner.Start();
            var first = await requests.Reader.ReadAsync(timeout.Token);
            VerifyPollBackend(first, native);

            var subscriptions = (RcmSubscriptionManager)RcmSubscriptionManager.Instance;
            if (subscriptions.GetSnapshotSubscription(Product) is not { InstallChanges: { } install } original)
            {
                throw new InvalidOperationException("The tracer did not register guarded APM delivery");
            }

            var observing = new Subscription(original.Invoke, Product)
            {
                InstallChanges = (upserts, removals, tryCommit) =>
                {
                    var paused = upserts.TryGetValue(Product, out var files) && files.Exists(file => file.Path.Id == "obsolete");
                    if (paused)
                    {
                        entered.TrySetResult();
                        resume.Task.WaitAsync(timeout.Token).GetAwaiter().GetResult();
                    }

                    try
                    {
                        return install(upserts, removals, tryCommit);
                    }
                    finally
                    {
                        if (paused)
                        {
                            completed.TrySetResult();
                        }
                    }
                },
            };
            subscriptions.Replace(original, observing);
            first.Completion.SetResult(new Reply(204, []));

            var pending = await requests.Reader.ReadAsync(timeout.Token);
            pending.Completion.SetResult(new Reply(200, WatchAssignment(1, ("obsolete", 1, obsolete))));
            await entered.Task.WaitAsync(timeout.Token);
            Volatile.Write(ref endpoints, ["/debugger/v1/input"]);
            await discovery.RunOneIterationAsync(null).WaitAsync(timeout.Token);
            Require(discovery.CurrentConfiguration?.ConfigurationEndpoint is null, "Discovery did not withdraw the Remote Configuration route");
            resume.TrySetResult();
            await completed.Task.WaitAsync(timeout.Token);
            Require(owner.PerTraceSettings.Settings.GlobalSamplingRate == 1.0, "Raw APM published a sampling rate after route withdrawal");

            Volatile.Write(ref endpoints, [endpoint]);
            await discovery.RunOneIterationAsync(null).WaitAsync(timeout.Token);
            var restored = await requests.Reader.ReadAsync(timeout.Token);
            restored.Completion.SetResult(new Reply(200, WatchAssignment(2, ("current", 2, current))));
            var acknowledged = await requests.Reader.ReadAsync(timeout.Token);
            acknowledged.Completion.SetResult(new Reply(204, []));
            VerifyReceipt(acknowledged, "current", 2, version: 2);
            Require(owner.PerTraceSettings.Settings.GlobalSamplingRate == 1.0, "Raw APM retained an assignment that never committed");
        }
        finally
        {
            resume.TrySetResult();
            await owner.ShutdownAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }

        Console.WriteLine($"Raw APM ignored settings after route withdrawal with the {(native ? "native" : "managed")} worker");
    }

    public static async Task VerifyRemoteConfigRouteWithdrawal(string backend, string artifact)
    {
        const string endpoint = "/v0.7/config";
        const string debuggerEndpoint = "/debugger/v1/input";
        AppDomain.CurrentDomain.SetData(NativeAgentPoller.LibraryPathSetting, backend == "managed" ? null : artifact);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var endpoints = new[] { endpoint, debuggerEndpoint };
        var requests = Channel.CreateUnbounded<Exchange>();
        await using var agent = CreateAgent(endpoint, socketPath: null, requests.Writer, timeout.Token);
        agent.MapGet("/info", () => Results.Json(new { endpoints = Volatile.Read(ref endpoints) }));
        await agent.StartAsync(timeout.Token);

        var tracerSettings = ReplacementSettings(agent.Urls.Single(), "test", remoteConfigEnabled: true);
        await using var discovery = CreateManualDiscovery(tracerSettings.Manager.InitialExporterSettings);
        await discovery.RunOneIterationAsync(null).WaitAsync(timeout.Token);
        var published = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        discovery.SubscribeToChanges(configuration =>
        {
            if (configuration.ConfigurationEndpoint is null)
            {
                published.TrySetResult();
                resume.Task.WaitAsync(timeout.Token).GetAwaiter().GetResult();
            }
        });
        var observedDiscovery = new ObservedWithdrawal(discovery);
        var firstDelivery = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var staleDelivery = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var watchCommit = backend == "watch-commit";
        var pauseCommit = false;
        var commitReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumeCommit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var commitResult = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var settingsSubscription = watchCommit ? tracerSettings.Manager.SubscribeToChanges(_ => staleDelivery.TrySetResult()) : null;
        var deliveries = 0;
        var subscriptions = new RcmSubscriptionManager();
        subscriptions.SubscribeToChanges(new Subscription(
            (upserts, _) =>
            {
                if (!upserts.TryGetValue(Product, out var files))
                {
                    return [];
                }

                RecordDelivery();
                return files.Select(file => ApplyDetails.FromOk(file.Path.Path)).ToArray();
            },
            Product)
        {
            InstallSnapshot = backend is "watch" or "watch-commit"
                                  ? (files, _, _, tryCommit) =>
                                  {
                                      if (Volatile.Read(ref pauseCommit))
                                      {
                                          var payload = Encoding.UTF8.GetString(files.Single().Contents);
                                          var configuration = new DynamicConfigConfigurationSource(payload, ConfigurationOrigins.RemoteConfig);
                                          var accepted = tracerSettings.Manager.TryUpdateDynamicConfigurationSettings(configuration, new ConfigurationTelemetry(), PauseBeforePublication, out _);
                                          commitResult.TrySetResult(accepted);
                                          return accepted;
                                      }

                                      return tryCommit(() =>
                                      {
                                          if (files.Count > 0)
                                          {
                                              RecordDelivery();
                                          }
                                      });

                                      bool PauseBeforePublication(Action publishSettings)
                                      {
                                          commitReady.TrySetResult();
                                          resumeCommit.Task.WaitAsync(timeout.Token).GetAwaiter().GetResult();
                                          return tryCommit(publishSettings);
                                      }
                                  }
                                  : null,
        });

        var telemetry = new ConfigurationTelemetry();
        var source = new DictionaryConfigurationSource(new Dictionary<string, string> { ["DD_REMOTE_CONFIG_POLL_INTERVAL_SECONDS"] = "0.05" });
        var manager = RemoteConfigurationManager.Create(
            observedDiscovery, RemoteConfigurationSettings.FromSource(source, telemetry), tracerSettings, new NullGitMetadataProvider(), subscriptions);
        manager.Start();
        Task<int?>? withdrawal = null;
        Task? stopping = null;
        try
        {
            var first = await requests.Reader.ReadAsync(timeout.Token);
            first.Completion.SetResult(new Reply(200, AgentComparison.CreateFixture(1, 0, 1).Response));
            await firstDelivery.Task.WaitAsync(timeout.Token);

            var pending = await requests.Reader.ReadAsync(timeout.Token);
            var firstState = ConfigStates(pending).EnumerateArray().Single();
            Require(firstState.GetProperty("apply_state").GetInt32() == 2, "The first reply was not acknowledged");
            if (watchCommit)
            {
                const string payload = """{"lib_config":{"tracing_sampling_rate":0.42}}""";
                Volatile.Write(ref pauseCommit, true);
                pending.Completion.SetResult(new Reply(200, WatchAssignment(2, ("watch-commit", 2, payload))));
                await commitReady.Task.WaitAsync(timeout.Token);
            }

            Volatile.Write(ref endpoints, [debuggerEndpoint]);
            withdrawal = Task.Run(() => discovery.RunOneIterationAsync(null));
            await published.Task.WaitAsync(timeout.Token);

            if (watchCommit)
            {
                resumeCommit.TrySetResult();
                var accepted = await commitResult.Task.WaitAsync(timeout.Token);
                Require(!staleDelivery.Task.IsCompleted, "The APM watch published settings after its route was withdrawn");
                Require(!accepted, "The APM watch accepted settings after its route was withdrawn");
            }
            else
            {
                pending.Completion.SetResult(new Reply(200, AgentComparison.CreateFixture(1, 0, 2).Response));
                var outcome = await Task.WhenAny(observedDiscovery.WorkerPassedWithdrawal.Task, staleDelivery.Task).WaitAsync(timeout.Token);
                Require(outcome != staleDelivery.Task, "Remote Configuration applied a reply after its route was withdrawn");
            }

            stopping = manager.RetireAsync();
            await stopping.WaitAsync(timeout.Token);
            Require(Volatile.Read(ref deliveries) == 1, "Remote Configuration delivered an obsolete revision");
            Require(!requests.Reader.TryRead(out _), "Remote Configuration polled a withdrawn endpoint");
            Console.WriteLine($"Discarded the held {backend} reply after /info withdrew its route");
        }
        finally
        {
            resumeCommit.TrySetResult();
            resume.TrySetResult();
            if (withdrawal is not null)
            {
                await withdrawal.WaitAsync(timeout.Token);
            }

            await (stopping ?? manager.RetireAsync()).WaitAsync(timeout.Token);
        }

        void RecordDelivery()
        {
            if (Interlocked.Increment(ref deliveries) == 1)
            {
                firstDelivery.TrySetResult();
            }
            else
            {
                staleDelivery.TrySetResult();
            }
        }
    }

    public static async Task VerifyDebuggerUploadAdmission(string routeChange)
    {
        const string debuggerEndpoint = "/debugger/v1/input";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var uploads = 0;
        await using var agent = CreateAgent("/unused", socketPath: null, Channel.CreateUnbounded<Exchange>().Writer, timeout.Token);
        agent.MapPost(debuggerEndpoint, () =>
        {
            Interlocked.Increment(ref uploads);
            return Results.Ok();
        });
        await agent.StartAsync(timeout.Token);

        var settings = ReplacementSettings(agent.Urls.Single(), "debugger-upload", remoteConfigEnabled: false).Manager;
        var discovery = new DebuggerTakeoverDiscovery(debuggerEndpoint, settings.InitialExporterSettings, configurationEndpoint: null);
        using var transport = new DebuggerAgentTransport(settings, discovery, CreateDebuggerRequestFactory);
        var data = new ArraySegment<byte>("[]"u8.ToArray());
        var currentTags = new PausedUploadTags(timeout.Token);
        currentTags.Resume.TrySetResult();
        var current = new DiagnosticsUploadApi(transport, currentTags);
        Require(await current.SendBatchAsync(data).WaitAsync(timeout.Token), "A route with only the Debugger endpoint did not accept uploads");
        Require(Volatile.Read(ref uploads) == 1, "The current route did not reach the loopback Agent");

        var pausedTags = new PausedUploadTags(timeout.Token);
        var obsolete = new DiagnosticsUploadApi(transport, pausedTags);
        var pending = Task.Run(() => obsolete.SendBatchAsync(data));
        try
        {
            await pausedTags.Entered.Task.WaitAsync(timeout.Token);
            if (routeChange == "replay")
            {
                discovery.ReplayCurrentSnapshot();
            }
            else if (routeChange == "dispose")
            {
                transport.Dispose();
            }
            else
            {
                Require(MoveDebuggerExporter(settings), "The exporter settings did not change");
            }

            pausedTags.Resume.TrySetResult();
            var sent = await pending.WaitAsync(timeout.Token);
            if (routeChange == "replay")
            {
                Require(sent && Volatile.Read(ref uploads) == 2, "Replaying discovery dropped an upload on the current route");
                Console.WriteLine("Debugger upload admission preserved the replayed route");
            }
            else
            {
                Require(!sent && Volatile.Read(ref uploads) == 1, "Debugger upload started after its route retired");
                Console.WriteLine("Debugger upload admission rejected the retired route");
            }
        }
        finally
        {
            pausedTags.Resume.TrySetResult();
            await pending.WaitAsync(timeout.Token);
        }
    }

    public static async Task VerifyDebuggerUploadBlockedConnect()
    {
        const string debuggerEndpoint = "/debugger/v1/input";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var agent = CreateAgent("/unused", socketPath: null, Channel.CreateUnbounded<Exchange>().Writer, timeout.Token);
        agent.MapPost(debuggerEndpoint, async (HttpContext context) =>
        {
            await context.Request.Body.CopyToAsync(Stream.Null, timeout.Token);
            context.Response.ContentLength = 2;
            context.Response.Headers["Connection"] = "close";
            await context.Response.WriteAsync("{}", timeout.Token);
        });
        await agent.StartAsync(timeout.Token);

        var address = new Uri(agent.Urls.Single());
        var connections = new PausedUploadConnection(address.Port, timeout.Token);
        var factory = new HttpStreamRequestFactory(connections, new DatadogHttpClient(MinimalAgentHeaderHelper.Instance), address);
        var settings = ReplacementSettings(address.ToString(), "debugger-connect", remoteConfigEnabled: false).Manager;
        var discovery = new DebuggerTakeoverDiscovery(debuggerEndpoint, settings.InitialExporterSettings, configurationEndpoint: null);
        using var transport = new DebuggerAgentTransport(settings, discovery, _ => factory);
        var tags = new PausedUploadTags(timeout.Token);
        tags.Resume.TrySetResult();
        var uploader = new DiagnosticsUploadApi(transport, tags);
        var pending = Task.Run(() => uploader.SendBatchAsync(new ArraySegment<byte>("[]"u8.ToArray())));
        Task<bool>? moved = null;
        try
        {
            await connections.Entered.Task.WaitAsync(timeout.Token);
            moved = Task.Run(() => MoveDebuggerExporter(settings));
            bool changed;
            try
            {
                changed = await moved.WaitAsync(TimeSpan.FromSeconds(3), timeout.Token);
            }
            catch (TimeoutException exception)
            {
                throw new InvalidOperationException("Debugger connection blocked exporter publication", exception);
            }

            Require(changed && transport.Current is null, "The exporter was not moved while the connection waited");
            connections.Resume.TrySetResult();
            Require(await pending.WaitAsync(timeout.Token), "The admitted upload did not finish after the exporter moved");
            Console.WriteLine("Debugger connection setup left exporter publication available");
        }
        finally
        {
            connections.Resume.TrySetResult();
            if (moved is not null)
            {
                await moved.WaitAsync(timeout.Token);
            }

            await pending.WaitAsync(timeout.Token);
        }
    }

    private static bool MoveDebuggerExporter(TracerSettings.SettingsManager settings)
    {
        var manual = new ManualInstrumentationConfigurationSource(
            new Dictionary<string, object?> { ["DD_TRACE_AGENT_URL"] = "http://127.0.0.1:18126" }, useDefaultSources: true);
        return settings.UpdateManualConfigurationSettings(manual, new ConfigurationTelemetry());
    }

    private static IApiRequestFactory CreateDebuggerRequestFactory(ExporterSettings exporter)
        => AgentTransportStrategy.Get(exporter, "debugger", TimeSpan.FromSeconds(2), MinimalAgentHeaderHelper.Instance);

    private static DiscoveryService CreateManualDiscovery(ExporterSettings exporter)
        => new(CreateDebuggerRequestFactory(exporter), new ServiceRemappingHash(null), 1, 1, 1, autoStartLoop: false, exporterSettings: exporter);

    public static async Task VerifyDebuggerTakeover(string artifact, bool splitRoute, bool changeRouteOnTeardown)
    {
        const string endpoint = "/v0.7/config";
        const string debuggerEndpoint = "/debugger/v1/input";
        const string enabled = """{"lib_config":{"dynamic_instrumentation_enabled":true}}""";
        AppDomain.CurrentDomain.SetData(NativeAgentPoller.LibraryPathSetting, artifact);
        Environment.SetEnvironmentVariable("DD_REMOTE_CONFIG_POLL_INTERVAL_SECONDS", "0.05");
        Environment.SetEnvironmentVariable("DD_DYNAMIC_INSTRUMENTATION_ENABLED", "true");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var requests = Channel.CreateUnbounded<Exchange>();
        var configOnlyRequests = Channel.CreateUnbounded<Exchange>();
        await using var oldAgent = CreateAgent(endpoint, socketPath: null, requests.Writer, timeout.Token);
        await using var configOnlyAgent = CreateAgent(endpoint, socketPath: null, configOnlyRequests.Writer, timeout.Token);
        await using var newAgent = CreateAgent(endpoint, socketPath: null, requests.Writer, timeout.Token);
        oldAgent.MapGet("/info", () => Results.Json(new { endpoints = new[] { debuggerEndpoint } }));
        configOnlyAgent.MapGet("/info", () => Results.Json(new { endpoints = new[] { endpoint } }));
        newAgent.MapGet("/info", () => Results.Json(new { endpoints = new[] { endpoint, debuggerEndpoint } }));
        await Task.WhenAll(oldAgent.StartAsync(timeout.Token), configOnlyAgent.StartAsync(timeout.Token), newAgent.StartAsync(timeout.Token));

        var probeFile = Path.GetTempFileName();
        try
        {
            const string probe = """[{"id":"file-only","language":"dotnet","type":"LOG_PROBE","where":{"sourceFile":"Missing.cs","lines":["1"]}}]""";
            await File.WriteAllTextAsync(probeFile, probe, timeout.Token);
            Environment.SetEnvironmentVariable("DD_DYNAMIC_INSTRUMENTATION_PROBE_FILE", probeFile);
            var fileSettings = ReplacementSettings(oldAgent.Urls.Single(), "file-probes", remoteConfigEnabled: false);
            DebuggerTakeoverDiscovery? fileDiscovery = null;
            if (changeRouteOnTeardown)
            {
                fileDiscovery = new DebuggerTakeoverDiscovery(debuggerEndpoint, fileSettings.Manager.InitialExporterSettings, configurationEndpoint: null);
                TracerManager.ReplaceGlobalManager(fileSettings, new DiscoveryTracerManagerFactory(fileDiscovery));
            }
            else
            {
                Tracer.Configure(fileSettings);
            }

            var manager = DebuggerManager.Instance;
            var startup = manager.UpdateConfiguration(Tracer.Instance.Settings);
            await WaitForDebugger(() => manager.DynamicInstrumentation is not null, startup, timeout.Token);
            var fileRuntime = manager.DynamicInstrumentation;

            var subscriptions = (RcmSubscriptionManager)RcmSubscriptionManager.Instance;
            Require(!subscriptions.ProductKeys.Contains(RcmProducts.LiveDebugging), "The file-only runtime registered without an RC endpoint");

            Tracer.Configure(ReplacementSettings(configOnlyAgent.Urls.Single(), "config-only", remoteConfigEnabled: true));
            var configOnly = await configOnlyRequests.Reader.ReadAsync(timeout.Token);
            configOnly.Completion.SetResult(new Reply(200, WatchAssignment(1, ("enable-debugger", 1, enabled))));
            for (var poll = 0; poll < 12; poll++)
            {
                var exchange = await configOnlyRequests.Reader.ReadAsync(timeout.Token);
                exchange.Completion.SetResult(new Reply(204, []));
                if (poll == 0)
                {
                    VerifyReceipt(exchange, "enable-debugger", 2, version: 1);
                }

                Require(!AdvertisesProduct(exchange, RcmProducts.LiveDebugging), "Live Debugging registered without a debugger endpoint");
                Require(ReferenceEquals(manager.DynamicInstrumentation, fileRuntime), "Replacing the tracer disabled file probes without an available debugger endpoint");
            }

            var rateLimiter = DebuggerGlobalRateLimiter.Instance;
            rateLimiter.SetRate(0);
            Require(!rateLimiter.ShouldSampleSnapshot("file-only"), "The active debugger did not use the zero-sample budget");
            var preparingSettings = ReplacementSettings(newAgent.Urls.Single(), "preparing-takeover", remoteConfigEnabled: true);
            var discovery = new DebuggerTakeoverDiscovery(debuggerEndpoint, preparingSettings.Manager.InitialExporterSettings, splitRoute || changeRouteOnTeardown ? endpoint : null);
            bool? releasedAfterTakeover = null;
            if (fileDiscovery is not null)
            {
                discovery.PublishReadyRoute();
                fileDiscovery.TransportRemoved = () =>
                {
                    releasedAfterTakeover = manager.DynamicInstrumentation is { IsRegisteredForRemoteConfiguration: true } active && !ReferenceEquals(active, fileRuntime);
                    discovery.PublishConfigurationOnlyRoute();
                };
            }

            TracerManager.ReplaceGlobalManager(preparingSettings, new DiscoveryTracerManagerFactory(discovery));
            var preparingOwner = Tracer.Instance.TracerManager;
            var takeover = manager.UpdateConfiguration(preparingOwner.Settings, owner: preparingOwner);
            await discovery.CandidateCreated.WaitAsync(timeout.Token);
            if (fileDiscovery is not null)
            {
                await takeover.WaitAsync(timeout.Token);
                Require(releasedAfterTakeover is true, "The old Debugger transport changed discovery before takeover committed");
                await preparingOwner.ShutdownAsync().WaitAsync(timeout.Token);
                Console.WriteLine("The Debugger committed takeover before releasing the old transport");
                return;
            }

            if (splitRoute)
            {
                preparingOwner.Start();
                for (var poll = 0; poll < 12; poll++)
                {
                    var exchange = await requests.Reader.ReadAsync(timeout.Token);
                    exchange.Completion.SetResult(new Reply(204, []));
                    Require(!AdvertisesProduct(exchange, RcmProducts.LiveDebugging), "Live Debugging registered using endpoints from separate discoveries");
                    Require(ReferenceEquals(manager.DynamicInstrumentation, fileRuntime), "Separate discovery results retired the file debugger");
                }

                discovery.PublishReadyRoute();
                await takeover.WaitAsync(timeout.Token);
                Require(manager.DynamicInstrumentation is { IsRegisteredForRemoteConfiguration: true } active && !ReferenceEquals(active, fileRuntime),
                        "The ready route did not take over the file debugger");
                await preparingOwner.ShutdownAsync().WaitAsync(timeout.Token);
                Console.WriteLine("The file debugger stayed active until the current discovery advertised both endpoints");
                return;
            }

            Require(!takeover.IsCompleted, "The candidate did not wait for Remote Config discovery");

            Tracer.Configure(ReplacementSettings(oldAgent.Urls.Single(), "disabled-successor", remoteConfigEnabled: false));
            await takeover.WaitAsync(timeout.Token);
            Require(ReferenceEquals(manager.DynamicInstrumentation, fileRuntime), "Canceling a takeover while it prepared removed the file debugger");
            Require(!subscriptions.ProductKeys.Contains(RcmProducts.LiveDebugging), "The canceled candidate registered Live Debugging");
            Require(!rateLimiter.ShouldSampleSnapshot("file-only"), "The canceled candidate reset the running debugger's sampling budget");

            Tracer.Configure(ReplacementSettings(newAgent.Urls.Single(), "remote-probes", remoteConfigEnabled: true));
            var first = await requests.Reader.ReadAsync(timeout.Token);
            Require(!AdvertisesProduct(first, RcmProducts.LiveDebugging), "The successor advertised Live Debugging before it applied its settings");
            first.Completion.SetResult(new Reply(200, WatchAssignment(1, ("enable-debugger", 1, enabled))));
            var acknowledged = await requests.Reader.ReadAsync(timeout.Token);
            VerifyReceipt(acknowledged, "enable-debugger", 2, version: 1);
            acknowledged.Completion.SetResult(new Reply(204, []));

            for (var poll = 0; poll < 60; poll++)
            {
                var exchange = await requests.Reader.ReadAsync(timeout.Token);
                exchange.Completion.SetResult(new Reply(204, []));
                if (AdvertisesProduct(exchange, RcmProducts.LiveDebugging))
                {
                    await startup.WaitAsync(timeout.Token);
                    Console.WriteLine("The successor registered Live Debugging after replacing a file-only debugger");
                    return;
                }
            }

            throw new InvalidOperationException("The successor never advertised Live Debugging after replacing a file-only debugger");
        }
        finally
        {
            File.Delete(probeFile);
        }
    }

    public static async Task VerifyDebuggerDisable(string artifact)
    {
        const string endpoint = "/v0.7/config";
        const string disabled = """{"lib_config":{"dynamic_instrumentation_enabled":false}}""";
        AppDomain.CurrentDomain.SetData(NativeAgentPoller.LibraryPathSetting, artifact);
        Environment.SetEnvironmentVariable("DD_REMOTE_CONFIG_POLL_INTERVAL_SECONDS", "0.05");
        Environment.SetEnvironmentVariable("DD_DYNAMIC_INSTRUMENTATION_ENABLED", "true");
        Environment.SetEnvironmentVariable("DD_DYNAMIC_INSTRUMENTATION_PROBE_FILE", null);
        Environment.SetEnvironmentVariable(ConfigurationKeys.Debugger.CodeOriginForSpansEnabled, "false");
        Environment.SetEnvironmentVariable(ConfigurationKeys.Debugger.ExceptionReplayEnabled, "false");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var requests = Channel.CreateUnbounded<Exchange>();
        await using var agent = CreateAgent(endpoint, socketPath: null, requests.Writer, timeout.Token);
        agent.MapGet("/info", () => Results.Json(new { endpoints = new[] { endpoint } }));
        await agent.StartAsync(timeout.Token);
        Tracer.Configure(ReplacementSettings(agent.Urls.Single(), "disable-debugger", remoteConfigEnabled: true));
        var first = await requests.Reader.ReadAsync(timeout.Token);
        Require(!AdvertisesProduct(first, RcmProducts.LiveDebugging), "The Agent did not provide a debugger endpoint");

        var manager = DebuggerManager.Instance;
        var startup = manager.UpdateConfiguration(Tracer.Instance.Settings);
        await WaitForDebugger(() => manager.HasActiveDynamicDebuggerProduct, startup, timeout.Token);
        Require(manager.DynamicInstrumentation is null, "The ownerless startup did not wait for the debugger endpoint");
        first.Completion.SetResult(new Reply(200, WatchAssignment(1, ("disable-debugger", 1, disabled))));
        var acknowledged = await requests.Reader.ReadAsync(timeout.Token);
        try
        {
            VerifyReceipt(acknowledged, "disable-debugger", 2, version: 1);
            await startup.WaitAsync(timeout.Token);
            Require(!AdvertisesProduct(acknowledged, RcmProducts.LiveDebugging), "The ownerless startup registered Live Debugging after disable");
            Require(manager.DebuggerSettings.DynamicSettings.DynamicInstrumentationEnabled == false, "The disable update did not reach the debugger");
            Require(!manager.HasActiveDynamicDebuggerProduct, "The debugger stayed active after disable");
            Console.WriteLine("Remote Config disable canceled ownerless debugger startup while discovery was pending");
        }
        finally
        {
            acknowledged.Completion.SetResult(new Reply(204, []));
        }
    }

    public static async Task VerifyDebuggerStartupReplacement()
    {
        const string configEndpoint = "/v0.7/config";
        const string debuggerEndpoint = "/debugger/v1/input";
        const string uploadEndpoint = "/debugger/v2/input";
        const string marker = "after-startup-replacement";
        Environment.SetEnvironmentVariable("DD_DYNAMIC_INSTRUMENTATION_ENABLED", "true");
        Environment.SetEnvironmentVariable("DD_DYNAMIC_INSTRUMENTATION_PROBE_FILE", null);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        var uploaded = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        await using var agent = builder.Build();
        agent.MapGet("/info", () => Results.Json(new { endpoints = new[] { configEndpoint, debuggerEndpoint, uploadEndpoint } }));
        agent.MapPost(configEndpoint, () => Results.NoContent());
        agent.MapPost(uploadEndpoint, async (HttpContext context) =>
        {
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            uploaded.TrySetResult(await reader.ReadToEndAsync());
            return Results.Ok();
        });
        await agent.StartAsync(timeout.Token);

        var address = agent.Urls.Single();
        Tracer.Configure(ReplacementSettings(address, "debugger-predecessor", remoteConfigEnabled: true));
        var capturedSettings = Tracer.Instance.Settings;
        Tracer.Configure(ReplacementSettings(address, "debugger-successor", remoteConfigEnabled: true));
        var successor = Tracer.Instance.TracerManager;
        try
        {
            var manager = DebuggerManager.Instance;
            // Startup has already captured the predecessor's settings when the replacement occurs.
            await manager.UpdateConfiguration(capturedSettings).WaitAsync(timeout.Token);
            var runtime = manager.DynamicInstrumentation;
            Require(runtime is { IsInitialized: true }, "The debugger did not start on the successor");
            var probe = new ProbeInfo("fixture", 1, ProbeType.Snapshot, default, default, null, string.Empty, false, [], null, default, IsEmitted: true);
            using var creator = new DebuggerSnapshotCreator(
                isFullSnapshot: true, ProbeLocation.Method, hasCondition: false, [], default, DebuggerManager.ProcessTagsProvider, DebuggerManager.ServiceNameProvider);
            creator.StartEntry();
            creator.EndEntry();
            creator.StartReturn();
            creator.EndReturn();
            creator.FinalizeSnapshot(marker, nameof(NativeWorkerProof), null);
            runtime!.AddSnapshot(probe, creator.GetSnapshotJson(), creator.IncompleteReasons);
            using var upload = JsonDocument.Parse(await uploaded.Task.WaitAsync(timeout.Token));
            var snapshot = upload.RootElement.EnumerateArray().Single();
            Require(snapshot.GetProperty("service").GetString() == "debugger-successor", "Debugger labeled the startup snapshot with the predecessor's service");
        }
        finally
        {
            await successor.ShutdownAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }

        Console.WriteLine("Debugger startup uploaded a snapshot after tracer replacement");
    }

    public static async Task VerifyDebuggerAgentChange(string artifact)
    {
        const string configEndpoint = "/v0.7/config";
        const string debuggerEndpoint = "/debugger/v1/input";
        const string uploadEndpoint = "/debugger/v2/input";
        AppDomain.CurrentDomain.SetData(NativeAgentPoller.LibraryPathSetting, artifact);
        Environment.SetEnvironmentVariable("DD_REMOTE_CONFIG_POLL_INTERVAL_SECONDS", "0.05");
        Environment.SetEnvironmentVariable("DD_DYNAMIC_INSTRUMENTATION_ENABLED", null);
        Environment.SetEnvironmentVariable("DD_DYNAMIC_INSTRUMENTATION_PROBE_FILE", null);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var originalPolls = Channel.CreateUnbounded<Exchange>();
        var updatedPolls = Channel.CreateUnbounded<Exchange>();
        var uploads = Channel.CreateUnbounded<(string Agent, string Body)>();
        await using var original = CreateAgent(configEndpoint, socketPath: null, originalPolls.Writer, timeout.Token);
        await using var updated = CreateAgent(configEndpoint, socketPath: null, updatedPolls.Writer, timeout.Token);
        MapAgent(original, "original");
        MapAgent(updated, "updated");
        await Task.WhenAll(original.StartAsync(timeout.Token), updated.StartAsync(timeout.Token));

        var settings = ReplacementSettings(original.Urls.Single(), "debugger-agent-change", remoteConfigEnabled: true);
        var discovery = new DiscoveryService(settings.Manager, ContainerMetadata.Instance, new ServiceRemappingHash(null), TimeSpan.FromSeconds(2), 10, 100, 100);
        TracerManager.ReplaceGlobalManager(settings, new DiscoveryTracerManagerFactory(discovery));
        var owner = Tracer.Instance.TracerManager;
        try
        {
            var originalPoll = await originalPolls.Reader.ReadAsync(timeout.Token);
            await MoveAgent(updated);
            const string superseded = """{"lib_config":{"tracing_sampling_rate":0.81}}""";
            originalPoll.Completion.TrySetResult(new Reply(200, WatchAssignment(1, ("superseded-agent", 1, superseded))));

            var enable = await updatedPolls.Reader.ReadAsync(timeout.Token);
            Require(owner.PerTraceSettings.Settings.GlobalSamplingRate == 1.0, "The old Agent's in-flight response changed current settings");
            Require(ConfigStates(enable).GetArrayLength() == 0, "The new Agent received a receipt from the old Agent's response");
            const string config = """{"lib_config":{"dynamic_instrumentation_enabled":true}}""";
            enable.Completion.SetResult(new Reply(200, WatchAssignment(1, ("debugger-agent-change", 1, config))));
            var debugger = DebuggerManager.Instance;
            while (debugger.DynamicInstrumentation is null)
            {
                var poll = await updatedPolls.Reader.ReadAsync(timeout.Token);
                poll.Completion.SetResult(new Reply(204, []));
            }

            var runtime = debugger.DynamicInstrumentation;
            Require(runtime is { IsInitialized: true }, "The debugger did not start after the first Agent change");
            await SendSnapshot(runtime!, "after-activation", "updated");

            await MoveAgent(original);
            Require(ReferenceEquals(runtime, debugger.DynamicInstrumentation), "Changing the Agent replaced the debugger runtime");
            await SendSnapshot(runtime!, "after-retarget", "original");
        }
        finally
        {
            await owner.ShutdownAsync().WaitAsync(TimeSpan.FromSeconds(5));
        }

        Console.WriteLine("Debugger snapshots reached the current Agent before and after activation");

        void MapAgent(WebApplication agent, string name)
        {
            agent.MapGet("/info", () => Results.Json(new { endpoints = new[] { configEndpoint, debuggerEndpoint, uploadEndpoint } }));
            agent.MapPost(uploadEndpoint, async (HttpContext http) =>
            {
                using var body = new StreamReader(http.Request.Body, leaveOpen: true);
                await uploads.Writer.WriteAsync((name, await body.ReadToEndAsync()), timeout.Token);
                return Results.Ok();
            });
        }

        async Task MoveAgent(WebApplication agent)
        {
            var address = new Uri(agent.Urls.Single());
            var discovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            void Observe(AgentConfiguration configuration)
            {
                if (configuration.DiscoverySettings?.AgentUri == address)
                {
                    discovered.TrySetResult();
                }
            }

            discovery.SubscribeToChanges(Observe);
            try
            {
                var manual = new ManualInstrumentationConfigurationSource(
                    new Dictionary<string, object?> { ["DD_TRACE_AGENT_URL"] = address }, useDefaultSources: true);
                Require(settings.Manager.UpdateManualConfigurationSettings(manual, new ConfigurationTelemetry()), "The fixture did not change the Agent");
                await discovered.Task.WaitAsync(timeout.Token);
                Require(ReferenceEquals(owner, Tracer.Instance.TracerManager), "Changing the Agent replaced the tracer owner");
            }
            finally
            {
                discovery.RemoveSubscription(Observe);
            }
        }

        async Task SendSnapshot(DynamicInstrumentation instrumentation, string marker, string expectedAgent)
        {
            var probe = new ProbeInfo("fixture", 1, ProbeType.Snapshot, default, default, null, string.Empty, false, [], null, default, IsEmitted: true);
            instrumentation.AddSnapshot(probe, JsonSerializer.Serialize(new { marker }), incompleteReasons: 0);
            var upload = await uploads.Reader.ReadAsync(timeout.Token);
            Require(upload.Agent == expectedAgent, $"Expected snapshot upload to {expectedAgent}, received it at {upload.Agent}");
            Require(upload.Body.Contains(marker, StringComparison.Ordinal), "The Agent did not receive the synthetic snapshot");
        }
    }

    private static async Task WaitForDebugger(Func<bool> ready, Task startup, CancellationToken cancellationToken)
    {
        var deadline = Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        while (!ready())
        {
            Require(!startup.IsCompleted, "Debugger startup finished before reaching the expected state");
            await Task.WhenAny(startup, deadline, Task.Delay(20));
            if (!ready())
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }
    }

    private static bool AdvertisesProduct(Exchange exchange, string product)
        => exchange.Request.GetProperty("client").GetProperty("products")
                   .EnumerateArray().Any(entry => entry.GetString() == product);

    private static TracerSettings ReplacementSettings(string agentUrl, string service, bool remoteConfigEnabled, bool? featureFlagsEnabled = null)
    {
        var values = new Dictionary<string, string>
        {
            ["DD_SERVICE"] = service,
            ["DD_TRACE_AGENT_URL"] = agentUrl,
            ["DD_TRACE_SAMPLE_RATE"] = "1.0",
            ["DD_TRACE_STARTUP_LOGS"] = "false",
            ["DD_INSTRUMENTATION_TELEMETRY_ENABLED"] = "false",
            ["DD_RUNTIME_METRICS_ENABLED"] = "false",
            ["DD_REMOTE_CONFIGURATION_ENABLED"] = remoteConfigEnabled ? "true" : "false",
        };
        if (featureFlagsEnabled is { } enabled)
        {
            values[ConfigurationKeys.FeatureFlags.FeatureFlagsEnabled] = enabled ? "true" : "false";
        }

        if (featureFlagsEnabled is true)
        {
            values[ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource] = "remote_config";
        }

        return new TracerSettings(new DictionaryConfigurationSource(values));
    }

    private static WebApplication CreateAgent(string route, string? socketPath, ChannelWriter<Exchange> requests, CancellationToken cancellationToken)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options =>
        {
            if (socketPath is null)
            {
                options.Listen(IPAddress.Loopback, 0);
            }
            else
            {
                options.ListenUnixSocket(socketPath);
            }
        });
        var server = builder.Build();
        server.MapPost(route, async (HttpContext context) =>
        {
            using var request = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: cancellationToken);
            var completion = new TaskCompletionSource<Reply>(TaskCreationOptions.RunContinuationsAsynchronously);
            var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var registration = context.RequestAborted.Register(() => disconnected.TrySetResult());
            await requests.WriteAsync(new Exchange(context.Request.Path.Value!, request.RootElement.Clone(), completion, disconnected.Task), cancellationToken);
            var reply = await completion.Task.WaitAsync(cancellationToken);
            context.Response.StatusCode = reply.Status;
            context.Response.ContentType = "application/json";
            if (!reply.OmitContentLength)
            {
                context.Response.ContentLength = reply.DeclaredLength ?? reply.Body.Length;
            }

            if (reply.Location is { } location)
            {
                context.Response.Headers.Location = location;
            }

            if (reply.HeadersSent is { } headersSent)
            {
                await context.Response.StartAsync(cancellationToken);
                await context.Response.Body.FlushAsync(cancellationToken);
                headersSent.SetResult();
                await disconnected.Task.WaitAsync(cancellationToken);
                return;
            }

            if (reply.Truncate)
            {
                await context.Response.Body.WriteAsync(reply.Body.AsMemory(0, 1), cancellationToken);
                await context.Response.Body.FlushAsync(cancellationToken);
                context.Abort();
                return;
            }

            await context.Response.Body.WriteAsync(reply.Body, cancellationToken);
        });

        return server;
    }

    private static async Task Verify(string? socketPath, string? pipeName = null, bool watch = false)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var requests = Channel.CreateUnbounded<Exchange>();
        var deliveries = Channel.CreateUnbounded<Delivery>();
        await using var server = CreateAgent("/rc/{endpoint}", socketPath, requests.Writer, timeout.Token);
        await server.StartAsync(timeout.Token);

        var discovery = new Discovery();
        var subscriptions = new RcmSubscriptionManager();
        subscriptions.SetCapability(RcmCapabilitiesIndices.ApmTracingSampleRate, true);
        var rawSubscription = CreateRawSubscription();
        var agentUrl = socketPath is null ? server.Urls.Single() : $"unix://{socketPath}";
        var manager = CreateManager(pipeName is null ? agentUrl : null, "0.005", discovery, subscriptions, out var tracerSettings, pipeName);
        if (!watch)
        {
            subscriptions.SubscribeToChanges(rawSubscription);
        }

        var (valid, expectedFiles) = AgentComparison.CreateFixture(FileCount, 8, 1);
        var (empty, _) = AgentComparison.CreateFixture(0, 8, 2);
        using var relayCancellation = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        var relay = pipeName is null ? Task.CompletedTask : RelayPipe(pipeName, new Uri(agentUrl), relayCancellation.Token);
        manager.Start();
        Task? stopped = null;
        try
        {
            if (watch)
            {
                await VerifyWatch();
                return;
            }

            var exporter = tracerSettings.Manager.InitialExporterSettings;
            discovery.Publish("/rc/old", exporter);
            var superseded = await Next("/rc/old");
            discovery.Publish("/rc/current", exporter);
            superseded.Completion.SetResult(new Reply(200, valid));

            var first = await Next("/rc/current");
            Require(!deliveries.Reader.TryRead(out _), "The superseded endpoint applied a response");
            var tracer = first.Request.GetProperty("client").GetProperty("client_tracer");
            Require(tracer.GetProperty("service").GetString() == "shared-rc-proof", "The worker lost tracer metadata");
            VerifyCapabilities(first, [16, 0]);
            var rawOwner = CreateRawSubscription();
            subscriptions.Replace(rawSubscription, rawOwner);
            if (pipeName is null && socketPath is null)
            {
                var padded = new byte[Reactor.MaxBufferBytes];
                var offset = padded.Length - valid.Length;
                padded.AsSpan(0, offset).Fill((byte)' ');
                valid.CopyTo(padded, offset);
                first.Completion.SetResult(new Reply(200, padded, OmitContentLength: true));
            }
            else
            {
                // Put the assignment after the buffer reuse limit so the test requires the full body.
                var padded = new byte[(512 * 1024) + valid.Length];
                padded.AsSpan(0, 512 * 1024).Fill((byte)' ');
                valid.CopyTo(padded, 512 * 1024);
                first.Completion.SetResult(new Reply(200, padded));
            }

            var successor = await Next("/rc/current");
            var delivery = await deliveries.Reader.ReadAsync(timeout.Token);
            Require(delivery.Removed.Length == 0 && delivery.Files.Count == FileCount, "The new subscription did not replay the assignment");
            Require(expectedFiles.All(expected => delivery.Files.TryGetValue(expected.Key, out var actual) && expected.Value.AsSpan().SequenceEqual(actual)), "Assignment bytes changed");
            successor.Completion.SetResult(new Reply(204, []));

            var acknowledged = await Next("/rc/current");
            var states = ConfigStates(acknowledged);
            Require(states.GetArrayLength() == FileCount, "The next poll lost receipts");
            Require(states.EnumerateArray().Count(state => state.GetProperty("apply_state").GetInt32() == 2) == FileCount - 1, "Successful receipts were not acknowledged");
            var rejected = states.EnumerateArray().Single(state => state.GetProperty("apply_state").GetInt32() == 3);
            Require(rejected.GetProperty("apply_error").GetString() == "proof apply error", "The error receipt lost its message");
            if (pipeName is null && socketPath is null)
            {
                acknowledged.Completion.SetResult(new Reply(200, valid, Truncate: true));
                acknowledged = await Next("/rc/current");
                Require(ConfigStates(acknowledged).GetRawText() == states.GetRawText(), "A disconnected Agent lost the current receipts");
                Require(!deliveries.Reader.TryRead(out _), "A disconnected Agent changed the current assignment");

                var oversizedHeaders = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                acknowledged.Completion.SetResult(new Reply(200, [], oversizedHeaders, DeclaredLength: Reactor.MaxBufferBytes + 1));
                await oversizedHeaders.Task.WaitAsync(timeout.Token);
                await acknowledged.Disconnected.WaitAsync(timeout.Token);
                acknowledged = await Next("/rc/current");
                Require(ConfigStates(acknowledged).GetRawText() == states.GetRawText(), "An oversized response lost the current receipts");
                Require(!deliveries.Reader.TryRead(out _), "An oversized response changed the current assignment");

                acknowledged.Completion.SetResult(new Reply(200, new byte[Reactor.MaxBufferBytes + 1], OmitContentLength: true));
                acknowledged = await Next("/rc/current");
                Require(ConfigStates(acknowledged).GetRawText() == states.GetRawText(), "An oversized chunked response lost the current receipts");
                Require(!deliveries.Reader.TryRead(out _), "An oversized chunked response changed the current assignment");
            }

            subscriptions.SetCapability(RcmCapabilitiesIndices.ApmTracingSampleRate, false);
            subscriptions.SetCapability(RcmCapabilitiesIndices.ApmTracingTracingEnabled, true);
            var withdrawalOwner = CreateRawSubscription("LIVE_DEBUGGING", [Product]);
            subscriptions.Replace(rawOwner, withdrawalOwner);
            var detachedCalled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var detached = new Subscription((_, _) =>
            {
                detachedCalled.TrySetResult();
                return Array.Empty<ApplyDetails>();
            }, "LIVE_DEBUGGING");
            subscriptions.SubscribeToChanges(detached);
            acknowledged.Completion.SetResult(new Reply(404, []));

            if (pipeName is not null)
            {
                var redirected = await Next("/rc/current");
                redirected.Completion.SetResult(new Reply(307, [], Location: "/rc/redirected"));
            }

            var retained = await Next("/rc/current");
            VerifyCapabilities(retained, [8, 0, 0]);
            Require(retained.Request.GetProperty("client").GetProperty("products").EnumerateArray()
                            .Select(item => item.GetString()).SequenceEqual(["LIVE_DEBUGGING"]),
                    "Withdrawal interest changed the advertised products");
            Require(!deliveries.Reader.TryRead(out _) && ConfigStates(retained).GetArrayLength() == FileCount, "A 404 cleared a live assignment");
            subscriptions.Unsubscribe(detached);
            retained.Completion.SetResult(new Reply(200, empty));

            var late = await Next("/rc/current");
            Require(deliveries.Reader.TryRead(out var removal) && removal.Removed.Length == FileCount && removal.Files.Count == 0,
                    "The subscriber lost its withdrawal after narrowing poll interest");
            Require(!detachedCalled.Task.IsCompleted, "A removed subscriber was invoked for another product's withdrawal");
            Require(ConfigStates(late).GetArrayLength() == 0, "Removed files remained in the request");
            late = await VerifyInterruptedWithdrawal(late, withdrawalOwner);
            var headers = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            late.Completion.SetResult(new Reply(200, valid, headers));
            await headers.Task.WaitAsync(timeout.Token);
            stopped = manager.StopAsync();
            await Task.WhenAll(stopped, late.Disconnected).WaitAsync(TimeSpan.FromSeconds(5), timeout.Token);
            Require(!deliveries.Reader.TryRead(out _), "A late result was applied after shutdown");
            var transport = pipeName is not null ? "a named pipe" : socketPath is not null ? "a Unix socket" : "TCP";
            Console.WriteLine($"Native worker passed over {transport}");
        }
        finally
        {
            relayCancellation.Cancel();
            await relay;
            await (stopped ?? manager.StopAsync()).WaitAsync(timeout.Token);
        }

        async ValueTask<Exchange> Next(string path)
        {
            var next = await requests.Reader.ReadAsync(timeout.Token);
            Require(next.Path == path, $"Expected poll of {path}, received {next.Path}");
            return next;
        }

        Subscription CreateRawSubscription(string requestedProduct = Product, IReadOnlyCollection<string>? removalProducts = null) => new Subscription(
            (upserts, removals) =>
            {
                upserts.TryGetValue(Product, out var files);
                var removedPaths = removals?.TryGetValue(Product, out var removed) is true ? removed.Select(path => path.Path).ToArray() : [];
                var appliedFiles = (files ?? []).ToDictionary(file => file.Path.Path, file => file.Contents);
                Append(deliveries.Writer, new Delivery(appliedFiles, removedPaths));
                return appliedFiles.Keys.Select((path, index) => index == 0 ? ApplyDetails.FromError(path, "proof apply error") : ApplyDetails.FromOk(path)).ToArray();
            },
            requestedProduct)
        {
            RemovalProductKeys = removalProducts,
        };

        async Task<Exchange> VerifyInterruptedWithdrawal(Exchange pending, Subscription rawOwner)
        {
            var installed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var successor = CreateRawSubscription();
            Subscription? applying = null;
            applying = new Subscription((upserts, _) =>
            {
                var path = upserts[Product].Single().Path.Path;
                installed.TrySetResult(path);
                subscriptions.Replace(applying!, successor);
                return [ApplyDetails.FromOk(path)];
            }, Product);
            subscriptions.Replace(rawOwner, applying);
            pending.Completion.SetResult(new Reply(204, []));

            var update = await Next("/rc/current");
            var (oneFile, _) = AgentComparison.CreateFixture(1, 8, 3);
            update.Completion.SetResult(new Reply(200, oneFile));
            var withdrawal = await Next("/rc/current");
            Require(installed.Task.IsCompletedSuccessfully, "The callback did not install a file before replacing its registration");
            var installedPath = await installed.Task;
            Require(deliveries.Reader.TryRead(out var replay) && replay.Files.ContainsKey(installedPath), "The replacement did not replay the interrupted installation");
            VerifyReceipt(withdrawal, RemoteConfigurationPath.FromPath(installedPath).Id, 3, "proof apply error");
            var (none, _) = AgentComparison.CreateFixture(0, 8, 4);
            withdrawal.Completion.SetResult(new Reply(200, none));
            var next = await Next("/rc/current");
            Require(deliveries.Reader.TryRead(out var removed) &&
                    removed.Removed.Length == 1 && removed.Removed[0] == installedPath &&
                    removed.Files.Count == 0, "The worker lost the withdrawal for the interrupted install");
            return next;
        }

        async Task VerifyWatch()
        {
            const string endpoint = "/rc/watch";
            const string before = "{\"lib_config\":{\"tracing_sampling_rate\":0.1}}";
            const string after = "{\"lib_config\":{\"tracing_sampling_rate\":0.7}}";
            Environment.SetEnvironmentVariable("DD_TRACE_AGENT_URL", agentUrl);
            Environment.SetEnvironmentVariable("DD_REMOTE_CONFIGURATION_ENABLED", "false");
            Environment.SetEnvironmentVariable("DD_INSTRUMENTATION_TELEMETRY_ENABLED", "false");
            var owner = Tracer.Instance.TracerManager;
            var originalRate = owner.PerTraceSettings.Settings.GlobalSamplingRate;
            using var dynamicConfiguration = new DynamicConfigurationManager(subscriptions);
            dynamicConfiguration.Start(owner);
            dynamicConfiguration.Start(owner);

            if (subscriptions.GetSnapshotSubscription(Product) is not { InstallSnapshot: { } install } original)
            {
                throw new InvalidOperationException("The tracer did not register its APM watch");
            }

            var snapshots = Channel.CreateUnbounded<(int Owner, string[] Paths)>();
            var failNextUpdate = true;
            var observed = ObserveWatch(1);
            subscriptions.Replace(original, observed);
            Exchange final;
            try
            {
                discovery.Publish(endpoint, tracerSettings.Manager.InitialExporterSettings);
                var initial = await Next(endpoint);
                initial.Completion.SetResult(new Reply(200, WatchAssignment(1, ("a", 1, before), ("b", 1, before))));
                await RequireSnapshot(1, "a", "b");

                var acknowledged = await Next(endpoint);
                VerifyReceipt(acknowledged, "a", 2);
                VerifyReceipt(acknowledged, "b", 2);
                Require(Tracer.Instance.CurrentTraceSettings.Settings.GlobalSamplingRate == 0.1, "The initial watch did not install the sample rate");
                acknowledged.Completion.SetResult(new Reply(200, WatchAssignment(2, ("a", 2, after), ("b", 1, before), ("invalid", 1, "{"))));
                await RequireSnapshot(1, "a", "b", "invalid");

                var failed = await Next(endpoint);
                VerifyReceipt(failed, "a", 3, "proof install failed");
                VerifyReceipt(failed, "b", 2);
                VerifyReceipt(failed, "invalid", 3, "proof install failed");
                Require(Tracer.Instance.CurrentTraceSettings.Settings.GlobalSamplingRate == 0.1, "The failed install changed the sample rate");
                failed.Completion.SetResult(new Reply(204, []));
                await RequireSnapshot(1, "a", "b", "invalid");

                var retried = await Next(endpoint);
                VerifyReceipt(retried, "a", 2);
                VerifyReceipt(retried, "b", 2);
                VerifyReceipt(retried, "invalid", 3, "Invalid APM tracing configuration or missing lib_config");
                Require(Tracer.Instance.CurrentTraceSettings.Settings.GlobalSamplingRate == 0.7, "The retried install did not publish the sample rate");
                var replacement = ObserveWatch(2);
                subscriptions.Replace(observed, replacement);
                observed = replacement;
                retried.Completion.SetResult(new Reply(204, []));

                var replaced = await Next(endpoint);
                replaced.Completion.SetResult(new Reply(204, []));
                await RequireSnapshot(2, "a", "b", "invalid");
                var empty = await Next(endpoint);
                empty.Completion.SetResult(new Reply(200, WatchAssignment(3)));
                await RequireSnapshot(2);
                final = await Next(endpoint);
                Require(ConfigStates(final).GetArrayLength() == 0, "Removed watched files remained in the request");
                Require(Tracer.Instance.CurrentTraceSettings.Settings.GlobalSamplingRate == originalRate, "Watch removal did not reset the sample rate");
            }
            finally
            {
                subscriptions.Replace(observed, original);
            }

            final.Completion.SetResult(new Reply(204, []));

            var attach = await Next(endpoint);
            attach.Completion.SetResult(new Reply(200, WatchAssignment(4, ("a", 1, after))));
            var installed = await Next(endpoint);
            VerifyReceipt(installed, "a", 2);
            Require(Tracer.Instance.CurrentTraceSettings.Settings.GlobalSamplingRate == 0.7, "The real APM installer did not publish the updated sample rate");
            installed.Completion.SetResult(new Reply(200, WatchAssignment(5, ("a", 2, "{"))));

            var rejected = await Next(endpoint);
            VerifyReceipt(rejected, "a", 3, "Invalid APM tracing configuration or missing lib_config");
            Require(Tracer.Instance.CurrentTraceSettings.Settings.GlobalSamplingRate == 0.7, "An all-invalid assignment replaced the last good sample rate");
            rejected.Completion.SetResult(new Reply(200, WatchAssignment(6, ("b", 1, "{"))));

            var withdrawn = await Next(endpoint);
            VerifyReceipt(withdrawn, "b", 3, "Invalid APM tracing configuration or missing lib_config");
            Require(ConfigStates(withdrawn).GetArrayLength() == 1, "The withdrawn file kept a receipt");
            Require(Tracer.Instance.CurrentTraceSettings.Settings.GlobalSamplingRate == originalRate, "A rejected file kept a withdrawn configuration active");
            withdrawn.Completion.SetResult(new Reply(200, WatchAssignment(7, ("a", 3, after), ("b", 2, before))));

            var twoFiles = await Next(endpoint);
            VerifyReceipt(twoFiles, "a", 2);
            VerifyReceipt(twoFiles, "b", 2);
            Require(Tracer.Instance.CurrentTraceSettings.Settings.GlobalSamplingRate == 0.7, "The higher-priority file was not applied");
            twoFiles.Completion.SetResult(new Reply(200, WatchAssignment(8, ("b", 3, "{"))));

            var retained = await Next(endpoint);
            VerifyReceipt(retained, "b", 3, "Invalid APM tracing configuration or missing lib_config");
            Require(ConfigStates(retained).GetArrayLength() == 1, "The withdrawn file kept a receipt after rejecting its sibling");
            Require(Tracer.Instance.CurrentTraceSettings.Settings.GlobalSamplingRate == 0.1, "The remaining file lost its last good configuration");
            retained.Completion.SetResult(new Reply(200, WatchAssignment(9)));

            var reset = await Next(endpoint);
            Require(Tracer.Instance.CurrentTraceSettings.Settings.GlobalSamplingRate == originalRate, "An authoritative removal did not restore the initial sample rate");
            var manual = new ManualInstrumentationConfigurationSource(new Dictionary<string, object?> { ["DD_SERVICE"] = "retargeted" }, useDefaultSources: true);
            Require(tracerSettings.Manager.UpdateManualConfigurationSettings(manual, new ConfigurationTelemetry()), "The fixture did not change the target service");
            reset.Completion.SetResult(new Reply(204, []));
            var retargeted = await Next(endpoint);
            Require(retargeted.Request.GetProperty("client").GetProperty("client_tracer").GetProperty("service").GetString() == "retargeted", "A service change did not refresh the Remote Config target");
            dynamicConfiguration.Dispose();
            Require(subscriptions.GetSnapshotSubscription(Product) is null, "Repeated start left a stale watch subscriber after dispose");
            retargeted.Completion.SetResult(new Reply(204, []));
            Console.WriteLine("Native APM watch passed over TCP");

            Subscription ObserveWatch(int watchOwner) => new Subscription(original.Invoke, Product)
            {
                InstallSnapshot = (files, rejections, generation, tryCommit) =>
                {
                    Append(snapshots.Writer, (watchOwner, files.Select(file => file.Path.Id).ToArray()));
                    if (failNextUpdate && files.Exists(file => file.Path.Id == "a" && file.Version == 2))
                    {
                        failNextUpdate = false;
                        throw new InvalidOperationException("proof install failed");
                    }

                    return install(files, rejections, generation, tryCommit);
                },
            };

            async Task RequireSnapshot(int watchOwner, params string[] paths)
            {
                var snapshot = await snapshots.Reader.ReadAsync(timeout.Token);
                Require(snapshot.Owner == watchOwner && snapshot.Paths.SequenceEqual(paths), $"Wrong snapshot: owner {snapshot.Owner}, paths {string.Join(", ", snapshot.Paths)}");
            }
        }
    }

    private static byte[] WatchAssignment(int version, params (string Id, int Version, string Text)[] configs)
        => Assignment(Product, version, configs);

    private static byte[] Assignment(string product, int version, params (string Id, int Version, string Text)[] configs)
        => Assignment(version, configs.Select(config => (product, config.Id, config.Version, config.Text)));

    private static byte[] Assignment(int version, IEnumerable<(string Product, string Id, int Version, string Text)> configs)
        => AssignmentFixture.Encode(version, "watch-proof", configs.Select(config => new AssignmentFixture.TargetFile(
            $"datadog/2/{config.Product}/{config.Id}/config",
            Encoding.UTF8.GetBytes(config.Text),
            checked((ulong)config.Version))));

    private static void VerifyReceipt(Exchange exchange, string id, int expectedState, string? error = null, int? version = null)
    {
        var receipt = ConfigStates(exchange).EnumerateArray().Single(state => state.GetProperty("id").GetString() == id);
        Require(receipt.GetProperty("apply_state").GetInt32() == expectedState, $"Wrong receipt for {id}: {receipt}");
        if (version is not null)
        {
            Require(receipt.GetProperty("version").GetInt32() == version, $"Wrong revision for {id}: {receipt}");
        }

        if (error is not null)
        {
            Require(receipt.GetProperty("apply_error").GetString() == error, $"Wrong error for {id}: {receipt}");
        }
    }

    private static async Task VerifyPipePeerDisconnect()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var pipeName = $"dd-rc-{Guid.NewGuid():N}";
        using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        using (var peer = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous))
        {
            await Task.WhenAll(pipe.WaitForConnectionAsync(timeout.Token), peer.ConnectAsync(timeout.Token));
            // Closing a Unix pipe with unread data can reset the server's pending read.
            byte[] unread = [1];
            await pipe.WriteAsync(unread, timeout.Token);
        }

        using var request = new MemoryStream();
        await CopyRelayStream(pipe, request, timeout.Token);
        Require(request.Length == 0, "The relay received a request from a closed named-pipe peer");

        using var response = new MemoryStream([1]);
        await CopyRelayStream(response, pipe, timeout.Token);
        Require(!pipe.IsConnected, "The relay did not observe the closed named-pipe peer");
    }

    private static async Task CopyRelayStream(Stream source, Stream destination, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        while (true)
        {
            int bytesRead;
            try
            {
                bytesRead = await source.ReadAsync(buffer, cancellationToken);
            }
            catch (IOException error) when (PipePeerClosed(source, error))
            {
                Console.WriteLine("Named-pipe peer closed while the relay was reading");
                return;
            }

            if (bytesRead == 0)
            {
                return;
            }

            try
            {
                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }
            catch (IOException error) when (PipePeerClosed(destination, error))
            {
                Console.WriteLine("Named-pipe peer closed while the relay was writing");
                return;
            }
        }
    }

    private static bool PipePeerClosed(Stream stream, IOException error)
        => stream is NamedPipeServerStream pipe &&
           (!pipe.IsConnected || error.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionReset });

    private static async Task RelayPipe(string pipeName, Uri endpoint, CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            using var pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            using var connection = CancellationTokenSource.CreateLinkedTokenSource(stop);
            try
            {
                await pipe.WaitForConnectionAsync(stop);
                using var client = new TcpClient();
                await client.ConnectAsync(endpoint.Host, endpoint.Port, stop);
                using var stream = client.GetStream();
                var request = CopyRelayStream(pipe, stream, connection.Token);
                var response = CopyRelayStream(stream, pipe, connection.Token);
                await Task.WhenAny(request, response);
                connection.Cancel();
                await Task.WhenAll(request, response);
            }
            catch (OperationCanceledException) when (connection.IsCancellationRequested)
            {
                if (stop.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    internal static RemoteConfigurationManager CreateManager(string? agentUrl, string intervalSeconds, Discovery discovery, RcmSubscriptionManager subscriptions, out TracerSettings tracerSettings, string? pipeName = null)
    {
        var configuration = new Dictionary<string, string>
        {
            ["DD_REMOTE_CONFIG_POLL_INTERVAL_SECONDS"] = intervalSeconds,
            ["DD_SERVICE"] = "shared-rc-proof",
            ["DD_ENV"] = "test",
        };
        if (pipeName is not null)
        {
            configuration["DD_TRACE_PIPE_NAME"] = pipeName;
        }
        else
        {
            configuration["DD_TRACE_AGENT_URL"] = agentUrl ?? throw new ArgumentNullException(nameof(agentUrl));
        }

        var source = new DictionaryConfigurationSource(configuration);
        var telemetry = new ConfigurationTelemetry();
        tracerSettings = new TracerSettings(source, telemetry);
        return RemoteConfigurationManager.Create(
            discovery, RemoteConfigurationSettings.FromSource(source, telemetry), tracerSettings, new NullGitMetadataProvider(), subscriptions);
    }

    private static JsonElement ConfigStates(Exchange exchange) => exchange.Request.GetProperty("client").GetProperty("state").GetProperty("config_states");

    private static void VerifyPollBackend(Exchange exchange, bool native)
    {
        // The managed serializer emits base64. The native reactor emits an array of bytes.
        var capabilities = exchange.Request.GetProperty("client").GetProperty("capabilities");
        Require(capabilities.ValueKind == (native ? JsonValueKind.Array : JsonValueKind.String), "The poll did not come from the expected worker");
    }

    private static void VerifyCapabilities(Exchange exchange, byte[] expected)
    {
        var actual = exchange.Request.GetProperty("client").GetProperty("capabilities");
        Require(actual.EnumerateArray().Select(value => value.GetByte()).SequenceEqual(expected), "The worker did not send current capability bits");
    }

    private static void Append<T>(ChannelWriter<T> writer, T value) => Require(writer.TryWrite(value), "The proof channel closed");

    private static void Require(bool condition, string error)
    {
        if (!condition)
        {
            throw new InvalidOperationException(error);
        }
    }

    private sealed record Exchange(string Path, JsonElement Request, TaskCompletionSource<Reply> Completion, Task Disconnected);

    private sealed record Reply(
        int Status,
        byte[] Body,
        TaskCompletionSource? HeadersSent = null,
        string? Location = null,
        bool Truncate = false,
        long? DeclaredLength = null,
        bool OmitContentLength = false);

    private sealed record Delivery(Dictionary<string, byte[]> Files, string[] Removed);

    private sealed class DiscoveryTracerManagerFactory(IDiscoveryService discovery) : TracerManagerFactory
    {
        internal override IDiscoveryService GetDiscoveryService(TracerSettings settings, ServiceRemappingHash serviceRemappingHash) => discovery;
    }

    private sealed class ObservedWithdrawal(DiscoveryService discovery) : IDiscoveryService
    {
        private int _readsAfterWithdrawal;

        internal TaskCompletionSource WorkerPassedWithdrawal { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public AgentConfiguration? CurrentConfiguration
        {
            get
            {
                var configuration = discovery.CurrentConfiguration;
                if (configuration is { ConfigurationEndpoint: null } && Interlocked.Increment(ref _readsAfterWithdrawal) == 2)
                {
                    // A single worker reads once for the held reply and again on its next poll.
                    WorkerPassedWithdrawal.TrySetResult();
                }

                return configuration;
            }
        }

        public void SubscribeToChanges(Action<AgentConfiguration> callback) => discovery.SubscribeToChanges(callback);

        public void RemoveSubscription(Action<AgentConfiguration> callback) => discovery.RemoveSubscription(callback);

        public void SetCurrentConfigStateHash(string configStateHash) => discovery.SetCurrentConfigStateHash(configStateHash);

        public bool TryRunWithCurrentConfiguration(Func<AgentConfiguration, bool> apply)
            => discovery.TryRunWithCurrentConfiguration(apply);

        public Task DisposeAsync() => discovery.DisposeAsync();
    }

    private sealed class PausedUploadConnection(int port, CancellationToken cancellationToken) : IStreamFactory
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Info() => "loopback test connection";

        public Stream GetBidirectionalStream()
        {
            Entered.TrySetResult();
            Resume.Task.WaitAsync(cancellationToken).GetAwaiter().GetResult();
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                socket.Connect(IPAddress.Loopback, port);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        public Task<Stream> GetBidirectionalStreamAsync(CancellationToken token)
            => throw new InvalidOperationException("The fixture expects the synchronous stream connect");
    }

    private sealed class PausedUploadTags(CancellationToken cancellationToken) : IGitMetadataTagsProvider
    {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool TryExtractGitMetadata([NotNullWhen(true)] out GitMetadata? gitMetadata)
        {
            Entered.TrySetResult();
            Resume.Task.WaitAsync(cancellationToken).GetAwaiter().GetResult();
            gitMetadata = null;
            return false;
        }
    }

    private sealed class DebuggerTakeoverDiscovery(string debuggerEndpoint, ExporterSettings exporter, string? configurationEndpoint) : IDiscoveryService
    {
        private readonly object _gate = new();
        private readonly TaskCompletionSource _candidateCreated = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private AgentConfiguration _configuration = CreateConfiguration(null, debuggerEndpoint, exporter);
        private Action<AgentConfiguration>? _observers;

        public Task CandidateCreated => _candidateCreated.Task;

        public AgentConfiguration? CurrentConfiguration => Volatile.Read(ref _configuration);

        public Action? TransportRemoved { get; set; }

        public bool TryRunWithCurrentConfiguration(Func<AgentConfiguration, bool> apply)
        {
            lock (_gate)
            {
                return apply(_configuration);
            }
        }

        public void SubscribeToChanges(Action<AgentConfiguration> callback)
        {
            var candidate = callback.Target is DebuggerAgentTransport;
            Action<AgentConfiguration>? notify = callback;
            AgentConfiguration configuration;
            lock (_gate)
            {
                _observers += callback;
                if (candidate && configurationEndpoint is not null && _configuration.ConfigurationEndpoint is null && !_candidateCreated.Task.IsCompleted)
                {
                    // Switch the advertised endpoints after the manager has prepared the candidate.
                    Volatile.Write(ref _configuration, CreateConfiguration(configurationEndpoint, null, exporter));
                    notify = _observers;
                }

                configuration = _configuration;
            }

            notify?.Invoke(configuration);
            if (candidate)
            {
                _candidateCreated.TrySetResult();
            }
        }

        public void RemoveSubscription(Action<AgentConfiguration> callback)
        {
            lock (_gate)
            {
                _observers -= callback;
            }

            if (callback.Target is DebuggerAgentTransport)
            {
                TransportRemoved?.Invoke();
            }
        }

        public void SetCurrentConfigStateHash(string configStateHash)
        {
        }

        public Task DisposeAsync() => Task.CompletedTask;

        public void PublishReadyRoute(ExporterSettings? agentExporter = null)
            => PublishConfiguration(CreateConfiguration(configurationEndpoint, debuggerEndpoint, agentExporter ?? exporter));

        public void ReplayCurrentSnapshot() => PublishConfiguration(_configuration);

        public void PublishConfigurationOnlyRoute(ExporterSettings? agentExporter = null)
            => PublishConfiguration(CreateConfiguration(configurationEndpoint, null, agentExporter ?? exporter));

        private void PublishConfiguration(AgentConfiguration configuration)
        {
            Action<AgentConfiguration>? observers;
            lock (_gate)
            {
                Volatile.Write(ref _configuration, configuration);
                observers = _observers;
            }

            observers?.Invoke(configuration);
        }

        private static AgentConfiguration CreateConfiguration(string? configurationEndpoint, string? debuggerEndpoint, ExporterSettings exporter) => new(
            configurationEndpoint, debuggerEndpoint, null, null, null, null, null, null, null, null, null,
            containerTagsHash: null, clientDropP0: false, spanMetaStructs: false, spanEvents: false, discoverySettings: exporter);
    }

    internal sealed class Discovery : IDiscoveryService
    {
        private readonly object _publicationGate = new();
        private Action<AgentConfiguration>? _observers;
        private AgentConfiguration? _configuration;

        public AgentConfiguration? CurrentConfiguration => Volatile.Read(ref _configuration);

        public bool TryRunWithCurrentConfiguration(Func<AgentConfiguration, bool> apply)
        {
            lock (_publicationGate)
            {
                return _configuration is { } configuration && apply(configuration);
            }
        }

        public void SubscribeToChanges(Action<AgentConfiguration> callback) => _observers += callback;

        public void RemoveSubscription(Action<AgentConfiguration> callback) => _observers -= callback;

        public void Publish(string endpoint, ExporterSettings exporter)
        {
            var configuration = new AgentConfiguration(
                endpoint, null, null, null, null, null, null, null, null, null, null,
                containerTagsHash: null, clientDropP0: false, spanMetaStructs: false, spanEvents: false, discoverySettings: exporter);
            lock (_publicationGate)
            {
                Volatile.Write(ref _configuration, configuration);
            }

            _observers?.Invoke(configuration);
        }

        public void SetCurrentConfigStateHash(string configStateHash) => throw new NotSupportedException();

        public Task DisposeAsync() => Task.CompletedTask;
    }
}
#endif
