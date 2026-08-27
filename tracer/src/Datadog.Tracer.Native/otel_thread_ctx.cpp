//---------------------------------------------------------------------------------------
// OTEP 4947 ("Thread Context: Sharing Thread-Level Information with External Readers")
// requires the publishing SDK to expose a pointer to the current thread's Thread-Local
// Context Record through a thread-local variable named `otel_thread_ctx_v1`, exported as
// an ELF TLS symbol (STT_TLS) in the dynamic symbol table, so out-of-process readers -
// such as the OpenTelemetry eBPF profiler - can resolve it by walking `/proc/<pid>/maps`
// and each mapped module's `.dynsym`.
//
// This is the only part of the feature that cannot live in managed code: nothing in the
// BCL emits an ELF TLS symbol, and nothing can compute a TLS address. So the native
// surface is kept to the minimum - the slot itself, plus an address getter - and the
// managed tracer owns everything else: allocating the record, writing it, installing the
// pointer and clearing it. See docs/OTelContextPropagation.md.
//
// NOTE: Must keep this signature in sync with the DllImport in NativeMethods.cs!
//
// This file is intentionally self-contained (no includes): it is part of the SHARED
// target rather than the static library, so that the TLS definition can never be dropped
// as an unreferenced archive member.
//---------------------------------------------------------------------------------------

#ifdef LINUX

// The project is compiled with `-fvisibility=hidden`, so the explicit default visibility
// below is what places the symbol in `.dynsym`. Datadog.Tracer.Native has no version
// script, so nothing filters it out afterwards.
//
// The slot deliberately holds an 8-byte pointer rather than the 640-byte record itself.
// This library is dlopen'd, so if the linker relaxes the access to initial-exec the slot
// must fit in glibc's static TLS surplus: 8 bytes always does, 640 would consume most of
// it.
//
// NOTE the braced linkage block. `extern "C" __thread void* x;` would be only a *declaration*:
// a declaration directly contained in a linkage-specification is treated as if it carried the
// `extern` specifier, so the symbol would come out UND and no reader would ever find a context.
// Inside braces, a declaration without `extern` is a definition. Left without an initializer
// so it lands in `.tbss`, which the loader zeroes for every new thread.
extern "C"
{
    __attribute__((visibility("default"))) __thread void* otel_thread_ctx_v1;
}

// Returns the address of the calling thread's `otel_thread_ctx_v1` slot. The managed
// tracer calls this once per OS thread, caches the address, and performs every subsequent
// store itself - so there is no interop at all on the span activation path.
extern "C" __attribute__((visibility("default"))) void** GetOtelThreadContextSlot()
{
    return &otel_thread_ctx_v1;
}

#endif
