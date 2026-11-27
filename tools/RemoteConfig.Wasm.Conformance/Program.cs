using System.Security.Cryptography;
using System.Text.Json;
using Datadog.Trace.RemoteConfigurationManagement.Shared;
using Datadog.Trace.Tools.RemoteConfigConformance;
using Wasmtime;

#if VERIFY_SHARED_WORKER
if (args is ["--worker-benchmark-fixtures"])
{
    LiveWorkerBenchmark.WriteFixtures();
    return;
}

if (args is ["--measure-live-worker", var agentUrl, var backend, var benchmarkArtifact])
{
    await LiveWorkerBenchmark.Run(agentUrl, backend, benchmarkArtifact);
    return;
}

if (args is ["--verify-native-worker", var workerArtifact])
{
    await NativeWorkerProof.Run(workerArtifact);
    return;
}

if (args is ["--verify-native-raw-product-handoff", var rawHandoffArtifact])
{
    await NativeWorkerProof.VerifyRawProductHandoff(rawHandoffArtifact);
    return;
}

if (args is ["--verify-native-replacement" or "--verify-native-retired-callback", var replacementArtifact])
{
    await NativeWorkerProof.VerifyTracerReplacement(replacementArtifact, retireDuringDelivery: args[0] == "--verify-native-retired-callback");
    return;
}

if (args is ["--verify-managed-replacement"])
{
    await NativeWorkerProof.VerifyTracerReplacement(null, retireDuringDelivery: false);
    return;
}

if (args is ["--verify-managed-replacement-disabling-rc" or "--verify-managed-replacement-during-delivery"])
{
    await NativeWorkerProof.VerifyManagedRetiredResponse(null, pauseDuringDelivery: args[0] == "--verify-managed-replacement-during-delivery");
    return;
}

if (args is ["--verify-managed-replacement-with-native", var nativeSuccessorArtifact])
{
    await NativeWorkerProof.VerifyManagedRetiredResponse(nativeSuccessorArtifact, pauseDuringDelivery: false);
    return;
}

if (args is ["--verify-managed-retirement-cancels-request"])
{
    await NativeWorkerProof.VerifyManagedRetiredResponse(null, pauseDuringDelivery: false, cancelInFlight: true);
    return;
}

if (args is ["--verify-native-shutdown" or "--verify-native-shutdown-after-replacement", var shutdownArtifact])
{
    await NativeWorkerProof.VerifyTracerShutdown(shutdownArtifact, replaceBeforeShutdown: args[0] == "--verify-native-shutdown-after-replacement");
    return;
}

if (args is ["--verify-native-managed-successor-shutdown", var managedSuccessorArtifact])
{
    await NativeWorkerProof.VerifyManagedSuccessorShutdown(managedSuccessorArtifact);
    return;
}

if (args is ["--verify-native-late-managed-start", var lateManagedStartArtifact])
{
    await NativeWorkerProof.VerifyLateManagedStart(lateManagedStartArtifact);
    return;
}

if (args is ["--verify-native-feature-flags-shutdown" or "--verify-native-feature-flags-handoff" or "--verify-native-feature-flags-managed-chain", var featureFlagsArtifact])
{
    await NativeWorkerProof.VerifyFeatureFlagsLifetime(
        featureFlagsArtifact,
        replace: args[0] != "--verify-native-feature-flags-shutdown",
        managedPredecessors: args[0] == "--verify-native-feature-flags-managed-chain");
    return;
}

if (args is ["--verify-native-debugger-takeover" or "--verify-native-debugger-split-route" or "--verify-native-debugger-cutover", var debuggerArtifact])
{
    await NativeWorkerProof.VerifyDebuggerTakeover(
        debuggerArtifact,
        splitRoute: args[0] == "--verify-native-debugger-split-route",
        changeRouteOnTeardown: args[0] == "--verify-native-debugger-cutover");
    return;
}

if (args is ["--verify-native-debugger-disable", var disableArtifact])
{
    await NativeWorkerProof.VerifyDebuggerDisable(disableArtifact);
    return;
}

if (args is ["--verify-debugger-exporter-publication"])
{
    await NativeWorkerProof.VerifyDebuggerExporterPublication();
    return;
}

if (args is ["--verify-debugger-discovery-publication"])
{
    await NativeWorkerProof.VerifyDebuggerDiscoveryPublication();
    return;
}

if (args is ["--verify-native-debugger-agent-change", var agentChangeArtifact])
{
    await NativeWorkerProof.VerifyDebuggerAgentChange(agentChangeArtifact);
    return;
}
#endif

#if RC_AGENT_COMPARISON
if (args is ["--compare-agent", var artifact])
{
    AgentComparison.Run(artifact);
    return;
}

if (args is ["--compare-agent", var wasiArtifact, var nativeArtifact])
{
    AgentComparison.Run(wasiArtifact, nativeArtifact);
    return;
}
#endif

var native = args is ["--native", _];
if (!native && args.Length != 1)
{
    throw new ArgumentException("Pass the WASI artifact or --native with the absolute native library path");
}

using var engine = native ? null : new Engine();
using var module = engine is null ? null : Module.FromFile(engine, args[0]);
using var linker = engine is null ? null : new Linker(engine);
linker?.DefineWasi();
using var store = engine is null ? null : new Store(engine);
using var reactor = native ? Reactor.OpenNative(args[1]) : WasiReactorBinding.Open(store!, linker!, module!);

const string configPath = "datadog/2/APM_TRACING/wasm/config";
byte[] payload = [0, 255, 128, 1];
var identity = new { clientId = "dotnet-client", runtimeId = "dotnet-runtime" };
var client = reactor.WithJson(
    new
    {
        target = new { service = "original", env = "test", appVersion = "1" },
        identity,
        interest = new { products = new[] { "APM_TRACING" }, capabilities = new[] { "APM_TRACING_SAMPLE_RATE" } },
        language = "dotnet",
        tracerVersion = "1",
        url = "http://127.0.0.1:8126",
        timeoutMs = 2000,
    },
    reactor.Open);

try
{
    var unchanged = "{}"u8.ToArray();
    var initial = Exchange(reactor, client, unchanged, out _);
    Require(!initial.GetProperty("ready").GetBoolean() && initial.GetProperty("batch").GetInt32() == 0, "No change made the initial session ready");

    var first = Exchange(reactor, client, Assignment(configPath, 1, payload, includePayload: true), out _);
    var firstView = first.GetProperty("batch").GetInt32();
    Require(first.GetProperty("ready").GetBoolean() && firstView > 0, "Initial assignment is missing");
    var firstReceipt = reactor.Check(reactor.File(firstView, 0, 0));
    Require(reactor.Check(reactor.Apply(client, firstReceipt, 2, 0)) == 1, "Initial receipt could not be acknowledged");

    const ulong exactVersion = 9007199254740993;
    var cachedAssignment = Assignment(configPath, exactVersion, payload, includePayload: false);
    var reissued = Exchange(reactor, client, cachedAssignment, out _);
    var reissuedView = reissued.GetProperty("batch").GetInt32();
    Require(reissuedView > 0, "Version-only update did not produce a batch");
    var description = reactor.TakeJson(reactor.Check(reactor.Describe(reissuedView)));
    var record = description.GetProperty("records")[0];
    var file = record.GetProperty("file");
    Require(file.GetProperty("version").GetString() == "9007199254740993", "The version lost precision");
    Require(record.GetProperty("previous").GetProperty("version").GetString() == "1", "The retained revision changed");
    Require(reactor.Copy(firstReceipt).AsSpan().SequenceEqual(payload), "The previous bytes changed");
    Require(reactor.Check(reactor.Apply(client, firstReceipt, 2, 0)) == 0, "The old receipt was accepted");
    reactor.Free(firstReceipt);
    reactor.Check(reactor.Release(firstView));

    var current = reactor.Check(reactor.File(reissuedView, 0, 0));
    reactor.Check(reactor.Release(reissuedView));
    Require(reactor.Copy(current).AsSpan().SequenceEqual(payload), "The retained current bytes changed");
    Exchange(reactor, client, unchanged, out var pendingRequest);
    Require(State(pendingRequest).GetProperty("config_states")[0].GetProperty("apply_state").GetInt32() == 1, "Reissued revision did not start pending");
    Require(reactor.Check(reactor.Apply(client, current, 2, 0)) == 1, "Current receipt could not be acknowledged");
    Exchange(reactor, client, unchanged, out var appliedRequest);
    var applied = State(appliedRequest).GetProperty("config_states")[0];
    Require(applied.GetProperty("apply_state").GetInt32() == 2 && applied.GetProperty("version").GetUInt64() == exactVersion, "The next request lost the acknowledgement or version");

    var generation = reactor.WithJson(new { target = new { service = "new-target", env = "test", appVersion = "1" }, identity }, input => reactor.Retarget(client, input));
    Require(generation == 2 && reactor.Check(reactor.Snapshot(client)) == 0, "Retarget did not reset readiness");
    Require(reactor.Check(reactor.Apply(client, current, 2, 0)) == 0, "A receipt survived its target scope");
    reactor.Free(current);
    var pending = Exchange(reactor, client, unchanged, out var retargetRequest);
    Require(!pending.GetProperty("ready").GetBoolean(), "An unchanged reply restored readiness after retarget");
    Require(retargetRequest.GetProperty("client").GetProperty("client_tracer").GetProperty("service").GetString() == "new-target", "The request used the old target");
    Require(State(retargetRequest).GetProperty("config_states").GetArrayLength() == 0, "The request carried old apply states");

    var (http, timer, _) = Start(reactor, client);
    reactor.Check(reactor.Cancel(client));
    var cancellations = new HashSet<int>();
    for (var index = 0; index < 2; index++)
    {
        var cancelled = NextEvent(reactor, client);
        Require(cancelled.GetProperty("kind").GetString() == "cancel", "Cancellation did not drain host operations");
        cancellations.Add(cancelled.GetProperty("id").GetInt32());
    }

    Require(cancellations.SetEquals([http, timer]) && reactor.Check(reactor.Event(client)) == 0, "Cancellation lost an operation");
    var stopped = reactor.TakeJson(reactor.Check(reactor.Result(client)));
    Require(stopped.GetProperty("error").GetString()?.Contains("cancelled", StringComparison.Ordinal) is true, "Cancellation did not settle the poll");
    var resumed = Exchange(reactor, client, cachedAssignment, out _);
    var resumedView = resumed.GetProperty("batch").GetInt32();
    Require(resumed.GetProperty("ready").GetBoolean() && resumedView > 0, "The session did not recover after cancellation");
    var resumedDescription = reactor.TakeJson(reactor.Check(reactor.Describe(resumedView)));
    Require(resumedDescription.GetProperty("targetGeneration").GetString() == "2", "The assignment belongs to the wrong target generation");
    Require(reactor.Take(reactor.Check(reactor.File(resumedView, 0, 0))).AsSpan().SequenceEqual(payload), "The new target lost the cached bytes");
    reactor.Check(reactor.Release(resumedView));
}
finally
{
    reactor.Check(reactor.Close(client));
}

Console.WriteLine(native ? ".NET native Remote Config conformance passed" : ".NET WASI Remote Config conformance passed");

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static JsonElement State(JsonElement request) => request.GetProperty("client").GetProperty("state");

static JsonElement NextEvent(Reactor reactor, int client) => reactor.TakeJson(reactor.Check(reactor.Event(client)));

static (int Http, int Timer, JsonElement Request) Start(Reactor reactor, int client)
{
    reactor.Check(reactor.Poll(client));
    var request = NextEvent(reactor, client);
    var timer = NextEvent(reactor, client);
    Require(request.GetProperty("kind").GetString() == "http" && request.GetProperty("method").GetString() == "POST", "Expected the Agent HTTP operation");
    Require(request.GetProperty("url").GetString() == "http://127.0.0.1:8126/v0.7/config", "Unexpected HTTP endpoint");
    Require(timer.GetProperty("kind").GetString() == "sleep" && timer.GetProperty("delayMs").GetInt32() == 2000, "Expected the Agent deadline operation");
    Require(reactor.Check(reactor.Event(client)) == 0 && reactor.Check(reactor.Result(client)) == 0, "The poll produced an unexpected event or result");
    return (request.GetProperty("id").GetInt32(), timer.GetProperty("id").GetInt32(), reactor.TakeJson(request.GetProperty("body").GetInt32()));
}

static JsonElement Exchange(Reactor reactor, int client, byte[] response, out JsonElement request)
{
    var (http, timer, requestBody) = Start(reactor, client);
    request = requestBody;
    reactor.CompleteHttp(client, http, response);
    var cancelled = NextEvent(reactor, client);
    Require(cancelled.GetProperty("kind").GetString() == "cancel" && cancelled.GetProperty("id").GetInt32() == timer, "HTTP completion did not cancel its timer");
    var result = reactor.TakeJson(reactor.Check(reactor.Result(client)));
    if (result.TryGetProperty("error", out var error))
    {
        throw new InvalidOperationException(error.GetString());
    }

    return result;
}

static byte[] Assignment(string path, ulong version, byte[] payload, bool includePayload)
{
    var targets = new Dictionary<string, object>(StringComparer.Ordinal)
    {
        [path] = new { custom = new { v = version }, hashes = new { sha256 = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant() }, length = payload.Length },
    };
    var signed = JsonSerializer.SerializeToUtf8Bytes(new { signed = new { custom = new { opaque_backend_state = "dotnet-test" }, targets, version = 1 } });
    var files = includePayload ? new[] { new { path, raw = payload } } : [];
    return JsonSerializer.SerializeToUtf8Bytes(new { targets = signed, target_files = files, client_configs = new[] { path } });
}
