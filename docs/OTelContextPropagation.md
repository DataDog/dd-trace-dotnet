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

Almost all of it is managed code. The one thing the BCL cannot do is emit an ELF TLS symbol or compute a
TLS address, so the native tracer contributes a thread-local slot and a getter for its address — and
nothing else.

## Configuration

| | |
| --- | --- |
| Setting | `DD_TRACE_OTEL_CTX_ENABLED` (`TracerSettings.OtelThreadContextEnabled`) |
| Default | off |
| Requires | Linux, x64 or arm64, and automatic instrumentation attached |

The platform gate is `OtelThreadContextPublisher.IsPlatformSupported()`, shared by the publisher and the
announcer. OTEP 4947 is deliberately Linux-only: it relies on ELF thread-local storage, and its readers are
themselves Linux-specific. Automatic instrumentation is required because that is what rewrites the P/Invoke
map to point at the deployed native library.

## How it works

### At startup, once

```
TracerManager.OneTimeSetup
        |
        +--> ServiceDiscoveryHelper.StoreTracerMetadata(...)   libdatadog publishes the process context
        +--> OtelProcessContextAnnouncer.Announce(settings)    appends the threadlocal.* keys to it
```

### On every span activation

```
AsyncLocalScopeManager.OnScopeChanged            (the existing AsyncLocal<Scope> callback)
        |
        +--> Profiler.Instance.ContextTracker.Set(...)          Continuous Profiler, unchanged
        +--> OtelThreadContextPublisher.Set(span)
                 |
                 +-- [ThreadStatic] record  -- miss --> rent a 640B block from the pool,
                 |                                      GetOtelThreadContextSlot()   <-- the only P/Invoke
                 |                                      *slot = block                <-- managed store
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

Because the pointer is installed once and never changed, detaching is done by clearing `valid`. The spec
requires a writer to pick one mechanism or the other — swapping the pointer, or toggling the flag — and
never both.

Only the owning thread writes its record, and the spec requires readers to observe a thread while it is
stopped or interrupted, so there is no cross-thread race to guard against. The only hazard is reordering,
which is why `valid` is cleared first and set last: a reader that samples mid-update sees an invalid record
and skips it rather than reading a torn one.

### Record layout

Byte-packed with no padding. Multi-byte scalars use native endianness; the trace and span ids use W3C
Trace Context format, i.e. big endian. `OtelThreadContextRecord` is the only type that knows this layout.

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

Records are pooled by `OtelThreadContextRecordPool` and **never returned to the allocator**. Threads die
at arbitrary points and an out-of-process reader may hold the address of a record, so freeing the memory
would risk a use-after-free across a process boundary. Recycling instead bounds the footprint by the peak
number of threads that have carried an active span, at 640 bytes each.

A thread's record is owned by a finalizable object held in a `[ThreadStatic]` field, so it becomes
collectable when the thread dies and the finalizer returns the block to the pool. Three subtleties:

- The finalizer **must not** clear the thread's slot. It runs on the finalizer thread, so the cached slot
  address belongs to a thread whose TLS block pthread has already reclaimed. Nothing is left dangling
  anyway, because the slot dies together with its thread.
- Blocks are zeroed **on release**, not just on rent. A new thread's `.tbss` is zero-initialized by the
  loader, so it should never observe a recycled block at all — but if it somehow did, a zeroed block reads
  as "no context" rather than as another thread's context.
- The free list is guarded by a plain `lock`, not a lock-free CAS. A block is rented once per thread and
  returned once that thread is gone, so contention is negligible and the lock avoids having to reason about
  ABA on a recycled node.

## The native surface

`tracer/src/Datadog.Tracer.Native/otel_thread_ctx.cpp`, in full:

```cpp
extern "C"
{
    __attribute__((visibility("default"))) __thread void* otel_thread_ctx_v1;
}

extern "C" __attribute__((visibility("default"))) void** GetOtelThreadContextSlot()
{
    return &otel_thread_ctx_v1;
}
```

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
  the compiler version, and since the slot address is resolved once per thread the dialect has no
  measurable cost either way.
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

`CompileTracerNativeSrcLinux` asserts with `nm --dynamic --defined-only` that the symbol is actually
exported. Without that check a compiler, linker or visibility change could drop it silently — readers would
just never find any context, and nothing at runtime would say why.

## Announcing the schema in the process context

Per the spec's reading protocol, a reader will not look for `otel_thread_ctx_v1` until the process's
[OTEP 4719 process context](https://github.com/open-telemetry/opentelemetry-specification/blob/main/oteps/profiles/4719-process-ctx.md)
advertises `threadlocal.schema_version`. Two attributes are needed:

| Key | Value |
| --- | --- |
| `threadlocal.schema_version` | `tlsdesc_v1_dev` |
| `threadlocal.attribute_key_map` | `["datadog.local_root_span_id"]` |

The tracer already publishes a process context through libdatadog (`ServiceDiscoveryHelper.StoreTracerMetadata`),
and the spec says there is at most one per process. So `OtelProcessContextAnnouncer` **extends the existing
one** rather than publishing another. It runs once, from `TracerManager.OneTimeSetup`, immediately after
the call that publishes the context:

1. Locate the mapping by reading `/proc/self/maps` and matching `[anon_shmem:OTEL_CTX]`, `[anon:OTEL_CTX]`
   or `/memfd:OTEL_CTX`.
2. Validate the header signature and version, and check the publication timestamp is non-zero (zero means
   another writer is mid-update). This is our own address space, so this and everything below is plain
   pointer access — no `process_vm_readv`, no interop.
3. Build a longer payload: the existing bytes, plus the two attributes appended. **Nothing is parsed.**
   `ProcessContext.attributes` is a repeated field and protobuf defines concatenation as merging, so
   appending is equivalent to having encoded them originally — and fields we know nothing about survive
   untouched.
4. Point the header at the new buffer using the OTEP 4719 update protocol: timestamp `0`, barrier, new
   pointer and size, barrier, new timestamp.

libdatadog keeps owning the mapping (it holds it in a Rust `static` for the life of the process) and its
own payload buffer, which we copy from but never modify or free. The only field we take over is the 8-byte
`payload` pointer in the header. The announcer never throws: on any unexpected state it logs and leaves the
process context exactly as libdatadog published it, and thread context records are still written — they
just will not be discovered.

### Two deliberate deviations

- **`prctl(PR_SET_VMA_ANON_NAME)` is not re-issued after the update.** The spec says a writer MUST re-name
  the mapping on every update; managed code cannot call `prctl`. The mapping is already named from
  libdatadog's original publication, so it stays discoverable — what is lost is the wake-up for readers
  that hook `prctl` to detect changes. Polling readers are unaffected.
- **The timestamp is not `CLOCK_BOOTTIME`.** The spec recommends it but only requires a non-zero value that
  is strictly later than the previous one, since readers use the field purely to detect change and torn
  reads. We use a monotonic reading where it is already ahead of the existing value, and otherwise simply
  advance the existing value by one — which satisfies the ordering requirement even on a machine that has
  been suspended, where a `CLOCK_BOOTTIME` value written by libdatadog can exceed our monotonic clock.

Both disappear with one `DllImport("libc")` each, if strict conformance is ever wanted.

### Why not fix this in libdatadog

That would be cleaner, and it is a small change: `TracerMetadata` already has a `ThreadLocalMetadata`
struct, and `to_otel_process_ctx()` already emits both keys from it. It is purely an FFI surface gap —
`ddog_tracer_metadata_set` exposes only `MetadataKind::RuntimeId..ContainerId`, with no way to populate
`threadlocal_metadata`, and the section sits behind the `otel-thread-ctx` cargo feature. Changing
libdatadog was ruled out, hence the approach above.

### The risk to know about

We mutate a header libdatadog owns, with no coordination. Nothing triggers a conflict today — the C ABI
only offers `ddog_tracer_metadata_store`, and the tracer calls it exactly once — but if a future libdatadog
calls `update()` on that handle, it overwrites the payload pointer and the two keys disappear silently. The
announcer also skips its work if the payload already contains `threadlocal.schema_version`, so a future
libdatadog that emits the keys itself will not end up with duplicates.

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

**No P/Invoke-map registration was needed.** `GetOtelThreadContextSlot` lives in the existing
`Datadog.Trace.ClrProfiler.NativeMethods+NonWindows` class, which `cor_profiler.cpp` already rewrites to
point at the deployed native library.

**Self-disabling.** The first failure — the slot not resolving, or a write throwing — latches the publisher
off permanently and logs one warning. It never probes again.

**A parked thread keeps its last context.** The record is per OS thread, the context is per
`ExecutionContext`. A thread parked in the thread pool keeps `valid == 1` from its last work item until the
next restore. `Reset` on a null scope covers normal completion.

## Files

### Publishing

| Path | Role |
| --- | --- |
| `tracer/src/Datadog.Tracer.Native/otel_thread_ctx.cpp` | The TLS slot and its address getter |
| `tracer/src/Datadog.Tracer.Native/CMakeLists.txt` | Adds it to the SHARED target; probes the TLS dialect |
| `tracer/src/Datadog.Trace/OtelThreadContext/OtelThreadContextRecord.cs` | The only code that knows the byte layout |
| `.../OtelThreadContext/OtelThreadContextPublisher.cs` | Per-thread lifecycle, publication, platform gate, self-disabling |
| `.../OtelThreadContext/OtelThreadContextRecordPool.cs` | Free list of aligned unmanaged blocks |
| `.../OtelThreadContext/IOtelThreadContextPublisher.cs`, `NullOtelThreadContextPublisher.cs` | The publisher abstraction and its no-op |
| `.../OtelThreadContext/IOtelThreadContextSlotProvider.cs`, `OtelThreadContextSlotProvider.cs` | The single point of contact with native code |
| `tracer/src/Datadog.Trace/ClrProfiler/NativeMethods.cs` | `GetOtelThreadContextSlot` P/Invoke |
| `tracer/src/Datadog.Trace/AsyncLocalScopeManager.cs` | Drives the publisher from the scope-changed callback |
| `tracer/src/Datadog.Trace/TracerManagerFactory.cs` | Creates the publisher and passes it to the scope manager |

### Announcing

| Path | Role |
| --- | --- |
| `.../OtelThreadContext/OtelProcessContextAnnouncer.cs` | Locates the mapping and applies the 4719 update protocol |
| `.../OtelThreadContext/ThreadLocalMetadataPayload.cs` | Protobuf encoding of the two attributes |
| `tracer/src/Datadog.Trace/TracerManager.cs` | Calls the announcer from `OneTimeSetup` |

### Supporting

| Path | Role |
| --- | --- |
| `tracer/src/Datadog.Trace/Util/HexString.cs` | `ToHexBytes(ulong, Span<byte>)`, for the local root span id |
| `tracer/src/Datadog.Trace/Configuration/supported-configurations.yaml`, `TracerSettings.cs` | `DD_TRACE_OTEL_CTX_ENABLED` |
| `tracer/build/_build/Build.Steps.cs` | Asserts the ELF symbol is exported after the Linux native build |

## Tests

48 unit test cases, all runnable on any platform — the native slot is stood in for by
`FakeOtelThreadContextSlotProvider`, and the process context by a hand-built header laid out like the real
mapping. They live in `tracer/test/Datadog.Trace.Tests/OtelThreadContext/`:

- `OtelThreadContextRecordTests.cs` — the record layout, with offsets hard-coded from the spec rather than
  read back from the implementation, since the layout is an inter-process contract.
- `OtelThreadContextRecordPoolTests.cs` — alignment, recycling, that a reused block carries nothing from
  its previous owner, and concurrent rent/return.
- `OtelThreadContextPublisherTests.cs` — that the slot is resolved exactly once per thread, that each
  thread gets its own record, that `Reset` only clears `valid`, that the publisher disables itself when the
  slot is unavailable, and that publishing does not force a sampling decision.
- `ThreadLocalMetadataPayloadTests.cs` — the protobuf wire format, asserted against tags, lengths and
  offsets worked out by hand rather than round-tripped through the same encoder.
- `OtelProcessContextAnnouncerTests.cs` — `/proc/self/maps` parsing, and the update protocol: that unknown
  payload bytes survive, that libdatadog's buffer is not modified, that the timestamp always advances
  (including when the existing one is already ahead of our clock), and that a bad signature, wrong version
  or in-progress update is refused without touching anything.

Plus `tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/OtelThreadContextTests.cs`, Linux only: that
the native symbol resolves in a real instrumented process, that the announcement succeeds, that tracing is
undisturbed, and that nothing at all is logged when the feature is off.

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
to end. In-process, the tracer log line `Announced the OpenTelemetry thread context schema` says the same
thing, and the integration test asserts on it.
