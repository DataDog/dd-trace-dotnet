#ifndef DD_CLR_PROFILER_LIVEDEBUGGER_TOKENS_H_
#define DD_CLR_PROFILER_LIVEDEBUGGER_TOKENS_H_

#include <corhlpr.h>

#include <mutex>
#include <unordered_map>
#include <unordered_set>

#include "clr_helpers.h"
#include "com_ptr.h"
#include "il_rewriter.h"
#include "integration.h"
#include "string.h" // NOLINT

#define FASTPATH_COUNT 9

namespace trace
{

/// <summary>
/// Class to control all the token references of the module where the livedebugger will be called.
/// Also provides useful helpers for the rewriting process
/// </summary>
class LiveDebuggerTokens
{
private:
    ModuleMetadata* module_metadata_ptr = nullptr;
    const bool enable_by_ref_instrumentation = false;

    // CorLib tokens
    mdAssemblyRef corLibAssemblyRef = mdAssemblyRefNil;
    mdTypeRef objectTypeRef = mdTypeRefNil;
    mdTypeRef exTypeRef = mdTypeRefNil;
    mdTypeRef typeRef = mdTypeRefNil;
    mdTypeRef runtimeTypeHandleRef = mdTypeRefNil;
    mdToken getTypeFromHandleToken = mdTokenNil;
    mdTypeRef runtimeMethodHandleRef = mdTypeRefNil;

    // LiveDebugger tokens
    mdAssemblyRef profilerAssemblyRef = mdAssemblyRefNil;
    mdTypeRef liveDebuggerTypeRef = mdTypeRefNil;
    mdTypeRef liveDebuggerStateTypeRef = mdTypeRefNil;
    mdTypeRef liveDebuggerReturnVoidTypeRef = mdTypeRefNil;
    mdTypeRef liveDebuggerReturnTypeRef = mdTypeRefNil;

    mdMemberRef beginArrayMemberRef = mdMemberRefNil;
    mdMemberRef beginMethodFastPathRefs[FASTPATH_COUNT];
    mdMemberRef endVoidMemberRef = mdMemberRefNil;

    mdMemberRef logExceptionRef = mdMemberRefNil;

    mdMemberRef liveDebuggerStateTypeGetDefault = mdMemberRefNil;
    mdMemberRef liveDebuggerReturnVoidTypeGetDefault = mdMemberRefNil;
    mdMemberRef getDefaultMemberRef = mdMemberRefNil;

    ModuleMetadata* GetMetadata();
    HRESULT EnsureCorLibTokens();
    HRESULT EnsureBaseLivedebuggerTokens();
    mdTypeRef GetTargetStateTypeRef();
    mdTypeRef GetTargetVoidReturnTypeRef();
    mdTypeSpec GetTargetReturnValueTypeRef(FunctionMethodArgument* returnArgument);
    mdMemberRef GetLiveDebuggerStateDefaultMemberRef();
    mdMemberRef GetLiveDebuggerReturnVoidDefaultMemberRef();
    mdMemberRef GetLiveDebuggerReturnValueDefaultMemberRef(mdTypeSpec liveDebuggerReturnTypeSpec);
    mdMethodSpec GetLiveDebuggerDefaultValueMethodSpec(FunctionMethodArgument* methodArgument);
    mdToken GetCurrentTypeRef(const TypeInfo* currentType, bool& isValueType);

    HRESULT ModifyLocalSig(ILRewriter* reWriter, FunctionMethodArgument* methodReturnValue, ULONG* liveDebuggerStateIndex,
                           ULONG* exceptionIndex, ULONG* liveDebuggerReturnIndex, ULONG* returnValueIndex,
                           mdToken* liveDebuggerStateToken, mdToken* exceptionToken, mdToken* liveDebuggerReturnToken);

    HRESULT WriteBeginMethodWithArgumentsArray(void* rewriterWrapperPtr, mdTypeRef integrationTypeRef,
                                               const TypeInfo* currentType, ILInstr** instruction);

public:
    LiveDebuggerTokens(ModuleMetadata* module_metadata_ptr, const bool enableByRefInstrumentation);

    mdTypeRef GetObjectTypeRef();
    mdTypeRef GetExceptionTypeRef();
    mdAssemblyRef GetCorLibAssemblyRef();

    HRESULT ModifyLocalSigAndInitialize(void* rewriterWrapperPtr, FunctionInfo* functionInfo,
                                        ULONG* liveDebuggerStateIndex, ULONG* exceptionIndex,
                                        ULONG* liveDebuggerReturnIndex, ULONG* returnValueIndex,
                                        mdToken* liveDebuggerStateToken, mdToken* exceptionToken,
                                        mdToken* liveDebuggerReturnToken, ILInstr** firstInstruction);

    HRESULT WriteBeginMethod(void* rewriterWrapperPtr, mdTypeRef integrationTypeRef, const TypeInfo* currentType,
                             const std::vector<FunctionMethodArgument>& methodArguments, ILInstr** instruction);

    HRESULT WriteEndVoidReturnMemberRef(void* rewriterWrapperPtr, mdTypeRef integrationTypeRef,
                                        const TypeInfo* currentType, ILInstr** instruction);

    HRESULT WriteEndReturnMemberRef(void* rewriterWrapperPtr, mdTypeRef integrationTypeRef, const TypeInfo* currentType,
                                    FunctionMethodArgument* returnArgument, ILInstr** instruction);

    HRESULT WriteLogException(void* rewriterWrapperPtr, mdTypeRef integrationTypeRef, const TypeInfo* currentType,
                              ILInstr** instruction);

    HRESULT WriteLiveDebuggerReturnGetReturnValue(void* rewriterWrapperPtr, mdTypeSpec liveDebuggerReturnTypeSpec,
                                                ILInstr** instruction);
};

} // namespace trace

#endif // DD_CLR_PROFILER_LIVEDEBUGGER_TOKENS_H_