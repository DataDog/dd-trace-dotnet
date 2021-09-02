#ifndef DD_CLR_PROFILER_REJIT_HANDLER_H_
#define DD_CLR_PROFILER_REJIT_HANDLER_H_

#include <atomic>
#include <mutex>
#include <string>
#include <unordered_map>
#include <vector>

#include "cor.h"
#include "corprof.h"
#include "module_metadata.h"

namespace trace
{

struct RejitItem
{
    int m_length = 0;
    std::unique_ptr<ModuleID> m_modulesId = nullptr;
    std::unique_ptr<mdMethodDef> m_methodDefs = nullptr;

    RejitItem(int length, std::unique_ptr<ModuleID>&& modulesId, std::unique_ptr<mdMethodDef>&& methodDefs);
    static std::unique_ptr<RejitItem> CreateEndRejitThread();
};

// forward declarations...
class RejitHandlerModule;
class RejitHandler;

/// <summary>
/// Rejit handler representation of a method
/// </summary>
class RejitHandlerModuleMethod
{
private:
    mdMethodDef m_methodDef;
    ICorProfilerFunctionControl* m_pFunctionControl;
    std::unique_ptr<FunctionInfo> m_functionInfo;
    std::unique_ptr<MethodReplacement> m_methodReplacement;

    std::mutex m_ngenModulesLock;
    std::unordered_map<ModuleID, bool> m_ngenModules;

    RejitHandlerModule* m_module;

public:
    RejitHandlerModuleMethod(mdMethodDef methodDef, RejitHandlerModule* module);
    mdMethodDef GetMethodDef();
    RejitHandlerModule* GetModule();

    ICorProfilerFunctionControl* GetFunctionControl();
    void SetFunctionControl(ICorProfilerFunctionControl* pFunctionControl);

    FunctionInfo* GetFunctionInfo();
    void SetFunctionInfo(const FunctionInfo& functionInfo);

    MethodReplacement* GetMethodReplacement();
    void SetMethodReplacement(const MethodReplacement& methodReplacement);

    void RequestRejitForInlinersInModule(ModuleID moduleId);
};

/// <summary>
/// Rejit handler representation of a module
/// </summary>
class RejitHandlerModule
{
private:
    ModuleID m_moduleId;
    ModuleMetadata* m_metadata;
    std::mutex m_methods_lock;
    std::unordered_map<mdMethodDef, std::unique_ptr<RejitHandlerModuleMethod>> m_methods;
    RejitHandler* m_handler;

public:
    RejitHandlerModule(ModuleID moduleId, RejitHandler* handler);
    ModuleID GetModuleId();
    RejitHandler* GetHandler();

    ModuleMetadata* GetModuleMetadata();
    void SetModuleMetadata(ModuleMetadata* metadata);

    RejitHandlerModuleMethod* GetOrAddMethod(mdMethodDef methodDef);
    bool TryGetMethod(mdMethodDef methodDef, RejitHandlerModuleMethod** methodHandler);
    bool ContainsMethod(mdMethodDef methodDef);

    void RequestRejitForInlinersInModule(ModuleID moduleId);
};

/// <summary>
/// Class to control the ReJIT mechanism and to make sure all the required
/// information is present before calling a method rewrite
/// </summary>
class RejitHandler
{
private:
    std::mutex m_modules_lock;
    std::unordered_map<ModuleID, std::unique_ptr<RejitHandlerModule>> m_modules;

    ICorProfilerInfo4* m_profilerInfo;
    ICorProfilerInfo6* m_profilerInfo6;
    ICorProfilerInfo10* m_profilerInfo10;
    std::function<HRESULT(RejitHandlerModule*, RejitHandlerModuleMethod*)> m_rewriteCallback;

    std::unique_ptr<UniqueBlockingQueue<RejitItem>> m_rejit_queue;
    std::unique_ptr<std::thread> m_rejit_queue_thread;

    std::mutex m_ngenModules_lock;
    std::vector<ModuleID> m_ngenModules;

    static void EnqueueThreadLoop(RejitHandler* handler);

    void RequestRejitForInlinersInModule(ModuleID moduleId);

public:
    RejitHandler(ICorProfilerInfo4* pInfo,
                 std::function<HRESULT(RejitHandlerModule*, RejitHandlerModuleMethod*)> rewriteCallback);
    RejitHandler(ICorProfilerInfo6* pInfo,
                 std::function<HRESULT(RejitHandlerModule*, RejitHandlerModuleMethod*)> rewriteCallback);
    RejitHandler(ICorProfilerInfo10* pInfo,
                 std::function<HRESULT(RejitHandlerModule*, RejitHandlerModuleMethod*)> rewriteCallback);

    RejitHandlerModule* GetOrAddModule(ModuleID moduleId);

    bool TryGetModule(ModuleID moduleId, RejitHandlerModule** moduleHandler);
    void RemoveModule(ModuleID moduleId);

    void AddNGenModule(ModuleID moduleId);

    void EnqueueForRejit(std::vector<ModuleID>& modulesVector, std::vector<mdMethodDef>& modulesMethodDef);
    void Shutdown();

    HRESULT NotifyReJITParameters(ModuleID moduleId, mdMethodDef methodId,
                                  ICorProfilerFunctionControl* pFunctionControl, ModuleMetadata* metadata);
    HRESULT NotifyReJITCompilationStarted(FunctionID functionId, ReJITID rejitId);

    ICorProfilerInfo4* GetCorProfilerInfo();
    ICorProfilerInfo6* GetCorProfilerInfo6();

    void RequestRejitForNGenInliners();
};

} // namespace trace

#endif // DD_CLR_PROFILER_REJIT_HANDLER_H_