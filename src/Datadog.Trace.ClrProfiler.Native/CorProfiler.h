#pragma once

#include <corhlpr.h>
#include <corprof.h>
#include "ModuleMetadata.h"
#include "IDToInfoMap.h"
#include "CorProfilerBase.h"

class CorProfiler : public CorProfilerBase
{
private:
    bool bIsAttached = false;
    IDToInfoMap<ModuleID, ModuleMetadata*> m_moduleIDToInfoMap;

public:
    bool IsAttached() const;

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
