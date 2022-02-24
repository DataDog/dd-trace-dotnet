#ifndef DD_CLR_PROFILER_CALLTARGET_TOKENS_H_
#define DD_CLR_PROFILER_CALLTARGET_TOKENS_H_

#include <corhlpr.h>

#include <mutex>
#include <unordered_map>
#include <unordered_set>

#include "clr_helpers.h"
#include "com_ptr.h"
#include "il_rewriter.h"
#include "integration.h"
#include "../../../shared/src/native-src/string.h" // NOLINT

namespace trace
{

/// <summary>
/// Class to control all the token references of the module where the calltarget will be called.
/// Also provides useful helpers for the rewriting process
/// </summary>
class CallTargetTokens
{
private:
    ModuleMetadata* module_metadata_ptr = nullptr;

    // CorLib tokens
    mdAssemblyRef corLibAssemblyRef = mdAssemblyRefNil;
    mdTypeRef objectTypeRef = mdTypeRefNil;
    mdTypeRef typeRef = mdTypeRefNil;
    mdTypeRef runtimeTypeHandleRef = mdTypeRefNil;
    mdToken getTypeFromHandleToken = mdTokenNil;
    mdTypeRef runtimeMethodHandleRef = mdTypeRefNil;

    // CallTarget tokens
    mdAssemblyRef profilerAssemblyRef = mdAssemblyRefNil;

    mdMemberRef callTargetStateTypeGetDefault = mdMemberRefNil;
    mdMemberRef callTargetReturnVoidTypeGetDefault = mdMemberRefNil;
    mdMemberRef getDefaultMemberRef = mdMemberRefNil;

    HRESULT EnsureCorLibTokens();
    mdTypeRef GetTargetStateTypeRef();
    mdTypeRef GetTargetVoidReturnTypeRef();
    mdMemberRef GetCallTargetStateDefaultMemberRef();
    mdMemberRef GetCallTargetReturnVoidDefaultMemberRef();
    mdMemberRef GetCallTargetReturnValueDefaultMemberRef(mdTypeSpec callTargetReturnTypeSpec);
    mdMethodSpec GetCallTargetDefaultValueMethodSpec(TypeSignature* methodArgument);

    HRESULT ModifyLocalSig(ILRewriter* reWriter, TypeSignature* methodReturnValue, ULONG* callTargetStateIndex,
                           ULONG* exceptionIndex, ULONG* callTargetReturnIndex, ULONG* returnValueIndex,
                           mdToken* callTargetStateToken, mdToken* exceptionToken, mdToken* callTargetReturnToken);

protected:
    const bool enable_by_ref_instrumentation = false;
    const bool enable_calltarget_state_by_ref = false;
    mdTypeRef callTargetTypeRef = mdTypeRefNil;
    mdTypeRef callTargetStateTypeRef = mdTypeRefNil;
    mdTypeRef callTargetReturnVoidTypeRef = mdTypeRefNil;
    mdTypeRef callTargetReturnTypeRef = mdTypeRefNil;
    mdTypeRef exTypeRef = mdTypeRefNil;

    ModuleMetadata* GetMetadata();
    HRESULT EnsureBaseCalltargetTokens();
    mdTypeSpec GetTargetReturnValueTypeRef(TypeSignature* returnArgument);
    mdToken GetCurrentTypeRef(const TypeInfo* currentType, bool& isValueType);

    virtual const shared::WSTRING& GetCallTargetType() = 0;
    virtual const shared::WSTRING& GetCallTargetStateType() = 0;
    virtual const shared::WSTRING& GetCallTargetReturnType() = 0;
    virtual const shared::WSTRING& GetCallTargetReturnGenericType() = 0;

    CallTargetTokens(ModuleMetadata* moduleMetadataPtr, const bool enableByRefInstrumentation,
                     const bool enableCallTargetStateByRef);

public:
    mdTypeRef GetObjectTypeRef();
    mdTypeRef GetExceptionTypeRef();
    mdAssemblyRef GetCorLibAssemblyRef();

    HRESULT ModifyLocalSigAndInitialize(void* rewriterWrapperPtr, FunctionInfo* functionInfo,
                                        ULONG* callTargetStateIndex, ULONG* exceptionIndex,
                                        ULONG* callTargetReturnIndex, ULONG* returnValueIndex,
                                        mdToken* callTargetStateToken, mdToken* exceptionToken,
                                        mdToken* callTargetReturnToken, ILInstr** firstInstruction);

    HRESULT WriteCallTargetReturnGetReturnValue(void* rewriterWrapperPtr, mdTypeSpec callTargetReturnTypeSpec,
                                                ILInstr** instruction);
};

} // namespace trace

#endif // DD_CLR_PROFILER_CALLTARGET_TOKENS_H_