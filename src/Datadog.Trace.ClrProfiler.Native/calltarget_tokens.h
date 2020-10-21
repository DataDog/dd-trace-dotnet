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
#include "string.h" // NOLINT

namespace trace {

class CallTargetTokens {
 private:
  void* module_metadata_ptr = nullptr;

  // CallTarget constants
  WSTRING managed_profiler_calltarget_type =
      "Datadog.Trace.ClrProfiler.CallTarget.CallTargetInvoker"_W;
  WSTRING managed_profiler_calltarget_beginmethod_name = "BeginMethod"_W;
  WSTRING managed_profiler_calltarget_endmethod_name = "EndMethod"_W;
  WSTRING managed_profiler_calltarget_logexception_name = "LogException"_W;
  WSTRING managed_profiler_calltarget_getdefaultvalue_name =
      "GetDefaultValue"_W;

  WSTRING managed_profiler_calltarget_statetype =
      "Datadog.Trace.ClrProfiler.CallTarget.CallTargetState"_W;
  WSTRING managed_profiler_calltarget_statetype_getdefault_name =
      "GetDefault"_W;

  WSTRING managed_profiler_calltarget_returntype =
      "Datadog.Trace.ClrProfiler.CallTarget.CallTargetReturn"_W;
  WSTRING managed_profiler_calltarget_returntype_getdefault_name =
      "GetDefault"_W;

  WSTRING managed_profiler_calltarget_returntype_generics =
      "Datadog.Trace.ClrProfiler.CallTarget.CallTargetReturn`1"_W;
  WSTRING managed_profiler_calltarget_returntype_getreturnvalue_name =
      "GetReturnValue"_W;

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

  std::mutex mdTypeSpecMap_lock;
  std::unordered_map<FunctionMethodArgument*, mdTypeSpec> mdTypeSpecMap;

  mdMemberRef beginArrayMemberRef = mdMemberRefNil;
  mdMemberRef beginP0MemberRef = mdMemberRefNil;
  mdMemberRef beginP1MemberRef = mdMemberRefNil;
  mdMemberRef beginP2MemberRef = mdMemberRefNil;
  mdMemberRef beginP3MemberRef = mdMemberRefNil;
  mdMemberRef beginP4MemberRef = mdMemberRefNil;
  mdMemberRef beginP5MemberRef = mdMemberRefNil;
  mdMemberRef beginP6MemberRef = mdMemberRefNil;

  mdMemberRef endReturnMemberRef = mdMemberRefNil;
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

 public:
  CallTargetTokens(void* module_metadata_ptr) {
    this->module_metadata_ptr = module_metadata_ptr;
  }

  mdMethodSpec GetBeginMethodWithArgumentsArrayMemberRef(
      mdTypeRef integrationTypeRef, mdTypeRef currentTypeRef);
  mdMethodSpec GetBeginMethodWithoutArgumentsMemberRef(
      mdTypeRef integrationTypeRef, mdTypeRef currentTypeRef);
  mdMethodSpec GetBeginMethodWith1ArgumentsMemberRef(
      mdTypeRef integrationTypeRef, mdTypeRef currentTypeRef,
      mdTypeRef arg1TypeRef);
  mdMethodSpec GetBeginMethodWith2ArgumentsMemberRef(
      mdTypeRef integrationTypeRef, mdTypeRef currentTypeRef,
      mdTypeRef arg1TypeRef, mdTypeRef arg2TypeRef);
  mdMethodSpec GetBeginMethodWith3ArgumentsMemberRef(
      mdTypeRef integrationTypeRef, mdTypeRef currentTypeRef,
      mdTypeRef arg1TypeRef, mdTypeRef arg2TypeRef, mdTypeRef arg3TypeRef);
  mdMethodSpec GetBeginMethodWith4ArgumentsMemberRef(
      mdTypeRef integrationTypeRef, mdTypeRef currentTypeRef,
      mdTypeRef arg1TypeRef, mdTypeRef arg2TypeRef, mdTypeRef arg3TypeRef,
      mdTypeRef arg4TypeRef);
  mdMethodSpec GetBeginMethodWith5ArgumentsMemberRef(
      mdTypeRef integrationTypeRef, mdTypeRef currentTypeRef,
      mdTypeRef arg1TypeRef, mdTypeRef arg2TypeRef, mdTypeRef arg3TypeRef,
      mdTypeRef arg4TypeRef, mdTypeRef arg5TypeRef);
  mdMethodSpec GetBeginMethodWith6ArgumentsMemberRef(
      mdTypeRef integrationTypeRef, mdTypeRef currentTypeRef,
      mdTypeRef arg1TypeRef, mdTypeRef arg2TypeRef, mdTypeRef arg3TypeRef,
      mdTypeRef arg4TypeRef, mdTypeRef arg5TypeRef, mdTypeRef arg6TypeRef);

  mdMethodSpec GetEndVoidReturnMemberRef(mdTypeRef integrationTypeRef,
                                         mdTypeRef currentTypeRef);
  mdMethodSpec GetEndReturnMemberRef(mdTypeRef integrationTypeRef,
                                     mdTypeRef currentTypeRef,
                                     mdTypeRef returnTypeRef);

  mdMethodSpec GetLogExceptionMemberRef(mdTypeRef integrationTypeRef,
                                        mdTypeRef currentTypeRef);

  HRESULT ModifyLocalSig(ILRewriter* reWriter,
                         FunctionMethodArgument* methodReturnValue,
                         ULONG* callTargetStateIndex, ULONG* exceptionIndex,
                         ULONG* callTargetReturnIndex, ULONG* returnValueIndex,
                         mdToken* callTargetStateToken, mdToken* exceptionToken,
                         mdToken* callTargetReturnToken);

  mdMemberRef GetCallTargetStateDefaultMemberRef();
  mdMemberRef GetCallTargetReturnVoidDefaultMemberRef();
  mdMemberRef GetCallTargetReturnValueDefaultMemberRef(
      mdTypeSpec callTargetReturnMemberSpec);
  mdMethodSpec GetCallTargetDefaultValueMethodSpec(
      FunctionMethodArgument* methodReturnValue);
};

}  // namespace trace

#endif  // DD_CLR_PROFILER_CALLTARGET_TOKENS_H_