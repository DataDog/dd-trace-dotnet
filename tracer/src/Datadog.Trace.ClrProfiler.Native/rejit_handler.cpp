#include "rejit_handler.h"

#include "dd_profiler_constants.h"
#include "logger.h"
#include "stats.h"

namespace trace
{

//
// RejitHandlerModuleMethod
//

RejitHandlerModuleMethod::RejitHandlerModuleMethod(mdMethodDef methodDef, RejitHandlerModule* module, const FunctionInfo& functionInfo)
{
    m_methodDef = methodDef;
    SetFunctionInfo(functionInfo);
    m_pFunctionControl = nullptr;
    m_module = module;
}

mdMethodDef RejitHandlerModuleMethod::GetMethodDef()
{
    return m_methodDef;
}

RejitHandlerModule* RejitHandlerModuleMethod::GetModule()
{
    return m_module;
}

ICorProfilerFunctionControl* RejitHandlerModuleMethod::GetFunctionControl()
{
    return m_pFunctionControl;
}

void RejitHandlerModuleMethod::SetFunctionControl(ICorProfilerFunctionControl* pFunctionControl)
{
    m_pFunctionControl = pFunctionControl;
}

FunctionInfo* RejitHandlerModuleMethod::GetFunctionInfo()
{
    return m_functionInfo.get();
}

void RejitHandlerModuleMethod::SetFunctionInfo(const FunctionInfo& functionInfo)
{
    m_functionInfo = std::make_unique<FunctionInfo>(functionInfo);
}

void RejitHandlerModuleMethod::RequestRejitForInlinersInModule(ModuleID moduleId)
{
    Logger::Debug("RejitHandlerModuleMethod::RequestRejitForInlinersInModule: ", moduleId);
    std::lock_guard<std::mutex> guard(m_ngenModulesLock);

    // We check first if we already processed this module to skip it.
    auto find_res = m_ngenModules.find(moduleId);
    if (find_res != m_ngenModules.end())
    {
        return;
    }

    // Enumerate all inliners and request rejit
    ModuleID currentModuleId = m_module->GetModuleId();
    mdMethodDef currentMethodDef = m_methodDef;
    RejitHandler* handler = m_module->GetHandler();
    ICorProfilerInfo7* pInfo = handler->GetCorProfilerInfo();

    if (pInfo != nullptr)
    {
        // Now we enumerate all methods that inline the current methodDef
        BOOL incompleteData = false;
        ICorProfilerMethodEnum* methodEnum;

        HRESULT hr = pInfo->EnumNgenModuleMethodsInliningThisMethod(moduleId, currentModuleId, currentMethodDef,
                                                                    &incompleteData, &methodEnum);
        std::ostringstream hexValue;
        hexValue << std::hex << hr;
        if (SUCCEEDED(hr))
        {
            COR_PRF_METHOD method;
            unsigned int total = 0;
            std::vector<ModuleID> modules;
            std::vector<mdMethodDef> methods;
            while (methodEnum->Next(1, &method, NULL) == S_OK)
            {
                Logger::Debug("NGEN:: Asking rewrite for inliner [ModuleId=", method.moduleId,
                              ",MethodDef=", method.methodId, "]");
                modules.push_back(method.moduleId);
                methods.push_back(method.methodId);
                total++;
            }
            methodEnum->Release();
            methodEnum = nullptr;
            if (total > 0)
            {
                handler->EnqueueForRejit(modules, methods);
                Logger::Info("NGEN:: Processed with ", total, " inliners [ModuleId=", currentModuleId,
                             ",MethodDef=", currentMethodDef, "]");
            }

            if (!incompleteData)
            {
                m_ngenModules[moduleId] = true;
            }
            else
            {
                Logger::Warn("NGen inliner data for module '", moduleId, "' is incomplete.");
            }
        }
        else if (hr == E_INVALIDARG)
        {
            Logger::Info("NGEN:: Error Invalid arguments in [ModuleId=", currentModuleId,
                         ",MethodDef=", currentMethodDef, ", HR=", hexValue.str(), "]");
        }
        else if (hr == CORPROF_E_DATAINCOMPLETE)
        {
            Logger::Info("NGEN:: Error Incomplete data in [ModuleId=", currentModuleId, ",MethodDef=", currentMethodDef,
                         ", HR=", hexValue.str(), "]");
        }
        else if (hr == CORPROF_E_UNSUPPORTED_CALL_SEQUENCE)
        {
            Logger::Info("NGEN:: Unsupported call sequence error in [ModuleId=", currentModuleId, ",MethodDef=", currentMethodDef,
                         ", HR=", hexValue.str(), "]");
        }
        else
        {
            Logger::Info("NGEN:: Error in [ModuleId=", currentModuleId, ",MethodDef=", currentMethodDef,
                         ", HR=", hexValue.str(), "]");
        }
    }
}

//
// TracerRejitHandlerModuleMethod
//

TracerRejitHandlerModuleMethod::TracerRejitHandlerModuleMethod(
    mdMethodDef methodDef, RejitHandlerModule* module, const FunctionInfo& functionInfo,
    const IntegrationDefinition& integrationDefinition) :
    RejitHandlerModuleMethod(methodDef, module, functionInfo),
    m_integrationDefinition(std::make_unique<IntegrationDefinition>(integrationDefinition))
{
}

IntegrationDefinition* TracerRejitHandlerModuleMethod::GetIntegrationDefinition()
{
    return m_integrationDefinition.get();
}

MethodRewriter* TracerRejitHandlerModuleMethod::GetMethodRewriter()
{
    return TracerMethodRewriter::Instance();
}

//
// RejitHandlerModule
//

RejitHandlerModule::RejitHandlerModule(ModuleID moduleId, RejitHandler* handler)
{
    m_moduleId = moduleId;
    m_metadata = nullptr;
    m_handler = handler;
}

ModuleID RejitHandlerModule::GetModuleId()
{
    return m_moduleId;
}

RejitHandler* RejitHandlerModule::GetHandler()
{
    return m_handler;
}

ModuleMetadata* RejitHandlerModule::GetModuleMetadata()
{
    return m_metadata.get();
}

void RejitHandlerModule::SetModuleMetadata(ModuleMetadata* metadata)
{
    m_metadata = std::unique_ptr<ModuleMetadata>(metadata);
}

bool RejitHandlerModule::CreateMethodIfNotExists(const mdMethodDef methodDef,
                                                 RejitHandlerModuleMethodCreatorFunc creator)
{
    std::lock_guard<std::mutex> guard(m_methods_lock);

    auto find_res = m_methods.find(methodDef);
    if (find_res != m_methods.end())
    {
        return false; // already exist and was not created
    }

    m_methods[methodDef] = creator(methodDef, this);
    return true;
}

bool RejitHandlerModule::TryGetMethod(mdMethodDef methodDef, RejitHandlerModuleMethod** methodHandler)
{
    std::lock_guard<std::mutex> guard(m_methods_lock);

    auto find_res = m_methods.find(methodDef);
    if (find_res != m_methods.end())
    {
        *methodHandler = find_res->second.get();
        return true;
    }

    return false;
}

bool RejitHandlerModule::ContainsMethod(mdMethodDef methodDef)
{
    std::lock_guard<std::mutex> guard(m_methods_lock);
    return m_methods.find(methodDef) != m_methods.end();
}

void RejitHandlerModule::RequestRejitForInlinersInModule(ModuleID moduleId)
{
    std::lock_guard<std::mutex> guard(m_methods_lock);
    for (const auto& method : m_methods)
    {
        method.second.get()->RequestRejitForInlinersInModule(moduleId);
    }
}

//
// RejitHandler
//

void RejitHandler::RequestRejitForInlinersInModule(ModuleID moduleId)
{
    if (IsShutdownRequested())
    {
        return;
    }

    std::lock_guard<std::mutex> guard(m_modules_lock);
    if (m_profilerInfo != nullptr)
    {
        for (const auto& mod : m_modules)
        {
            mod.second->RequestRejitForInlinersInModule(moduleId);
        }
    }
}

void RejitHandler::RequestRejit(std::vector<ModuleID>& modulesVector,
                                std::vector<mdMethodDef>& modulesMethodDef)
{
    if (IsShutdownRequested())
    {
        return;
    }

    // Request the ReJIT for all integrations found in the module.
    HRESULT hr;

    if (!modulesVector.empty())
    {
        // *************************************
        // Request ReJIT
        // *************************************

        if (m_profilerInfo10 != nullptr)
        {
            // RequestReJITWithInliners is currently always failing with `Fatal error. Internal CLR error.
            // (0x80131506)` more research is required, meanwhile we fallback to the normal RequestReJIT and
            // manual track of inliners.

            /*hr = m_profilerInfo10->RequestReJITWithInliners(COR_PRF_REJIT_BLOCK_INLINING, (ULONG) modulesVector.size(),
            &modulesVector[0], &modulesMethodDef[0]); if (FAILED(hr))
            {
                Warn("Error requesting ReJITWithInliners for ", vtModules.size(),
                     " methods, falling back to a normal RequestReJIT");
                hr = m_profilerInfo10->RequestReJIT((ULONG) modulesVector.size(), &modulesVector[0], &modulesMethodDef[0]);
            }*/

            hr = m_profilerInfo10->RequestReJIT((ULONG) modulesVector.size(), &modulesVector[0], &modulesMethodDef[0]);
        }
        else
        {
            hr = m_profilerInfo->RequestReJIT((ULONG) modulesVector.size(), &modulesVector[0], &modulesMethodDef[0]);
        }
        if (SUCCEEDED(hr))
        {
            Logger::Info("Request ReJIT done for ", modulesVector.size(), " methods");
        }
        else
        {
            Logger::Warn("Error requesting ReJIT for ", modulesVector.size(), " methods");
        }
    }
}

RejitHandler::RejitHandler(ICorProfilerInfo7* pInfo, std::shared_ptr<RejitWorkOffloader> work_offloader)
{
    m_profilerInfo = pInfo;
    m_profilerInfo10 = nullptr;
    m_work_offloader = work_offloader;
}

RejitHandler::RejitHandler(ICorProfilerInfo10* pInfo, std::shared_ptr<RejitWorkOffloader> work_offloader)
{
    m_profilerInfo = pInfo;
    m_profilerInfo10 = pInfo;
    m_work_offloader = work_offloader;
}

RejitHandlerModule* RejitHandler::GetOrAddModule(ModuleID moduleId)
{
    if (IsShutdownRequested())
    {
        return nullptr;
    }

    std::lock_guard<std::mutex> guard(m_modules_lock);
    auto find_res = m_modules.find(moduleId);
    if (find_res != m_modules.end())
    {
        return find_res->second.get();
    }

    RejitHandlerModule* moduleHandler = new RejitHandlerModule(moduleId, this);
    m_modules[moduleId] = std::unique_ptr<RejitHandlerModule>(moduleHandler);
    return moduleHandler;
}

bool RejitHandler::HasModuleAndMethod(ModuleID moduleId, mdMethodDef methodDef)
{
    if (IsShutdownRequested())
    {
        return false;
    }

    std::lock_guard<std::mutex> guard(m_modules_lock);
    auto find_res = m_modules.find(moduleId);
    if (find_res != m_modules.end())
    {
        auto moduleHandler = find_res->second.get();
        return moduleHandler->ContainsMethod(methodDef);
    }

    return false;
}

void RejitHandler::RemoveModule(ModuleID moduleId)
{
    if (IsShutdownRequested())
    {
        return;
    }

    std::lock_guard<std::mutex> guard(m_modules_lock);
    m_modules.erase(moduleId);
}

void RejitHandler::AddNGenModule(ModuleID moduleId)
{
    if (IsShutdownRequested())
    {
        return;
    }

    std::lock_guard<std::mutex> guard(m_ngenModules_lock);
    m_ngenModules.push_back(moduleId);
    RequestRejitForInlinersInModule(moduleId);
}

void RejitHandler::EnqueueForRejit(std::vector<ModuleID>& modulesVector, std::vector<mdMethodDef>& modulesMethodDef)
{
    if (IsShutdownRequested() || modulesVector.size() == 0 || modulesMethodDef.size() == 0)
    {
        return;
    }

    Logger::Debug("RejitHandler::EnqueueForRejit");

    std::function<void()> action = [=, modules = std::move(modulesVector),
                                    methods = std::move(modulesMethodDef)]() mutable {
        // Request ReJIT
        RequestRejit(modules, methods);
    };

    // Enqueue
    m_work_offloader->Enqueue(std::make_unique<RejitWorkItem>(std::move(action)));
}

void RejitHandler::Shutdown()
{
    Logger::Debug("RejitHandler::Shutdown");

    // Wait for exiting the thread
    m_work_offloader->Enqueue(RejitWorkItem::CreateTerminatingWorkItem());
    m_work_offloader->WaitForTermination();

    std::lock_guard<std::mutex> moduleGuard(m_modules_lock);
    std::lock_guard<std::mutex> ngenModuleGuard(m_ngenModules_lock);

    WriteLock w_lock(m_shutdown_lock);
    m_shutdown.store(true);

    m_modules.clear();
    m_profilerInfo = nullptr;
    m_profilerInfo10 = nullptr;
}

bool RejitHandler::IsShutdownRequested()
{
    ReadLock r_lock(m_shutdown_lock);
    return m_shutdown;
}

HRESULT RejitHandler::NotifyReJITParameters(ModuleID moduleId, mdMethodDef methodId,
                                            ICorProfilerFunctionControl* pFunctionControl)
{
    if (IsShutdownRequested())
    {
        return S_FALSE;
    }

    auto moduleHandler = GetOrAddModule(moduleId);
    if (moduleHandler == nullptr)
    {
        return S_FALSE;
    }

    RejitHandlerModuleMethod* methodHandler = nullptr;
    if (!moduleHandler->TryGetMethod(methodId, &methodHandler))
    {
        return S_FALSE;
    }

    methodHandler->SetFunctionControl(pFunctionControl);

    if (methodHandler->GetMethodDef() == mdMethodDefNil)
    {
        Logger::Warn("NotifyReJITCompilationStarted: mdMethodDef is missing for "
                     "MethodDef: ",
                     methodId);
        return S_FALSE;
    }

    if (methodHandler->GetFunctionControl() == nullptr)
    {
        Logger::Warn("NotifyReJITCompilationStarted: ICorProfilerFunctionControl is missing "
                     "for "
                     "MethodDef: ",
                     methodId);
        return S_FALSE;
    }

    if (methodHandler->GetFunctionInfo() == nullptr)
    {
        Logger::Warn("NotifyReJITCompilationStarted: FunctionInfo is missing for "
                     "MethodDef: ",
                     methodId);
        return S_FALSE;
    }

    if (moduleHandler->GetModuleId() == 0)
    {
        Logger::Warn("NotifyReJITCompilationStarted: ModuleID is missing for "
                     "MethodDef: ",
                     methodId);
        return S_FALSE;
    }

    if (moduleHandler->GetModuleMetadata() == nullptr)
    {
        Logger::Warn("NotifyReJITCompilationStarted: ModuleMetadata is missing for "
                     "MethodDef: ",
                     methodId);
        return S_FALSE;
    }

    auto rewriter = methodHandler->GetMethodRewriter();

    if (rewriter == nullptr)
    {
        Logger::Error("NotifyReJITCompilationStarted: The rewriter is missing for "
                      "MethodDef: ",
                      methodId, ", methodHandler type name = ", typeid(methodHandler).name());
        return S_FALSE;
    }

    return rewriter->Rewrite(moduleHandler, methodHandler);
}

HRESULT RejitHandler::NotifyReJITCompilationStarted(FunctionID functionId, ReJITID rejitId)
{
    return S_OK;
}

ICorProfilerInfo7* RejitHandler::GetCorProfilerInfo()
{
    return m_profilerInfo;
}

void RejitHandler::SetCorAssemblyProfiler(AssemblyProperty* pCorAssemblyProfiler)
{
    m_pCorAssemblyProperty = pCorAssemblyProfiler;
}

AssemblyProperty* RejitHandler::GetCorAssemblyProperty()
{
    return m_pCorAssemblyProperty;
}

void RejitHandler::RequestRejitForNGenInliners()
{
    if (IsShutdownRequested())
    {
        return;
    }

    std::lock_guard<std::mutex> guard(m_ngenModules_lock);
    if (m_profilerInfo != nullptr)
    {
        for (const auto& mod : m_ngenModules)
        {
            RequestRejitForInlinersInModule(mod);
        }
    }
}

void RejitHandler::SetEnableByRefInstrumentation(bool enableByRefInstrumentation)
{
    enable_by_ref_instrumentation = enableByRefInstrumentation;
}

void RejitHandler::SetEnableCallTargetStateByRef(bool enableCallTargetStateByRef)
{
    enable_calltarget_state_by_ref = enableCallTargetStateByRef;
}

bool RejitHandler::GetEnableCallTargetStateByRef()
{
    return enable_calltarget_state_by_ref;
}

bool RejitHandler::GetEnableByRefInstrumentation()
{
    return enable_by_ref_instrumentation;
}

} // namespace trace
