#ifndef DD_CLR_PROFILER_CALLTARGET_TOKENS_H_
#define DD_CLR_PROFILER_CALLTARGET_TOKENS_H_

#include <corhlpr.h>

#include <mutex>
#include <unordered_map>
#include <unordered_set>

#include "clr_helpers.h"
#include "il_rewriter.h"
#include "integration.h"
#include "../../../shared/src/native-src/string.h" // NOLINT
#include "../../../shared/src/native-src/com_ptr.h"

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
    mdToken getTypeFromHandleToken = mdTokenNil;

    mdMemberRef callTargetStateTypeGetDefault = mdMemberRefNil;
    mdMemberRef callTargetReturnVoidTypeGetDefault = mdMemberRefNil;
    mdMemberRef getDefaultMemberRef = mdMemberRefNil;

    mdTypeRef GetTargetStateTypeRef();
    mdTypeRef GetTargetVoidReturnTypeRef();
    mdMemberRef GetCallTargetStateDefaultMemberRef();
    mdMemberRef GetCallTargetReturnVoidDefaultMemberRef();
    mdMemberRef GetCallTargetReturnValueDefaultMemberRef(mdTypeSpec callTargetReturnTypeSpec);
    mdMethodSpec GetCallTargetDefaultValueMethodSpec(const TypeSignature* methodArgument);

    HRESULT ModifyLocalSig(ILRewriter* reWriter, TypeSignature* methodReturnValue, ULONG* callTargetStateIndex,
                           ULONG* exceptionIndex, ULONG* callTargetReturnIndex, ULONG* returnValueIndex,
                           mdToken* callTargetStateToken, mdToken* exceptionToken, mdToken* callTargetReturnToken);

protected:
    // CallTarget tokens
    mdAssemblyRef profilerAssemblyRef = mdAssemblyRefNil;

    const bool enable_by_ref_instrumentation = false;
    const bool enable_calltarget_state_by_ref = false;
    mdTypeRef callTargetTypeRef = mdTypeRefNil;
    mdTypeRef callTargetStateTypeRef = mdTypeRefNil;
    mdTypeRef callTargetReturnVoidTypeRef = mdTypeRefNil;
    mdTypeRef callTargetReturnTypeRef = mdTypeRefNil;
    mdTypeRef exTypeRef = mdTypeRefNil;
    mdTypeRef runtimeTypeHandleRef = mdTypeRefNil;
    mdTypeRef runtimeMethodHandleRef = mdTypeRefNil;

    ModuleMetadata* GetMetadata();
    virtual HRESULT EnsureBaseCalltargetTokens();
    mdTypeSpec GetTargetReturnValueTypeRef(TypeSignature* returnArgument);

    virtual const shared::WSTRING& GetCallTargetType() = 0;
    virtual const shared::WSTRING& GetCallTargetStateType() = 0;
    virtual const shared::WSTRING& GetCallTargetReturnType() = 0;
    virtual const shared::WSTRING& GetCallTargetReturnGenericType() = 0;
    virtual int GetAdditionalLocalsCount();
    virtual void AddAdditionalLocals(COR_SIGNATURE (&signatureBuffer)[500], ULONG& signatureOffset, ULONG& signatureSize);

    CallTargetTokens(ModuleMetadata* moduleMetadataPtr, const bool enableByRefInstrumentation,
                     const bool enableCallTargetStateByRef);

public:
    mdTypeRef GetObjectTypeRef();
    mdTypeRef GetExceptionTypeRef();
    mdTypeRef GetRuntimeTypeHandleTypeRef();
    mdTypeRef GetRuntimeMethodHandleTypeRef();
    mdAssemblyRef GetCorLibAssemblyRef();
    mdToken GetCurrentTypeRef(const TypeInfo* currentType, bool& isValueType);

    HRESULT EnsureCorLibTokens();

    HRESULT ModifyLocalSigAndInitialize(void* rewriterWrapperPtr, TypeSignature* methodReturnType,
                                        ULONG* callTargetStateIndex, ULONG* exceptionIndex,
                                        ULONG* callTargetReturnIndex, ULONG* returnValueIndex,
                                        mdToken* callTargetStateToken, mdToken* exceptionToken,
                                        mdToken* callTargetReturnToken, ILInstr** firstInstruction);

    HRESULT WriteCallTargetReturnGetReturnValue(void* rewriterWrapperPtr, mdTypeSpec callTargetReturnTypeSpec,
                                                ILInstr** instruction);
};

} // namespace trace

#endif // DD_CLR_PROFILER_CALLTARGET_TOKENS_H_