# Azure Functions Span Parenting Testing (APMSVLS-58)

## Phase 1: Local Testing — COMPLETED

Ran 4 Azure Functions sample apps locally with mock trace agent to compare released tracer (3.37.0) vs dev build.

### Local Test Results

| # | Scenario | Spans | Key Observation |
|---|----------|-------|-----------------|
| 1 | Released + ASP.NET Core | 3 (2 chunks) | `aspnet_core.request` (host) + `azure_functions.invoke` + `manual` (worker). Same trace_id. |
| 2 | Released + non-ASP.NET Core | 3 (1 chunk) | `azure_functions.invoke` -> `test_span` -> `http.request`. No `aspnet_core.request`. Correct baseline. |
| 3 | Dev + ASP.NET Core | 3 (2 chunks) | `aspnet_core.request` in **different trace_id** from worker spans. Parenting broken locally (expected — `func start` uses separate ports). |
| 4 | Dev + non-ASP.NET Core | **2** (1 chunk) | **`azure_functions.invoke` MISSING** due to JIT-time `FileNotFoundException` for `Microsoft.AspNetCore.Http.Abstractions`. |

### Root Cause & Fix (committed as `053d1c6`)

`AzureFunctionsCommon.cs:370` had a direct reference to `Microsoft.AspNetCore.Http.HttpContext`. JIT compilation of `GetAspNetCoreScope` fails in non-ASP.NET Core workers because the assembly doesn't exist — the try/catch can't intercept a JIT-time error.

**Fix**: Replaced direct type reference with duck typing (`IHttpContextItems` + `TryDuckCast`).
- New file: `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Azure/Functions/Isolated/IHttpContextItems.cs`
- Modified: `AzureFunctionsCommon.cs:370` — `httpContextObj.TryDuckCast<IHttpContextItems>(out var httpContext)`

### Post-Fix Local Re-test (Scenario 4 only)

After rebuilding dev NuGet (`3.38.0-dev20260219213105`), scenario 4 now shows 3 spans including `azure_functions.invoke`. No errors in logs.

---

## Phase 2: Azure Deployment Testing — COMPLETED

Deploy the same 4 scenarios to Azure and verify traces via Datadog API. This validates behavior in a real Azure environment where host/worker share the HTTP pipeline (unlike local `func start`).

### Azure App Mapping

| # | Scenario | Sample App | Azure Function App | Tracer |
|---|----------|------------|--------------------|--------|
| 1 | Released + ASP.NET Core | `apm-serverless-test-apps/.../isolated-dotnet8-aspnetcore` | `lucasp-premium-linux-isolated-aspnet` | 3.37.0 |
| 2 | Released + non-ASP.NET Core | `apm-serverless-test-apps/.../isolated-dotnet8` | `lucasp-premium-linux-isolated` | 3.37.0 |
| 3 | Dev + ASP.NET Core | `apm-serverless-test-apps-dev/.../isolated-dotnet8-aspnetcore` | `lucasp-premium-linux-isolated-aspnet-dev` | 3.38.0-dev* |
| 4 | Dev + non-ASP.NET Core | `apm-serverless-test-apps-dev/.../isolated-dotnet8` | `lucasp-premium-linux-isolated-dev` | 3.38.0-dev* |

### Azure Test Results

| # | Scenario | Trace ID | Spans | Hierarchy |
|---|----------|----------|-------|-----------|
| 1 | Released + ASP.NET Core | `6997b0ca...` | 4 | `azure_functions.invoke` (host) → `http.request` + `azure_functions.invoke` (worker) → `manual` |
| 2 | Released + non-ASP.NET Core | `6997b0eb...` | 4 | `azure_functions.invoke` (host) → `azure_functions.invoke` (worker) → `test_span` → `http.request` |
| 3 | Dev + ASP.NET Core | `6997b41f...` | **5** | `azure_functions.invoke` (host) → `http.request` → **`aspnet_core.request`** → `azure_functions.invoke` (worker) → `manual` |
| 4 | Dev + non-ASP.NET Core | `6997b119...` | 4 | `azure_functions.invoke` (host) → `azure_functions.invoke` (worker) → `test_span` → `http.request` |

### Verification

- **Scenario 3**: `aspnet_core.request` → `azure_functions.invoke` parenting works in Azure via HttpContext.Items bridge. The ASP.NET Core worker handles HTTP directly (unlike local `func start`).
- **Scenario 4**: `azure_functions.invoke` present. Duck typing fix confirmed working in Azure. No `aspnet_core.request` in non-ASP.NET Core worker.
- **All traces**: Single root span, correct parent-child relationships.

---

## Phase 3: CI Test Fixes — IN PROGRESS

The duck typing fix causes 5 additional `aspnet_core.request` spans in ASP.NET Core isolated worker CI tests (one per HTTP trigger endpoint). Tests failed on span count assertion before reaching snapshot verification.

### CI Build: [196172](https://dev.azure.com/datadoghq/a51c4863-3eb4-4c5d-878a-58b41a049e4e/_build/results?buildId=196172)

**Failed tests** (same failure across all 5 Windows runtimes: net6.0, net7.0, net8.0, net9.0, net10.0):
- `AzureFunctionsTests+IsolatedRuntimeV4AspNetCore.SubmitsTraces`
- `AzureFunctionsTests+IsolatedRuntimeV4AspNetCoreV1.SubmitsTraces`

**Error**: `Expected spans to contain 26 item(s), but found 31`

**Unrelated failure**: `integration_tests_arm64` / `debian_net7.0` — infrastructure issue, not related to this PR.

### Fixes Applied

- [x] **Update span counts** — Changed `expectedSpanCount` from 26 to 31 in both test methods:
  - `AzureFunctionsTests.cs:251` (`IsolatedRuntimeV4AspNetCoreV1`)
  - `AzureFunctionsTests.cs:383` (`IsolatedRuntimeV4AspNetCore`)
- [x] **Download snapshots from build** — Ran `UpdateSnapshotsFromBuild --BuildId 196172`. No Azure Functions snapshot changes needed — existing snapshots already contain `aspnet_core.request` spans (the tests failed before reaching snapshot verification).
- [ ] **Push and re-run CI** — Awaiting push

### Why 31 spans?

The duck typing fix allows `GetAspNetCoreScope` to successfully retrieve the ASP.NET Core scope via `HttpContext.Items` in the CI environment. This creates 5 additional `aspnet_core.request` spans (one per HTTP trigger: `/api/simple`, `/api/exception`, `/api/error`, `/api/badrequest`, `/api/trigger`). Previously, the JIT-time `FileNotFoundException` silently prevented these spans from being created.

---

## Context Files

- `docs/development/investigations/APMSVLS-58-CI-failure-analysis.md` — Full CI failure analysis
- `docs/development/investigations/APMSVLS-58-Azure-Functions-span-parenting.md` — Original investigation
- `AGENTS.md` — Repository guidelines
- `.claude/skills/azure-functions/` — Azure Functions dev/test workflow skill
- `D:\source\datadog\CLAUDE.md` — Azure environment details and app naming convention
