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
    FunctionInfo* m_functionInfo;
    MethodReplacement* m_methodReplacement;
    std::mutex m_functionsIds_lock;
    std::unordered_set<FunctionID> m_functionsIds;
    RejitHandlerModule* m_module;

public:
    RejitHandlerModuleMethod(mdMethodDef methodDef, RejitHandlerModule* module);
    mdMethodDef GetMethodDef();
    RejitHandlerModule* GetModule();

    ICorProfilerFunctionControl* GetFunctionControl();
    void SetFunctionControl(ICorProfilerFunctionControl* pFunctionControl);

    FunctionInfo* GetFunctionInfo();
    void SetFunctionInfo(FunctionInfo* functionInfo);

    MethodReplacement* GetMethodReplacement();
    void SetMethodReplacement(MethodReplacement* methodReplacement);

    void AddFunctionId(FunctionID functionId);
    bool ExistFunctionId(FunctionID functionId);
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
    std::unordered_map<mdMethodDef, RejitHandlerModuleMethod*> m_methods;
    RejitHandler* m_handler;

public:
    RejitHandlerModule(ModuleID moduleId, RejitHandler* handler);
    ModuleID GetModuleId();
    RejitHandler* GetHandler();

    ModuleMetadata* GetModuleMetadata();
    void SetModuleMetadata(ModuleMetadata* metadata);

    RejitHandlerModuleMethod* GetOrAddMethod(mdMethodDef methodDef);
    bool TryGetMethod(mdMethodDef methodDef, RejitHandlerModuleMethod** methodHandler);
};

/// <summary>
/// Class to control the ReJIT mechanism and to make sure all the required
/// information is present before calling a method rewrite
/// </summary>
class RejitHandler
{
private:
    std::mutex m_modules_lock;
    std::unordered_map<ModuleID, RejitHandlerModule*> m_modules;

    std::mutex m_methodByFunctionId_lock;
    std::unordered_map<FunctionID, RejitHandlerModuleMethod*> m_methodByFunctionId;

    ICorProfilerInfo4* m_profilerInfo;
    std::function<HRESULT(RejitHandlerModule*, RejitHandlerModuleMethod*)> m_rewriteCallback;

    UniqueBlockingQueue<RejitItem>* m_rejit_queue_;
    std::thread* m_rejit_queue_thread_;

    RejitHandlerModuleMethod* GetModuleMethodFromFunctionId(FunctionID functionId);
    static void EnqueueThreadLoop(RejitHandler* handler);

public:
    RejitHandler(ICorProfilerInfo4* pInfo,
                 std::function<HRESULT(RejitHandlerModule*, RejitHandlerModuleMethod*)> rewriteCallback);

    RejitHandlerModule* GetOrAddModule(ModuleID moduleId);

    bool TryGetModule(ModuleID moduleId, RejitHandlerModule** moduleHandler);

    void _addFunctionToSet(FunctionID functionId, RejitHandlerModuleMethod* method);

    void EnqueueForRejit(size_t length, ModuleID* moduleIds, mdMethodDef* methodDefs);
    void Shutdown();

    HRESULT NotifyReJITParameters(ModuleID moduleId, mdMethodDef methodId,
                                  ICorProfilerFunctionControl* pFunctionControl, ModuleMetadata* metadata);
    HRESULT NotifyReJITCompilationStarted(FunctionID functionId, ReJITID rejitId);
};

} // namespace trace

#endif // DD_CLR_PROFILER_REJIT_HANDLER_H_