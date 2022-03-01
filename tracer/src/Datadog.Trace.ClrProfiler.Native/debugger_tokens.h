#ifndef DD_CLR_PROFILER_DEBUGGER_TOKENS_H_
#define DD_CLR_PROFILER_DEBUGGER_TOKENS_H_

#include "calltarget_tokens.h"

#include <corhlpr.h>

#include <mutex>
#include <unordered_map>
#include <unordered_set>

#include "clr_helpers.h"
#include "../../../shared/src/native-src/com_ptr.h"
#include "il_rewriter.h"
#include "integration.h"
#include "../../../shared/src/native-src/string.h" // NOLINT

using namespace shared;
using namespace trace;

namespace debugger
{

/// <summary>
/// Class to control all the token references of the module where the Debugger will be called.
/// Also provides useful helpers for the rewriting process
/// </summary>
class DebuggerTokens : public CallTargetTokens
{
private:
    mdMemberRef beginMethodStartMarkerRef = mdMemberRefNil;
    mdMemberRef beginMethodEndMarkerRef = mdMemberRefNil;
    mdMemberRef endMethodEndMarkerRef = mdMemberRefNil;
    mdMemberRef logArgRef = mdMemberRefNil;
    mdMemberRef logLocalRef = mdMemberRefNil;
    mdMemberRef endVoidMemberRef = mdMemberRefNil;
    mdMemberRef logExceptionRef = mdMemberRefNil;

    HRESULT WriteLogArgOrLocal(void* rewriterWrapperPtr, const TypeSignature& argOrLocal, ILInstr** instruction,
                               bool isArg);

protected:
    const WSTRING& GetCallTargetType() override;
    const WSTRING& GetCallTargetStateType() override;
    const WSTRING& GetCallTargetReturnType() override;
    const WSTRING& GetCallTargetReturnGenericType() override;

public:
    DebuggerTokens(ModuleMetadata* module_metadata_ptr);

    HRESULT WriteBeginMethod_StartMarker(void* rewriterWrapperPtr, const TypeInfo* currentType, ILInstr** instruction);

    HRESULT WriteEndVoidReturnMemberRef(void* rewriterWrapperPtr, const TypeInfo* currentType, ILInstr** instruction);

    HRESULT WriteEndReturnMemberRef(void* rewriterWrapperPtr, const TypeInfo* currentType,
                                    TypeSignature* returnArgument, ILInstr** instruction);

    HRESULT WriteLogException(void* rewriterWrapperPtr, const TypeInfo* currentType);

    HRESULT WriteLogArg(void* rewriterWrapperPtr, const TypeSignature& argument, ILInstr** instruction);

    HRESULT WriteLogLocal(void* rewriterWrapperPtr, const TypeSignature& local, ILInstr** instruction);

    HRESULT WriteBeginOrEndMethod_EndMarker(void* rewriterWrapperPtr, bool isBeginMethod, ILInstr** instruction);
    HRESULT WriteBeginMethod_EndMarker(void* rewriterWrapperPtr, ILInstr** instruction);
    HRESULT WriteEndMethod_EndMarker(void* rewriterWrapperPtr, ILInstr** instruction);
};

} // namespace debugger

#endif // DD_CLR_PROFILER_DEBUGGER_TOKENS_H_