#ifndef DD_CLR_PROFILER_RUNTIME_ASYNC_H_
#define DD_CLR_PROFILER_RUNTIME_ASYNC_H_

#include <corhlpr.h>

#include "clr_helpers.h"

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

// Walks a declared return TypeSignature looking for Task-shaped syntax, without resolving any
// names. On success `openTypeToken` is the TypeDef/TypeRef of the (possibly generic) return type,
// `isValueTypeShape` says whether it was spelled ELEMENT_TYPE_VALUETYPE rather than
// ELEMENT_TYPE_CLASS (ValueTask is a struct, Task is not), and when `isGenericInst` is true with
// exactly one generic argument, `typeArg` is a slice of the same signature blob covering that
// argument.
//
// Returns E_FAIL for every other shape, including generic instantiations with an argument count
// other than one. Public for testing.
HRESULT ParseTaskLikeReturnShape(const TypeSignature& declared, mdToken& openTypeToken, bool& isGenericInst,
                                 bool& isValueTypeShape, TypeSignature& typeArg);

// Maps a runtime-async method's declared return type to the type its body actually leaves on the
// evaluation stack at `ret`: void for Task/ValueTask, T for Task<T>/ValueTask<T>.
//
// Returns E_FAIL when the declared return is not one of those four. MethodImplAttributes.Async can
// be set on a method it has no effect on, and we must not guess unknown types. The match requires
// both the name and the element type (Task as a class, ValueTask as a struct), so a type that only
// borrows one of those names is declined rather than rewritten.
HRESULT GetRuntimeAsyncEffectiveReturnType(const TypeSignature& declared,
                                           const ComPtr<IMetaDataImport2>& metadata_import, TypeSignature& effective);

} // namespace trace

#endif // DD_CLR_PROFILER_RUNTIME_ASYNC_H_
