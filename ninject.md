# APMS-18833: ASP.NET WebForms + Ninject Integration Test

## Problem

Customer has an ASP.NET WebForms app (.NET Framework 4.6.2) on IIS that is not producing APM traces. The Datadog .NET tracer is confirmed attached to the process, but no `aspnet.request` spans are generated for WebForms page loads.

The tracer logs show:
```
10/22/25 02:25:05.155 PM [18404|10864] [info] JITCompilationStarted: NInjectModuleLoader appdomain detected. Not registering startup hook.
```

### Root Cause Hypothesis

Ninject's `AssemblyNameRetriever` creates a temporary AppDomain named `"NinjectModuleLoader"` to scan assemblies for modules ([source](https://github.com/ninject/Ninject/blob/01c86efb64f551e1f6a043ef2d5c03a2612918dd/src/Ninject/Modules/AssemblyNameRetriever.cs#L45-L65)). The tracer's native profiler detects this AppDomain and intentionally skips startup hook registration to avoid `CannotUnloadAppDomainException` (see `cor_profiler.cpp:1488-1496`, [PR #2600](https://github.com/DataDog/dd-trace-dotnet/pull/2600)).

The question is: **does the tracer properly initialize in the main AppDomain after Ninject finishes its module loading?** The customer's page loads happen after Ninject initialization, so they should be traced.

## What Was Built

### Test Application: `Samples.WebForms.Ninject`

**Location:** `tracer/test/test-applications/aspnet/Samples.WebForms.Ninject/`

A minimal ASP.NET WebForms app (.NET Framework 4.8) that replicates the customer's environment:

| File | Purpose |
|------|---------|
| `App_Start/NinjectWebCommon.cs` | Standard Ninject bootstrapper using `[WebActivatorEx.PreApplicationStartMethod]` — runs **before** `Application_Start()` and before the tracer's `BuildManager.InvokePreStartInitMethodsCore` hook. Registers `NinjectHttpModule`, `OnePerRequestHttpModule`, and initializes the kernel with assembly scanning (triggers `NinjectModuleLoader` AppDomain). |
| `Global.asax.cs` | Exposes the kernel via `Bootstrapper().Kernel`. No longer initializes Ninject directly — that's handled by `NinjectWebCommon`. |
| `Services/AppModule.cs` | `NinjectModule` that registers `IDataRepository` -> `InMemoryDataRepository` |
| `Services/IDataRepository.cs` | Interface mimicking customer's `IAcaRepository` |
| `Services/InMemoryDataRepository.cs` | Simple in-memory implementation |
| `Default.aspx` + `.cs` | WebForms page that resolves `IDataRepository` through the kernel (mimics `NinjectProvider.Get<T>()` pattern) |
| `Account/Login.aspx` + `.cs` | Shutdown endpoint for clean test teardown |
| `App_Start/RouteConfig.cs` | FriendlyUrls routing |
| `Web.config` | Binding redirects for Ninject assembly versions |
| `packages.config` | NuGet deps: Ninject 3.3.6, Ninject.Web.Common 3.3.2, Ninject.Web.Common.WebHost 3.3.2, WebActivatorEx 2.2.0 |

### Integration Test: `AspNetWebFormsNinjectTests`

**Location:** `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/AspNet/AspNetWebFormsNinjectTests.cs`

- Uses `[UsesVerify]` snapshot verification (Verify library)
- Tests 3 endpoints: `/`, `/Default`, `/Account/Login`
- Uses `IisFixture` with `IisAppType.AspNetIntegrated`
- Collects spans via `GetWebServerSpans` and verifies against snapshot files

### Snapshot Files

**Location:** `tracer/test/snapshots/`

- `AspNetWebFormsNinjectTests.__path=__statusCode=200.verified.txt` — root path `/`, expects 2 spans (root + redirect to default.aspx)
- `AspNetWebFormsNinjectTests.__path=_Default_statusCode=200.verified.txt` — `/Default` page, expects 1 span
- `AspNetWebFormsNinjectTests.__path=_Account_Login_statusCode=200.verified.txt` — `/Account/Login`, expects 1 span

### Solution File

`Datadog.Trace.sln` updated to include the new project with GUID `{B1C2D3E4-F5A6-7890-BCDE-F12345678901}`, placed in the same solution folder as `Samples.WebForms`.

## How to Run

```bash
dotnet test tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests -f net48 --filter "FullyQualifiedName~AspNetWebFormsNinjectTests"
```

## Test Results

### Attempt 1: Ninject initialized in `Application_Start()`
- Ninject kernel created manually inside `Application_Start()` (runs after tracer hooks in)
- **Result: PASS** — Tracer correctly produces `aspnet.request` spans

### Attempt 2: Ninject bootstrapped via `WebActivatorEx.PreApplicationStartMethod`
- Standard `NinjectWebCommon.cs` pattern using `[WebActivatorEx.PreApplicationStartMethod]`
- Runs **before** `Application_Start()` and potentially before the tracer's `BuildManager.InvokePreStartInitMethodsCore` hook
- `Bootstrapper.Initialize(CreateKernel)` triggers assembly scanning -> `NinjectModuleLoader` AppDomain
- **Result: PASS** — Tracer still correctly produces spans

### Conclusion So Far
Neither initialization pattern reproduces the customer's issue. The tracer handles the `NinjectModuleLoader` AppDomain correctly in both cases — it skips it and still instruments the main AppDomain.

## Waiting For (from customer)

The TSE has requested:
- `Global.asax` / `Global.asax.cs`
- `NinjectWebCommon.cs`
- Any `WebActivator` attributes (`PreApplicationStartMethod`, `ApplicationShutdownMethod`)
- `.csproj` file
- Updated tracer logs from the debugging session

Once received, we can adjust the test app to match their exact initialization flow and pinpoint the difference.

## Key Code References

| Location | What |
|----------|------|
| `tracer/src/Datadog.Tracer.Native/cor_profiler.cpp:1488-1496` | NinjectModuleLoader AppDomain detection and skip logic |
| `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/AspNet/HttpModule_Integration.cs` | ASP.NET instrumentation entry point (hooks `BuildManager.InvokePreStartInitMethodsCore`) |
| `tracer/src/Datadog.Trace/AspNet/TracingHttpModule.cs` | HTTP module that creates `aspnet.request` spans |
| `docs/CHANGELOG.md:6662` | PR #2600 changelog entry |
