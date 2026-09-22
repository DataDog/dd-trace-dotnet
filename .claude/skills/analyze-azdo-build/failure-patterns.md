# Common CI Failure Patterns

Reference guide for categorizing and troubleshooting common CI failures in dd-trace-dotnet.

## Table of Contents

- [Infrastructure Failures](#infrastructure-failures) — Docker rate limiting, network issues, timeouts, disk space
- [Flaky Tests](#flaky-tests) — Stack walking, auto-retried tests, single-runtime failures, ASM init
- [Real Failures](#real-failures) — Assertion failures, snapshot mismatches, compilation errors, segfaults, missing deps
- [Platform-Specific Patterns](#platform-specific-patterns) — Windows-only, Linux-only, ARM64
- [Framework-Specific Patterns](#framework-specific-patterns) — .NET Framework, .NET 6/7/8
- [Test-Specific Patterns](#test-specific-patterns) — Azure Functions, integration tests, smoke tests
- [Categorization Decision Tree](#categorization-decision-tree) — Flowchart for classifying failures
- [Quick Reference Table](#quick-reference-table) — Pattern → Category → Action lookup
- [When to Investigate vs Retry](#when-to-investigate-vs-retry)

## Infrastructure Failures

These are typically transient issues with CI infrastructure, not code problems. **Recommendation: Retry the build. Alert #apm-dotnet if persistent after retries.**

### Docker Rate Limiting

**Pattern**:
```
toomanyrequests: You have reached your unauthenticated pull rate limit
denied: requested access to the resource is denied
```

**Cause**: Docker Hub rate limiting (100 pulls per 6 hours for unauthenticated requests)

**Solution**:
- Retry after waiting
- CI should use authenticated pulls (may need configuration)

**Example Build**: 195137 (Azure Functions tests)

---

### Network Issues

**Patterns**:
```
TLS handshake timeout
Connection reset by peer
ECONNRESET
EOF
Error: read ECONNRESET
curl: (35) OpenSSL SSL_connect: SSL_ERROR_SYSCALL
```

**Cause**: Temporary network connectivity issues between CI and external services

**Solution**: Retry the build

---

### Timeouts

**Patterns**:
```
maximum execution time
The job running on agent ... ran longer than the maximum time
Test run timed out after 600000 ms
```

**Cause**: Job exceeded Azure DevOps time limits (often due to slow infrastructure)

**Solution**:
- Retry the build
- If persistent, investigate test performance and alert **#apm-dotnet** on Slack

#### Timeout via Cancellation

**Detection**: Jobs with `result == "canceled"` and duration >= 55 minutes

**Why not "failed"?**: Azure DevOps marks timed-out jobs as "canceled" rather than "failed".

**Differentiation**:
| Duration | Classification | Likely Cause |
|----------|---------------|--------------|
| >= 55 min | Timeout | Azure DevOps 60-min limit exceeded |
| < 5 min | Collateral | Parent stage failure triggered cascade cancellation |
| 5-55 min | Unknown | Could be manual cancellation or other cause |

**Example**: Build 195486
- Stage: `integration_tests_linux` - result: "failed"
- Job: `DockerTest alpine_netcoreapp3.0_group1` - result: "canceled", duration: 60.3 min
- Diagnosis: Job timed out, causing stage to fail
- Action: Retry once; if persistent, investigate and alert **#apm-dotnet** on Slack (check test performance, resource contention)

**Solution**:
- Retry the build (may be transient infrastructure slowness)
- If persistent after 2 runs, investigate and alert **#apm-dotnet** on Slack:
  - Check job logs for stuck tests or infinite loops
  - Look for resource contention (CPU, memory, I/O)
  - Compare with successful runs to identify anomalies

---

### Disk Space

**Patterns**:
```
No space left on device
ENOSPC
out of disk space
```

**Cause**: Build artifacts filled agent disk

**Solution**:
- Retry (agents are cleaned between builds)
- Post in #apm-dotnet if frequent

---

## Flaky Tests

Tests that intermittently fail, often passing on retry. **Recommendation: Retry, then investigate and alert #apm-dotnet if persistent.**

### Stack Walking Failures (Alpine/musl)

**Pattern**:
```
Failed to walk N stacks for sampled exception: E_FAIL
```

**Cause**: Known issue with stack walking on Alpine Linux (musl libc)

**Affected**: Tests on Alpine containers

**Solution**:
- Expected occasional failures
- Retry if blocking PR
- Track in issue if becoming more frequent

**Tracking Issue**: Check GitHub for related issues

---

### Tests with Previous Attempts

**Pattern**: Timeline shows `previousAttempts > 0`

**Cause**: Azure DevOps already retried the test/job automatically

**Solution**:
- Likely flaky test
- Check if it passed on retry
- If still failing after retries, investigate deeper and alert **#apm-dotnet** on Slack

---

### Single-Runtime Failures

**Pattern**: Same test passes on most .NET runtimes but fails on only one (especially net6 and above).

**Example**:
| Runtime | Result |
|---------|--------|
| net6.0 | Pass |
| net8.0 | Pass |
| net10.0 | **Fail** |

**Cause**: Timing-sensitive behavior, runtime-specific quirks, or transient environment issues affecting a single runtime variant.

**Solution**:
- Likely flaky — retry the build
- If persistent on the same runtime after 2 retries, investigate runtime-specific behavior and alert **#apm-dotnet** on Slack
- A real regression would typically fail across all runtimes, not just one

---

### ASM Initialization Tests

**Pattern**:
```
AspNetCore5AsmInitializationSecurityEnabled.TestSecurityInitialization
Failed to initialize security
```

**Cause**: Timing-sensitive initialization, occasionally fails in CI

**Affected**: Multiple platforms (Linux, Windows, various .NET versions)

**Solution**:
- Check if also failing in master
- Retry if isolated to PR
- If persistent across master and PR, investigate and alert **#apm-dotnet** on Slack

**Example Build**: 195137 (failed on both PR and master)

---

## Real Failures

These indicate actual code problems that need investigation. **Recommendation: Investigate before merging.**

### Test Assertion Failures

**Patterns**:
```
Expected 21 spans but got 14
Assert.Equal() Failure
Expected: True
Actual:   False
Xunit.Sdk.EqualException
```

**Cause**: Test expectations don't match actual behavior

**Solution**:
- Check if new failure (not in master)
- Review code changes that might affect the test
- Debug locally to understand root cause

**Example**:
- `AzureFunctionsTests+IsolatedRuntimeV4.SubmitsTraces` (Build 195137)
- Expected 21 spans, got 14 → Missing spans in worker process

---

### Snapshot Mismatches

**Patterns**:
```
Received file does not match the verified file
*.received.* vs *.verified.*
Verify assertion failure
```

**Common test names**: `*.SubmitsTraces` and other integration tests in `Datadog.Trace.ClrProfiler.IntegrationTests` — but only when the error is a snapshot content diff, not a span count mismatch.

**Important**: `SubmitsTraces` tests typically assert on span count first (`Expected N spans but got M`), then compare snapshots. A span count mismatch indicates missing/extra instrumentation and is a **Test Assertion Failure** (see above), not a snapshot problem. Updating snapshots won't help in that case.

**Cause**: Code changes affected trace output (span tags, names, ordering, etc.). The actual output (`.received.txt`) no longer matches the expected snapshots (`.verified.txt` files in `tracer/test/snapshots/`).

**Solution**:
- If the changes are **intentional**, update snapshots:
  - **Windows**: `./tracer/build.ps1 UpdateSnapshotsFromBuild --BuildId <BUILD_ID>`
  - **Linux/macOS**: `./tracer/build.sh UpdateSnapshotsFromBuild --BuildId <BUILD_ID>`
  - This downloads `.received.txt` artifacts from the CI build and replaces local `.verified.txt` files
  - The build must have run far enough to produce snapshot artifacts (even if tests failed)
- If the changes are **unintentional**, investigate the code change that caused the regression

**Example**: Integration test `HttpClientTests.HttpClient_GetAsync_SubmitsTraces` fails because a new span tag was added, changing the snapshot output.

---

### Compilation Errors

**Patterns**:
```
error CS0103: The name 'X' does not exist in the current context
error MSB3073: The command "X" exited with code Y
Build FAILED
```

**Cause**: Code syntax errors, missing references, build configuration issues

**Solution**:
- Fix compilation errors
- Ensure all platforms can build (check TFM-specific code)

---

### Segmentation Faults / Access Violations

**Patterns**:
```
SIGSEGV
Segmentation fault
Access Violation
Exception: System.AccessViolationException
```

**Cause**: Native code crash (profiler, CLR, or native dependencies)

**Solution**:
- Enable native debugging logs
- Check for memory corruption, null pointer dereference
- Review recent native code changes

**Critical**: These often indicate serious bugs, investigate immediately

---

### Missing Dependencies

**Patterns**:
```
Could not load file or assembly 'X'
FileNotFoundException
DllNotFoundException
```

**Cause**: Missing NuGet packages, platform-specific libraries, or incorrect deployment

**Solution**:
- Check package references
- Verify platform-specific dependencies (Windows vs Linux vs macOS)
- Ensure native libraries are deployed correctly

---

## Platform-Specific Patterns

### Windows-Only Failures

**Common Causes**:
- Path separator issues (`\` vs `/`)
- Case-sensitive file operations
- Windows-specific APIs or behaviors

**Example Patterns**:
```
DirectoryNotFoundException: Could not find path 'C:\...'
Platform not supported: Unix
```

---

### Linux-Only Failures

**Common Causes**:
- File permissions (`chmod` needed)
- Line ending issues (CRLF vs LF)
- Docker container isolation

**Example Patterns**:
```
Permission denied
/bin/sh: ./script.sh: Permission denied
cannot execute binary file
```

---

### ARM64 Failures

**Common Causes**:
- Missing ARM64 native binaries
- Emulation issues
- Architecture-specific bugs
- **Flaky infrastructure**: ARM64 CI agents are more prone to transient issues (slow startup, timeouts, resource contention)

**Example Patterns**:
```
DllNotFoundException: libddwaf
Could not load native library for ARM64
```

**Note**: ARM64 support is newer, check if native components are built for ARM64

#### ARM64 Single-Runtime Timeout (Flaky Infrastructure)

**Pattern**: In a `unit_tests_arm64` (or similar ARM64 stage), one runtime job times out (~60 min) while all other runtimes complete normally (~14 min).

**Example**:
| Job | Duration | Result |
|-----|----------|--------|
| test glibc_net5.0 | ~60 min | ❌ Cancelled (timeout) |
| test musl_net5.0 | ~14 min | ✅ Pass |
| test glibc_net6.0 | ~14 min | ✅ Pass |
| test musl_net6.0 | ~14 min | ✅ Pass |
| ... (all others) | ~14 min | ✅ Pass |

**Indicators**:
- Only one platform/runtime failed in an ARM64 stage
- The failed job was cancelled (not "failed") after ~60 minutes
- All other runtimes in the same stage completed in ~14 minutes
- The failing runtime is not consistently the same across multiple runs

**Cause**: Transient ARM64 infrastructure issue — the agent likely timed out waiting for something (container startup, package download, slow I/O), not a code problem. A real regression would fail across multiple runtimes or consistently on the same runtime.

**Solution**: Retry the build. This is almost certainly a flaky CI infrastructure issue, not a code regression.

---

## Framework-Specific Patterns

### .NET Framework (net462) Failures

**Common Causes**:
- Missing polyfills for newer C# features
- Framework-specific API differences
- GAC/assembly resolution issues

**Solution**: Ensure code is compatible with .NET Framework 4.6.2+

---

### .NET 6/7/8 Failures

**Common Causes**:
- Runtime behavior changes
- TFM-specific code paths
- Native dependencies (different CoreCLR versions)

**Solution**: Test locally on specific TFM, check for version-specific issues

---

## Test-Specific Patterns

### Azure Functions Tests

**Common Patterns**:
```
Expected N spans but got M
Worker process did not start
Function host initialization failed
```

**Causes**:
- Span parenting issues (host vs worker process)
- Initialization timing (worker not ready)
- Missing instrumentation in worker process

**Files to Check**:
- `tracer/src/Datadog.AzureFunctions/`
- `tracer/test/test-applications/azure-functions/`

**Local Testing**: See `docs/development/AzureFunctions.md`

---

### Integration Tests

**Common Patterns**:
```
Container failed to start
Database connection failed
Expected HTTP 200 but got 500
```

**Causes**:
- Docker Compose service not ready
- Missing environment variables
- Integration not instrumented correctly

**Files to Check**:
- `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/<Library>/`
- `docker-compose.yml`

---

### Smoke Tests

**Common Patterns**:
```
Tracer initialization failed
No spans received
Expected tracer log but found none
```

**Causes**:
- Profiler not attached
- Configuration incorrect
- Tracer not loaded

**Files to Check**:
- `tracer/test/test-applications/integrations/`
- Native loader and profiler logs

---

## Categorization Decision Tree

```
Are there canceled jobs?
├─ Yes → Check duration:
│   ├─ >= 55 min → **Timeout** (infrastructure, retry)
│   ├─ < 5 min → **Collateral Cancellation** (check parent failure)
│   └─ 5-55 min → **Unknown** (review manually, could be manual cancellation)
│
└─ No canceled jobs or after classifying them →
    Is it an infrastructure issue (network, rate limit, disk)?
    ├─ Yes → **Infrastructure** (retry)
    └─ No → Is it an ARM64 stage where only one runtime timed out (~60 min) while others passed (~14 min)?
        ├─ Yes → **Flaky Infrastructure** (retry — transient ARM64 agent issue)
        └─ No → Does the test fail on only one runtime but pass on others?
        ├─ Yes → **Flaky** (retry, alert #apm-dotnet if persistent)
        └─ No → Does it have previousAttempts > 0 or is it a known flaky test?
            ├─ Yes → **Flaky** (retry, monitor)
            └─ No → Is the error a snapshot content diff (*.received.* vs *.verified.*)?
                ├─ Yes → **Snapshot Mismatch** (update snapshots if intentional, investigate if not)
                └─ No → **Real Failure** (investigate)
                    Note: SubmitsTraces tests with span count mismatches are real failures, not snapshot issues

```

---

## Quick Reference Table

| Pattern | Category | Action | Priority |
|---------|----------|--------|----------|
| `toomanyrequests`, `rate limit` | Infrastructure | Retry | Low |
| `TLS handshake`, `Connection reset` | Infrastructure | Retry | Low |
| `maximum execution time` | Infrastructure | Retry | Medium |
| Canceled job, duration >= 55 min | Infrastructure (Timeout) | Retry, alert #apm-dotnet if persistent | Medium |
| Canceled job, duration < 5 min | Collateral | Check parent failure cause | None |
| `Failed to walk N stacks` | Flaky | Retry, monitor | Low |
| `previousAttempts > 0` | Flaky | Retry | Low |
| ARM64 stage: 1 runtime timeout (~60 min), all others pass (~14 min) | Flaky Infrastructure | Retry | Low |
| Fails on 1 runtime, passes on others | Flaky | Retry, alert #apm-dotnet if persistent | Low |
| `Expected X but got Y` | Real | Investigate | **High** |
| `Received file does not match` | Real (Snapshot) | Update snapshots or investigate | **High** |
| `error CS`, `MSB` | Real | Fix code | **High** |
| `SIGSEGV`, `Access Violation` | Real | Investigate urgently | **Critical** |

---

## When to Investigate vs Retry

### Retry First
- Infrastructure failures (network, rate limiting, disk)
- Tests with `previousAttempts > 0`
- Known flaky tests (Alpine stack walking)
- Tests failing on only one runtime but passing on others

### Investigate Immediately (and Alert #apm-dotnet)
- Compilation errors
- Segmentation faults / access violations
- Tests failing consistently across all runtimes
- Consistent failures after 2 retries

### Monitor
- Flaky tests that are becoming more frequent
- Platform-specific issues that don't block all platforms

---

## Historical Context

### Known Issues
- Alpine stack walking: Ongoing musl libc limitation
- ARM64 support: Added recently, may have gaps in native binaries
- ASM initialization: Timing-sensitive, occasionally fails in CI

### Recent Changes
Track major changes that might introduce new failure patterns:
- New integrations
- Runtime updates (.NET 8, 9, etc.)
- Native profiler changes
- CI infrastructure updates

---

## Contributing

When you encounter a new failure pattern:
1. Document the pattern, cause, and solution
2. Add to this file under appropriate category
3. Include example build ID and error messages
4. Update decision tree if needed
