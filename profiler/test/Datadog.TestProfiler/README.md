# ManagedCodeCache Test Strategy

This directory contains a comprehensive testing strategy for the `ManagedCodeCache` component of the Datadog .NET Profiler.

## Overview

The ManagedCodeCache is a critical component that maps instruction pointers (IPs) to FunctionIDs in a signal-safe, high-performance manner. This test strategy validates its correctness across various scenarios.

## Test Components

### 1. GoogleTest Unit Tests

**Location**: `profiler/test/Datadog.Profiler.Native.Tests/ManagedCodeCacheTest.cpp`

**Purpose**: Fast, isolated unit tests for core ManagedCodeCache functionality

**Coverage**:
- Basic operations (AddFunction, GetFunctionId)
- Tiered JIT compilation (Tier 0 → Tier 1 code replacement)
- Thread safety and concurrent access
- Signal safety (IsManaged can be called from signal handlers)
- Boundary conditions and edge cases
- Multiple code ranges per function

**Running**:
```bash
cd /path/to/dd-trace-dotnet
cmake --build obj --target profiler-native-tests -j
./obj/profiler/test/Datadog.Profiler.Native.Tests/profiler-native-tests --gtest_filter=ManagedCodeCacheTest.*
```

### 2. Test Profiler

**Location**: `profiler/test/Datadog.TestProfiler/`

**Purpose**: A minimal ICorProfilerCallback10 implementation specifically designed to test ManagedCodeCache in a real CLR environment

**Key Files**:
- `TestProfilerCallback.{h,cpp}` - Full profiler callback implementation
- `Validation.{h,cpp}` - Validation logic and exported functions
- `dllmain.cpp` - DLL entry point and class factory
- `CMakeLists.txt` (Linux) / `.vcxproj` (Windows) - Build configuration

**Features**:
- Monitors JIT compilation, dynamic methods, and ReJIT events
- Adds compiled functions to ManagedCodeCache
- Collects 3-4 sampled instruction pointers per code range
- Randomly selects 20% of functions for ReJIT testing
- Tests invalid IPs (null, unmapped addresses, native code pointers)
- Validates cache results against CLR's GetFunctionFromIP (source of truth)

**GUID**: `{12345678-ABCD-1234-ABCD-123456789ABC}`

**Exported Functions**:
- `PrepareForValidation()` - Triggers ReJIT for selected functions, waits for worker thread
- `ValidateManagedCodeCache(options, result)` - Validates all collected IPs and generates report

**Building**:
```bash
cd /path/to/dd-trace-dotnet
cmake --build obj --target Datadog.TestProfiler -j
```

**Output**: `profiler/_build/DDProf-Test/linux-x64/Datadog.TestProfiler.so` (Linux)

### 3. .NET Test Application

**Location**: `profiler/src/Demos/Samples.TestProfiler/`

**Purpose**: Exercises various .NET method types to generate diverse JIT scenarios

**Test Scenarios**:
- **Regular Methods**: Instance, static, with/without parameters, nested classes
- **Generic Methods**: Generic types, generic methods, multiple type parameters
- **Async Methods**: async/await, Task-returning methods
- **Dynamic Methods**: Reflection.Emit DynamicMethod
- **Lambda/Delegates**: Closures, delegate targets

**P/Invoke Layer**: Declares ValidationOptions and ValidationResult structs with proper marshaling

**Execution Flow**:
1. Executes all test scenarios to trigger JIT compilation
2. Calls `PrepareForValidation()` to trigger ReJIT
3. Calls `ValidateManagedCodeCache()` to validate all collected IPs
4. Exits with 0 on success, non-zero on failure
5. Writes detailed report to `validation_report.txt`

**Building**:
```bash
dotnet build profiler/src/Demos/Samples.TestProfiler/Samples.TestProfiler.csproj -c Release
```

### 4. xUnit Integration Test

**Location**: `profiler/test/Datadog.Profiler.IntegrationTests/ManagedCodeCache/ManagedCodeCacheTest.cs`

**Purpose**: Automated integration test that runs the test application with the test profiler

**Features**:
- Uses `TestApplicationRunner` to launch test app with profiler attached
- Creates `MockDatadogAgent` (required by TestApplicationRunner)
- Tests multiple frameworks (net6.0, net8.0, net10.0)
- Validates exit code (TestApplicationRunner.Run automatically asserts exit code is 0)
- Checks for validation report generation
- Logs report content to xUnit output

**Running**:
```bash
dotnet test profiler/test/Datadog.Profiler.IntegrationTests/Datadog.Profiler.IntegrationTests.csproj \
  --filter "FullyQualifiedName~ManagedCodeCacheTest"
```

## Validation Logic

The test profiler performs comprehensive validation:

### 1. Valid IP Validation

For each collected instruction pointer:
1. Query ManagedCodeCache: `cache->GetFunctionId(ip)`
2. Query CLR (source of truth): `ICorProfilerInfo4->GetFunctionFromIP(ip, &functionId)`
3. Verify both return the same FunctionID
4. **FAIL** if cache is incorrect or disagrees with CLR

### 2. Invalid IP Validation

Tests that invalid IPs are correctly rejected:
- Null pointer (0x0)
- Very low addresses (0x1, 0x123)
- Unmapped marker addresses (0xDEADBEEF, 0xBADF00D, 0xFEEDFACE)
- Max address (0xFFFFFFFFFFFFFFFF)
- Native code addresses (C++ standard library symbols)
- **FAIL** if cache returns a FunctionID for any of these

### 3. ReJIT Validation

- Randomly selects 20% of JIT-compiled, dynamic, and R2R methods for ReJIT
- Triggers ReJIT via `ICorProfilerInfo4->RequestReJIT`
- Waits for ReJIT callbacks (5s timeout)
- Waits for ManagedCodeCache worker thread to process updates (2s)
- Validates that ReJIT'd IPs return correct FunctionIDs
- Skips validation for IPs not yet updated in cache (marked in report)

### 4. Report Generation

Generates detailed `validation_report.txt` with:
- Summary (total functions, code ranges, IPs tested, failures)
- Per-function failure details (IP, expected vs actual FunctionID)
- Invalid IP test results
- Final PASS/FAIL verdict

## Test Execution Matrix

| Test Type | Scope | Speed | Isolation | Coverage |
|-----------|-------|-------|-----------|----------|
| GoogleTest Unit Tests | ManagedCodeCache class | Very Fast | High (mocked ICorProfilerInfo4) | Core functionality |
| Test Profiler + App | Full integration | Moderate | Medium (real CLR, test profiler) | Real-world scenarios |
| xUnit Integration Test | End-to-end | Moderate | Low (automated harness) | Multi-framework validation |

## Development Workflow

### Adding New Test Scenarios

1. **Unit Test**: Add test case to `ManagedCodeCacheTest.cpp`
2. **Integration**: Add method type to `Samples.TestProfiler/Program.cs`
3. **Verify**: Run xUnit integration test to validate

### Debugging Failures

1. **Check unit tests first**: `profiler-native-tests --gtest_filter=ManagedCodeCacheTest.*`
2. **Run test app manually**:
   ```bash
   export CORECLR_ENABLE_PROFILING=1
   export CORECLR_PROFILER={12345678-ABCD-1234-ABCD-123456789ABC}
   export CORECLR_PROFILER_PATH=/path/to/Datadog.TestProfiler.so
   dotnet run --project profiler/src/Demos/Samples.TestProfiler
   ```
3. **Inspect validation report**: `validation_report.txt` in working directory
4. **Check profiler logs**: Look for `[TestProfiler]` prefixed messages

### CI Integration

The xUnit integration test should be added to the profiler test suite:
- Runs on all supported platforms (Linux, Windows, macOS)
- Tests multiple .NET versions (net6.0, net8.0, net10.0)
- Fails build if validation fails

## Architecture Notes

### Why Two Validation Approaches?

**GoogleTest (unit tests)**:
- Fast iteration during development
- Tests specific edge cases in isolation
- Mock profiler API to control test conditions
- No CLR startup overhead

**Test Profiler (integration tests)**:
- Validates real CLR behavior
- Tests actual JIT compilation, tiered JIT, ReJIT
- Uncovers timing issues and race conditions
- Validates against CLR as source of truth

### Signal Safety Considerations

The `IsManaged()` method is called from signal handlers (profiler sampling), so it must be signal-safe:
- No locks (lock-free data structure)
- No memory allocation
- No blocking operations

Unit tests verify this by calling IsManaged from multiple threads concurrently.

### Thread Safety

ManagedCodeCache uses a worker thread to process additions asynchronously:
- JIT callbacks add to a queue (lock-protected)
- Worker thread drains queue and updates cache (lock-free reads)
- Validation waits for worker thread to process all updates before testing

### FrameStore Integration

The test profiler creates a FrameStore with `nullptr` for ManagedCodeCache parameter to get CLR-only method names. This ensures:
- FrameStore doesn't depend on the ManagedCodeCache being tested
- GetFrame() uses only CLR APIs for method resolution
- Test results show clear method names in failure reports

## Future Enhancements

Potential improvements to the test strategy:

1. **Native Method Testing**: Add R2R (ReadyToRun) method testing
2. **Stress Testing**: Long-running scenario with continuous JIT/ReJIT
3. **Memory Validation**: Verify cache doesn't leak memory during churn
4. **Performance Benchmarking**: Measure GetFunctionId latency under load
5. **Multi-AppDomain**: Test cache behavior across AppDomain boundaries (if supported)

## References

- [ManagedCodeCache Implementation](../../src/ProfilerEngine/Datadog.Profiler.Native/ManagedCodeCache.h)
- [FrameStore](../../src/ProfilerEngine/Datadog.Profiler.Native/FrameStore.h)
- [ICorProfilerCallback Documentation](https://learn.microsoft.com/en-us/dotnet/framework/unmanaged-api/profiling/icorprofilercallback-interface)
