#pragma once

#include <corhlpr.h>
#include <corprof.h>
#include "ModuleMetadata.h"
#include "IDToInfoMap.h"
#include "CorProfilerBase.h"
#include "GlobalTypeReferences.h"

class CorProfiler : public CorProfilerBase
{
private:
    bool bIsAttached = false;
    IDToInfoMap<ModuleID, ModuleMetadata> m_moduleIDToInfoMap;

    static HRESULT RewriteIL(ICorProfilerInfo* pICorProfilerInfo,
                             ICorProfilerFunctionControl* pICorProfilerFunctionControl,
                             const IntegrationBase* integration,
                             const MemberReference& instrumentedMethod,
                             ModuleID moduleID,
                             mdToken functionToken,
                             const ModuleMetadata& metadataHelper,
                             const MemberReference& entryProbe,
                             const MemberReference& exitProbe);

    const TypeReference Datadog_Trace_ClrProfiler_Instrumentation = { ELEMENT_TYPE_CLASS, L"Datadog.Trace.ClrProfiler.Managed", L"Datadog.Trace.ClrProfiler.Instrumentation" };

    // static object
    // Datadog.Trace.ClrProfiler.Instrumentation.OnMethodEntered(int integrationTypeValue,
    //                                                           ulong moduleId,
    //                                                           uint methodToken,
    //                                                           object[] args)
    const MemberReference Datadog_Trace_ClrProfiler_Instrumentation_OnMethodEntered = {
        Datadog_Trace_ClrProfiler_Instrumentation,
        L"OnMethodEntered",
        // IsVirtual
        false,
        IMAGE_CEE_CS_CALLCONV_DEFAULT,
        GlobalTypeReferences.System_Object,
        {
            GlobalTypeReferences.System_Int32,
            GlobalTypeReferences.System_UInt64,
            GlobalTypeReferences.System_UInt32,
            GlobalTypeReferences.System_Object_Array
        }
    };

    // static void
    // Datadog.Trace.ClrProfiler.Instrumentation.OnMethodExit(object args)
    const MemberReference Datadog_Trace_ClrProfiler_Instrumentation_OnMethodExit_ReturnVoid = {
        Datadog_Trace_ClrProfiler_Instrumentation,
        L"OnMethodExit",
        // IsVirtual
        false,
        IMAGE_CEE_CS_CALLCONV_DEFAULT,
        GlobalTypeReferences.System_Void,
        {
            GlobalTypeReferences.System_Object,
        }
    };

    // static object
    // Datadog.Trace.ClrProfiler.Instrumentation.OnMethodExit(object args, object originalReturnValue)
    const MemberReference Datadog_Trace_ClrProfiler_Instrumentation_OnMethodExit_ReturnObject = {
        Datadog_Trace_ClrProfiler_Instrumentation,
        L"OnMethodExit",
        // IsVirtual
        false,
        IMAGE_CEE_CS_CALLCONV_DEFAULT,
        GlobalTypeReferences.System_Object,
        {
            GlobalTypeReferences.System_Object,
            GlobalTypeReferences.System_Object,
        }
    };
public:
    bool IsAttached() const;

    bool GetMetadataNames(ModuleID moduleId,
                          mdMethodDef methodToken,
                          LPWSTR wszModulePath,
                          ULONG cchModulePath,
                          LPWSTR wszTypeDefName,
                          ULONG cchTypeDefName,
                          LPWSTR wszMethodDefName,
                          ULONG cchMethodDefName);

    HRESULT STDMETHODCALLTYPE Initialize(IUnknown* pICorProfilerInfoUnk) override;
    HRESULT STDMETHODCALLTYPE JITCompilationStarted(FunctionID functionId, BOOL fIsSafeToBlock) override;
    HRESULT STDMETHODCALLTYPE ModuleLoadFinished(ModuleID moduleId, HRESULT hrStatus) override;
    HRESULT STDMETHODCALLTYPE ModuleUnloadFinished(ModuleID moduleId, HRESULT hrStatus) override;
};

// Note: Generally you should not have a single, global callback implementation, as that
// prevents your profiler from analyzing multiply loaded in-process side-by-side CLRs.
// However, this profiler implements the "profile-first" alternative of dealing with
// multiple in-process side-by-side CLR instances. First CLR to try to load us into this
// process wins; so there can only be one callback implementation created. (See
// ProfilerCallback::CreateObject.)
extern CorProfiler* g_pCallbackObject; // global reference to callback object
