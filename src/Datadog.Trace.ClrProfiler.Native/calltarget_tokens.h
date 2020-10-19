#ifndef DD_CLR_PROFILER_CALLTARGET_TOKENS_H_
#define DD_CLR_PROFILER_CALLTARGET_TOKENS_H_

#include <corhlpr.h>

#include <unordered_map>
#include <unordered_set>

#include "clr_helpers.h"
#include "com_ptr.h"
#include "integration.h"
#include "string.h"

namespace trace {

class CallTargetTokens {
 private:
  void* module_metadata_ptr = nullptr;

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

  mdMemberRef beginSlowMemberRef = mdMemberRefNil;
  mdMemberRef beginP0MemberRef = mdMemberRefNil;
  mdMemberRef beginP1MemberRef = mdMemberRefNil;
  mdMemberRef beginP2MemberRef = mdMemberRefNil;
  mdMemberRef beginP3MemberRef = mdMemberRefNil;
  mdMemberRef beginP4MemberRef = mdMemberRefNil;
  mdMemberRef beginP5MemberRef = mdMemberRefNil;

  mdMemberRef endReturnMemberRef = mdMemberRefNil;
  mdMemberRef endVoidMemberRef = mdMemberRefNil;

  mdMemberRef logExceptionRef = mdMemberRefNil;
  mdMemberRef getDefaultMemberRef = mdMemberRefNil;

  inline ModuleMetadata* GetMetadata() {
    return (ModuleMetadata*)module_metadata_ptr;
  }
  HRESULT EnsureCorLibTokens();
  HRESULT EnsureBaseCalltargetTokens();

  public:
  CallTargetTokens(void* module_metadata_ptr) {
     this->module_metadata_ptr = module_metadata_ptr;
  }
};

}

#endif  // DD_CLR_PROFILER_CALLTARGET_TOKENS_H_