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
#include "string.h"  // NOLINT

namespace trace {

/// <summary>
/// Class to control all the token references of the module where the calltarget will be called.
/// Also provides useful helpers for the rewriting process
/// </summary>
class CallTargetTokens {
 private:
  void* module_metadata_ptr = nullptr;

  // CallTarget constants
  WSTRING managed_profiler_calltarget_type = WStr("Datadog.Trace.ClrProfiler.CallTarget.CallTargetInvoker");
  WSTRING managed_profiler_calltarget_beginmethod_name = WStr("BeginMethod");
  WSTRING managed_profiler_calltarget_endmethod_name = WStr("EndMethod");
  WSTRING managed_profiler_calltarget_logexception_name = WStr("LogException");
  WSTRING managed_profiler_calltarget_getdefaultvalue_name = WStr("GetDefaultValue");

  WSTRING managed_profiler_calltarget_statetype = WStr("Datadog.Trace.ClrProfiler.CallTarget.CallTargetState");
  WSTRING managed_profiler_calltarget_statetype_getdefault_name =WStr("GetDefault");

  WSTRING managed_profiler_calltarget_returntype = WStr("Datadog.Trace.ClrProfiler.CallTarget.CallTargetReturn");
  WSTRING managed_profiler_calltarget_returntype_getdefault_name = WStr("GetDefault");

  WSTRING managed_profiler_calltarget_returntype_generics = WStr("Datadog.Trace.ClrProfiler.CallTarget.CallTargetReturn`1");
  WSTRING managed_profiler_calltarget_returntype_getreturnvalue_name = WStr("GetReturnValue");

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
  mdMemberRef beginP0MemberRef = mdMemberRefNil;
  mdMemberRef beginP1MemberRef = mdMemberRefNil;
  mdMemberRef beginP2MemberRef = mdMemberRefNil;
  mdMemberRef beginP3MemberRef = mdMemberRefNil;
  mdMemberRef beginP4MemberRef = mdMemberRefNil;
  mdMemberRef beginP5MemberRef = mdMemberRefNil;
  mdMemberRef beginP6MemberRef = mdMemberRefNil;

  mdMemberRef endVoidMemberRef = mdMemberRefNil;

  mdMemberRef logExceptionRef = mdMemberRefNil;

  mdMemberRef callTargetStateTypeGetDefault = mdMemberRefNil;
  mdMemberRef callTargetReturnVoidTypeGetDefault = mdMemberRefNil;
  mdMemberRef getDefaultMemberRef = mdMemberRefNil;

  inline ModuleMetadata* GetMetadata() {
    return (ModuleMetadata*)module_metadata_ptr;
  }
  HRESULT EnsureCorLibTokens();
  HRESULT EnsureBaseCalltargetTokens();
  mdTypeRef GetTargetStateTypeRef();
  mdTypeRef GetTargetVoidReturnTypeRef();
  mdTypeSpec GetTargetReturnValueTypeRef(
      FunctionMethodArgument* returnArgument);
  mdMemberRef GetCallTargetStateDefaultMemberRef();
  mdMemberRef GetCallTargetReturnVoidDefaultMemberRef();
  mdMemberRef GetCallTargetReturnValueDefaultMemberRef(
      mdTypeSpec callTargetReturnTypeSpec);
  mdMethodSpec GetCallTargetDefaultValueMethodSpec(
      FunctionMethodArgument* methodArgument);
  mdToken GetCurrentTypeRef(const TypeInfo* currentType, bool& isValueType);

  HRESULT ModifyLocalSig(ILRewriter* reWriter,
                         FunctionMethodArgument* methodReturnValue,
                         ULONG* callTargetStateIndex, ULONG* exceptionIndex,
                         ULONG* callTargetReturnIndex, ULONG* returnValueIndex,
                         mdToken* callTargetStateToken, mdToken* exceptionToken,
                         mdToken* callTargetReturnToken);

 public:
  CallTargetTokens(void* module_metadata_ptr) {
    this->module_metadata_ptr = module_metadata_ptr;
  }
  mdTypeRef GetObjectTypeRef();
  mdTypeRef GetExceptionTypeRef();
  mdAssemblyRef GetCorLibAssemblyRef();

  HRESULT ModifyLocalSigAndInitialize(
      void* rewriterWrapperPtr, FunctionInfo* functionInfo,
      ULONG* callTargetStateIndex, ULONG* exceptionIndex,
      ULONG* callTargetReturnIndex, ULONG* returnValueIndex,
      mdToken* callTargetStateToken, mdToken* exceptionToken,
      mdToken* callTargetReturnToken, ILInstr** firstInstruction);

  HRESULT WriteBeginMethodWithoutArguments(void* rewriterWrapperPtr,
                                           mdTypeRef integrationTypeRef,
                                           const TypeInfo* currentType,
                                           ILInstr** instruction);

  HRESULT WriteBeginMethodWithArguments(void* rewriterWrapperPtr,
                                        mdTypeRef integrationTypeRef,
                                        const TypeInfo* currentType,
                                        FunctionMethodArgument* arg1,
                                        ILInstr** instruction);

  HRESULT WriteBeginMethodWithArguments(void* rewriterWrapperPtr,
                                        mdTypeRef integrationTypeRef,
                                        const TypeInfo* currentType,
                                        FunctionMethodArgument* arg1,
                                        FunctionMethodArgument* arg2,
                                        ILInstr** instruction);

  HRESULT WriteBeginMethodWithArguments(void* rewriterWrapperPtr,
                                        mdTypeRef integrationTypeRef,
                                        const TypeInfo* currentType,
                                        FunctionMethodArgument* arg1,
                                        FunctionMethodArgument* arg2,
                                        FunctionMethodArgument* arg3,
                                        ILInstr** instruction);

  HRESULT WriteBeginMethodWithArguments(
      void* rewriterWrapperPtr, mdTypeRef integrationTypeRef,
      const TypeInfo* currentType, FunctionMethodArgument* arg1,
      FunctionMethodArgument* arg2, FunctionMethodArgument* arg3,
      FunctionMethodArgument* arg4, ILInstr** instruction);

  HRESULT WriteBeginMethodWithArguments(
      void* rewriterWrapperPtr, mdTypeRef integrationTypeRef,
      const TypeInfo* currentType, FunctionMethodArgument* arg1,
      FunctionMethodArgument* arg2, FunctionMethodArgument* arg3,
      FunctionMethodArgument* arg4, FunctionMethodArgument* arg5,
      ILInstr** instruction);

  HRESULT WriteBeginMethodWithArguments(
      void* rewriterWrapperPtr, mdTypeRef integrationTypeRef,
      const TypeInfo* currentType, FunctionMethodArgument* arg1,
      FunctionMethodArgument* arg2, FunctionMethodArgument* arg3,
      FunctionMethodArgument* arg4, FunctionMethodArgument* arg5,
      FunctionMethodArgument* arg6, ILInstr** instruction);

  HRESULT WriteBeginMethodWithArgumentsArray(void* rewriterWrapperPtr,
                                             mdTypeRef integrationTypeRef,
                                             const TypeInfo* currentType,
                                             ILInstr** instruction);

  HRESULT WriteEndVoidReturnMemberRef(void* rewriterWrapperPtr,
                                      mdTypeRef integrationTypeRef,
                                      const TypeInfo* currentType,
                                      ILInstr** instruction);

  HRESULT WriteEndReturnMemberRef(void* rewriterWrapperPtr,
                                  mdTypeRef integrationTypeRef,
                                  const TypeInfo* currentType,
                                  FunctionMethodArgument* returnArgument,
                                  ILInstr** instruction);

  HRESULT WriteLogException(void* rewriterWrapperPtr,
                            mdTypeRef integrationTypeRef,
                            const TypeInfo* currentType, ILInstr** instruction);

  HRESULT WriteCallTargetReturnGetReturnValue(
      void* rewriterWrapperPtr, mdTypeSpec callTargetReturnTypeSpec,
      ILInstr** instruction);
};

}  // namespace trace

#endif  // DD_CLR_PROFILER_CALLTARGET_TOKENS_H_