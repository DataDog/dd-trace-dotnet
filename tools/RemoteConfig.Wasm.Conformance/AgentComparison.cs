#if RC_AGENT_COMPARISON
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.Transports;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.RemoteConfigurationManagement.Shared;
using Datadog.Trace.RemoteConfigurationManagement.Transport;
using Wasmtime;

namespace Datadog.Trace.Tools.RemoteConfigConformance;

internal static class AgentComparison
{
    private const string Product = "APM_TRACING";
    private const string AgentUrl = "http://127.0.0.1:8126";
    private const string Endpoint = "/v0.7/config";
    private static readonly byte[] Unchanged = "{}"u8.ToArray();

    public static void Run(string artifact, string? nativeArtifact = null)
    {
        using var engine = new Engine();
        using var module = Module.FromFile(engine, artifact);
        using var linker = new Linker(engine);
        linker.DefineWasi();
        string[] backends = nativeArtifact is null ? ["legacy", "wasi"] : ["legacy", "wasi", "native"];

        Console.WriteLine("workload,trial,engine,ns/poll,managed-bytes/poll");
        foreach (var (name, count, padding, iterations, changed) in new[]
                 {
                     ("unchanged", 1, 1024, 20_000, false),
                     ("single", 1, 1024, 4_000, true),
                     ("multi", 24, 4096, 300, true),
                 })
        {
            var first = CreateFixture(count, padding, 1);
            var second = CreateFixture(count, padding, 2);
            for (var trial = 0; trial < 5; trial++)
            {
                for (var index = 0; index < backends.Length; index++)
                {
                    Measure(name, trial, backends[(trial + index) % backends.Length]);
                }
            }

            void Measure(string workload, int trial, string backend)
            {
                using var handler = new AgentHandler();
                var factory = new HttpClientRequestFactory(new Uri(AgentUrl), [], handler);
                using var store = backend == "wasi" ? new Store(engine) : null;
                using var reactor = backend switch
                {
                    "wasi" => WasiReactorBinding.Open(store!, linker, module),
                    "native" => Reactor.OpenNative(nativeArtifact!),
                    _ => null,
                };
                var delivered = 0;
                Dictionary<string, byte[]>? expected = null;
                var subscriptions = new RcmSubscriptionManager();
                subscriptions.SetCapability(RcmCapabilitiesIndices.ApmTracingSampleRate, true);
                subscriptions.SetCapability(RcmCapabilitiesIndices.ApmTracingTracingEnabled, true);
                var capabilityBits = subscriptions.GetCapabilities();
                var poll = reactor is null
                               ? CreateLegacyPoll(subscriptions, factory, Observe)
                               : CreateReactorPoll(reactor, factory, Observe, Reactor.GetCapabilityIndices(capabilityBits));
                Exchange(first.Response, first.Files, expectAck: false);
                Exchange(second.Response, second.Files, expectAck: true);
                Exchange(first.Response, first.Files, expectAck: true);
                Exchange(Unchanged, [], expectAck: true);
                expected = null;
                handler.Verify = null;
                Loop(512);
                delivered = 0;

                var allocated = GC.GetTotalAllocatedBytes(precise: true);
                var timestamp = Stopwatch.GetTimestamp();
                Loop(iterations);
                var nanoseconds = (Stopwatch.GetTimestamp() - timestamp) * (1e9 / Stopwatch.Frequency);
                allocated = GC.GetTotalAllocatedBytes(precise: true) - allocated;
                Require(delivered == (changed ? count * iterations : 0), "Wrong number of measured deliveries");
                Console.WriteLine(FormattableString.Invariant($"{workload},{trial},{backend},{nanoseconds / iterations:F1},{(double)allocated / iterations:F1}"));

                void Observe(string path, byte[] payload)
                {
                    Require(expected is null || (expected.TryGetValue(path, out var bytes) && payload.AsSpan().SequenceEqual(bytes)), "Unexpected assignment payload");
                    delivered++;
                }

                void Exchange(byte[] response, Dictionary<string, byte[]> files, bool expectAck)
                {
                    expected = files;
                    handler.Verify = expectAck ? request => VerifyAcknowledgements(request, count, capabilityBits, reactor is null) : null;
                    handler.Response = response;
                    delivered = 0;
                    poll().GetAwaiter().GetResult();
                    Require(delivered == files.Count, "Wrong number of preflight deliveries");
                }

                void Loop(int size)
                {
                    for (var index = 0; index < size; index++)
                    {
                        handler.Response = changed ? (index % 2 == 0 ? second.Response : first.Response) : Unchanged;
                        poll().GetAwaiter().GetResult();
                    }
                }
            }
        }
    }

    private static Func<Task> CreateLegacyPoll(RcmSubscriptionManager manager, HttpClientRequestFactory factory, Action<string, byte[]> observe)
    {
        manager.SubscribeToChanges(new Subscription(
            (configs, _) =>
            {
                if (!configs.TryGetValue(Product, out var files))
                {
                    return [];
                }

                var receipts = new ApplyDetails[files.Count];
                for (var index = 0; index < files.Count; index++)
                {
                    var file = files[index];
                    observe(file.Path.Path, file.Contents);
                    receipts[index] = ApplyDetails.FromOk(file.Path.Path);
                }

                return receipts;
            },
            Product));
        var tracer = new RcmClientTracer("benchmark", "1", "test", "test", "1", [], null);
        var api = new RemoteConfigurationApi(factory);
        Func<GetRcmRequest, Task<GetRcmResponse?>> fetch = request => api.GetConfigs(request, Endpoint);
        return () => manager.SendRequest(tracer, fetch);
    }

    private static Func<Task> CreateReactorPoll(Reactor reactor, HttpClientRequestFactory factory, Action<string, byte[]> observe, int[] capabilityIndices)
    {
        var client = reactor.WithJson(
            new
            {
                target = new { service = "test", env = "test", appVersion = "1" },
                identity = new { clientId = "dotnet-benchmark", runtimeId = "benchmark" },
                interest = new { products = new[] { Product }, capabilityIndices },
                language = "dotnet",
                tracerVersion = "1",
                url = AgentUrl,
                timeoutMs = 2000,
            },
            reactor.Open);
        var metadata = ContainerMetadata.Instance;
        return async () =>
        {
            reactor.Check(reactor.Poll(client));
            var request = reactor.TakeJson(reactor.Check(reactor.Event(client)));
            var timer = reactor.TakeJson(reactor.Check(reactor.Event(client)));
            Require(request.GetProperty("kind").GetString() == "http" && timer.GetProperty("kind").GetString() == "sleep", "Unexpected host operations");
            var httpRequest = factory.Create(new Uri(request.GetProperty("url").GetString()!));
            httpRequest.AddContainerMetadataHeaders(metadata);
            var bytes = reactor.Take(request.GetProperty("body").GetInt32());
            using var response = await httpRequest.PostAsync(new ArraySegment<byte>(bytes), "application/json").ConfigureAwait(false);
            Require(response.StatusCode == 200, "Unexpected response status");
            using var body = new MemoryStream();
            var stream = await response.GetStreamAsync().ConfigureAwait(false);
            await stream.CopyToAsync(body).ConfigureAwait(false);
            reactor.CompleteHttp(client, request.GetProperty("id").GetInt32(), body.GetBuffer().AsSpan(0, checked((int)body.Length)));
            var cancelled = reactor.TakeJson(reactor.Check(reactor.Event(client)));
            Require(cancelled.GetProperty("kind").GetString() == "cancel" && cancelled.GetProperty("id").GetInt32() == timer.GetProperty("id").GetInt32(), "Expected timer cancellation");
            var result = reactor.TakeJson(reactor.Check(reactor.Result(client)));
            Require(!result.TryGetProperty("error", out _), "Unexpected reactor error");
            var batch = result.GetProperty("batch").GetInt32();
            if (batch == 0)
            {
                return;
            }

            try
            {
                var records = reactor.TakeJson(reactor.Check(reactor.Describe(batch))).GetProperty("records");
                var index = 0;
                foreach (var record in records.EnumerateArray())
                {
                    Require(record.GetProperty("kind").GetString() == "upsert", "Unexpected assignment kind");
                    var path = record.GetProperty("file").GetProperty("path").GetString()!;
                    var receipt = reactor.Check(reactor.File(batch, index++, 0));
                    try
                    {
                        observe(path, reactor.Copy(receipt));
                        Require(reactor.Check(reactor.Apply(client, receipt, 2, 0)) == 1, "Receipt could not be acknowledged");
                    }
                    finally
                    {
                        reactor.Free(receipt);
                    }
                }
            }
            finally
            {
                reactor.Check(reactor.Release(batch));
            }
        };
    }

    internal static (byte[] Response, Dictionary<string, byte[]> Files) CreateFixture(int count, int padding, int variant)
    {
        var files = new Dictionary<string, byte[]>(count, StringComparer.Ordinal);
        var targets = new Dictionary<string, object>(count, StringComparer.Ordinal);
        var pad = new string('x', padding);
        for (var index = 0; index < count; index++)
        {
            var path = $"datadog/2/{Product}/benchmark-{index}/config";
            var payload = Encoding.UTF8.GetBytes($$"""{"action":"enable","service_target":{"service":"test","env":"test"},"lib_config":{"tracing_sampling_rate":0.{{variant}}},"padding":"{{pad}}"}""");
            files.Add(path, payload);
            targets.Add(path, new { custom = new { v = 1 }, hashes = new { sha256 = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant() }, length = payload.Length });
        }

        var metadata = JsonSerializer.SerializeToUtf8Bytes(new { signed = new { custom = new { opaque_backend_state = "benchmark" }, targets, version = 1 } });
        return (JsonSerializer.SerializeToUtf8Bytes(new { targets = metadata, target_files = files.Select(file => new { path = file.Key, raw = file.Value }), client_configs = files.Keys }), files);
    }

    private static void VerifyAcknowledgements(byte[] request, int count, byte[] expectedCapabilities, bool legacy)
    {
        using var document = JsonDocument.Parse(request);
        var client = document.RootElement.GetProperty("client");
        var capabilities = client.GetProperty("capabilities");
        var bits = legacy ? capabilities.GetBytesFromBase64() : capabilities.EnumerateArray().Select(value => value.GetByte()).ToArray();
        Require(bits.AsSpan().SequenceEqual(expectedCapabilities), "Wrong capabilities in request");
        var state = client.GetProperty("state");
        var configs = state.GetProperty("config_states");
        Require(configs.GetArrayLength() == count, "Wrong number of config states in request");
        foreach (var config in configs.EnumerateArray())
        {
            Require(config.GetProperty("apply_state").GetUInt64() == 2, "Config was not acknowledged in the next poll");
        }
    }

    private static void Require(bool condition, string error)
    {
        if (!condition)
        {
            throw new InvalidOperationException(error);
        }
    }

    private sealed class AgentHandler : HttpMessageHandler
    {
        public byte[] Response { get; set; } = Unchanged;

        public Action<byte[]>? Verify { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Require(request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == Endpoint, "Wrong Agent request");
            if (Verify is { } verify)
            {
                verify(await request.Content!.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false));
            }
            else
            {
                await request.Content!.CopyToAsync(Stream.Null, cancellationToken).ConfigureAwait(false);
            }

            var content = new ByteArrayContent(Response);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }
}
#endif
