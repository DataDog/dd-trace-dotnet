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
enum ProbeType {MethodProbeInNonAsyncMethod, MethodProbeInAsyncMethod, LineProbe};

/**
 * DEBUGGER CALLTARGET CONSTANTS
 **/

static const WSTRING managed_profiler_debugger_beginmethod_startmarker_name = WStr("BeginMethod_StartMarker");
static const WSTRING managed_profiler_debugger_beginmethod_endmarker_name = WStr("BeginMethod_EndMarker");
static const WSTRING managed_profiler_debugger_endmethod_startmarker_name = WStr("EndMethod_StartMarker");
static const WSTRING managed_profiler_debugger_endmethod_endmarker_name = WStr("EndMethod_EndMarker");
static const WSTRING managed_profiler_debugger_logexception_name = WStr("LogException");
static const WSTRING managed_profiler_debugger_logarg_name = WStr("LogArg");
static const WSTRING managed_profiler_debugger_loglocal_name = WStr("LogLocal");
static const WSTRING managed_profiler_debugger_method_type = WStr("Datadog.Trace.Debugger.Instrumentation.MethodDebuggerInvoker");
static const WSTRING managed_profiler_debugger_methodstatetype = WStr("Datadog.Trace.Debugger.Instrumentation.MethodDebuggerState");
static const WSTRING managed_profiler_debugger_returntype = WStr("Datadog.Trace.Debugger.Instrumentation.DebuggerReturn");
static const WSTRING managed_profiler_debugger_returntype_generics = WStr("Datadog.Trace.Debugger.Instrumentation.DebuggerReturn`1");

// Line Probe Methods & Types
static const WSTRING managed_profiler_debugger_line_type = WStr("Datadog.Trace.Debugger.Instrumentation.LineDebuggerInvoker");
static const WSTRING managed_profiler_debugger_linestatetype = WStr("Datadog.Trace.Debugger.Instrumentation.LineDebuggerState");
static const WSTRING managed_profiler_debugger_beginline_name = WStr("BeginLine");
static const WSTRING managed_profiler_debugger_endline_name = WStr("EndLine");

// Async method probes
static const WSTRING managed_profiler_debugger_is_first_entry_field_name =WStr("<>dd_liveDebugger_isReEntryToMoveNext");
static const WSTRING managed_profiler_debugger_begin_async_method_name = WStr("BeginMethod");
static const WSTRING managed_profiler_debugger_async_method_invoker_type = WStr("Datadog.Trace.Debugger.Instrumentation.AsyncMethodDebuggerInvoker");
static const WSTRING managed_profiler_debugger_async_method_state_type = WStr("Datadog.Trace.Debugger.Instrumentation.AsyncMethodDebuggerState");


/// <summary>
/// Class to control all the token references of the module where the Debugger will be called.
/// Also provides useful helpers for the rewriting process
/// </summary>
class DebuggerTokens : public CallTargetTokens
{
private:

    // Method probe members:
    mdMemberRef beginMethodStartMarkerRef = mdMemberRefNil;
    mdMemberRef beginMethodEndMarkerRef = mdMemberRefNil;
    mdMemberRef endVoidMethodStartMarkerRef = mdMemberRefNil;
    mdMemberRef endNonVoidMethodEndMarkerRef = mdMemberRefNil;
    mdMemberRef endMethodEndMarkerRef = mdMemberRefNil;
    mdMemberRef methodLogArgRef = mdMemberRefNil;
    mdMemberRef methodLogLocalRef = mdMemberRefNil;
    mdMemberRef methodLogExceptionRef = mdMemberRefNil;
    mdMemberRef beginLineRef = mdMemberRefNil;
    mdMemberRef endLineRef = mdMemberRefNil;

    // Async method probe members:
    mdTypeRef asyncMethodDebuggerStateTypeRef = mdTypeRefNil;
    mdTypeRef asyncMethodDebuggerInvokerTypeRef = mdTypeRefNil;
    mdMemberRef beginAsyncMethodStartMarkerRef = mdMemberRefNil;
    mdMemberRef endVoidAsyncMethodStartMarkerRef = mdMemberRefNil;
    mdMemberRef endNonVoidAsyncMethodEndMarkerRef = mdMemberRefNil;
    mdMemberRef endAsyncMethodEndMarkerRef = mdMemberRefNil;
    mdMemberRef asyncMethodLogExceptionRef = mdMemberRefNil;
    mdMemberRef asyncMethodLogArgRef = mdMemberRefNil;
    mdMemberRef asyncMethodLogLocalRef = mdMemberRefNil;

    // Line probe members:
    mdMemberRef lineDebuggerStateTypeRef = mdTypeRefNil;
    mdMemberRef lineInvokerTypeRef = mdTypeRefNil;
    mdMemberRef lineLogExceptionRef = mdMemberRefNil;
    mdMemberRef lineLogArgRef = mdMemberRefNil;
    mdMemberRef lineLogLocalRef = mdMemberRefNil;

    HRESULT WriteLogArgOrLocal(void* rewriterWrapperPtr, const TypeSignature& argOrLocal, ILInstr** instruction,
                               bool isArg, ProbeType probeType);

    [[nodiscard]] mdTypeRef GetDebuggerInvoker(const ProbeType probeType) const
    {
        switch (probeType)
        {
            case MethodProbeInNonAsyncMethod:
                return callTargetTypeRef;
            case MethodProbeInAsyncMethod:
                return asyncMethodDebuggerInvokerTypeRef;
            case LineProbe:
                return lineInvokerTypeRef;
        }
        return mdTypeRefNil;
    }

    [[nodiscard]] mdTypeRef GetDebuggerState(const ProbeType probeType) const
    {
        switch (probeType)
        {
            case MethodProbeInNonAsyncMethod:
                return callTargetStateTypeRef;
            case MethodProbeInAsyncMethod:
                return asyncMethodDebuggerStateTypeRef;
            case LineProbe:
                return lineDebuggerStateTypeRef;
        }
        return mdTypeRefNil;
    }

    mdMemberRef GetBeginMethodStartMarker(ProbeType probeType) const
    {
        switch (probeType)
        {
            case MethodProbeInNonAsyncMethod:
                return beginMethodStartMarkerRef;
            case MethodProbeInAsyncMethod:
                return beginAsyncMethodStartMarkerRef;
            case LineProbe:
                return beginLineRef;
        }
        return mdTypeRefNil;
    }

    void SetBeginMethodStartMarker(const ProbeType probeType, const mdMemberRef beginMethodMemberRef)
    {
        switch (probeType)
        {
            case MethodProbeInNonAsyncMethod:
                beginMethodStartMarkerRef = beginMethodMemberRef;
            break;
            case MethodProbeInAsyncMethod:
                beginAsyncMethodStartMarkerRef = beginMethodMemberRef;
            break;
            case LineProbe:
                beginLineRef = beginMethodMemberRef;
            break;
        }
    }

    [[nodiscard]] std::tuple<mdMemberRef,WSTRING> GetBeginOrEndMethodEndMarker(ProbeType probeType, bool isBegin) const
    {
        switch (probeType)
        {
            case MethodProbeInNonAsyncMethod:
                return isBegin ? 
                std::make_tuple(beginMethodEndMarkerRef, managed_profiler_debugger_beginmethod_endmarker_name.data()) : 
                std::make_tuple(endMethodEndMarkerRef, managed_profiler_debugger_endmethod_endmarker_name.data());
            case LineProbe:
                return std::make_tuple(mdMemberRefNil, nullptr);
            case MethodProbeInAsyncMethod:
                // async invoker has only EndMethodEndMarker
                return std::make_tuple(endAsyncMethodEndMarkerRef, managed_profiler_debugger_endmethod_endmarker_name.data());
        }
        return std::make_tuple(mdMemberRefNil, nullptr);
    }

    void SetBeginOrEndMethodEndMarker(ProbeType probeType, bool isBegin, mdMemberRef endMethod)
    {
        switch (probeType)
        {
            case MethodProbeInNonAsyncMethod:
                (isBegin ? beginMethodEndMarkerRef : endMethodEndMarkerRef) = endMethod;
            case LineProbe:
                return;
            case MethodProbeInAsyncMethod:
                // async invoker has only EndMethodEndMarker
               endAsyncMethodEndMarkerRef = endMethod;
        }
    }
    
    mdMemberRef GetEndMethodStartMarker(ProbeType probeType, bool isVoid) const
    {
        switch (probeType)
        {
            case MethodProbeInNonAsyncMethod:
                return isVoid ? endVoidMethodStartMarkerRef : endNonVoidMethodEndMarkerRef;
            case LineProbe:
                return mdTypeRefNil;
            case MethodProbeInAsyncMethod:
                return isVoid ? endVoidAsyncMethodStartMarkerRef : endNonVoidAsyncMethodEndMarkerRef;
        }
        return mdTypeRefNil;
    }

    void SetEndMethodStartMarker(const ProbeType probeType, bool isVoid, const mdMemberRef endMethodMemberRef)
    {
        switch (probeType)
        {
            case MethodProbeInNonAsyncMethod:
                (isVoid ? endVoidMethodStartMarkerRef : endNonVoidMethodEndMarkerRef) = endMethodMemberRef;
            break;
            case LineProbe:
                return;
            case MethodProbeInAsyncMethod:
                (isVoid ? endVoidAsyncMethodStartMarkerRef : endNonVoidAsyncMethodEndMarkerRef) = endMethodMemberRef;
            break;
        }
    }

    [[nodiscard]] mdMemberRef GetLogExceptionMemberRef(const ProbeType probeType) const
    {
        switch (probeType)
        {
            case MethodProbeInNonAsyncMethod:
                return methodLogExceptionRef;
            case MethodProbeInAsyncMethod:
                return asyncMethodLogExceptionRef;
            case LineProbe:
                return lineLogExceptionRef;
        }
        return mdTypeRefNil;
    }

    void SetLogExceptionMemberRef(const ProbeType probeType, const mdMemberRef logExceptionMemberRef)
    {
        switch (probeType)
        {
            case MethodProbeInNonAsyncMethod:
                methodLogExceptionRef = logExceptionMemberRef;
            break;
            case MethodProbeInAsyncMethod:
                asyncMethodLogExceptionRef = logExceptionMemberRef;
            break;
            case LineProbe:
                lineLogExceptionRef = logExceptionMemberRef;
            break;
        }
    }

    [[nodiscard]] mdMemberRef GetLogArgMemberRef(const ProbeType probeType) const
    {
        switch (probeType)
        {
            case MethodProbeInNonAsyncMethod:
                return methodLogArgRef;
            case MethodProbeInAsyncMethod:
                return asyncMethodLogArgRef;
            case LineProbe:
                return lineLogArgRef;
        }
        return mdTypeRefNil;
    }

    void SetLogArgMemberRef(const ProbeType probeType, const mdMemberRef logArgMemberRef)
    {
        switch (probeType)
        {
            case MethodProbeInNonAsyncMethod:
                methodLogArgRef = logArgMemberRef;
            break;
            case MethodProbeInAsyncMethod:
                asyncMethodLogArgRef = logArgMemberRef;
            break;
            case LineProbe:
                lineLogArgRef = logArgMemberRef;
            break;
        }
    }

    [[nodiscard]] mdMemberRef GetLogLocalMemberRef(const ProbeType probeType) const
    {
        switch (probeType)
        {
            case MethodProbeInNonAsyncMethod:
                return methodLogLocalRef;
            case MethodProbeInAsyncMethod:
                return asyncMethodLogLocalRef;
            case LineProbe:
                return lineLogLocalRef;
        }
        return mdTypeRefNil;
    }

    void SetLogLocalMemberRef(const ProbeType probeType, const mdMemberRef logLocalMemberRef)
    {
        switch (probeType)
        {
            case MethodProbeInNonAsyncMethod:
                methodLogLocalRef = logLocalMemberRef;
            break;
            case MethodProbeInAsyncMethod:
                asyncMethodLogLocalRef = logLocalMemberRef;
            break;
            case LineProbe:
                lineLogLocalRef = logLocalMemberRef;
            break;
        }
    }

    [[nodiscard]] std::tuple<mdTypeRef,mdTypeRef> GetDebuggerInvokerAndState(ProbeType probeType) const
    {
        return {GetDebuggerInvoker(probeType), GetDebuggerState(probeType)};
    }

protected:
    virtual HRESULT EnsureBaseCalltargetTokens() override;

    const WSTRING& GetCallTargetType() override;
    const WSTRING& GetCallTargetStateType() override;
    const WSTRING& GetCallTargetReturnType() override;
    const WSTRING& GetCallTargetReturnGenericType() override;

    int GetAdditionalLocalsCount() override;
    void AddAdditionalLocals(COR_SIGNATURE (&signatureBuffer)[500], ULONG& signatureOffset, ULONG& signatureSize) override;
    
public:
    DebuggerTokens(ModuleMetadata* module_metadata_ptr);

    HRESULT WriteBeginMethod_StartMarker(void* rewriterWrapperPtr, const TypeInfo* currentType, ILInstr** instruction, ProbeType probeType);
    HRESULT CreateEndMethodStartMarkerRefSignature(ProbeType probeType, mdMemberRef& endMethodRef, mdTypeRef returnTypeRef, bool isVoid);

    HRESULT WriteEndVoidReturnMemberRef(void* rewriterWrapperPtr, const TypeInfo* currentType, ILInstr** instruction, ProbeType probeType);

    HRESULT WriteEndReturnMemberRef(void* rewriterWrapperPtr, const TypeInfo* currentType,
                                    TypeSignature* returnArgument, ILInstr** instruction, ProbeType probeType);

    HRESULT WriteLogException(void* rewriterWrapperPtr, const TypeInfo* currentType, ProbeType probeType);

    HRESULT WriteLogArg(void* rewriterWrapperPtr, const TypeSignature& argument, ILInstr** instruction, ProbeType probeType);

    HRESULT WriteLogLocal(void* rewriterWrapperPtr, const TypeSignature& local, ILInstr** instruction, ProbeType probeType);

    HRESULT WriteBeginOrEndMethod_EndMarker(void* rewriterWrapperPtr, bool isBeginMethod, ILInstr** instruction, ProbeType probeType);
    HRESULT WriteBeginMethod_EndMarker(void* rewriterWrapperPtr, ILInstr** instruction, ProbeType probeType);
    HRESULT WriteEndMethod_EndMarker(void* rewriterWrapperPtr, ILInstr** instruction, ProbeType probeType);

    HRESULT WriteBeginLine(void* rewriterWrapperPtr, const TypeInfo* currentType, ILInstr** instruction);
    HRESULT WriteEndLine(void* rewriterWrapperPtr, ILInstr** instruction);

    HRESULT ModifyLocalSigForLineProbe(ILRewriter* reWriter, ULONG* callTargetStateIndex, mdToken* callTargetStateToken);
    HRESULT GetDebuggerLocals(void* rewriterWrapperPtr, ULONG* callTargetStateIndex, mdToken* callTargetStateToken, ULONG* asyncMethodStateIndex);
    HRESULT CreateBeginMethodStartMarkerRefSignature(ProbeType probeType, mdMemberRef& beginMethodRef);

    mdFieldDef GetIsFirstEntryToMoveNextFieldToken(mdToken type);
};

} // namespace debugger

#endif // DD_CLR_PROFILER_DEBUGGER_TOKENS_H_