#ifndef DD_CLR_PROFILER_CALLTARGET_TOKENS_H_
#define DD_CLR_PROFILER_CALLTARGET_TOKENS_H_

#define BUFFER_SIZE 1000

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
    volatile mdAssemblyRef corLibAssemblyRef = mdAssemblyRefNil;
    volatile mdTypeRef objectTypeRef = mdTypeRefNil;
    volatile mdTypeRef typeRef = mdTypeRefNil;
    volatile mdToken getTypeFromHandleToken = mdTokenNil;

    volatile mdMemberRef callTargetStateTypeGetDefault = mdMemberRefNil;
    volatile mdMemberRef callTargetReturnVoidTypeGetDefault = mdMemberRefNil;
    volatile mdMemberRef getDefaultMemberRef = mdMemberRefNil;

    mdTypeRef GetTargetStateTypeRef();
    mdTypeRef GetTargetVoidReturnTypeRef();
    mdMemberRef GetCallTargetStateDefaultMemberRef();
    mdMemberRef GetCallTargetReturnVoidDefaultMemberRef();
    mdMemberRef GetCallTargetReturnValueDefaultMemberRef(mdTypeSpec callTargetReturnTypeSpec);
    mdMethodSpec GetCallTargetDefaultValueMethodSpec(const TypeSignature* methodArgument);

    HRESULT ModifyLocalSig(ILRewriter* reWriter, TypeSignature* methodReturnValue, std::vector<TypeSignature>* methodTypeArguments,
                           ULONG* callTargetStateIndex, ULONG* exceptionIndex, ULONG* callTargetReturnIndex, ULONG* returnValueIndex,
                           mdToken* callTargetStateToken, mdToken* exceptionToken, mdToken* callTargetReturnToken,
                           std::vector<ULONG>& additionalLocalIndices, bool isAsyncMethod = false);

protected:
    // CallTarget tokens
    volatile mdAssemblyRef profilerAssemblyRef = mdAssemblyRefNil;

    std::recursive_mutex metadata_mutex;

    const bool enable_by_ref_instrumentation = false;
    const bool enable_calltarget_state_by_ref = false;
    volatile mdTypeRef callTargetTypeRef = mdTypeRefNil;
    volatile mdTypeRef callTargetStateTypeRef = mdTypeRefNil;
    volatile mdTypeRef callTargetReturnVoidTypeRef = mdTypeRefNil;
    volatile mdTypeRef callTargetReturnTypeRef = mdTypeRefNil;
    volatile mdTypeRef callTargetRefStructTypeRef = mdTypeRefNil;
    volatile mdTypeRef exTypeRef = mdTypeRefNil;
    volatile mdTypeRef runtimeTypeHandleRef = mdTypeRefNil;
    volatile mdTypeRef runtimeMethodHandleRef = mdTypeRefNil;

    ModuleMetadata* GetMetadata();
    virtual HRESULT EnsureBaseCalltargetTokens();
    mdTypeSpec GetTargetReturnValueTypeRef(TypeSignature* returnArgument);

    virtual const shared::WSTRING& GetCallTargetType() = 0;
    virtual const shared::WSTRING& GetCallTargetStateType() = 0;
    virtual const shared::WSTRING& GetCallTargetReturnType() = 0;
    virtual const shared::WSTRING& GetCallTargetReturnGenericType() = 0;
    virtual const shared::WSTRING& GetCallTargetRefStructType() = 0;

    virtual void AddAdditionalLocals(TypeSignature* methodReturnValue, std::vector<TypeSignature>* methodTypeArguments,
                                     COR_SIGNATURE (&signatureBuffer)[BUFFER_SIZE], ULONG& signatureOffset,
                                     ULONG& signatureSize, bool isAsyncMethod);

    CallTargetTokens(ModuleMetadata* moduleMetadataPtr, bool enableByRefInstrumentation,
                     bool enableCallTargetStateByRef);

public:
    HRESULT EnsureCorLibTokens();

    virtual int GetAdditionalLocalsCount(const std::vector<TypeSignature>& methodTypeArguments);
    mdTypeRef GetObjectTypeRef();
    mdTypeRef GetExceptionTypeRef();
    mdTypeRef GetRuntimeTypeHandleTypeRef();
    mdTypeRef GetRuntimeMethodHandleTypeRef();
    mdAssemblyRef GetCorLibAssemblyRef();
    mdToken GetCurrentTypeRef(const TypeInfo* currentType, bool& isValueType);

    HRESULT ModifyLocalSigAndInitialize(void* rewriterWrapperPtr, TypeSignature* methodReturnType, std::vector<TypeSignature>* methodTypeArguments,
                                        ULONG* callTargetStateIndex, ULONG* exceptionIndex, ULONG* callTargetReturnIndex, ULONG* returnValueIndex,
                                        mdToken* callTargetStateToken, mdToken* exceptionToken,
                                        mdToken* callTargetReturnToken, ILInstr** firstInstruction, std::vector<ULONG>& additionalLocalIndices, bool
                                        isAsyncMethod = false);

    HRESULT WriteCallTargetReturnGetReturnValue(void* rewriterWrapperPtr, mdTypeSpec callTargetReturnTypeSpec,
                                                ILInstr** instruction);
};

} // namespace trace

#endif // DD_CLR_PROFILER_CALLTARGET_TOKENS_H_