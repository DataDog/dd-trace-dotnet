#include "rejit_handler.h"

#include "logging.h"

namespace trace
{

//
// RejitItem
//

RejitItem::RejitItem(int length, std::unique_ptr<ModuleID>&& modulesId, std::unique_ptr<mdMethodDef>&& methodDefs)
{
    m_length = length;
    if (length > 0)
    {
        m_modulesId = std::move(modulesId);
        m_methodDefs = std::move(m_methodDefs);
    }
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
    return m_functionInfo;
}

void RejitHandlerModuleMethod::SetFunctionInfo(FunctionInfo* functionInfo)
{
    m_functionInfo = functionInfo;
}

MethodReplacement* RejitHandlerModuleMethod::GetMethodReplacement()
{
    return m_methodReplacement;
}

void RejitHandlerModuleMethod::SetMethodReplacement(MethodReplacement* methodReplacement)
{
    m_methodReplacement = methodReplacement;
}

void RejitHandlerModuleMethod::AddFunctionId(FunctionID functionId)
{
    std::lock_guard<std::mutex> guard(m_functionsIds_lock);
    m_functionsIds.insert(functionId);
    m_module->GetHandler()->_addFunctionToSet(functionId, this);
}
bool RejitHandlerModuleMethod::ExistFunctionId(FunctionID functionId)
{
    std::lock_guard<std::mutex> guard(m_functionsIds_lock);
    return m_functionsIds.find(functionId) != m_functionsIds.end();
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
        return find_res->second;
    }

    RejitHandlerModuleMethod* methodHandler = new RejitHandlerModuleMethod(methodDef, this);
    m_methods[methodDef] = methodHandler;
    return methodHandler;
}
bool RejitHandlerModule::TryGetMethod(mdMethodDef methodDef, RejitHandlerModuleMethod** methodHandler)
{
    std::lock_guard<std::mutex> guard(m_methods_lock);

    auto find_res = m_methods.find(methodDef);
    if (find_res != m_methods.end())
    {
        *methodHandler = find_res->second;
        return true;
    }
    *methodHandler = nullptr;
    return false;
}

//
// RejitHandler
//

RejitHandlerModuleMethod* RejitHandler::GetModuleMethodFromFunctionId(FunctionID functionId)
{
    {
        std::lock_guard<std::mutex> guard(m_methodByFunctionId_lock);
        auto find_res = m_methodByFunctionId.find(functionId);
        if (find_res != m_methodByFunctionId.end())
        {
            return find_res->second;
        }
    }

    ModuleID moduleId;
    mdToken function_token = mdTokenNil;

    HRESULT hr = m_profilerInfo->GetFunctionInfo(functionId, nullptr, &moduleId, &function_token);

    if (FAILED(hr))
    {
        Warn("RejitHandler::GetModuleMethodFromFunctionId: Call to "
             "ICorProfilerInfo4.GetFunctionInfo() "
             "failed for ",
             functionId);
        m_methodByFunctionId[functionId] = nullptr;
        return nullptr;
    }

    auto moduleHandler = GetOrAddModule(moduleId);
    auto methodHandler = moduleHandler->GetOrAddMethod(function_token);
    methodHandler->AddFunctionId(functionId);
    return methodHandler;
}

void RejitHandler::EnqueueThreadLoop(RejitHandler* handler)
{
    auto queue = handler->m_rejit_queue_;
    auto profilerInfo = handler->m_profilerInfo;

    Info("Initializing ReJIT request thread.");
    HRESULT hr = profilerInfo->InitializeCurrentThread();
    if (FAILED(hr))
    {
        Warn("Call to InitializeCurrentThread fail.");
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
            Info("Request ReJIT done for ", item->m_length, " methods");
        }
        else
        {
            Warn("Error requesting ReJIT for ", item->m_length, " methods");
        }
    }
    Info("Exiting ReJIT request thread.");
}

RejitHandler::RejitHandler(ICorProfilerInfo4* pInfo,
             std::function<HRESULT(RejitHandlerModule*, RejitHandlerModuleMethod*)> rewriteCallback)
{
    m_profilerInfo = pInfo;
    m_rewriteCallback = rewriteCallback;
    m_rejit_queue_ = new UniqueBlockingQueue<RejitItem>();
    m_rejit_queue_thread_ = new std::thread(EnqueueThreadLoop, this);
}


RejitHandlerModule* RejitHandler::GetOrAddModule(ModuleID moduleId)
{
    std::lock_guard<std::mutex> guard(m_modules_lock);

    auto find_res = m_modules.find(moduleId);
    if (find_res != m_modules.end())
    {
        return find_res->second;
    }

    RejitHandlerModule* moduleHandler = new RejitHandlerModule(moduleId, this);
    m_modules[moduleId] = moduleHandler;
    return moduleHandler;
}

bool RejitHandler::TryGetModule(ModuleID moduleId, RejitHandlerModule** moduleHandler)
{
    std::lock_guard<std::mutex> guard(m_modules_lock);

    auto find_res = m_modules.find(moduleId);
    if (find_res != m_modules.end())
    {
        *moduleHandler = find_res->second;
        return true;
    }
    *moduleHandler = nullptr;
    return false;
}

void RejitHandler::_addFunctionToSet(FunctionID functionId, RejitHandlerModuleMethod* method)
{
    std::lock_guard<std::mutex> guard(m_methodByFunctionId_lock);
    m_methodByFunctionId[functionId] = method;
}

void RejitHandler::EnqueueForRejit(size_t length, ModuleID* moduleIds, mdMethodDef* methodDefs)
{
    m_rejit_queue_->push(std::make_unique<RejitItem>((int) length, std::unique_ptr<ModuleID>(moduleIds),
                                                     std::unique_ptr<mdMethodDef>(methodDefs)));
}

void RejitHandler::Shutdown()
{
    m_rejit_queue_->push(RejitItem::CreateEndRejitThread());
    if (m_rejit_queue_thread_->joinable())
    {
        m_rejit_queue_thread_->join();
    }
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
        Warn("NotifyReJITCompilationStarted: mdMethodDef is missing for "
             "MethodDef: ",
             methodId);
        return S_FALSE;
    }

    if (methodHandler->GetFunctionControl() == nullptr)
    {
        Warn("NotifyReJITCompilationStarted: ICorProfilerFunctionControl is missing "
             "for "
             "MethodDef: ",
             methodId);
        return S_FALSE;
    }

    if (methodHandler->GetFunctionInfo() == nullptr)
    {
        Warn("NotifyReJITCompilationStarted: FunctionInfo is missing for "
             "MethodDef: ",
             methodId);
        return S_FALSE;
    }

    if (methodHandler->GetMethodReplacement() == nullptr)
    {
        Warn("NotifyReJITCompilationStarted: MethodReplacement is missing for "
             "MethodDef: ",
             methodId);
        return S_FALSE;
    }

    if (moduleHandler->GetModuleId() == 0)
    {
        Warn("NotifyReJITCompilationStarted: ModuleID is missing for "
             "MethodDef: ",
             methodId);
        return S_FALSE;
    }

    if (moduleHandler->GetModuleMetadata() == nullptr)
    {
        Warn("NotifyReJITCompilationStarted: ModuleMetadata is missing for "
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