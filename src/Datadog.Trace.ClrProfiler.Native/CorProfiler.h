#pragma once

#include <corhlpr.h>
#include "IDToInfoMap.h"
#include "ModuleInfo.h"
#include "CorProfilerBase.h"
#include "GlobalTypeReferences.h"

class CorProfiler : public CorProfilerBase
{
private:
    bool bIsAttached = false;
    IDToInfoMap<ModuleID, ModuleInfo> m_moduleIDToInfoMap;

    static HRESULT RewriteIL(ICorProfilerInfo* pICorProfilerInfo,
                             ICorProfilerFunctionControl* pICorProfilerFunctionControl,
                             const IntegrationBase* integration,
                             const MemberReference& instrumentedMethod,
                             ModuleID moduleID,
                             mdToken functionToken,
                             const TypeRefLookup& typeDefLookup,
                             const MemberRefLookup& memberRefLookup,
                             const MemberReference& entryProbe,
                             const MemberReference& exitProbe);

    static void GetClassAndFunctionNamesFromMethodDef(IMetaDataImport* pImport,
                                                      mdMethodDef methodDef,
                                                      LPWSTR wszTypeDefName,
                                                      ULONG cchTypeDefName,
                                                      LPWSTR wszMethodDefName,
                                                      ULONG cchMethodDefName);

    static HRESULT FindAssemblyRef(const std::wstring& assemblyName,
                                   IMetaDataAssemblyImport* pAssemblyImport,
                                   mdAssemblyRef* assemblyRef);

    static HRESULT FindAssemblyRefIterator(const std::wstring& assemblyName,
                                           IMetaDataAssemblyImport* pAssemblyImport,
                                           mdAssemblyRef* rgAssemblyRefs,
                                           ULONG cAssemblyRefs,
                                           mdAssemblyRef* assemblyRef);

    static HRESULT EmitAssemblyRef(IMetaDataAssemblyEmit* pAssemblyEmit, mdAssemblyRef* assemblyRef);

    static HRESULT ResolveTypeReference(const TypeReference& type,
                                        const std::wstring& assemblyName,
                                        IMetaDataImport* metadataImport,
                                        IMetaDataEmit* metadataEmit,
                                        IMetaDataAssemblyImport* assemblyImport,
                                        mdModule module,
                                        TypeRefLookup& typeRefLookup);

    static HRESULT ResolveMemberReference(const MemberReference& method,
                                          IMetaDataImport* metadataImport,
                                          IMetaDataEmit* metadataEmit,
                                          const TypeRefLookup& typeRefLookup,
                                          MemberRefLookup& memberRefLookup);

    // static object
    // Datadog.Trace.ClrProfiler.Instrumentation.OnMethodEntered(int integrationTypeValue,
    //                                                           ulong moduleId,
    //                                                           uint methodToken,
    //                                                           object[] args)
    const MemberReference Datadog_Trace_ClrProfiler_Instrumentation_OnMethodEntered = {
        GlobalTypeReferences.Datadog_Trace_ClrProfiler_Instrumentation,
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
        GlobalTypeReferences.Datadog_Trace_ClrProfiler_Instrumentation,
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
        GlobalTypeReferences.Datadog_Trace_ClrProfiler_Instrumentation,
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
