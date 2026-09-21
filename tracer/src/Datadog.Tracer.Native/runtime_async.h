#ifndef DD_CLR_PROFILER_RUNTIME_ASYNC_H_
#define DD_CLR_PROFILER_RUNTIME_ASYNC_H_

#include <corhlpr.h>

namespace trace
{

// MethodImplAttributes.Async, introduced in .NET 11 for the "runtime async" feature.
//
// It is deliberately NOT added to shared/src/native-lib/coreclr/src/inc/corhdr.h: that file is a
// verbatim copy of the coreclr header, manually synced (it still stops at miInternalCall = 0x1000,
// and miUserMask excludes this bit). Defining the constant here means a future refresh of the
// vendored header is a no-op for us rather than a silent clobber.
//
// A runtime-async method has no compiler-generated state machine; the runtime handles suspension.
// Crucially its body does NOT return the declared Task: per the ECMA-335 augment
// (dotnet/runtime docs/design/specs/runtime-async.md), at `ret` the stack "should be empty in the
// case of Task or ValueTask, or the type argument in the case of Task<T> or ValueTask<T>".
constexpr DWORD miAsync = 0x2000;

inline bool IsMiAsync(DWORD methodImplFlags)
{
    return (methodImplFlags & miAsync) != 0;
}

} // namespace trace

#endif // DD_CLR_PROFILER_RUNTIME_ASYNC_H_
