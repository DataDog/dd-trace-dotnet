# Manual Verification: DogStatsD Named-Pipe Recovery (Azure App Service)

This document describes how to manually verify that `AgentProcessManager` detects and restarts a
`dogstatsd` process that is **running but has no bound named pipe**, in a real Azure App Service
(Windows) environment. It exists because there is no automated integration harness for
`AgentProcessManager` — nothing under `tracer/test` spawns the real `datadog-trace-agent`/`dogstatsd`
or drives the keep-alive loop; the manager is only invoked by the loader in production.

## Background

On Windows Azure App Service the Datadog site extension spawns `dogstatsd.exe` as a child process,
kept alive by `AgentProcessManager` (`tracer/src/Datadog.Trace/AgentProcessManager.cs`). The
dogstatsd listener is a **named pipe only** — `DD_DOGSTATSD_PORT=0` (UDP disabled), no UDS. The pipe
name is a fixed GUID baked into `applicationHost.xdt`, identical across every worker process and
every recycle.

During an IIS overlapped app-pool recycle, the old worker (and its `dogstatsd.exe`) can still be
alive while the new worker spawns its own `dogstatsd.exe` against the same pipe name. The second
`CreateNamedPipe` collides and dogstatsd logs `named pipe error: ... Access is denied` /
`Could not start dogstatsd: listening on neither udp nor socket`, then keeps running as a fully
alive but non-functional process with no listener bound. Because the process never exits, the old
health check (`NamedPipeIsBound() || ProgramIsRunning()`) considered it healthy forever and never
restarted it — so the .NET DogStatsD client could not connect and spawned runaway threads.

The fix makes a pipe-only dogstatsd healthy **only when its pipe is actually bound**, so once the
pipe frees up the manager restarts the broken instance.

> **Note:** The real OS-level pipe collision (`Access is denied`) is only reproducible with two real
> `dogstatsd.exe` processes — a generic `NamedPipeServerStream` does not trigger it. The procedure
> below therefore stages the **observable state** the manager must react to (a dogstatsd process
> running while its pipe is not bound), which is exactly what the fix keys on.

## Prerequisites

- A Windows Azure App Service app running the Datadog site extension built from a **private build
  containing the fix** (contrast runs use the currently released extension).
- App settings:
  - `DD_API_KEY` — set.
  - `DD_DOGSTATSD_PORT=0` — UDP disabled. This is what turns on the strict pipe-only health check.
  - `DD_AAS_ENABLE_CUSTOM_METRICS` — custom metrics enabled, so dogstatsd is expected.
  - `DD_TRACE_DEBUG=1` — verbose `AgentProcessManager` logs.
- Kudu access: **Advanced Tools → Go → Debug console → PowerShell**.
- The dogstatsd pipe name and the Agent path. In Kudu, read the pipe name with
  `$env:DD_DOGSTATSD_PIPE_NAME`; the Agent path looks like
  `C:\home\SiteExtensions\<extension>\Agent\dogstatsd.exe`.

## Baseline sanity check (app healthy)

```powershell
Test-Path "\\.\pipe\$env:DD_DOGSTATSD_PIPE_NAME"    # expect True (dogstatsd bound)
Get-Process dogstatsd | Select-Object Id, StartTime
```

## Stage the broken state and observe recovery

1. Record the current (functional) `dogstatsd` PID(s) and the pipe name `<GUID>`.

2. In Kudu PowerShell, start a **second real dogstatsd** against the same pipe to create a colliding,
   non-functional instance:

   ```powershell
   $env:DD_DOGSTATSD_PIPE_NAME = "<GUID>"
   $env:DD_DOGSTATSD_PORT = "0"
   & "C:\home\SiteExtensions\<extension>\Agent\dogstatsd.exe" start -c "C:\home\SiteExtensions\<extension>\Agent"
   ```

   Expect it to log `named pipe error: ... Access is denied` and
   `listening on neither udp nor socket`, and to keep running (broken).

3. Kill the **original functional** `dogstatsd` (the one the extension started that holds the pipe).
   Now the observable state is exactly the bug: the pipe is **not bound** (`Test-Path` → `False`)
   while a dogstatsd process is **still running** (the broken one).

4. Tail the tracer log (extension log directory, e.g. under the site's `LogFiles\datadog` or the
   configured `DD_TRACE_LOG_DIRECTORY`) and watch for `AgentProcessManager` messages:

   - **Fixed build:** within roughly 10–30s expect the sequence
     `NamedPipe ... is not present` → `Killing broken dogstatsd ... before restart` →
     `Attempting to start ...` → `Successfully started dogstatsd`. Then `Test-Path` flips back to
     `True` and `Get-Process dogstatsd | Select Id, StartTime` shows a freshly-started, bound
     instance.
   - **Old build (contrast):** no restart occurs; `Test-Path` stays `False`; the broken dogstatsd
     persists; custom metrics stop flowing and the app's DogStatsD-client thread count climbs.

5. Confirm recovery end-to-end: emit a custom metric from the app and verify it lands in Datadog;
   confirm the app's thread count stabilizes.

## Closest-to-production variant (overlapped recycle)

With custom metrics flowing, trigger an app restart/recycle from the portal and watch whether any
broken dogstatsd ever persists with an unbound pipe. With the fix, such an instance is detected and
relaunched. This is timing-dependent, so use it as a confirmation rather than the primary test.

## Detection commands recap

```powershell
Test-Path "\\.\pipe\$env:DD_DOGSTATSD_PIPE_NAME"       # pipe bound?
Get-Process dogstatsd | Select-Object Id, StartTime    # count + fresh StartTime => relaunched
```

plus tailing the tracer log for `AgentProcessManager` lines.
