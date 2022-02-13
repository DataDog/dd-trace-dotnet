#ifndef DD_CLR_PROFILER_DEBUGGER_TOKENS_H_
#define DD_CLR_PROFILER_DEBUGGER_TOKENS_H_

#include <corhlpr.h>

#include <mutex>
#include <unordered_map>
#include <unordered_set>

#include "clr_helpers.h"
#include "com_ptr.h"
#include "il_rewriter.h"
#include "integration.h"
#include "string.h" // NOLINT

using namespace trace;

namespace debugger
{

/// <summary>
/// Class to control all the token references of the module where the Debugger will be called.
/// Also provides useful helpers for the rewriting process
/// </summary>
class DebuggerTokens
{
private:
    ModuleMetadata* module_metadata_ptr = nullptr;
    const bool enable_by_ref_instrumentation = false;
    const bool enable_debugger_state_by_ref = false;

    // CorLib tokens
    mdAssemblyRef corLibAssemblyRef = mdAssemblyRefNil;
    mdTypeRef objectTypeRef = mdTypeRefNil;
    mdTypeRef exTypeRef = mdTypeRefNil;
    mdTypeRef typeRef = mdTypeRefNil;
    mdTypeRef runtimeTypeHandleRef = mdTypeRefNil;
    mdToken getTypeFromHandleToken = mdTokenNil;
    mdTypeRef runtimeMethodHandleRef = mdTypeRefNil;

    // CallTarget tokens
    mdAssemblyRef profilerAssemblyRef = mdAssemblyRefNil;
    mdTypeRef callTargetTypeRef = mdTypeRefNil;
    mdTypeRef callTargetStateTypeRef = mdTypeRefNil;
    mdTypeRef callTargetReturnVoidTypeRef = mdTypeRefNil;
    mdTypeRef callTargetReturnTypeRef = mdTypeRefNil;

    mdMemberRef beginArrayMemberRef = mdMemberRefNil;
    mdMemberRef beginMethodStartMarkerRef = mdMemberRefNil;
    mdMemberRef beginMethodEndMarkerRef = mdMemberRefNil;
    mdMemberRef endMethodEndMarkerRef = mdMemberRefNil;
    mdMemberRef endVoidMemberRef = mdMemberRefNil;

    mdMemberRef logExceptionRef = mdMemberRefNil;

    mdMemberRef logArgRef = mdMemberRefNil;
    mdMemberRef logLocalRef = mdMemberRefNil;

    mdMemberRef callTargetStateTypeGetDefault = mdMemberRefNil;
    mdMemberRef callTargetReturnVoidTypeGetDefault = mdMemberRefNil;
    mdMemberRef getDefaultMemberRef = mdMemberRefNil;

    ModuleMetadata* GetMetadata();
    HRESULT EnsureCorLibTokens();
    HRESULT EnsureBaseDebuggerTokens();
    mdTypeRef GetTargetStateTypeRef();
    mdTypeRef GetTargetVoidReturnTypeRef();
    mdTypeSpec GetTargetReturnValueTypeRef(TypeSignature* returnArgument);
    mdMemberRef GetCallTargetStateDefaultMemberRef();
    mdMemberRef GetCallTargetReturnVoidDefaultMemberRef();
    mdMemberRef GetCallTargetReturnValueDefaultMemberRef(mdTypeSpec callTargetReturnTypeSpec);
    mdMethodSpec GetCallTargetDefaultValueMethodSpec(TypeSignature* methodArgument);
    mdToken GetCurrentTypeRef(const TypeInfo* currentType, bool& isValueType);

    HRESULT ModifyLocalSig(ILRewriter* reWriter, TypeSignature* methodReturnValue, ULONG* callTargetStateIndex,
                           ULONG* exceptionIndex, ULONG* callTargetReturnIndex, ULONG* returnValueIndex,
                           mdToken* callTargetStateToken, mdToken* exceptionToken, mdToken* callTargetReturnToken);

    //HRESULT WriteBeginMethodWithArgumentsArray(void* rewriterWrapperPtr, mdTypeRef integrationTypeRef,
    //                                           const TypeInfo* currentType, ILInstr** instruction);
    HRESULT WriteLogArgOrLocal(void* rewriterWrapperPtr, const TypeSignature& argOrLocal, ILInstr** instruction, bool isArg);

public:
    DebuggerTokens(ModuleMetadata* module_metadata_ptr, const bool enableByRefInstrumentation, const bool enableCallTargetStateByRef);

    mdTypeRef GetObjectTypeRef();
    mdTypeRef GetExceptionTypeRef();
    mdAssemblyRef GetCorLibAssemblyRef();

    HRESULT ModifyLocalSigAndInitialize(void* rewriterWrapperPtr, FunctionInfo* functionInfo,
                                        ULONG* callTargetStateIndex, ULONG* exceptionIndex,
                                        ULONG* callTargetReturnIndex, ULONG* returnValueIndex,
                                        mdToken* callTargetStateToken, mdToken* exceptionToken,
                                        mdToken* callTargetReturnToken, ILInstr** firstInstruction);

    HRESULT WriteBeginMethod_StartMarker(void* rewriterWrapperPtr, const TypeInfo* currentType, ILInstr** instruction);

    HRESULT WriteEndVoidReturnMemberRef(void* rewriterWrapperPtr, const TypeInfo* currentType, ILInstr** instruction);

    HRESULT WriteEndReturnMemberRef(void* rewriterWrapperPtr, const TypeInfo* currentType,
                                    TypeSignature* returnArgument, ILInstr** instruction);

    HRESULT WriteLogException(void* rewriterWrapperPtr, const TypeInfo* currentType, ILInstr** instruction);

    HRESULT WriteCallTargetReturnGetReturnValue(void* rewriterWrapperPtr, mdTypeSpec callTargetReturnTypeSpec,
                                                ILInstr** instruction);

    HRESULT WriteLogArg(void* rewriterWrapperPtr, const TypeSignature& argument, ILInstr** instruction);

    HRESULT WriteLogLocal(void* rewriterWrapperPtr, const TypeSignature& local, ILInstr** instruction);

    HRESULT WriteBeginOrEndMethod_EndMarker(void* rewriterWrapperPtr, bool isBeginMethod, ILInstr** instruction);
    HRESULT WriteBeginMethod_EndMarker(void* rewriterWrapperPtr, ILInstr** instruction);
    HRESULT WriteEndMethod_EndMarker(void* rewriterWrapperPtr, ILInstr** instruction);
};

} // namespace trace

#endif // DD_CLR_PROFILER_DEBUGGER_TOKENS_H_