# Console App Deadlock with ConfigurationManager Config Builders (APMS-19239)

## Problem

On .NET Framework 4.7.1+, console applications (and Windows services) **hang on startup** when both of these are true:

1. `app.config` uses **config builders** on `<appSettings>` or `<connectionStrings>` (e.g., `Microsoft.Configuration.ConfigurationBuilders.AzureAppConfiguration`) that make **outbound HTTP calls** during initialization.
2. The **Datadog .NET tracer** is enabled via the CLR profiler.

The process never reaches `Main()`. There is no error message, no exception, no log — the process simply hangs indefinitely.

IIS-hosted applications are **not affected** because PR [#6147](https://github.com/DataDog/dd-trace-dotnet/pull/6147) gates CallTarget integrations during `IisPreStartInit`.

## Customer Impact

- **Ticket**: APMS-19239
- **Customer**: Running a .NET Framework 4.7.2 console app (`pli.wms.scheduler`) with Azure App Configuration (`DefaultAzureCredential` + Azure Key Vault) wired through `app.config` config builders.
- **Workaround confirmed by customer**: Moving Azure App Configuration from `app.config` to `IConfiguration` in `Main()` avoids the deadlock — but the customer cannot do this because it would require rewriting configuration for a 15-year-old application.
- **Tracer version**: 3.41.0 (but the issue exists in all versions that read `ConfigurationManager.AppSettings` during initialization).

## Root Cause

The deadlock involves the CLR's `ConfigurationManager` internal lock, the type initializer (`.cctor`) lock mechanism, and the ThreadPool.

### The Chain of Events

1. The **native CLR profiler** attaches to the process and registers CallTarget instrumentation hooks for methods like `HttpWebRequest.GetResponse()`, `Process.Start()`, etc.

2. The **managed loader** (`Startup.cs`) runs and loads `Datadog.Trace.dll`. This triggers `ConfigurationManager` to process `app.config` for assembly binding redirects.

3. The CLR's `ConfigurationManager` processes `<appSettings configBuilders="...">`, which triggers the **config builder** (e.g., Azure App Configuration). The config builder makes outbound HTTP calls to Azure endpoints. Internally, the Azure SDK uses `Task.Run(() => ...).Wait()` patterns (e.g., for `DefaultAzureCredential` token acquisition).

4. `ConfigurationManager` holds an **internal lock** during config builder execution. The `Task.Run` schedules work on a **ThreadPool thread**. The main thread blocks on `task.Wait()`.

5. The **ThreadPool thread** executes an HTTP call (e.g., `WebRequest.GetResponse()`). Because the native profiler registered CallTarget hooks, the JIT compiler rewrites this method to include CallTarget instrumentation.

6. The CallTarget instrumentation triggers **type loading** for `IntegrationOptions<T,T>`, `BeginMethodHandler<T,T>`, `IntegrationMapper`, etc. These types have static field initializers that chain to `DatadogLogging` → `GlobalSettings` → `GlobalConfigurationSource`, which accesses `ConfigurationManager.AppSettings`.

7. The ThreadPool thread's `ConfigurationManager.AppSettings` access tries to acquire the **same internal lock** held by the main thread → **blocked**.

8. **Deadlock**: Main thread waits for `Task.Run` to complete (step 4). ThreadPool thread can't complete because it's blocked on the `ConfigurationManager` lock (step 7).

### Diagram

```
Thread A (main)                                 Thread B (ThreadPool)
───────────────                                 ─────────────────────
Managed loader loads Datadog.Trace.dll
  └─ CLR processes app.config
       └─ ConfigurationManager acquires lock
            └─ Config builder fires
                 └─ Azure SDK: Task.Run(...)
                      └─ task.Wait()              Lambda scheduled...
                           │                      └─ WebRequest.GetResponse()
                           │                           └─ CallTarget JIT rewrite
                           │                                └─ Type loading
                           │                                     └─ DatadogLogging .cctor
                           │                                          └─ GlobalConfigurationSource
                           │                                               └─ ConfigurationManager.AppSettings
                           │                                                    └─ BLOCKED (lock held by Thread A)
                           │  ◄── waiting ──────────────────────────────────────────────────────────────────┘
                           ▼
                       DEADLOCK
```

## Why IIS Is Not Affected

PR [#6147](https://github.com/DataDog/dd-trace-dotnet/pull/6147) added a guard in `CallTargetInvoker` that blocks ALL CallTarget integrations (except `HttpModule_Integration`) while `IisPreStartInit` is running. This prevents the instrumentation from triggering the `.cctor`/CM chain during the dangerous startup window. Console apps have no equivalent lifecycle hook.

## Reproduction

A minimal repro is available at `tracer/test/test-applications/integrations/Samples.ConsoleDeadLock/` with an integration test at `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/ConsoleDeadLockTests.cs`.

### Repro Components

1. **`App.config`**: Declares a custom config builder on `<appSettings>`:
   ```xml
   <appSettings configBuilders="HttpBuilder">
     <add key="TestKey" value="TestValue" />
   </appSettings>
   ```

2. **`HttpConfigBuilder.cs`**: A `ConfigurationBuilder` that makes an HTTP call on a background thread using the same `Task.Run(() => WebRequest.GetResponse()).Wait()` pattern as the Azure SDK:
   ```csharp
   public override XmlNode ProcessRawXml(XmlNode rawXml)
   {
       var task = Task.Run(() => {
           var request = WebRequest.Create(url);
           using (var response = request.GetResponse()) { }
       });
       task.Wait(); // Blocks main thread while holding CM lock
       return rawXml;
   }
   ```

3. **Integration test**: Starts a local `HttpListener`, launches the sample app with the tracer injected, and asserts it completes within 30 seconds. On master (without fix), the test times out.

### Running the Repro

```bash
# Build everything (native + managed)
./tracer/build.cmd BuildTracerHome

# Build the sample app
dotnet build tracer/test/test-applications/integrations/Samples.ConsoleDeadLock/

# Build and run the test
dotnet build tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/ -f net48
dotnet test tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/ -f net48 --no-build --filter "FullyQualifiedName~ConsoleDeadLockTests"
```

### Key Observation

The sample app runs fine **without the tracer**:
```bash
Samples.ConsoleDeadLock.exe
# Output: Main() reached - no deadlock!
```

It hangs with the tracer attached because the CallTarget JIT rewriting on the ThreadPool thread triggers the `.cctor` → `ConfigurationManager` chain.

## Things Already Tried

### 1. `CallTargetInvoker.IsLoadingConfigurationManagerAppSettings` Guard (PR #8456)

**Approach**: A `volatile bool` on `CallTargetInvoker` set before/after reading `ConfigurationManager.AppSettings` in `GlobalConfigurationSource`. When set, `CanExecuteCallTargetIntegration()` returns `false`, blocking all integrations.

**Result**: Does not work. The guard is a **runtime check** inside `CanExecuteCallTargetIntegration()`. But the deadlock occurs at the **JIT/type-loading level** — when the JIT compiles `CallTargetInvoker.BeginMethod<T,T>()` on the ThreadPool thread, it loads `IntegrationOptions<T,T>` which has a static field initializer (`IDatadogLogger Log = DatadogLogging.GetLoggerFor(...)`) that chains to `ConfigurationManager` through `DatadogLogging` → `GlobalConfigurationSource`. The guard code is never reached because the type initializer blocks first.

### 2. `Lazy<IDatadogLogger>` in `IntegrationOptions<T,T>`

**Approach**: Changed `IntegrationOptions<T,T>.Log` from `IDatadogLogger` to `Lazy<IDatadogLogger>` to break the `.cctor` chain. The logger is only resolved on first use (inside `LogException`), not during type initialization.

**Result**: Breaks the `.cctor` chain through `IntegrationOptions`, but the deadlock still occurs because `IntegrationMapper` (used in `BeginMethodHandler`'s `.cctor`) also has a non-lazy `IDatadogLogger Log` field. And even after fixing both, the deadlock persists because the fundamental issue is `ConfigurationManager`'s internal lock, not the `.cctor` lock.

### 3. Skip `ConfigurationManager.AppSettings` During Static Initialization

**Approach**: Added `isCalledFromStaticInitializer` parameter to `CreateDefaultConfigurationSource()`. When called from the static property initializer, AppSettings is skipped. A deferred `AddAppSettingsIfMissing()` method reads AppSettings after initialization completes.

**Result**: Does not work. The deadlock is NOT triggered by our code reading `ConfigurationManager.AppSettings`. It's triggered by the **CLR itself** processing `app.config` when loading assemblies (for binding redirects). The config builder fires during assembly loading, before any of our managed code runs.

### 4. Pre-read AppSettings in the Managed Loader

**Approach**: Read `ConfigurationManager.AppSettings` in `Startup.NetFramework.cs` before loading `Datadog.Trace.dll`, storing the result via `AppDomain.SetData` for later use.

**Result**: Does not work. `PreReadAppSettings()` triggers the config builder, which spawns a ThreadPool thread. That thread loads `Datadog.Trace.dll` (via the `AssemblyResolve` handler), which triggers the same `.cctor`/CM deadlock chain.

### 5. Remove AppSettings Access Entirely

**Approach**: Completely removed all `ConfigurationManager.AppSettings` access from the tracer. The tracer reads configuration only from environment variables.

**Result**: Does not work. The config builder is triggered by the **CLR's own `ConfigurationManager` access** when processing `app.config` for assembly binding redirects. The tracer doesn't need to call `ConfigurationManager.AppSettings` at all — the CLR does it automatically when any assembly is loaded and `app.config` has `<appSettings configBuilders="...">`.

### 6. Increase ThreadPool Minimum Threads

**Approach**: Called `ThreadPool.SetMinThreads(8, 8)` before reading AppSettings to ensure enough threads for config builder HTTP calls.

**Result**: Does not work. The issue is not ThreadPool starvation — it's a lock ordering deadlock between `ConfigurationManager`'s internal lock and the ThreadPool thread's need for that same lock.

### 7. `DD_TRACE_WebRequest_ENABLED=false`

**Approach**: Disable just the WebRequest integration via environment variable, preventing the managed integration handlers from running.

**Result**: Does not work. `DD_TRACE_WebRequest_ENABLED=false` disables the managed `OnMethodBegin`/`OnMethodEnd` handlers, but the native CallTarget JIT rewrite still fires — the method IL is still rewritten to call `CallTargetInvoker.BeginMethod`. The JIT compilation of that rewritten method still triggers the type loading `.cctor` chain.

### 8. `DD_DISABLED_INTEGRATIONS=WebRequest`

**Approach**: Disable the WebRequest integration via the disabled integrations list, hoping the native profiler would skip hook registration.

**Result**: Does not work. `DD_DISABLED_INTEGRATIONS` is read by the managed tracer, not the native profiler. The native profiler still registers CallTarget hooks for `WebRequest.GetResponse`.

### 9. `DD_TRACE_ENABLED=false`

**Approach**: Disable all tracing entirely while keeping the profiler attached.

**Result**: Does not work. The native profiler still loads, still processes module loads, still triggers assembly loading side effects that cause the `ConfigurationManager` deadlock. `DD_TRACE_ENABLED` only affects the managed tracer behavior, not the native profiler's presence.

### 10. `COR_ENABLE_PROFILING=0`

**Approach**: Disable the CLR profiler entirely.

**Result**: **Works** — the app starts instantly. This confirms the deadlock is caused by the native profiler's **mere presence**, not by any specific managed code path. Just having the profiler attached changes the CLR's assembly loading and `ConfigurationManager` timing enough to trigger the deadlock.

### 11. Managed PreStartInit Gate for Non-IIS Processes

**Approach**: In `CallTargetInvoker`'s static constructor, set `_isIisPreStartInitComplete = false` for non-IIS .NET Framework processes (instead of `true`). Release the gate from `Instrumentation.Initialize()` via `AppDomain.SetData("Datadog_IISPreInitStart", false)`.

**Result**: Does not work. The gate blocks `CanExecuteCallTargetIntegration()` at the managed level, but the deadlock occurs before any managed guard code runs. The ThreadPool thread's `WebRequest.Create()` accesses `ConfigurationManager` internally (for proxy settings), blocking on the CM lock held by Thread A. This is a framework-level lock contention, not a managed CallTarget issue.

### 12. `Lazy<IDatadogLogger>` in IntegrationOptions + IntegrationMapper + ContinuationGenerator (Combined with Gate)

**Approach**: Made ALL `IDatadogLogger` fields in the CallTarget handler infrastructure lazy (`Lazy<IDatadogLogger>`) to break every `.cctor` → `DatadogLogging` → `ConfigurationManager` chain. Combined with the managed PreStartInit gate (#11).

**Result**: Does not work. Even with all `.cctor` chains fully broken, the deadlock persists. The ThreadPool thread's `WebRequest.Create()` accesses `ConfigurationManager` internally — this is inside the .NET Framework's own implementation, not triggered by any Datadog type loading. The deadlock is between `ConfigurationManager`'s internal lock (held by Thread A during config builder execution) and `WebRequest`'s internal `ConfigurationManager` access (on Thread B).

## Key Findings

### 0. The Deadlock is Caused by the Native Profiler's Presence, Not Managed Code

**This is the most critical finding.** Setting `DD_TRACE_ENABLED=false`, `DD_DISABLED_INTEGRATIONS=WebRequest`, or removing ALL `ConfigurationManager.AppSettings` calls from the tracer does NOT prevent the deadlock. Setting `COR_ENABLE_PROFILING=0` DOES prevent it.

The native CLR profiler's presence changes the CLR's behavior during assembly loading:
- The CLR fires `ModuleLoadFinished` callbacks for every assembly
- The profiler processes each module, checking for instrumentation targets
- This additional processing extends the time the main thread spends inside `ConfigurationManager`'s lock scope
- The config builder's `Task.Run` ThreadPool thread starts and hits the CM lock before the main thread releases it

Without the profiler, the CLR processes `app.config` fast enough that the `Task.Run` thread doesn't start (or doesn't reach the CM lock) until after the main thread releases it. The profiler's overhead creates a window where the lock contention occurs.

**No managed-level setting (`DD_TRACE_ENABLED`, `DD_DISABLED_INTEGRATIONS`, `DD_TRACE_WebRequest_ENABLED`, etc.) can fix this.** The fix must be at the native profiler level.

### 1. The Deadlock is on `ConfigurationManager`'s Internal Lock, Not `.cctor` Locks

Initially we believed the deadlock was on the CLR's type initializer lock for `GlobalConfigurationSource`. While `.cctor` chains DO contribute to the problem (by triggering `ConfigurationManager` access on the ThreadPool thread), the actual blocking resource is `ConfigurationManager`'s internal lock.

Evidence: Even after making all `.cctor`s complete instantly (by making all `DatadogLogging` references lazy and skipping AppSettings), the deadlock persists because `WebRequest.Create()` itself accesses `ConfigurationManager` internally for proxy settings and binding redirects.

### 2. The Config Builder is Triggered by the CLR, Not by Our Code

The config builder fires when `ConfigurationManager` processes `app.config` — which happens the first time ANY configuration section is accessed, including `<runtime>` for assembly binding redirects. Loading `Datadog.Trace.dll` triggers assembly binding, which triggers `ConfigurationManager`, which triggers the config builder.

Evidence: Removing all `ConfigurationManager.AppSettings` calls from the tracer does not prevent the deadlock. The `[ConfigBuilder] ProcessRawXml` message still appears in stdout.

### 3. The Pattern `Task.Run(() => WebRequest.GetResponse()).Wait()` is Inherently Deadlock-Prone Inside Config Builders

Any code that does synchronous-over-async (`Task.Run().Wait()`, `.GetAwaiter().GetResult()`) inside a `ConfigurationBuilder.ProcessRawXml()` callback on .NET Framework is at risk of deadlocking if:
- The scheduled work accesses `ConfigurationManager` (directly or indirectly)
- Or the scheduled work triggers type loading that chains to `ConfigurationManager`

The Azure SDK's `DefaultAzureCredential` uses these patterns internally for credential acquisition.

### 4. Debug Mode Changes Timing and Hides the Deadlock

Running the test under a debugger (or with `DD_TRACE_DEBUG=1` and debug symbols loaded) changes the JIT timing enough that all `.cctor`s complete before the config builder's ThreadPool thread tries to load types. This makes the deadlock non-deterministic and harder to reproduce. The production (optimized) build consistently deadlocks.

### 5. The IIS Fix (PR #6147) Works Because It Gates at the Right Level

The IIS `PreStartInit` guard blocks CallTarget integrations **at the native level** — before any managed code runs. This prevents the JIT from rewriting methods with CallTarget hooks during the dangerous window. A similar approach for console apps would need to:
- Block CallTarget integrations until `Instrumentation.Initialize()` completes
- Signal from managed code to the native profiler when it's safe to enable integrations

## Proposed Solution: Native Profiler Must Defer CallTarget JIT Rewrites

### Why Managed-Only Fixes Cannot Work

We tried every managed-level approach (see "Things Already Tried" #1-12). The fundamental issue is:

1. The native profiler rewrites method IL **during JIT compilation** on the ThreadPool thread
2. The rewritten IL references `CallTargetInvoker.BeginMethod<T,T>` which triggers type loading
3. Even with all `.cctor` chains broken (lazy loggers), `WebRequest.Create()` itself accesses `ConfigurationManager` internally for proxy settings and binding redirects
4. This `ConfigurationManager` access blocks on the CM lock held by Thread A

The deadlock happens **inside the .NET Framework's own `WebRequest` implementation**, not in our managed code. No amount of managed-code changes can prevent `WebRequest.Create()` from accessing `ConfigurationManager`.

### What Needs to Happen

The native profiler must **not rewrite method IL with CallTarget hooks** while the `ConfigurationManager` lock may be held. Two approaches:

#### Option A: Extend `AddIISPreStartInitFlags` to All .NET Framework Processes

Currently, `AddIISPreStartInitFlags` (in `cor_profiler.cpp:4034`) is only called for IIS processes (`is_desktop_iis` check at line 1634). Extending it to ALL .NET Framework processes would:

1. Inject `AppDomain.SetData("Datadog_IISPreInitStart", true)` at the **start** of the startup hook method
2. Inject `AppDomain.SetData("Datadog_IISPreInitStart", false)` at the **end** of the startup hook method
3. The managed `CallTargetInvoker.CanExecuteCallTargetIntegration()` already checks this flag — no managed changes needed

The startup hook runs inside `Main()` (or the first JIT-compiled method). It completes before user code runs. During its execution, CallTarget integrations are blocked, preventing the deadlock.

**Key difference from current IIS behavior**: For IIS, the flag wraps `InvokePreStartInitMethods`. For console apps, it would wrap the startup hook inside `Main()`. The effect is the same — integrations are deferred until after tracer initialization.

```cpp
// cor_profiler.cpp, line 1634:
// Current:
if (is_desktop_iis)
{
    hr = AddIISPreStartInitFlags(module_id, function_token);
}

// Proposed:
if (!runtime_information_.is_core())  // All .NET Framework processes
{
    hr = AddIISPreStartInitFlags(module_id, function_token);
}
```

**Risk**: The startup hook for console apps wraps `Main()`. `AddIISPreStartInitFlags` sets the flag to `false` at the method's `ret` instruction — meaning integrations would only be released when `Main()` returns (far too late). The native code would need modification to set the flag to `false` **after the startup hook call** but **before the rest of `Main()`'s original code**, or the managed code (`Instrumentation.Initialize()`) would need to release it via `AppDomain.SetData`.

#### Option B: Defer CallTarget ReJIT Until After Managed Init Signal

Instead of rewriting methods immediately in `ModuleLoadFinished`, queue the ReJIT requests and apply them after `Instrumentation.Initialize()` signals completion. This is a larger change but more robust.

### Customer Workarounds (Available Today)

#### Move Config Builder to Code (Confirmed Working)

Move Azure App Configuration from `app.config` `configBuilders` to `IConfiguration` via `AddAzureAppConfiguration()` in `Main()`. The customer confirmed this works but says it's not viable for their 15-year-old application.

#### `COR_ENABLE_PROFILING=0` (Not Viable)

Disabling the profiler avoids the deadlock but also disables all auto-instrumentation.

### ~~Managed-Level Settings (All Ruled Out)~~

`DD_TRACE_ENABLED=false`, `DD_DISABLED_INTEGRATIONS`, `DD_TRACE_WebRequest_ENABLED=false`, `DD_CLR_DISABLE_OPTIMIZATIONS`, managed PreStartInit gate, `Lazy<IDatadogLogger>` in handlers, skipping AppSettings during `.cctor`, pre-reading AppSettings in managed loader — all tested and **do not work**. See "Things Already Tried" #1-12.

## Reproduction Branch

All repro code and the integration test are in the branch: https://github.com/DataDog/dd-trace-dotnet/compare/master...nacho/ConsoleHangs

| File | Purpose |
|------|---------|
| `Samples.ConsoleDeadLock/` | Minimal repro: custom config builder that does `Task.Run(() => WebRequest.GetResponse()).Wait()` |
| `ConsoleDeadLockTests.cs` | Integration test: starts local HTTP server, launches app with tracer, asserts it completes in 30s |
| `ConsoleDeadlockIssue.md` | This document |

## Related

- [PR #6147](https://github.com/DataDog/dd-trace-dotnet/pull/6147) — IIS config builder deadlock fix (`PreStartInit` guard)
- [PR #1157](https://github.com/DataDog/dd-trace-dotnet/pull/1157) — Earlier IIS startup deadlock fix
- [PR #7312](https://github.com/DataDog/dd-trace-dotnet/pull/7312) — Deferred resource name calculation (related: APMS-19184 resource name issue)
- [PR #8456](https://github.com/DataDog/dd-trace-dotnet/pull/8456) — Initial fix attempt (guard in `CallTargetInvoker`)
- [docs/development/for-ai/ConsoleLock.md](ConsoleLock.md) — Initial deadlock analysis (before full investigation)
