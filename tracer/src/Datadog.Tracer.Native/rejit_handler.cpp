#include "rejit_handler.h"

#include "cor_profiler.h"
#include "dd_profiler_constants.h"
#include "logger.h"
#include "stats.h"
#include "function_control_wrapper.h"

namespace trace
{

//
// RejitHandlerModuleMethod
//

RejitHandlerModuleMethod::RejitHandlerModuleMethod(mdMethodDef methodDef, RejitHandlerModule* module,
                                                   const FunctionInfo& functionInfo,
                                                   std::unique_ptr<MethodRewriter> methodRewriter) :
    m_methodDef(methodDef),
    m_module(module),
    m_pFunctionControl(nullptr),
    m_functionInfo(std::make_unique<FunctionInfo>(functionInfo)),
    m_methodRewriter(std::move(methodRewriter))
{
}

mdMethodDef RejitHandlerModuleMethod::GetMethodDef()
{
    return m_methodDef;
}

RejitHandlerModule* RejitHandlerModuleMethod::GetModule()
{
    return m_module;
}

FunctionInfo* RejitHandlerModuleMethod::GetFunctionInfo()
{
    return m_functionInfo.get();
}

void RejitHandlerModuleMethod::SetFunctionInfo(const FunctionInfo& functionInfo)
{
    m_functionInfo = std::make_unique<FunctionInfo>(functionInfo);
}

bool RejitHandlerModuleMethod::RequestRejitForInlinersInModule(ModuleID moduleId)
{
    // Enumerate all inliners and request rejit
    ModuleID currentModuleId = m_module->GetModuleId();
    mdMethodDef currentMethodDef = m_methodDef;

#if DEBUG
    // We generate this log hundreds of times, and isn't typically useful in escalations
    Logger::Debug("RejitHandlerModuleMethod::RequestRejitForInlinersInModule for ", "[ModuleInliner=", moduleId,
                  ", ModuleId=", currentModuleId, ", MethodDef=", currentMethodDef, "]");
#endif

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
            while (methodEnum->Next(1, &method, nullptr) == S_OK)
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

            if (incompleteData)
            {
                Logger::Warn("NGen inliner data for module '", moduleId, "' is incomplete.");
                return false;
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

            return false;
        }
        else if (hr == CORPROF_E_UNSUPPORTED_CALL_SEQUENCE)
        {
            Logger::Info("NGEN:: Unsupported call sequence error in [ModuleId=", currentModuleId,
                         ",MethodDef=", currentMethodDef, ", HR=", hexValue.str(), "]");
        }
        else
        {
            Logger::Info("NGEN:: Error in [ModuleId=", currentModuleId, ",MethodDef=", currentMethodDef,
                         ", HR=", hexValue.str(), "]");
        }

        return true;
    }

    return false;
}

MethodRewriter* RejitHandlerModuleMethod::GetMethodRewriter()
{
    return m_methodRewriter.get();
}


//
// RejitHandlerModule
//

RejitHandlerModule::RejitHandlerModule(ModuleID moduleId, RejitHandler* handler) :
    m_moduleId(moduleId), m_handler(handler), m_metadata(nullptr)
{
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
                                                 RejitHandlerModuleMethodCreatorFunc creator,
                                                 RejitHandlerModuleMethodUpdaterFunc updater)
{
    std::lock_guard<std::mutex> guard(m_methods_lock);

    auto find_res = m_methods.find(methodDef);
    if (find_res != m_methods.end())
    {
        updater(find_res->second.get());
        return false; // already exist and was not created
    }

    auto newModuleInfo = creator(methodDef, this);
    updater(newModuleInfo.get());
    m_methods[methodDef] = std::move(newModuleInfo);
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
    std::lock_guard<std::mutex> moduleGuard(m_ngenProcessedInlinerModulesLock);

    // We check first if we already processed this module to skip it.
    auto find_res = m_ngenProcessedInlinerModules.find(moduleId);
    if (find_res != m_ngenProcessedInlinerModules.end())
    {
        return;
    }

    std::lock_guard<std::mutex> methodsGuard(m_methods_lock);
    bool success = true;
    for (const auto& method : m_methods)
    {
        success = success && method.second.get()->RequestRejitForInlinersInModule(moduleId);
        // If we fail to process a method, we stop the processing and try again in another call.
        if (!success)
        {
            break;
        }
    }

    if (success)
    {
        // We mark module as processed.
        m_ngenProcessedInlinerModules[moduleId] = true;
    }
}

//
// RejitHandler
//

void RejitHandler::RequestRejit(std::vector<ModuleID>& modulesVector, std::vector<mdMethodDef>& modulesMethodDef, bool callRevertExplicitly)
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

        if (callRevertExplicitly)
        {
            HRESULT* status = nullptr;
            m_profilerInfo->RequestRevert((ULONG) modulesVector.size(), &modulesVector[0], &modulesMethodDef[0], status);
        }

        if (m_profilerInfo10 != nullptr)
        {
            // RequestReJITWithInliners is currently always failing with `Fatal error. Internal CLR error.
            // (0x80131506)` more research is required, meanwhile we fallback to the normal RequestReJIT and
            // manual track of inliners.

            /*hr = m_profilerInfo10->RequestReJITWithInliners(COR_PRF_REJIT_BLOCK_INLINING, (ULONG)
            modulesVector.size(), &modulesVector[0], &modulesMethodDef[0]); if (FAILED(hr))
            {
                Warn("Error requesting ReJITWithInliners for ", vtModules.size(),
                     " methods, falling back to a normal RequestReJIT");
                hr = m_profilerInfo10->RequestReJIT((ULONG) modulesVector.size(), &modulesVector[0],
            &modulesMethodDef[0]);
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

void RejitHandler::EnqueueRequestRejit(std::vector<MethodIdentifier>& rejitRequests,
                                       std::shared_ptr<std::promise<void>> promise, 
                                       bool callRevertExplicitly)
{
    std::vector<ModuleID> modulesVector;
    std::vector<mdMethodDef> methodsVector;

    for (const auto& request : rejitRequests)
    {
        modulesVector.push_back(request.moduleId);
        methodsVector.push_back(request.methodToken);
    }

    EnqueueForRejit(modulesVector, methodsVector, promise, callRevertExplicitly);
}

RejitHandler::RejitHandler(ICorProfilerInfo7* pInfo, std::shared_ptr<RejitWorkOffloader> work_offloader) :
    m_profilerInfo(pInfo), m_profilerInfo10(nullptr), m_work_offloader(work_offloader)
{
}

RejitHandler::RejitHandler(ICorProfilerInfo10* pInfo, std::shared_ptr<RejitWorkOffloader> work_offloader) :
    m_profilerInfo(pInfo), m_profilerInfo10(pInfo), m_work_offloader(work_offloader)
{
}

void RejitHandler::EnqueueForRejit(std::vector<ModuleID>& modulesVector, std::vector<mdMethodDef>& modulesMethodDef,
                                   std::shared_ptr<std::promise<void>> promise, bool callRevertExplicitly)
{
    if (IsShutdownRequested() || modulesVector.size() == 0 || modulesMethodDef.size() == 0)
    {
        if (promise != nullptr)
        {
            promise->set_value();
        }

        return;
    }

    Logger::Debug("RejitHandler::EnqueueForRejit");

    std::function<void()> action = [=, modules = std::move(modulesVector), methods = std::move(modulesMethodDef),
                                    localPromise = promise, callRevertExplicitly = callRevertExplicitly]() mutable {
        // Request ReJIT
        RequestRejit(modules, methods, callRevertExplicitly);

        // Resolve promise
        if (localPromise != nullptr)
        {
            localPromise->set_value();
        }
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

    WriteLock w_lock(m_shutdown_lock);
    m_shutdown.store(true);

    for (auto rejitter : m_rejitters)
    {
        rejitter->Shutdown();
    }

    m_profilerInfo = nullptr;
    m_profilerInfo10 = nullptr;
}

bool RejitHandler::IsShutdownRequested()
{
    ReadLock r_lock(m_shutdown_lock);
    return m_shutdown;
}

void RejitHandler::RegisterRejitter(Rejitter* rejitter)
{
    if (m_rejitters.size() == 0)
    {
        m_rejitters.push_back(rejitter);
    }
    else
    {
        auto it = m_rejitters.begin();
        for (; it < m_rejitters.end(); it++)
        {
            if ((*it)->GetPriority() > rejitter->GetPriority())
            {
                break;
            }
        }
        m_rejitters.insert(it, rejitter);
    }
}

HRESULT RejitHandler::NotifyReJITParameters(ModuleID moduleId, mdMethodDef methodId, ICorProfilerFunctionControl* pFunctionControl)
{
    if (IsShutdownRequested())
    {
        return S_FALSE;
    }

    HRESULT hr = S_OK;
    LPCBYTE originalMehodBody = nullptr;
    ULONG originalMehodLen = 0;

    // Create the FunctionControlWrapper
    FunctionControlWrapper functionControl((ICorProfilerInfo*)m_profilerInfo, moduleId, methodId);

    // Call all rejitters sequentially
    for (auto rejitter : m_rejitters)
    {
        hr = rejitter->RejitMethod(functionControl);
    }

    return functionControl.ApplyChanges(pFunctionControl);
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

bool RejitHandler::HasModuleAndMethod(ModuleID moduleId, mdMethodDef methodDef)
{
    if (IsShutdownRequested())
    {
        return false;
    }

    for (auto rejitter : m_rejitters)
    {
        if (rejitter->HasModuleAndMethod(moduleId, methodDef))
        {
            return true;
        }
    }

    return false;
}

void RejitHandler::RemoveModule(ModuleID moduleId)
{
    if (IsShutdownRequested())
    {
        return;
    }

    for (auto rejitter : m_rejitters)
    {
        rejitter->RemoveModule(moduleId);
    }
}

void RejitHandler::AddNGenInlinerModule(ModuleID moduleId)
{
    if (IsShutdownRequested())
    {
        return;
    }

    for (auto rejitter : m_rejitters)
    {
        rejitter->AddNGenInlinerModule(moduleId);
    }
}

} // namespace trace
