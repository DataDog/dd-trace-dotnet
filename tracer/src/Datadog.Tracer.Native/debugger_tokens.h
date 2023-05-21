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
enum ProbeType {NonAsyncMethodProbe, AsyncMethodProbe, NonAsyncLineProbe, AsyncLineProbe, AsyncMethodSpanProbe, NonAsyncMethodSpanProbe};

/**
 * DEBUGGER CALLTARGET CONSTANTS
 **/

static const WSTRING managed_profiler_debugger_beginmethod_startmarker_name = WStr("BeginMethod_StartMarker");
static const WSTRING managed_profiler_debugger_beginmethod_endmarker_name = WStr("BeginMethod_EndMarker");
static const WSTRING managed_profiler_debugger_endmethod_startmarker_name = WStr("EndMethod_StartMarker");
static const WSTRING managed_profiler_debugger_endmethod_endmarker_name = WStr("EndMethod_EndMarker");
static const WSTRING managed_profiler_debugger_logexception_name = WStr("LogException");
static const WSTRING managed_profiler_debugger_logcall_name = WStr("LogCall");
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

// Async line probe
static const WSTRING managed_profiler_debugger_async_line_type = WStr("Datadog.Trace.Debugger.Instrumentation.AsyncLineDebuggerInvoker");
static const WSTRING managed_profiler_debugger_async_linestatetype = WStr("Datadog.Trace.Debugger.Instrumentation.AsyncLineDebuggerState");

// Async method probes
static const WSTRING managed_profiler_debugger_is_first_entry_field_name =WStr("<>dd_liveDebugger_isReEntryToMoveNext");
static const WSTRING managed_profiler_debugger_begin_async_method_name = WStr("BeginMethod");
static const WSTRING managed_profiler_debugger_async_method_invoker_type = WStr("Datadog.Trace.Debugger.Instrumentation.AsyncMethodDebuggerInvoker");
static const WSTRING managed_profiler_debugger_async_method_state_type = WStr("Datadog.Trace.Debugger.Instrumentation.AsyncDebuggerState");

// Span probe
static const WSTRING managed_profiler_debugger_begin_span_name = WStr("BeginSpan");
static const WSTRING managed_profiler_debugger_end_span_name = WStr("EndSpan");
static const WSTRING managed_profiler_debugger_span_invoker_type = WStr("Datadog.Trace.Debugger.Instrumentation.SpanDebuggerInvoker");
static const WSTRING managed_profiler_debugger_span_state_type = WStr("Datadog.Trace.Debugger.Instrumentation.SpanDebuggerState");

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
    mdMemberRef methodLogCallRef = mdMemberRefNil;

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
    mdMemberRef asyncMethodLogCallRef = mdMemberRefNil;

    // Line probe members:
    mdMemberRef beginLineRef = mdMemberRefNil;
    mdMemberRef endLineRef = mdMemberRefNil;
    mdMemberRef lineDebuggerStateTypeRef = mdTypeRefNil;
    mdMemberRef lineInvokerTypeRef = mdTypeRefNil;
    mdMemberRef lineLogExceptionRef = mdMemberRefNil;
    mdMemberRef lineLogArgRef = mdMemberRefNil;
    mdMemberRef lineLogLocalRef = mdMemberRefNil;

    // Async line probe members:
    mdMemberRef asyncBeginLineRef = mdMemberRefNil;
    mdMemberRef asyncEndLineRef = mdMemberRefNil;
    mdMemberRef asyncLineDebuggerStateTypeRef = mdTypeRefNil;
    mdMemberRef asyncLineInvokerTypeRef = mdTypeRefNil;
    mdMemberRef asyncLineLogExceptionRef = mdMemberRefNil;
    mdMemberRef asyncLineLogArgRef = mdMemberRefNil;
    mdMemberRef asyncLineLogLocalRef = mdMemberRefNil;

    // (non-async method) span probe members:
    mdMemberRef beginMethodSpanProbeRef = mdMemberRefNil;
    mdMemberRef endMethodSpanProbeRef = mdMemberRefNil;
    mdMemberRef methodSpanProbeLogExceptionRef = mdMemberRefNil;

    // (async method) span probe members:
    mdMemberRef asyncBeginMethodSpanProbeRef = mdMemberRefNil;
    mdMemberRef asyncEndMethodSpanProbeRef = mdMemberRefNil;
    mdMemberRef asyncMethodSpanProbeLogExceptionRef = mdMemberRefNil;

    // (async & non-async method) span probe members:
    mdTypeRef methodSpanProbeDebuggerStateTypeRef = mdTypeRefNil;
    mdTypeRef methodSpanProbeDebuggerInvokerTypeRef = mdTypeRefNil;

    HRESULT WriteLogArgOrLocal(void* rewriterWrapperPtr, const TypeSignature& argOrLocal, ILInstr** instruction,
                               bool isArg, ProbeType probeType);

    [[nodiscard]] mdTypeRef GetDebuggerInvoker(const ProbeType probeType) const
    {
        switch (probeType)
        {
            case NonAsyncMethodProbe:
                return callTargetTypeRef;
            case AsyncMethodProbe:
                return asyncMethodDebuggerInvokerTypeRef;
            case NonAsyncMethodSpanProbe:
                return methodSpanProbeDebuggerInvokerTypeRef;
            case AsyncMethodSpanProbe:
                return methodSpanProbeDebuggerInvokerTypeRef;
            case NonAsyncLineProbe:
                return lineInvokerTypeRef;
            case AsyncLineProbe:
                return asyncLineInvokerTypeRef;
        }
        return mdTypeRefNil;
    }

    [[nodiscard]] mdTypeRef GetDebuggerState(const ProbeType probeType) const
    {
        switch (probeType)
        {
            case NonAsyncMethodProbe:
                return callTargetStateTypeRef;
            case AsyncMethodProbe:
                return asyncMethodDebuggerStateTypeRef;
            case NonAsyncMethodSpanProbe:
                return methodSpanProbeDebuggerStateTypeRef;
            case AsyncMethodSpanProbe:
                return asyncMethodDebuggerStateTypeRef;
            case NonAsyncLineProbe:
                return lineDebuggerStateTypeRef;
            case AsyncLineProbe:
                return asyncLineDebuggerStateTypeRef;
        }
        return mdTypeRefNil;
    }

    mdMemberRef GetBeginMethodStartMarker(ProbeType probeType) const
    {
        switch (probeType)
        {
            case NonAsyncMethodProbe:
                return beginMethodStartMarkerRef;
            case AsyncMethodProbe:
                return beginAsyncMethodStartMarkerRef;
            case NonAsyncLineProbe:
                return beginLineRef;
            case AsyncLineProbe:
                return asyncBeginLineRef;
            case NonAsyncMethodSpanProbe:
                return beginMethodSpanProbeRef;
            case AsyncMethodSpanProbe:
                return asyncBeginMethodSpanProbeRef;
        }
        return mdTypeRefNil;
    }

    void SetBeginMethodStartMarker(const ProbeType probeType, const mdMemberRef beginMethodMemberRef)
    {
        switch (probeType)
        {
            case NonAsyncMethodProbe:
                beginMethodStartMarkerRef = beginMethodMemberRef;
                break;
            case AsyncMethodProbe:
                beginAsyncMethodStartMarkerRef = beginMethodMemberRef;
                break;
            case NonAsyncLineProbe:
                beginLineRef = beginMethodMemberRef;
                break;
            case AsyncLineProbe:
                asyncBeginLineRef = beginMethodMemberRef;
                break;
            case NonAsyncMethodSpanProbe:
                beginMethodSpanProbeRef = beginMethodMemberRef;
                break;
            case AsyncMethodSpanProbe:
                asyncBeginMethodSpanProbeRef = beginMethodMemberRef;
                break;
        }
    }

    [[nodiscard]] std::tuple<mdMemberRef,WSTRING> GetBeginOrEndMethodEndMarker(ProbeType probeType, bool isBegin) const
    {
        switch (probeType)
        {
            case NonAsyncMethodProbe:
                return isBegin ? 
                std::make_tuple(beginMethodEndMarkerRef, managed_profiler_debugger_beginmethod_endmarker_name.data()) : 
                std::make_tuple(endMethodEndMarkerRef, managed_profiler_debugger_endmethod_endmarker_name.data());
            case AsyncMethodProbe:
                // async invoker has only EndMethodEndMarker
                return std::make_tuple(endAsyncMethodEndMarkerRef, managed_profiler_debugger_endmethod_endmarker_name.data());
            case NonAsyncMethodSpanProbe:
            case AsyncMethodSpanProbe:
            case NonAsyncLineProbe:
            case AsyncLineProbe:
                return std::make_tuple(mdMemberRefNil, nullptr);
        }
        return std::make_tuple(mdMemberRefNil, nullptr);
    }

    void SetBeginOrEndMethodEndMarker(ProbeType probeType, bool isBegin, mdMemberRef endMethod)
    {
        switch (probeType)
        {
            case NonAsyncMethodProbe:
                (isBegin ? beginMethodEndMarkerRef : endMethodEndMarkerRef) = endMethod;
            break;
            case AsyncMethodProbe:
                // async invoker has only EndMethodEndMarker
               endAsyncMethodEndMarkerRef = endMethod;
            break;
            case NonAsyncMethodSpanProbe:
            case AsyncMethodSpanProbe:
            case NonAsyncLineProbe:
            case AsyncLineProbe:
                return;
        }
    }
    
    mdMemberRef GetEndMethodStartMarker(ProbeType probeType, bool isVoid) const
    {
        switch (probeType)
        {
            case NonAsyncMethodProbe:
                return isVoid ? endVoidMethodStartMarkerRef : endNonVoidMethodEndMarkerRef;
            case AsyncMethodProbe:
                return isVoid ? endVoidAsyncMethodStartMarkerRef : endNonVoidAsyncMethodEndMarkerRef;
            case NonAsyncMethodSpanProbe:
                return endMethodSpanProbeRef;
            case AsyncMethodSpanProbe:
                return asyncEndMethodSpanProbeRef;
            case NonAsyncLineProbe:
                return endLineRef;
            case AsyncLineProbe:
                return asyncEndLineRef;
        }
        return mdTypeRefNil;
    }

    void SetEndMethodStartMarker(const ProbeType probeType, bool isVoid, const mdMemberRef endMethodMemberRef)
    {
        switch (probeType)
        {
            case NonAsyncMethodProbe:
                (isVoid ? endVoidMethodStartMarkerRef : endNonVoidMethodEndMarkerRef) = endMethodMemberRef;
            break;
            case AsyncMethodProbe:
                (isVoid ? endVoidAsyncMethodStartMarkerRef : endNonVoidAsyncMethodEndMarkerRef) = endMethodMemberRef;
            break;
            case NonAsyncMethodSpanProbe:
                endMethodSpanProbeRef = endMethodMemberRef;
                break;
            case AsyncMethodSpanProbe:
                asyncEndMethodSpanProbeRef = endMethodMemberRef;
                break;
            case NonAsyncLineProbe:
                endLineRef = endMethodMemberRef;
                break;
            case AsyncLineProbe:
                asyncEndLineRef = endMethodMemberRef;
                return;
        }
    }

    [[nodiscard]] mdMemberRef GetLogExceptionMemberRef(const ProbeType probeType) const
    {
        switch (probeType)
        {
            case NonAsyncMethodProbe:
                return methodLogExceptionRef;
            case AsyncMethodProbe:
                return asyncMethodLogExceptionRef;
            case NonAsyncMethodSpanProbe:
                return methodSpanProbeLogExceptionRef;
            case AsyncMethodSpanProbe:
                return asyncMethodSpanProbeLogExceptionRef;
            case NonAsyncLineProbe:
                return lineLogExceptionRef;
            case AsyncLineProbe:
                return asyncLineLogExceptionRef;
        }
        return mdTypeRefNil;
    }

    void SetLogExceptionMemberRef(const ProbeType probeType, const mdMemberRef logExceptionMemberRef)
    {
        switch (probeType)
        {
            case NonAsyncMethodProbe:
                methodLogExceptionRef = logExceptionMemberRef;
            break;
            case AsyncMethodProbe:
                asyncMethodLogExceptionRef = logExceptionMemberRef;
            break;
            case NonAsyncMethodSpanProbe:
                methodSpanProbeLogExceptionRef = logExceptionMemberRef;
                break;
            case AsyncMethodSpanProbe:
                asyncMethodSpanProbeLogExceptionRef = logExceptionMemberRef;
                break;
            case NonAsyncLineProbe:
                lineLogExceptionRef = logExceptionMemberRef;
            break;
            case AsyncLineProbe:
                asyncLineLogExceptionRef = logExceptionMemberRef;
            break;
        }
    }

    [[nodiscard]] mdMemberRef GetLogArgMemberRef(const ProbeType probeType) const
    {
        switch (probeType)
        {
            case NonAsyncMethodProbe:
                return methodLogArgRef;
            case AsyncMethodProbe:
                return asyncMethodLogArgRef;
            case NonAsyncMethodSpanProbe:
            case AsyncMethodSpanProbe:
                return mdTypeRefNil;
            case NonAsyncLineProbe:
                return lineLogArgRef;
            case AsyncLineProbe:
                return asyncLineLogArgRef;
        }
        return mdTypeRefNil;
    }

    void SetLogArgMemberRef(const ProbeType probeType, const mdMemberRef logArgMemberRef)
    {
        switch (probeType)
        {
            case NonAsyncMethodProbe:
                methodLogArgRef = logArgMemberRef;
            break;
            case AsyncMethodProbe:
                asyncMethodLogArgRef = logArgMemberRef;
            break;
            case NonAsyncMethodSpanProbe:
            case AsyncMethodSpanProbe:
                break;
            case NonAsyncLineProbe:
                lineLogArgRef = logArgMemberRef;
            break;
            case AsyncLineProbe:
                asyncLineLogArgRef = logArgMemberRef;
            break;
        }
    }

    [[nodiscard]] mdMemberRef GetLogCallMemberRef(const ProbeType probeType) const
    {
        switch (probeType)
        {
            case NonAsyncMethodProbe:
            return methodLogCallRef;
            case AsyncMethodProbe:
            return asyncMethodLogCallRef;
            case NonAsyncMethodSpanProbe:
            case AsyncMethodSpanProbe:
            return mdTypeRefNil;
            case NonAsyncLineProbe: // TODO
            return mdTypeRefNil;
            case AsyncLineProbe: // TODO
            return mdTypeRefNil;
        }
        return mdTypeRefNil;
    }

    void SetLogCallMemberRef(const ProbeType probeType, const mdMemberRef logArgMemberRef)
    {
        switch (probeType)
        {
            case NonAsyncMethodProbe:
            methodLogCallRef = logArgMemberRef;
            break;
            case AsyncMethodProbe:
            asyncMethodLogCallRef = logArgMemberRef;
            break;
            case NonAsyncMethodSpanProbe:
            case AsyncMethodSpanProbe:
            break;
            case NonAsyncLineProbe: // TODO
            break;
            case AsyncLineProbe: // TDOO
            break;
        }
    }

    [[nodiscard]] mdMemberRef GetLogLocalMemberRef(const ProbeType probeType) const
    {
        switch (probeType)
        {
            case NonAsyncMethodProbe:
                return methodLogLocalRef;
            case AsyncMethodProbe:
                return asyncMethodLogLocalRef;
            case NonAsyncMethodSpanProbe:
            case AsyncMethodSpanProbe:
                return mdTypeRefNil;
            case NonAsyncLineProbe:
                return lineLogLocalRef;
            case AsyncLineProbe:
                return asyncLineLogLocalRef;
        }
        return mdTypeRefNil;
    }

    void SetLogLocalMemberRef(const ProbeType probeType, const mdMemberRef logLocalMemberRef)
    {
        switch (probeType)
        {
            case NonAsyncMethodProbe:
                methodLogLocalRef = logLocalMemberRef;
            break;
            case AsyncMethodProbe:
                asyncMethodLogLocalRef = logLocalMemberRef;
            break;
            case NonAsyncMethodSpanProbe:
            case AsyncMethodSpanProbe:
                break;
            case NonAsyncLineProbe:
                lineLogLocalRef = logLocalMemberRef;
            break;
            case AsyncLineProbe:
                asyncLineLogLocalRef = logLocalMemberRef;
                break;
        }
    }

    [[nodiscard]] std::tuple<mdTypeRef,mdTypeRef> GetDebuggerInvokerAndState(ProbeType probeType) const
    {
        return {GetDebuggerInvoker(probeType), GetDebuggerState(probeType)};
    }
    
    HRESULT CreateEndMethodStartMarkerRefSignature(ProbeType probeType, mdMemberRef& endMethodRef, mdTypeRef returnTypeRef, bool isVoid);
    HRESULT CreateBeginMethodStartMarkerRefSignature(ProbeType probeType, mdMemberRef& beginMethodRef);

protected:
    HRESULT EnsureBaseCalltargetTokens() override;

    const WSTRING& GetCallTargetType() override;
    const WSTRING& GetCallTargetStateType() override;
    const WSTRING& GetCallTargetReturnType() override;
    const WSTRING& GetCallTargetReturnGenericType() override;

    void AddAdditionalLocals(COR_SIGNATURE (&signatureBuffer)[500], ULONG& signatureOffset, ULONG& signatureSize, bool isAsyncMethod) override;
    
public:
    DebuggerTokens(ModuleMetadata* module_metadata_ptr);

    int GetAdditionalLocalsCount() override;
    HRESULT WriteBeginMethod_StartMarker(void* rewriterWrapperPtr, const TypeInfo* currentType, ILInstr** instruction, ProbeType probeType);

    HRESULT WriteEndVoidReturnMemberRef(void* rewriterWrapperPtr, const TypeInfo* currentType, ILInstr** instruction, ProbeType probeType);
    HRESULT WriteEndReturnMemberRef(void* rewriterWrapperPtr, const TypeInfo* currentType, TypeSignature* returnArgument, ILInstr** instruction, ProbeType probeType);

    HRESULT WriteLogException(void* rewriterWrapperPtr, ProbeType probeType);

    HRESULT WriteLogArg(void* rewriterWrapperPtr, const TypeSignature& argument, ILInstr** instruction, ProbeType probeType);

    HRESULT WriteLogLocal(void* rewriterWrapperPtr, const TypeSignature& local, ILInstr** instruction, ProbeType probeType);

    HRESULT WriteBeginOrEndMethod_EndMarker(void* rewriterWrapperPtr, bool isBeginMethod, ILInstr** instruction, ProbeType probeType);
    HRESULT WriteBeginMethod_EndMarker(void* rewriterWrapperPtr, ILInstr** instruction, ProbeType probeType);
    HRESULT WriteEndMethod_EndMarker(void* rewriterWrapperPtr, ILInstr** instruction, ProbeType probeType);

    HRESULT WriteBeginLine(void* rewriterWrapperPtr, const TypeInfo* currentType, ILInstr** instruction, ProbeType probeType);
    HRESULT WriteEndLine(void* rewriterWrapperPtr, ILInstr** instruction, ProbeType probeType);

    HRESULT WriteBeginSpan(void* rewriterWrapperPtr, const TypeInfo* currentType, ILInstr** instruction, bool isAsyncMethod);
    HRESULT WriteEndSpan(void* rewriterWrapperPtr, ILInstr** instruction, bool isAsyncMethod);

    HRESULT WriteLogCall(void* rewriterWrapperPtr, const TypeSignature& argOrLocal, ILInstr** instruction, ProbeType probeType);

    HRESULT GetIsFirstEntryToMoveNextFieldToken(mdToken type, mdFieldDef& token);
};

} // namespace debugger

#endif // DD_CLR_PROFILER_DEBUGGER_TOKENS_H_