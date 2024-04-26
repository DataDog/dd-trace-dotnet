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
enum ProbeType
{
    NonAsyncMethodSingleProbe,
    NonAsyncMethodMultiProbe,
    AsyncMethodProbe,
    NonAsyncLineProbe,
    AsyncLineProbe,
    AsyncMethodSpanProbe,
    NonAsyncMethodSpanProbe
};

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
static const WSTRING managed_profiler_debugger_should_update_probe_info_name = WStr("ShouldUpdateProbeInfo");
static const WSTRING managed_profiler_debugger_update_probe_info_name = WStr("UpdateProbeInfo");
static const WSTRING managed_profiler_debugger_rent_array_name = WStr("RentArray");
static const WSTRING managed_profiler_debugger_dispose_name = WStr("Dispose");

static const WSTRING managed_profiler_debugger_line_type = WStr("Datadog.Trace.Debugger.Instrumentation.LineDebuggerInvoker");
static const WSTRING managed_profiler_debugger_linestatetype = WStr("Datadog.Trace.Debugger.Instrumentation.LineDebuggerState");
static const WSTRING managed_profiler_debugger_beginline_name = WStr("BeginLine");
static const WSTRING managed_profiler_debugger_endline_name = WStr("EndLine");

// Async line probe
static const WSTRING managed_profiler_debugger_async_line_type = WStr("Datadog.Trace.Debugger.Instrumentation.AsyncLineDebuggerInvoker");
static const WSTRING managed_profiler_debugger_async_linestatetype = WStr("Datadog.Trace.Debugger.Instrumentation.AsyncLineDebuggerState");

// Async method probes
static const WSTRING managed_profiler_debugger_is_first_entry_field_name = WStr("<>dd_liveDebugger_isReEntryToMoveNext");
static const WSTRING managed_profiler_debugger_async_method_debugger_state_field_name = WStr("LogState"); // See `Datadog.Trace.Debugger.Instrumentation.AsyncDebuggerState.LogState`
static const WSTRING managed_profiler_debugger_begin_async_method_name = WStr("BeginMethod");
static const WSTRING managed_profiler_debugger_async_method_invoker_type = WStr("Datadog.Trace.Debugger.Instrumentation.AsyncMethodDebuggerInvoker");
static const WSTRING managed_profiler_debugger_async_method_state_type = WStr("Datadog.Trace.Debugger.Instrumentation.AsyncDebuggerState");

// Span probe
static const WSTRING managed_profiler_debugger_begin_span_name = WStr("BeginSpan");
static const WSTRING managed_profiler_debugger_end_span_name = WStr("EndSpan");
static const WSTRING managed_profiler_debugger_span_invoker_type = WStr("Datadog.Trace.Debugger.Instrumentation.SpanDebuggerInvoker");
static const WSTRING managed_profiler_debugger_span_state_type = WStr("Datadog.Trace.Debugger.Instrumentation.SpanDebuggerState");

// Instrumentation Allocator
static const WSTRING instrumentation_allocator_invoker_name = WStr("Datadog.Trace.Debugger.Instrumentation.InstrumentationAllocator");


/// <summary>
/// Class to control all the token references of the module where the Debugger will be called.
/// Also provides useful helpers for the rewriting process
/// </summary>
class DebuggerTokens : public CallTargetTokens
{
private:
    // Method probe members:
    mdMemberRef nonAsyncShouldUpdateProbeInfoRef = mdMemberRefNil;
    mdMemberRef nonAsyncUpdateProbeInfoRef = mdMemberRefNil;

    // InstrumentationAllocator
    mdTypeRef rentArrayTypeRef = mdTypeRefNil;
    mdMemberRef rentArrayRef = mdMemberRefNil;

    //  Single Probe
    mdMemberRef beginMethodStartMarkerSingleProbeRef = mdMemberRefNil;
    mdMemberRef beginMethodEndMarkerSingleProbeRef = mdMemberRefNil;
    mdMemberRef endMethodEndMarkerSingleProbeRef = mdMemberRefNil;
    mdMemberRef endVoidMethodStartMarkerSingleProbeRef = mdMemberRefNil;
    mdMemberRef endNonVoidMethodEndMarkerSingleProbeRef = mdMemberRefNil;
    mdMemberRef methodLogArgSingleProbeRef = mdMemberRefNil;
    mdMemberRef methodLogLocalSingleProbeRef = mdMemberRefNil;
    mdMemberRef methodLogExceptionSingleProbeRef = mdMemberRefNil;
    mdMemberRef methodDisposeSingleProbeRef = mdMemberRefNil;

    //  Multi Probe
    mdMemberRef beginMethodStartMarkerMultiProbeRef = mdMemberRefNil;
    mdMemberRef beginMethodEndMarkerMultiProbeRef = mdMemberRefNil;
    mdMemberRef endMethodEndMarkerMultiProbeRef = mdMemberRefNil;
    mdMemberRef endVoidMethodStartMarkerMultiProbeRef = mdMemberRefNil;
    mdMemberRef endNonVoidMethodEndMarkerMultiProbeRef = mdMemberRefNil;
    mdMemberRef methodLogArgMultiProbeRef = mdMemberRefNil;
    mdMemberRef methodLogLocalMultiProbeRef = mdMemberRefNil;
    mdMemberRef methodLogExceptionMultiProbeRef = mdMemberRefNil;
    mdMemberRef methodDisposeMultiProbeRef = mdMemberRefNil;

    // Async method probe members:
    mdTypeRef asyncMethodDebuggerInvokerTypeRef = mdTypeRefNil;
    mdMemberRef asyncShouldUpdateProbeInfoRef = mdMemberRefNil;
    mdMemberRef asyncUpdateProbeInfoRef = mdMemberRefNil;
    mdTypeRef asyncMethodDebuggerStateTypeRef = mdTypeRefNil;
    mdMemberRef beginAsyncMethodStartMarkerRef = mdMemberRefNil;
    mdMemberRef endVoidAsyncMethodStartMarkerRef = mdMemberRefNil;
    mdMemberRef endNonVoidAsyncMethodEndMarkerRef = mdMemberRefNil;
    mdMemberRef endAsyncMethodEndMarkerRef = mdMemberRefNil;
    mdMemberRef asyncMethodLogExceptionRef = mdMemberRefNil;
    mdMemberRef asyncMethodLogArgRef = mdMemberRefNil;
    mdMemberRef asyncMethodLogLocalRef = mdMemberRefNil;
    mdMemberRef asyncMethodDisposeRef = mdMemberRefNil;

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
            case NonAsyncMethodSingleProbe:
            case NonAsyncMethodMultiProbe:
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
            case NonAsyncMethodSingleProbe:
            case NonAsyncMethodMultiProbe:
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
            case NonAsyncMethodSingleProbe:
                return beginMethodStartMarkerSingleProbeRef;
            case NonAsyncMethodMultiProbe:
                return beginMethodStartMarkerMultiProbeRef;
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
            case NonAsyncMethodSingleProbe:
                beginMethodStartMarkerSingleProbeRef = beginMethodMemberRef;
                break;
            case NonAsyncMethodMultiProbe:
                beginMethodStartMarkerMultiProbeRef = beginMethodMemberRef;
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
            case NonAsyncMethodSingleProbe:
                return isBegin ? 
                std::make_tuple(beginMethodEndMarkerSingleProbeRef, managed_profiler_debugger_beginmethod_endmarker_name.data()) : 
                std::make_tuple(endMethodEndMarkerSingleProbeRef, managed_profiler_debugger_endmethod_endmarker_name.data());
            case NonAsyncMethodMultiProbe:
                return isBegin ? std::make_tuple(beginMethodEndMarkerMultiProbeRef,
                                                 managed_profiler_debugger_beginmethod_endmarker_name.data())
                               : std::make_tuple(endMethodEndMarkerMultiProbeRef,
                                                 managed_profiler_debugger_endmethod_endmarker_name.data());
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
            case NonAsyncMethodSingleProbe:
                (isBegin ? beginMethodEndMarkerSingleProbeRef : endMethodEndMarkerSingleProbeRef) = endMethod;
            break;
            case NonAsyncMethodMultiProbe:
                (isBegin ? beginMethodEndMarkerMultiProbeRef : endMethodEndMarkerMultiProbeRef) = endMethod;
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
            case NonAsyncMethodSingleProbe:
                return isVoid ? endVoidMethodStartMarkerSingleProbeRef : endNonVoidMethodEndMarkerSingleProbeRef;
            case NonAsyncMethodMultiProbe:
                return isVoid ? endVoidMethodStartMarkerMultiProbeRef : endNonVoidMethodEndMarkerMultiProbeRef;
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
            case NonAsyncMethodSingleProbe:
                (isVoid ? endVoidMethodStartMarkerSingleProbeRef : endNonVoidMethodEndMarkerSingleProbeRef) = endMethodMemberRef;
            break;
            case NonAsyncMethodMultiProbe:
                (isVoid ? endVoidMethodStartMarkerMultiProbeRef : endNonVoidMethodEndMarkerMultiProbeRef) =
                    endMethodMemberRef;
                break;
            case AsyncMethodProbe:
                (isVoid ? endVoidAsyncMethodStartMarkerRef : endNonVoidAsyncMethodEndMarkerRef) =
                    endMethodMemberRef;
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
            case NonAsyncMethodSingleProbe:
                return methodLogExceptionSingleProbeRef;
            case NonAsyncMethodMultiProbe:
                return methodLogExceptionMultiProbeRef;
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
            case NonAsyncMethodSingleProbe:
                methodLogExceptionSingleProbeRef = logExceptionMemberRef;
                break;
            case NonAsyncMethodMultiProbe:
                methodLogExceptionMultiProbeRef = logExceptionMemberRef;
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

    [[nodiscard]] mdMemberRef GetDisposeMemberRef(const ProbeType probeType) const
    {
        switch (probeType)
        {
            case NonAsyncMethodSingleProbe:
            return methodDisposeSingleProbeRef;
            case NonAsyncMethodMultiProbe:
            return methodDisposeMultiProbeRef;
            case AsyncMethodProbe:
            return asyncMethodDisposeRef;
            default:
            break;
        }
        return mdTypeRefNil;
    }

    void SetDisposeMemberRef(const ProbeType probeType, const mdMemberRef disposeMemberRef)
    {
        switch (probeType)
        {
            case NonAsyncMethodSingleProbe:
            methodDisposeSingleProbeRef = disposeMemberRef;
            break;
            case NonAsyncMethodMultiProbe:
            methodDisposeMultiProbeRef = disposeMemberRef;
            break;
            case AsyncMethodProbe:
            asyncMethodDisposeRef = disposeMemberRef;
            break;
            default:
            break;
        }
    }

    [[nodiscard]] mdMemberRef GetLogArgMemberRef(const ProbeType probeType) const
    {
        switch (probeType)
        {
            case NonAsyncMethodSingleProbe:
                return methodLogArgSingleProbeRef;
            case NonAsyncMethodMultiProbe:
            return methodLogArgMultiProbeRef;
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
            case NonAsyncMethodSingleProbe:
                methodLogArgSingleProbeRef = logArgMemberRef;
            break;
            case NonAsyncMethodMultiProbe:
                methodLogArgMultiProbeRef = logArgMemberRef;
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

    [[nodiscard]] mdMemberRef GetLogLocalMemberRef(const ProbeType probeType) const
    {
        switch (probeType)
        {
            case NonAsyncMethodSingleProbe:
                return methodLogLocalSingleProbeRef;
            case NonAsyncMethodMultiProbe:
            return methodLogLocalMultiProbeRef;
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
            case NonAsyncMethodSingleProbe:
                methodLogLocalSingleProbeRef = logLocalMemberRef;
            break;
            case NonAsyncMethodMultiProbe:
                methodLogLocalMultiProbeRef = logLocalMemberRef;
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
    const WSTRING& GetCallTargetRefStructType() override;

    void AddAdditionalLocals(TypeSignature* methodReturnValue, std::vector<TypeSignature>* methodTypeArguments,
                             COR_SIGNATURE (&signatureBuffer)[BUFFER_SIZE], ULONG& signatureOffset,
                             ULONG& signatureSize, bool isAsyncMethod) override;
    
public:
    DebuggerTokens(ModuleMetadata* module_metadata_ptr);

    int GetAdditionalLocalsCount(const std::vector<TypeSignature>& methodTypeArguments) override;
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

    HRESULT WriteShouldUpdateProbeInfo(void* rewriterWrapperPtr, ILInstr** instruction, ProbeType probeType);

    HRESULT WriteUpdateProbeInfo(void* rewriterWrapperPtr, const TypeInfo* currentType, ILInstr** instruction,
                                 ProbeType probeType);

    HRESULT WriteRentArray(void* rewriterWrapperPtr, const TypeSignature& currentType, ILInstr** instruction);

    HRESULT GetIsFirstEntryToMoveNextFieldToken(mdToken type, mdFieldDef& token);

    HRESULT WriteDispose(void* rewriterWrapperPtr, ILInstr** instruction, ProbeType probeType);
};

} // namespace debugger

#endif // DD_CLR_PROFILER_DEBUGGER_TOKENS_H_