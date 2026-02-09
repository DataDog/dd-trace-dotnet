# Common CI Failure Patterns

Reference guide for categorizing and troubleshooting common CI failures in dd-trace-dotnet.

## Infrastructure Failures

These are typically transient issues with CI infrastructure, not code problems. **Recommendation: Retry the build.**

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
- If persistent, investigate test performance

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

Tests that intermittently fail, often passing on retry. **Recommendation: Retry, then investigate if persistent.**

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
- If still failing after retries, investigate deeper

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
- If persistent across master and PR, may need investigation

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

**Example Patterns**:
```
DllNotFoundException: libddwaf
Could not load native library for ARM64
```

**Note**: ARM64 support is newer, check if native components are built for ARM64

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
Is the failure in this PR only (not in master)?
├─ Yes → **Real Failure** (investigate)
└─ No → Is it an infrastructure issue?
    ├─ Yes → **Infrastructure** (retry)
    └─ No → Does it have previousAttempts > 0?
        ├─ Yes → **Flaky** (retry, monitor)
        └─ No → Is it a known flaky test?
            ├─ Yes → **Flaky** (retry, monitor)
            └─ No → **Pre-existing Real Failure** (investigate, may be blocking)
```

---

## Quick Reference Table

| Pattern | Category | Action | Priority |
|---------|----------|--------|----------|
| `toomanyrequests`, `rate limit` | Infrastructure | Retry | Low |
| `TLS handshake`, `Connection reset` | Infrastructure | Retry | Low |
| `maximum execution time` | Infrastructure | Retry | Medium |
| `Failed to walk N stacks` | Flaky | Retry, monitor | Low |
| `previousAttempts > 0` | Flaky | Retry | Low |
| `Expected X but got Y` (new) | Real | Investigate | **High** |
| `error CS`, `MSB` | Real | Fix code | **High** |
| `SIGSEGV`, `Access Violation` | Real | Investigate urgently | **Critical** |
| `Expected X but got Y` (also in master) | Pre-existing | Investigate if blocking | Medium |

---

## When to Investigate vs Retry

### Retry First
- Infrastructure failures (network, rate limiting, disk)
- Tests with `previousAttempts > 0`
- Known flaky tests (Alpine stack walking)
- Failures also present in recent master builds

### Investigate Immediately
- **New failures** introduced in the PR (not in master)
- Compilation errors
- Segmentation faults / access violations
- Consistent failures across retries

### Monitor
- Pre-existing failures also in master (may need separate fix)
- Flaky tests that are becoming more frequent
- Platform-specific issues that don't block all platforms

---

## Useful Commands for Investigation

### Get Recent Master Builds
```bash
curl -s "https://dev.azure.com/datadoghq/a51c4863-3eb4-4c5d-878a-58b41a049e4e/_apis/build/builds?branchName=refs/heads/master&\$top=5" | jq '.value[] | {id, result, finishTime}'
```

### Download Specific Test Logs
```bash
# Get timeline to find log ID
curl -s "https://dev.azure.com/datadoghq/.../builds/<BUILD_ID>/timeline" | jq '.records[] | select(.result == "failed")'

# Download log
curl -s "https://dev.azure.com/datadoghq/.../builds/<BUILD_ID>/logs/<LOG_ID>" > test.log
```

### Search for Common Patterns
```bash
# In downloaded log
grep -i "\[FAIL\]" test.log
grep -i "Expected" test.log
grep -i "error\|exception" test.log -A 5
```

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
