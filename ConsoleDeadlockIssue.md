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

## Proposed Solution: Reuse the IIS PreStartInit Gate for Non-IIS .NET Framework Processes

### Concept

The IIS deadlock fix (PR #6147) already implements a gate mechanism:
- The native profiler injects IL into `InvokePreStartInitMethods` that sets `AppDomain.CurrentDomain.SetData("Datadog_IISPreInitStart", true)` at method start and `false` at method end
- `CallTargetInvoker.CanExecuteCallTargetIntegration()` checks this AppDomain data and blocks all integrations while the flag is `true`
- This prevents CallTarget JIT rewrites from triggering type loading → `.cctor` → `ConfigurationManager` chains during the dangerous window

We can reuse this exact mechanism for non-IIS .NET Framework console apps with **managed-only changes**:

### Implementation (Managed-Only, Minimal Scope)

**Change 1: `CallTargetInvoker.cs` static constructor** — For non-IIS .NET Framework processes, set `_isIisPreStartInitComplete = false` instead of `true`. This blocks all CallTarget integrations until the gate is released.

```csharp
// Current (line 89-92):
else
{
    _isIisPreStartInitComplete = true;  // Integrations enabled immediately
}

// Proposed:
else
{
    // Block integrations until Instrumentation.Initialize() completes.
    // This prevents the ConfigurationManager deadlock when config builders
    // spawn ThreadPool threads that trigger type loading .cctor chains.
    _isIisPreStartInitComplete = false;
}
```

**Change 2: `Instrumentation.cs`** — After `InitializeNoNativeParts()` completes, release the gate by setting the AppDomain data to `false` (same value the IIS mechanism uses to signal "init complete").

```csharp
// After InitializeNoNativeParts(ref sw):
#if NETFRAMEWORK
AppDomain.CurrentDomain.SetData("Datadog_IISPreInitStart", false);
#endif
```

### Why This Works

1. When `CallTargetInvoker`'s `.cctor` runs, `_isIisPreStartInitComplete` is `false` for ALL .NET Framework processes
2. `CanExecuteCallTargetIntegration()` returns `false` → no CallTarget integration runs
3. ThreadPool threads spawned by config builders call instrumented methods → the native profiler rewrites the IL → JIT compiles it → but `BeginMethod` returns immediately (guard active)
4. No type loading for `IntegrationOptions`, `BeginMethodHandler`, etc. occurs on ThreadPool threads
5. No `.cctor` chains fire → no `ConfigurationManager` access from ThreadPool threads → no deadlock
6. After `Instrumentation.Initialize()` completes, the gate is released → integrations start running normally

### Scoping to Minimize Risk

The change should be scoped as narrowly as possible:
- **Only .NET Framework** (`#if NETFRAMEWORK`) — .NET Core doesn't have `ConfigurationManager` config builders
- **Only non-IIS console/service processes** — IIS already has its own mechanism; don't change that
- The `CanExecuteCallTargetIntegration` already allows `HttpModule_Integration` through during the gate (line 753-754), which is only relevant for IIS and harmless for console apps

### Risks and Mitigations

| Risk | Mitigation |
|------|-----------|
| Missing spans during startup | The gate is active only during `Instrumentation.Initialize()` — typically < 1 second. No user code runs during this window (startup hook runs before `Main()`). |
| `Instrumentation.Initialize()` fails → gate never released | Add a `finally` block or timeout fallback that releases the gate even on failure. The error catch in `CallTargetInvoker` already handles the case where `_isIisPreStartInitComplete` stays `false` — it polls `AppDomain.GetData` on each call. |
| Other .NET Framework apps affected (not just config builder users) | The extra overhead is one `AppDomain.GetData` check per integration call until the gate is released. This is the same overhead IIS apps already have. |
| IIS behavior changes | No change — the `is_desktop_iis` code path in the native profiler is untouched. IIS processes already have the flag set by native code before the managed `.cctor` runs. |

### Native Profiler: No Changes Required

The native profiler does NOT need to be modified. The existing IIS mechanism works by checking `AppDomain.GetData("Datadog_IISPreInitStart")`:
- If the data is not set (`null`), `CanExecuteCallTargetIntegration` falls through to the process name check
- For non-IIS processes, it currently sets `_isIisPreStartInitComplete = true` (our change sets it to `false` instead)
- For the release, `Instrumentation.Initialize()` calls `AppDomain.SetData("Datadog_IISPreInitStart", false)`
- The next `CanExecuteCallTargetIntegration` call sees `boolState is false` → sets `_isIisPreStartInitComplete = true` → cached forever

## Other Solutions Considered But Not Recommended

### A. Customer Workaround: Move Config Builder to Code

The customer confirmed this works: move Azure App Configuration from `app.config` `configBuilders` to `IConfiguration` via `AddAzureAppConfiguration()` in `Main()`.

**Pros**: Works today, no tracer changes needed.
**Cons**: Not viable for the current customer (15-year-old application). Only a workaround, not a fix.

### B. Customer Workaround: `COR_ENABLE_PROFILING=0` (Not Viable)

Disabling the profiler avoids the deadlock but also disables all auto-instrumentation.

### ~~C. Managed-Level Settings (Ruled Out)~~

`DD_TRACE_ENABLED=false`, `DD_DISABLED_INTEGRATIONS`, `DD_TRACE_WebRequest_ENABLED=false`, `DD_CLR_DISABLE_OPTIMIZATIONS`, and all other managed-level settings have been tested and **do not work**. The deadlock is caused by the native profiler's presence, not by any managed code path. See "Things Already Tried" #7-10.

## Files Modified During Investigation

| File | Change | Status |
|------|--------|--------|
| `CallTargetInvoker.cs` | Added `IsLoadingConfigurationManagerAppSettings` guard | In PR #8456 (dd-trace-6) |
| `GlobalConfigurationSource.cs` | Guard around AppSettings access, `isCalledFromStaticInitializer` param, `AddAppSettingsIfMissing` | Experimental (dd-trace-5) |
| `IntegrationOptions.cs` | Changed `Log` to `Lazy<IDatadogLogger>` | Experimental (dd-trace-5) |
| `Instrumentation.cs` | `AddAppSettingsIfMissing()` call after init | Experimental (dd-trace-5) |
| `Startup.NetFramework.cs` | `PreReadAppSettings()` (reverted) | Reverted |
| `Samples.ConsoleDeadLock/` | Repro sample app + integration test | In dd-trace-5 |
| `ConsoleDeadLockTests.cs` | Integration test with local HTTP server | In dd-trace-5 |

## Related

- [PR #6147](https://github.com/DataDog/dd-trace-dotnet/pull/6147) — IIS config builder deadlock fix (`PreStartInit` guard)
- [PR #1157](https://github.com/DataDog/dd-trace-dotnet/pull/1157) — Earlier IIS startup deadlock fix
- [PR #7312](https://github.com/DataDog/dd-trace-dotnet/pull/7312) — Deferred resource name calculation (related: APMS-19184 resource name issue)
- [PR #8456](https://github.com/DataDog/dd-trace-dotnet/pull/8456) — Initial fix attempt (guard in `CallTargetInvoker`)
- [docs/development/for-ai/ConsoleLock.md](ConsoleLock.md) — Initial deadlock analysis (before full investigation)
