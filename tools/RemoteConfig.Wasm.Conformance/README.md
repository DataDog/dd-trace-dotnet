# Remote Config reactor conformance

This tool exercises the shared Rust session through WASI ABI v4 and native C ABI v1. It does not change the installed tracer or load Remote Config in customer applications.

Build both artifacts from the same libdatadog revision. Pass an absolute path to the trusted native library. The native runner loads that file directly, checks the ABI, and resolves the exports once. It drops the reactor before it unloads the library. Run calls on one owner and do not dispose the reactor during a call or a buffer borrow.

```sh
dotnet run -c Release --project tools/RemoteConfig.Wasm.Conformance -- /path/to/reactor.wasm
dotnet run -c Release --project tools/RemoteConfig.Wasm.Conformance -- --native /absolute/path/to/reactor.dylib
```

For the Agent poll comparison, set `AgentComparison=true` to restore and build with the tracer reference. Use a core revision that supports `capabilityIndices`. The benchmark rotates the legacy, WASI, and native backends. The two reactor backends use the same managed transport and poll logic. A preflight checks delivered bytes, capability bits, and next-poll receipts before each timed run.

```sh
DOTNET_TieredCompilation=0 dotnet run -c Release -p:AgentComparison=true \
  --project tools/RemoteConfig.Wasm.Conformance -- \
  --compare-agent /path/to/reactor.wasm /absolute/path/to/reactor.dylib
```

The timed portion uses the tracer HTTP request factory with a memory-backed handler. It does not measure discovery, activation, product apply cost, native memory, or application latency. The tool targets .NET 10 and does not establish the supported tracer framework or package matrix.

## Live tracer worker

Use the native artifact with numeric interest and Agent 404 retention. The build below enables the candidate only in the tracer `net6.0` assembly. The `net461`, `netstandard2.0`, and `netcoreapp3.1` targets keep the existing client even when the build opts in. With no opt-in, all four targets keep the existing client.

The fixture runs the tracer worker from the `net6.0` assembly. It starts a local Agent over TCP, routes a named pipe to that Agent, and repeats the test over a Unix socket where supported. The fixture sets the `Datadog.Trace.SharedRemoteConfig.NativeLibrary` application-context value before it creates the tracer worker. The fixture runs on .NET 10 by default.

```sh
dotnet run -c Release -p:AgentComparison=true -p:SharedRemoteConfig=true \
  --project tools/RemoteConfig.Wasm.Conformance -- \
  --verify-native-worker /absolute/path/to/reactor.dylib
```

The fixture checks delivery, receipts, HTTP 404 retention, and removal across endpoint and capability changes. For shutdown, the Agent sends headers but holds the body. The worker must finish and disconnect without waiting for the body. The native preview uses the cancellable HTTP client for all three transports. The legacy Remote Config worker, tracer, and discovery keep their existing transport selection. Running the named-pipe fixture on macOS does not validate Windows, package compatibility, or application impact.

Raw subscribers keep their installed paths when they outlive a worker. A verified assignment withdraws omitted paths for the subscriber that received them. A response without an assignment preserves those paths. Returning from native polling starts a new managed client session with fresh receipts.

Run the replacement case in a fresh process with `--verify-native-replacement` and the same native library path. It uses the global tracer and holds a trace flush during replacement. It checks that the old poll disconnects, the successor omits the retired Feature Flags subscription, the new tracer starts with its own receipts, and a tracing-tag update retains its acknowledgment.

Run `--verify-managed-replacement` in a fresh process to use the managed client. It completes an Agent response between publishing the successor and starting its worker. The successor fetches and installs the assignment on its own first poll and reports the receipt on the next poll.

Run `--verify-managed-replacement-disabling-rc` or `--verify-managed-replacement-with-native /path/to/reactor` in fresh processes for incompatible successors. Both complete the old managed request after its worker stops accepting responses. The late assignment leaves the successor's settings intact.

Run `--verify-managed-replacement-during-delivery` in a fresh process to pause an admitted response inside a subscriber. After the tracer changes, resuming delivery leaves the next managed client's files and receipts clear of that response.

Run `--verify-managed-retirement-cancels-request` in a fresh process to replace a managed worker during a poll. The Agent observes the predecessor disconnect before it releases the response, and the successor starts polling with an empty receipt set.

Run `--verify-native-raw-product-handoff /path/to/reactor` to change one raw subscription during delivery and on an unchanged poll. The replacement receives the latest verified assignment. The sibling product keeps its receipt and receives one callback.

The `--verify-native-retired-callback` case holds an active subscription while the new tracer disables Remote Config. It checks the replacement's settings and checks that a successor can retry the same debugger settings after the old activation is canceled.

Run `--verify-native-shutdown` and `--verify-native-shutdown-after-replacement` in separate processes with the same native library path. Each pauses an APM callback. Both check that the flare subscription stays registered until the native worker finishes. The second case disables Remote Config on the successor before shutdown.

The `--verify-native-managed-successor-shutdown` case replaces the native worker with a managed one. After native retirement, it holds a managed poll in flight while the tracer shuts down.

The `--verify-native-late-managed-start` case delays the first managed startup until its native successor begins polling. It checks that the retired manager leaves the successor's APM snapshot subscription intact.

The `--verify-native-feature-flags-shutdown` case pauses a Feature Flags configuration handler. It checks that the Remote Config subscription remains registered until the handler returns.

Run `--verify-native-feature-flags-handoff` or `--verify-native-feature-flags-managed-chain` in a fresh process. The successor registers Feature Flags after the previous callback drains and before it polls. The managed chain replaces an intermediate worker before it starts polling.

The `--verify-native-debugger-takeover` case starts the debugger with a file probe. It checks that the runtime survives an Agent without a debugger endpoint and a canceled takeover. The final replacement Agent provides both endpoints, and its polls advertise Live Debugging.

The `--verify-native-debugger-split-route` case changes discovery from a debugger-only result to a config-only result while the candidate prepares. It checks that the file runtime stays active until the current discovery advertises both endpoints together.

The `--verify-native-debugger-cutover` case changes discovery when the old transport releases its subscription. It checks that the new runtime has taken over before the old transport can publish that change.

The `--verify-debugger-exporter-publication` case pauses settings notification before the Debugger transport updates its route. It checks that takeover waits for both endpoints on the published exporter.

The `--verify-debugger-discovery-publication` case keeps the Agent URL fixed and checks readiness with each endpoint alone. It also pauses discovery notification after `/info` withdraws the Debugger endpoint. Takeover waits until discovery publishes both endpoints again.

The `--verify-debugger-upload-admission` case pauses a diagnostics upload after it selects a route. It checks that disposal and exporter changes reject pending admission and that replay of an identical snapshot preserves admission. The control upload uses an Agent with only a Debugger endpoint.

The `--verify-debugger-upload-blocked-connect` case pauses a synchronous stream connection after admission. Exporter publication completes while the connection waits. The admitted upload can then finish.

The `--verify-rc-route-withdrawal` cases hold a Remote Configuration reply while `/info` withdraws that endpoint on the same Agent. They check that the managed poller, raw native poller, and native watch discard the held reply before discovery finishes notifying subscribers. The `watch-commit` case withdraws the route after the watch builds new settings and checks that those settings never reach subscribers.

The `--verify-native-debugger-disable` case applies a disable update while debugger startup waits for Agent discovery. It checks that startup ends and that the tracer has no Live Debugging subscription.

The `--verify-debugger-startup-replacement` case replaces the tracer after startup captures its settings. The debugger must send a snapshot through the successor's Agent route and label it with the successor's service.

The `--verify-native-debugger-agent-change` case changes the Agent before and after debugger activation. It leaves a poll outstanding on the original Agent during the first switch. The same tracer rejects that response and sends each synthetic snapshot to the current Agent.

On Linux, use the reactor archive that matches the host architecture and C library. The native worker passed on Ubuntu and Alpine ARM64 with .NET 10.0.15. Wasmtime 48.0.2 in this tool depends on glibc. Use a native-architecture runner for the Linux x64 worker checks. [Microsoft .NET 10 runtime support](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md)

To run the x64 check in CI, start the `Validate native configuration registry` workflow with a trusted libdatadog preview commit in `remote-config-source`. The job builds the matching library for each libc and runs the fixture with the default .NET runtime settings.

When APM tracing has one snapshot subscriber, Rust delivers the full verified assignment through a latest-value watch. The tracer merges and installs the settings before it reports the outcome. Unchanged files remain available when one file changes. Invalid files retain independent error receipts. A failed install can retry on an unchanged poll. Raw products still use the low-level API.

To run the worker on .NET 6, build the fixture with the repository SDK. Run it with an isolated .NET 6 runtime that includes ASP.NET Core.

```sh
dotnet build -c Release tools/RemoteConfig.Wasm.Conformance \
  -p:ConformanceTargetFramework=net6.0 -p:AgentComparison=true -p:SharedRemoteConfig=true \
  -p:UseAppHost=false -p:CheckEolTargetFramework=false
/path/to/dotnet6/dotnet exec --roll-forward LatestPatch \
  tools/RemoteConfig.Wasm.Conformance/bin/Release/net6.0/Datadog.Trace.Tests.dll \
  --verify-native-worker /absolute/path/to/reactor.dylib
```

`LatestPatch` restricts framework resolution to the .NET 6 patch line. Use this runtime for compatibility tests, not as a deployment recommendation.

Build all four tracer targets from a restored tree to check the framework gate:

```sh
dotnet build tracer/src/Datadog.Trace/Datadog.Trace.csproj -c Release --no-restore --disable-build-servers \
  -p:SharedRemoteConfig=true
```

To compare the live legacy, raw native, and watch workers, use the Release assembly from the same opt-in build:

```sh
node tools/RemoteConfig.Wasm.Conformance/compare-live-workers.mjs \
  "$PWD/tools/RemoteConfig.Wasm.Conformance/bin/Release/net10.0/Datadog.Trace.Tests.dll" \
  /absolute/path/to/reactor.dylib 5
```

Each trial starts a fresh tracer process with tiered compilation disabled. A separate Agent process checks capability bits, config state, and receipts. It holds the boundary requests so the two workers complete the same number of polls within the measured interval. The driver writes JSON lines for process CPU, elapsed time, managed allocation, working set, and time from worker start to its first HTTP request. The managed live-heap snapshot runs after the timed interval. This stress test uses a short poll interval and an ACK-only product callback. It does not measure the default cadence, installed package startup, or application latency.
