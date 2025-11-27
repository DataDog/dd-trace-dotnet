# Hybrid Unwinding on ARM64 Linux

## Why?

`libunwind` has issues unwinding managed .NET code on ARM64 Linux. Hybrid unwinding uses `libunwind` for native code and manual frame pointer unwinding for managed code.

## What?

The unwinding switches to manual when in managed code regions (detected via `LibrariesInfoCache`), and uses `libunwind` for native libraries.
The aim is to push this logic to libunwind in time. However to iterate faster, we are adding unwinding logics within `dd-trace-dotnet`.

## Build & Test

Build the image. Sadly this is alpine so crash analysis can be tricky.

```bash
#
# Rebuild the base build image (pulls in latest dependencies, including .NET 10 SDK/runtime)
# NOTE: this runs from the repo root and mirrors CI builds
#
./tracer/build_in_docker.sh --help >/dev/null

# Sanity check that the container has the expected SDK
./tracer/build_in_docker.sh dotnet --info | grep -E "SDKs installed|10\."
```

From within the docker

```bash
docker run -it --rm \
    --privileged \
    --mount type=bind,source="$(pwd)",target=/project \
    --env NugetPackageDirectory=/project/packages \
    --env artifacts=/project/tracer/bin/artifacts \
    --env DD_INSTRUMENTATION_TELEMETRY_ENABLED=0 \
    --env NUKE_TELEMETRY_OPTOUT=1 \
    --env DD_INTERNAL_USE_HYBRID_UNWINDING=1 \
    --network=host \
    -v /var/log/datadog:/var/log/datadog/dotnet \
    dd-trace-dotnet/alpine-base \
    bash
```

if you touch the build, you need to update the build dll:


```bash
dotnet build tracer/build/_build/_build.csproj
```

Todo: figure out how the dll gets delivered

```bash
dotnet tracer/build/_build/bin/Debug/_build.dll BuildTracerHome
dotnet tracer/build/_build/bin/Debug/_build.dll BuildNativeWrapper
DD_INTERNAL_USE_HYBRID_UNWINDING=1 dotnet tracer/build/_build/bin/Debug/_build.dll BuildProfilerHome
```

Build a test app

```bash
dotnet publish profiler/src/Demos/Samples.Computer01/Samples.Computer01.csproj \
  -c Release \
  -p:Platform=ARM64 \
  -r linux-arm64 \
  -f net10.0 \
  --no-self-contained \
  -o /project/profiler/_build/bin/Release-arm64/profiler/src/Demos/Samples.Computer01/net10.0
```

```
dotnet Samples.Computer01.dll --timeout 15 --scenario ManagedStackExercise
```

### Quick iteration (native-only rebuild)
```bash
DD_INTERNAL_USE_HYBRID_UNWINDING=1 ./tracer/build_in_docker.sh BuildProfilerHome
./test_hybrid_unwinding.sh
```

### Full integration tests
```bash
DD_INTERNAL_USE_HYBRID_UNWINDING=1 ./tracer/build_in_docker.sh BuildProfilerSamples BuildAndRunProfilerIntegrationTests --TargetPlatform x64 --filter "Category=Smoke"
```

### Manual test with logging
```bash
# Rebuild with hybrid unwinding enabled
DD_INTERNAL_USE_HYBRID_UNWINDING=1 ./tracer/build_in_docker.sh BuildProfilerHome

# Run test script (see test_hybrid_unwinding.sh)
DD_TRACE_DEBUG=1 ./test_hybrid_unwinding.sh

# Check logs
cat test_logs/DD-DotNet-Profiler-Native-*.log
```

## Key Files

- `LinuxStackFramesCollector.cpp`: Hybrid unwinding logic (`CollectStackHybrid`)
- `LibrariesInfoCache.cpp`: Signal-safe managed region detection
- `test_hybrid_unwinding.sh`: Simple .NET 10 test harness


## How to sync

```
rsync -avz --delete \
  --exclude 'build/' --exclude '.cache/' --exclude packages/ --exclude artifacts/ \
  --exclude build_data/ --exclude bin/Release/ \
  --files-from=<(git status --porcelain | awk '{print $2}') \
  /home/r1viollet/dd/dd-trace-dotnet/ \
  workspace-r1:~/dd/dd-trace-dotnet/
```

## Uwinding logic

### ARM64 JIT Frame Notes

- Canonical managed prologue still begins with

```
stp x29, x30, [sp, #-0x40]!
mov x29, sp
stp x19, x20, [sp, #0x10]
str x21, [sp, #0x20]
```

  but the size (`0x40` above) expands when the JIT spills additional locals or callee-saved registers. We have also captured shorter variants such as

```
stp x29, x30, [sp, #-0x20]!
mov x29, sp
str x19, [sp, #0x10]
```

  and “leaf-ish” frames where the return address stays in `x30` until the method tail calls another helper.

- Because the frame size is chosen per method, the saved LR (`[fp + 8]`) is only present when the JIT actually spilled it. If the method ends up tail-calling or using LR for helper trampolines, the saved slot either moves or disappears entirely.

- Epilogues mirror the prologue with sequences such as

```
ldp x19, x20, [sp, #0x10]
ldp x29, x30, [sp], #0x40
ret
```

  but we also see multi-stage restores when more registers were spilled; those interleave `ldp` instructions that do not touch `x29/x30` until the very end.

### Why simple frame reads are fragile

- The dynamic prologue means we cannot assume the saved LR lives at `[fp + 8]`. In large frames it can be farther down, and in tail-call optimized methods it may never be on the stack at all.
- The JIT sometimes reuses the frame space for helper calls, so a naïve read can observe scratch data instead of the actual return address.
- Only disassembling the method body (or letting the runtime expose unwind metadata) tells us which registers were saved and where the spill slots ended up. Without scanning the code we lack a reliable offset for the saved LR, so the hybrid unwinder must stay conservative and fall back unless the canonical layout is clearly detected.

### JIT Metadata Cache

- `JITCompilationFinished` now records each method's native `[start, end)` range and prologue size into `JitCodeCache`.
- The cache captures the decoded stack frame summary (frame size, saved FP/LR offsets, callee-saved register slots) plus the raw prologue bytes for later inspection. Updated decoder now handles `sub sp, sp, #imm` followed by large callee-save blocks so we get accurate frame sizes for Newtonsoft hot paths.
- The cache uses a lock-free, signal-safe linked list so hybrid unwinding can query it from the sampler thread.
- `LinuxStackFramesCollector::IsManagedCode` consults the cache first, reducing the reliance on `/proc/self/maps` refreshes.
- ARM64 manual unwinding now reads the caller FP/LR using the cached offsets and restores SP with the cached frame size when available.
- Sampling can land inside musl’s `__clone` / `__syscall_cp_asm` wrappers; we currently let libunwind handle those native frames. No action needed, but note this in case we later want to skip straight to the managed entry point.
