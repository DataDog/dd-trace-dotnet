#ifndef DD_CLR_PROFILER_REJIT_HANDLER_H_
#define DD_CLR_PROFILER_REJIT_HANDLER_H_

#include <atomic>
#include <mutex>
#include <string>
#include <unordered_map>
#include <vector>

#include "cor.h"
#include "corprof.h"
#include "logging.h"
#include "module_metadata.h"

namespace trace
{

struct RejitItem
{
    int length_ = 0;
    ModuleID* moduleIds_ = nullptr;
    mdMethodDef* methodDefs_ = nullptr;

    RejitItem(int length, ModuleID* modulesId, mdMethodDef* methodDefs);
    void DeleteArray();
};

class RejitHandlerModule;
class RejitHandler;

/// <summary>
/// Rejit handler representation of a method
/// </summary>
class RejitHandlerModuleMethod
{
private:
    mdMethodDef methodDef;
    ICorProfilerFunctionControl* pFunctionControl;
    FunctionInfo* functionInfo;
    MethodReplacement* methodReplacement;
    std::mutex functionsIds_lock;
    std::unordered_set<FunctionID> functionsIds;
    RejitHandlerModule* module;

public:
    RejitHandlerModuleMethod(mdMethodDef methodDef, RejitHandlerModule* module)
    {
        this->methodDef = methodDef;
        this->pFunctionControl = nullptr;
        this->module = module;
        this->functionInfo = nullptr;
        this->methodReplacement = nullptr;
    }
    ~RejitHandlerModuleMethod()
    {
        this->pFunctionControl = nullptr;
        this->module = nullptr;
        if (this->functionInfo != nullptr)
        {
            delete this->functionInfo;
            this->functionInfo = nullptr;
        }
        this->methodReplacement = nullptr;
        this->functionsIds.empty();
    }
    mdMethodDef GetMethodDef()
    {
        return this->methodDef;
    }
    ICorProfilerFunctionControl* GetFunctionControl()
    {
        return this->pFunctionControl;
    }
    void SetFunctionControl(ICorProfilerFunctionControl* pFunctionControl)
    {
        this->pFunctionControl = pFunctionControl;
    }
    FunctionInfo* GetFunctionInfo()
    {
        return this->functionInfo;
    }
    void SetFunctionInfo(FunctionInfo* functionInfo)
    {
        this->functionInfo = functionInfo;
    }
    MethodReplacement* GetMethodReplacement()
    {
        return this->methodReplacement;
    }
    void SetMethodReplacement(MethodReplacement* methodReplacement)
    {
        this->methodReplacement = methodReplacement;
    }
    RejitHandlerModule* GetModule()
    {
        return this->module;
    }
    void AddFunctionId(FunctionID functionId);
    bool ExistFunctionId(FunctionID functionId);
};

/// <summary>
/// Rejit handler representation of a module
/// </summary>
class RejitHandlerModule
{
private:
    ModuleID moduleId;
    ModuleMetadata* metadata;
    std::mutex methods_lock;
    std::unordered_map<mdMethodDef, RejitHandlerModuleMethod*> methods;
    RejitHandler* handler;

public:
    RejitHandlerModule(ModuleID moduleId, RejitHandler* handler)
    {
        this->moduleId = moduleId;
        this->metadata = nullptr;
        this->handler = handler;
    }
    ~RejitHandlerModule()
    {
        this->metadata = nullptr;
        this->handler = nullptr;
        for (auto moduleMethod : methods)
        {
            delete moduleMethod.second;
        }
        this->methods.empty();
    }
    ModuleID GetModuleId()
    {
        return this->moduleId;
    }
    ModuleMetadata* GetModuleMetadata()
    {
        return this->metadata;
    }
    void SetModuleMetadata(ModuleMetadata* metadata)
    {
        this->metadata = metadata;
    }
    RejitHandler* GetHandler()
    {
        return this->handler;
    }
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
    std::mutex modules_lock;
    std::unordered_map<ModuleID, RejitHandlerModule*> modules;
    std::mutex methodByFunctionId_lock;
    std::unordered_map<FunctionID, RejitHandlerModuleMethod*> methodByFunctionId;
    ICorProfilerInfo4* profilerInfo;
    std::function<HRESULT(RejitHandlerModule*, RejitHandlerModuleMethod*)> rewriteCallback;

    BlockingQueue<RejitItem>* rejit_queue_;
    std::thread* rejit_queue_thread_;

    RejitHandlerModuleMethod* GetModuleMethodFromFunctionId(FunctionID functionId);

public:
    RejitHandler(ICorProfilerInfo4* pInfo,
                 std::function<HRESULT(RejitHandlerModule*, RejitHandlerModuleMethod*)> rewriteCallback)
    {
        this->profilerInfo = pInfo;
        this->rewriteCallback = rewriteCallback;
        this->rejit_queue_ = new BlockingQueue<RejitItem>();
        this->rejit_queue_thread_ = new std::thread(enqueue_thread, this);
    }
    ~RejitHandler()
    {
        if (this->rejit_queue_ != nullptr)
        {
            delete this->rejit_queue_;
            this->rejit_queue_ = nullptr;
        }
        if (this->rejit_queue_thread_ != nullptr)
        {
            delete this->rejit_queue_thread_;
            this->rejit_queue_thread_ = nullptr;
        }
    }
    RejitHandlerModule* GetOrAddModule(ModuleID moduleId);

    bool TryGetModule(ModuleID moduleId, RejitHandlerModule** moduleHandler);
    void ReleaseModule(ModuleID moduleId);

    HRESULT NotifyReJITParameters(ModuleID moduleId, mdMethodDef methodId,
                                  ICorProfilerFunctionControl* pFunctionControl, ModuleMetadata* metadata);
    HRESULT NotifyReJITCompilationStarted(FunctionID functionId, ReJITID rejitId);
    void _addFunctionToSet(FunctionID functionId, RejitHandlerModuleMethod* method);

    void EnqueueForRejit(size_t length, ModuleID* moduleIds, mdMethodDef* methodDefs)
    {
        rejit_queue_->push(RejitItem((int) length, moduleIds, methodDefs));
    }

    void Shutdown()
    {
        rejit_queue_->push(RejitItem(-1, nullptr, nullptr));
        if (rejit_queue_thread_->joinable())
        {
            rejit_queue_thread_->join();
        }
    }

private:
    static void enqueue_thread(RejitHandler* handler)
    {
        auto queue = handler->rejit_queue_;
        auto profilerInfo = handler->profilerInfo;

        Info("Initializing ReJIT request thread.");
        HRESULT hr = profilerInfo->InitializeCurrentThread();
        if (FAILED(hr))
        {
            Warn("Call to InitializeCurrentThread fail.");
        }

        while (true)
        {
            RejitItem item = queue->pop();

            if (item.length_ == -1)
            {
                break;
            }

            hr = profilerInfo->RequestReJIT((ULONG) item.length_, item.moduleIds_, item.methodDefs_);
            if (SUCCEEDED(hr))
            {
                Info("Request ReJIT done for ", item.length_, " methods");
            }
            else
            {
                Warn("Error requesting ReJIT for ", item.length_, " methods");
            }

            item.DeleteArray();
        }
        Info("Exiting ReJIT request thread.");
    }
};

} // namespace trace

#endif // DD_CLR_PROFILER_REJIT_HANDLER_H_