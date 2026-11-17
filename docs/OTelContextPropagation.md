# OpenTelemetry Thread Context Propagation (OTEP 4947)

The tracer can publish the trace context active on each OS thread in a standard, externally readable
format, so out-of-process readers — the [OpenTelemetry eBPF profiler](https://github.com/open-telemetry/opentelemetry-ebpf-profiler),
[OBI](https://opentelemetry.io/docs/zero-code/obi/) — can attribute their own samples to the same trace
and span the tracer is working on.

The format is [OTEP 4947, "Thread Context: Sharing Thread-Level Information with External Readers"](https://github.com/open-telemetry/opentelemetry-specification/blob/main/oteps/profiles/4947-thread-ctx.md).
Other Datadog libraries implement the same spec: [libdatadog's `libdd-otel-thread-ctx`](https://github.com/DataDog/libdatadog/tree/main/libdd-otel-thread-ctx)
(Rust) and [java-profiler#347](https://github.com/DataDog/java-profiler/pull/347).

There are two halves to the feature, and both are needed: **publishing** a record per thread, and
**announcing** in the process context that the records exist. A reader ignores the first without the second.

Managed code owns the record layout and all hot-path context writes. Native code owns the parts tied to
ELF and OS-thread lifetime: it exports the thread-local pointer, allocates and pools the records, and
registers their pthread cleanup. Managed code enters the native layer only when a thread first needs a
record.

## Configuration

| | |
| --- | --- |
| Setting | `DD_TRACE_OTEL_CTX_ENABLED` (`TracerSettings.OtelThreadContextEnabled`) |
| Default | off |
| Requires | Linux, x64 or arm64, and automatic instrumentation attached |

The platform gate is `OtelThreadContextPublisher.IsPlatformSupported()`, shared by the publisher and the
process-metadata builder. OTEP 4947 is deliberately Linux-only: it relies on ELF thread-local storage,
and its readers are themselves Linux-specific. Automatic instrumentation is required because that is what
rewrites the P/Invoke map to point at the deployed native library.

## How it works

### At startup, once

```
TracerManager.OneTimeSetup
        |
        +--> ServiceDiscoveryHelper.StoreTracerMetadata(...)
                 |
                 +--> ddog_tracer_metadata_include_otel_thread_context(builder)
                 +--> ddog_tracer_metadata_store(builder)
                              libdatadog publishes one process context
```

### On every span activation

```
AsyncLocalScopeManager.OnScopeChanged            (the existing AsyncLocal<Scope> callback)
        |
        +--> Profiler.Instance.ContextTracker.Set(...)          Continuous Profiler, unchanged
        +--> OtelThreadContextPublisher.Set(span)
                 |
                 +-- [ThreadStatic] record  -- miss --> GetOrCreateOtelThreadContextRecord()
                 |                                      rent and publish a native-owned 640B block,
                 |                                      register its pthread destructor
                 |
                 +-- hit --> write the record in place, no interop at all
```

A thread's context lives in a 640-byte *Thread-Local Context Record*. A reader finds it by resolving the
thread-local variable `otel_thread_ctx_v1` — exported as an ELF TLS symbol — which holds a pointer to that
thread's record.

The native call happens **once per OS thread**, never per span. Everything after that is a handful of
managed writes into unmanaged memory:

```
valid = 0                       // offset 24, Volatile.Write
trace-id, span-id, trace-flags  // 26 bytes
attrs-data = hex(localRootSpanId)
valid = 1                       // offset 24, Volatile.Write
```

During the OS thread's usable lifetime the pointer remains installed, so context detachment is done by
clearing `valid`. The native pthread destructor clears the pointer only as part of thread teardown. The
spec requires a writer to pick one mechanism or the other for context transitions — swapping the pointer,
or toggling the flag — and never both.

Only the owning thread writes its record, and the spec requires readers to observe a thread while it is
stopped or interrupted, so there is no cross-thread race to guard against. The only hazard is reordering,
which is why `valid` is cleared first and set last: a reader that samples mid-update sees an invalid record
and skips it rather than reading a torn one.

### Record layout

Byte-packed with no padding. Multi-byte scalars use native endianness; the trace and span ids use W3C
Trace Context format, i.e. big endian. `OtelThreadContextRecord` owns the semantic encoding; native
lifecycle code mirrors only the total size, alignment, and `valid` offset needed to allocate and retire a
record safely.

| Offset | Size | Field             | What we write                                      |
| -----: | ---: | ----------------- | -------------------------------------------------- |
|      0 |   16 | `trace-id`        | `TraceId128.Upper` then `.Lower`, each big endian   |
|     16 |    8 | `span-id`         | `Span.SpanId`, big endian                          |
|     24 |    1 | `valid`           | 1 when readable, 0 while writing or detached       |
|     25 |    1 | `trace-flags`     | `0x01` when sampled, else `0x00`                   |
|     26 |    2 | `attrs-data-size` | constant `18`                                      |
|     28 |    2 | attr[0] key, len  | `0x00`, `0x10`                                     |
|     30 |   16 | attr[0] value     | `Span.RootSpanId` as 16 lower-case hex characters  |
|     46 |  594 | unused            | zeroed                                             |

Key index 0 is the cross-language convention for `datadog.local_root_span_id` (libdatadog's
`ROOT_SPAN_KEY_INDEX`, the Java profiler's `LOCAL_ROOT_SPAN_ATTR_INDEX`), so our records are
byte-compatible with the other Datadog writers.

Only the first 46 bytes are meaningful, but the whole 640 are allocated and zeroed: 640 bytes is the fixed
window the eBPF profiler reads, and allocating it keeps that read in bounds. Blocks are cache-line aligned
so the meaningful prefix never straddles two lines.

### Record lifetime

Records are owned and pooled by `otel_thread_ctx.cpp`, and are **never returned to the allocator**.
`GetOrCreateOtelThreadContextRecord()` rents a cache-line-aligned block, publishes it through the calling
thread's `otel_thread_ctx_v1` slot, and stores it in a process-lifetime pthread key. The managed
`[ThreadStatic]` field is only a fast address cache; it has no ownership responsibility.

The pthread-key destructor runs on the exiting OS thread. It first invalidates the record, clears that
thread's exported TLS slot, zeroes the record, and only then returns the block to the native free list.
This ties recycling to the lifetime of the actual ELF TLS slot instead of relying on managed finalizer
ordering.

The native key, mutex, and free list deliberately have process lifetime: thread destructors may run during
shutdown, so destroying the pool itself would introduce an ordering race. A plain pthread mutex guards the
free list; allocation happens once per participating OS thread and there is no contention on span
activation.

## The native surface

`tracer/src/Datadog.Tracer.Native/otel_thread_ctx.cpp` defines the exported slot and the one native entry
point:

```cpp
extern "C"
{
    __attribute__((visibility("default"))) __thread void* otel_thread_ctx_v1;
}

extern "C" __attribute__((visibility("default")))
void* GetOrCreateOtelThreadContextRecord();
```

The implementation owns a process-lifetime pthread key and native record pool. The first call on an OS
thread rents a zeroed record, registers it with `pthread_setspecific`, and publishes its pointer. Repeated
calls return the same address. The key destructor clears the TLS pointer and recycles the record on the
owning thread.

The braced linkage block matters. `extern "C" __thread void* otel_thread_ctx_v1;` is only a
*declaration* — a declaration directly contained in a linkage-specification is treated as if it carried
the `extern` specifier — so the symbol comes out `UND`, there is no `.tbss` at all, and no reader ever
finds a context. Inside braces, a declaration without `extern` is a definition. Left without an
initializer it lands in `.tbss`, which the loader zeroes for every new thread.

How the export is produced:

- `Datadog.Tracer.Native` is compiled with `-fvisibility=hidden`, so the explicit `visibility("default")`
  is what places the symbol in `.dynsym`. Unlike `Datadog.Profiler.Native` and the shared native loader,
  this project has no version script, so nothing filters it out afterwards.
- The file belongs to the **SHARED** CMake target, alongside `dllmain.cpp` and `interop.cpp`, rather than
  to `Datadog.Tracer.Native.static`. A TLS definition in a static archive is only pulled in if something
  references it; putting it in the shared target guarantees it is present.
- The body is `#ifdef LINUX`. The Windows MSVC project lists its sources explicitly and does not include
  this file, so there is nothing to export or declare there.
- The spec prefers the TLSDESC dialect but also supports traditional Global Dynamic, and requires readers
  to handle initial-exec/local-exec relaxation. CMake probes for `-mtls-dialect=gnu2` (x86-64) or
  `-mtls-dialect=desc` (arm64) and applies it to this one file, falling back silently — support depends on
  the compiler version, and since native TLS access is limited to record acquisition and teardown the
  dialect has no measurable cost either way.
- `--export-dynamic-symbol` is not needed. That is the spec's advice for symbols defined in an executable
  or a statically linked binary; ours is a definition with default visibility inside a shared object.

Measured on both toolchains, using the shipped CMake block:

| Compiler | TLSDESC probe | `nm -D` | Access model |
| --- | --- | --- | --- |
| clang 18 (what the repo builds with) | fails, falls back | `B otel_thread_ctx_v1` | Global Dynamic (`R_X86_64_DTPMOD64`/`DTPOFF64`) |
| gcc 13 | succeeds | `B otel_thread_ctx_v1` | TLSDESC (`R_X86_64_TLSDESC`) |

In both cases `readelf --dyn-syms` reports `TLS GLOBAL DEFAULT`, size 8, in `.tbss`. Note clang only
gained `-mtls-dialect=gnu2` on x86-64 in clang 19, which is why the flag has to be probed rather than
required.

The slot deliberately holds an 8-byte pointer rather than the record itself. This library is `dlopen`'d, so
if the linker relaxes the access to initial-exec the slot has to fit in glibc's static TLS surplus: 8 bytes
always does, 640 would consume most of it.

`CompileTracerNativeSrcLinux` checks the actual shared object with `readelf --dyn-syms --wide` and asserts
that the symbol is an 8-byte, global, default-visible ELF TLS symbol. Without that check a compiler, linker
or visibility change could silently make it unusable — readers would just never find any context, and
nothing at runtime would say why.

## Announcing the schema in the process context

Per the spec's reading protocol, a reader will not look for `otel_thread_ctx_v1` until the process's
[OTEP 4719 process context](https://github.com/open-telemetry/opentelemetry-specification/blob/main/oteps/profiles/4719-process-ctx.md)
advertises `threadlocal.schema_version`. Two attributes are needed:

| Key | Value |
| --- | --- |
| `threadlocal.schema_version` | `tlsdesc_v1_dev` |
| `threadlocal.attribute_key_map` | `["datadog.local_root_span_id"]` |

The tracer already publishes a process context through libdatadog (`ServiceDiscoveryHelper.StoreTracerMetadata`),
and the spec says there is at most one per process. Before that builder is stored,
`ServiceDiscoveryHelper` calls `ddog_tracer_metadata_include_otel_thread_context`. libdatadog adds its
default thread-local metadata to the builder and emits the two attributes as part of the same process
context publication.

The include operation is enabled only when `DD_TRACE_OTEL_CTX_ENABLED` is on and the process is on a
supported Linux architecture. If it returns an error, the tracer logs the owned error, releases it, and
continues storing the ordinary service-discovery metadata. libdatadog owns the process-context mapping,
payload, publication timestamp, update protocol, and lifetime; managed code does not inspect or mutate
that memory.

`tlsdesc_v1_dev` is the value libdatadog's own writer uses and the one current readers match on. OTEP 4947
anticipates renaming it to `tls_v1`; that has to move in step with the readers rather than ahead of them.

## Design notes

**One `AsyncLocal`, not two.** The publisher is driven from the existing `AsyncLocal<Scope>` value-changed
callback that already notifies the Continuous Profiler. That callback fires on the thread performing the
`ExecutionContext` restore — including async continuations and thread-pool hand-offs — which is exactly
what makes an OS-thread-local record correct. Registering a *second* `AsyncLocal` to observe the same value
would double the cost of every `ExecutionContext` restore.

**The sampling decision is never forced.** `trace-flags` is built from the sampling decision only if one
has already been made. Calling `GetOrMakeSamplingDecision()` here, as the W3C propagator does, would move
the decision to span activation time, which is an observable change in tracer behaviour. An undecided trace
is reported as not sampled.

**No P/Invoke-map registration was needed.** `GetOrCreateOtelThreadContextRecord` lives in the existing
`Datadog.Trace.ClrProfiler.NativeMethods+NonWindows` class, which `cor_profiler.cpp` already rewrites to
point at the deployed native library.

**Per-thread failure caching.** Each OS thread makes at most one record-acquisition attempt. If no record
is available, or a record operation throws, that thread caches an unavailable result and skips subsequent
publication attempts. Other threads continue publishing normally, and the first failure is logged once.

**A parked thread keeps its last context.** The record is per OS thread, the context is per
`ExecutionContext`. A thread parked in the thread pool keeps `valid == 1` from its last work item until the
next restore. `Reset` on a null scope covers normal completion.

## Files

### Publishing

| Path | Role |
| --- | --- |
| `tracer/src/Datadog.Tracer.Native/otel_thread_ctx.cpp`, `.h` | The TLS slot, native record pool, and pthread lifetime |
| `tracer/src/Datadog.Tracer.Native/CMakeLists.txt` | Adds it to the SHARED target; probes the TLS dialect |
| `tracer/src/Datadog.Trace/OtelThreadContext/OtelThreadContextRecord.cs` | Managed record encoding and updates |
| `.../OtelThreadContext/OtelThreadContextPublisher.cs` | Per-thread address/failure cache, publication, and platform gate |
| `.../OtelThreadContext/IOtelThreadContextPublisher.cs`, `NullOtelThreadContextPublisher.cs` | The publisher abstraction and its no-op |
| `.../OtelThreadContext/IOtelThreadContextRecordProvider.cs`, `OtelThreadContextRecordProvider.cs` | The single point of contact with native code |
| `tracer/src/Datadog.Trace/ClrProfiler/NativeMethods.cs` | `GetOrCreateOtelThreadContextRecord` P/Invoke |
| `tracer/src/Datadog.Trace/AsyncLocalScopeManager.cs` | Drives the publisher from the scope-changed callback |
| `tracer/src/Datadog.Trace/TracerManagerFactory.cs` | Creates the publisher and passes it to the scope manager |

### Announcing

| Path | Role |
| --- | --- |
| `.../LibDatadog/ServiceDiscovery/ServiceDiscoveryHelper.cs` | Includes the thread-local metadata on libdatadog's builder before storing it |
| `.../LibDatadog/ServiceDiscovery/VoidResult.cs` | Maps the include operation's success/error result |
| `.../LibDatadog/NativeInterop.cs` | Declares the libdatadog builder include operation |

### Supporting

| Path | Role |
| --- | --- |
| `tracer/src/Datadog.Trace/Util/HexString.cs` | `ToHexBytes(ulong, Span<byte>)`, for the local root span id |
| `tracer/src/Datadog.Trace/Configuration/supported-configurations.yaml`, `TracerSettings.cs` | `DD_TRACE_OTEL_CTX_ENABLED` |
| `tracer/build/_build/Build.Steps.cs` | Asserts the ELF symbol is exported after the Linux native build |

## Tests

Managed unit tests are runnable on any platform — the native record provider is stood in for by
`FakeOtelThreadContextRecordProvider`. They live in
`tracer/test/Datadog.Trace.Tests/OtelThreadContext/`:

- `OtelThreadContextRecordTests.cs` — the record layout, with offsets hard-coded from the spec rather than
  read back from the implementation, since the layout is an inter-process contract.
- `OtelThreadContextPublisherTests.cs` — that the record is acquired exactly once per thread, that each
  thread gets its own record, that `Reset` only clears `valid`, that acquisition failure is isolated and
  cached per thread, and that publishing does not force a sampling decision.

Plus `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/OtelThreadContextTests.cs`, Linux only. It
keeps a real instrumented process alive, reads its `OTEL_CTX` mapping with `process_vm_readv`, and decodes
the protobuf payload using a test-only reader that shares no parsing code with the tracer. With nested
root and child scopes active, the sample also resolves its current thread's public `otel_thread_ctx_v1`
TLS-slot address and blocks. The test process uses `process_vm_readv` to follow the slot's record pointer
and independently decode the record, checking the trace id, active child span id, trace flags, and local
root span id against the spans received by the mock agent. This verifies that libdatadog's resource
metadata survives, that the two `threadlocal.*` attributes are externally readable exactly once, that the
native TLS slot and record contain the expected active context, that tracing is undisturbed, and that the
attributes are absent when the feature is off.

That Linux test also exercises the real by-value `VoidResult` P/Invoke on the success path. The native
null/error arm, panic containment, and owned-error contract are tested in libdatadog; the tracer does not
add an interop abstraction solely to synthesize an otherwise unreachable builder error.

`tracer/test/Datadog.Tracer.Native.Tests/otel_thread_ctx_test.cpp` creates and joins real pthreads to verify
that an address is stable for an owning thread, concurrent threads receive distinct records, and thread
exit returns a zeroed, aligned record for reuse.

Useful checks against a built library:

```bash
# the symbol is present, TLS type, global, default visibility
readelf --dyn-syms -W Datadog.Tracer.Native.so | grep otel_thread_ctx_v1
nm --dynamic --defined-only  Datadog.Tracer.Native.so | grep otel_thread_ctx_v1

# which access model the compiler and linker actually produced:
# TLSDESC / TLS_GD are the dynamic models, TPOFF means it was relaxed to initial-exec
readelf -r Datadog.Tracer.Native.so | grep -i tls
```

And against a running process with the feature enabled:

```bash
# the process context mapping exists and is named
grep OTEL_CTX /proc/<pid>/maps
```

Confirming the announcement itself takes more than a grep: the `threadlocal.*` keys live in the protobuf
payload, which the header only points at, so it has to be read from `/proc/<pid>/mem` at the address the
header carries. The [`ctx-sharing-demo` reader](https://github.com/scottgerring/ctx-sharing-demo/tree/main/context-reader)
does both halves — process context and thread context — and is the quickest way to see the whole chain end
to end. The integration test independently decodes the published payload; a successful builder call alone
is not treated as proof that the process context was published.
