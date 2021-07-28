#include "rejit_handler.h"

#include "logger.h"

namespace trace
{

//
// RejitItem
//

RejitItem::RejitItem(int length, std::unique_ptr<ModuleID>&& modulesId, std::unique_ptr<mdMethodDef>&& methodDefs)
{
    m_length = length;
    m_modulesId = std::move(modulesId);
    m_methodDefs = std::move(methodDefs);
}

std::unique_ptr<RejitItem> RejitItem::CreateEndRejitThread()
{
    return std::make_unique<RejitItem>(RejitItem(-1, std::unique_ptr<ModuleID>(), std::unique_ptr<mdMethodDef>()));
}


//
// RejitHandlerModuleMethod
//

RejitHandlerModuleMethod::RejitHandlerModuleMethod(mdMethodDef methodDef, RejitHandlerModule* module)
{
    m_methodDef = methodDef;
    m_pFunctionControl = nullptr;
    m_module = module;
    m_functionInfo = nullptr;
    m_methodReplacement = nullptr;
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

MethodReplacement* RejitHandlerModuleMethod::GetMethodReplacement()
{
    return m_methodReplacement.get();
}

void RejitHandlerModuleMethod::SetMethodReplacement(const MethodReplacement& methodReplacement)
{
    m_methodReplacement = std::make_unique<MethodReplacement>(methodReplacement);
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
    return m_metadata;
}

void RejitHandlerModule::SetModuleMetadata(ModuleMetadata* metadata)
{
    m_metadata = metadata;
}

RejitHandlerModuleMethod* RejitHandlerModule::GetOrAddMethod(mdMethodDef methodDef)
{
    std::lock_guard<std::mutex> guard(m_methods_lock);

    auto find_res = m_methods.find(methodDef);
    if (find_res != m_methods.end())
    {
        return find_res->second.get();
    }

    RejitHandlerModuleMethod* methodHandler = new RejitHandlerModuleMethod(methodDef, this);
    m_methods[methodDef] = std::unique_ptr<RejitHandlerModuleMethod>(methodHandler);
    return methodHandler;
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
    *methodHandler = nullptr;
    return false;
}

bool RejitHandlerModule::ContainsMethod(mdMethodDef methodDef)
{
    std::lock_guard<std::mutex> guard(m_methods_lock);
    return m_methods.find(methodDef) != m_methods.end();
}


//
// RejitHandler
//

void RejitHandler::EnqueueThreadLoop(RejitHandler* handler)
{
    auto queue = handler->m_rejit_queue.get();
    auto profilerInfo = handler->m_profilerInfo;

    Logger::Info("Initializing ReJIT request thread.");
    HRESULT hr = profilerInfo->InitializeCurrentThread();
    if (FAILED(hr))
    {
        Logger::Warn("Call to InitializeCurrentThread fail.");
    }

    while (true)
    {
        const auto item = queue->pop();

        if (item->m_length == -1)
        {
            break;
        }

        hr = profilerInfo->RequestReJIT((ULONG) item->m_length, item->m_modulesId.get(), item->m_methodDefs.get());
        if (SUCCEEDED(hr))
        {
            Logger::Info("Request ReJIT done for ", item->m_length, " methods");
        }
        else
        {
            Logger::Warn("Error requesting ReJIT for ", item->m_length, " methods");
        }
    }
    Logger::Info("Exiting ReJIT request thread.");
}

RejitHandler::RejitHandler(ICorProfilerInfo4* pInfo,
             std::function<HRESULT(RejitHandlerModule*, RejitHandlerModuleMethod*)> rewriteCallback)
{
    m_profilerInfo = pInfo;
    m_rewriteCallback = rewriteCallback;
    m_rejit_queue = std::make_unique<UniqueBlockingQueue<RejitItem>>();
    m_rejit_queue_thread = std::make_unique<std::thread>(EnqueueThreadLoop, this);
}


RejitHandlerModule* RejitHandler::GetOrAddModule(ModuleID moduleId)
{
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

bool RejitHandler::TryGetModule(ModuleID moduleId, RejitHandlerModule** moduleHandler)
{
    std::lock_guard<std::mutex> guard(m_modules_lock);

    auto find_res = m_modules.find(moduleId);
    if (find_res != m_modules.end())
    {
        *moduleHandler = find_res->second.get();
        return true;
    }
    *moduleHandler = nullptr;
    return false;
}

void RejitHandler::RemoveModule(ModuleID moduleId)
{
    std::lock_guard<std::mutex> guard(m_modules_lock);
    m_modules.erase(moduleId);
}

void RejitHandler::EnqueueForRejit(std::vector<ModuleID>& modulesVector, std::vector<mdMethodDef>& modulesMethodDef)
{
    const size_t length = modulesMethodDef.size();

    auto moduleIds = new ModuleID[length];
    std::copy(modulesVector.begin(), modulesVector.end(), moduleIds);

    auto mDefs = new mdMethodDef[length];
    std::copy(modulesMethodDef.begin(), modulesMethodDef.end(), mDefs);

    m_rejit_queue->push(std::make_unique<RejitItem>((int) length, std::unique_ptr<ModuleID>(moduleIds),
                                                    std::unique_ptr<mdMethodDef>(mDefs)));
}

void RejitHandler::Shutdown()
{
    m_rejit_queue->push(RejitItem::CreateEndRejitThread());
    if (m_rejit_queue_thread->joinable())
    {
        m_rejit_queue_thread->join();
    }

    m_modules.clear();
    m_profilerInfo = nullptr;
    m_rewriteCallback = nullptr;
}


HRESULT RejitHandler::NotifyReJITParameters(ModuleID moduleId, mdMethodDef methodId,
                                            ICorProfilerFunctionControl* pFunctionControl, ModuleMetadata* metadata)
{
    auto moduleHandler = GetOrAddModule(moduleId);
    moduleHandler->SetModuleMetadata(metadata);
    auto methodHandler = moduleHandler->GetOrAddMethod(methodId);
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

    if (methodHandler->GetMethodReplacement() == nullptr)
    {
        Logger::Warn("NotifyReJITCompilationStarted: MethodReplacement is missing for "
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

    return m_rewriteCallback(moduleHandler, methodHandler);
}

HRESULT RejitHandler::NotifyReJITCompilationStarted(FunctionID functionId, ReJITID rejitId)
{
    return S_OK;
}

} // namespace trace