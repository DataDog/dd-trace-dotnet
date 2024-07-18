// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include <string>

#include "CorProfilerCallbackFactory.h"
#include "CorProfilerCallback.h"

#include "IConfiguration.h"

std::mutex CorProfilerCallbackFactory::_lock;


CorProfilerCallbackFactory::CorProfilerCallbackFactory(std::shared_ptr<IConfiguration> configuration) :
    _configuration{std::move(configuration)}
{

}

CorProfilerCallbackFactory::~CorProfilerCallbackFactory()
{
}

HRESULT STDMETHODCALLTYPE CorProfilerCallbackFactory::QueryInterface(REFIID riid, void** ppvObject)
{
    if (ppvObject == nullptr)
    {
        return E_POINTER;
    }

    if (riid == __uuidof(IUnknown) || riid == __uuidof(IClassFactory))
    {
        *ppvObject = static_cast<IClassFactory*>(this);
        this->AddRef();
        return S_OK;
    }

    *ppvObject = nullptr;
    return E_NOINTERFACE;
}

ULONG STDMETHODCALLTYPE CorProfilerCallbackFactory::AddRef()
{
    ULONG refCount = _refCount.fetch_add(1) + 1;
    return refCount;
}

ULONG STDMETHODCALLTYPE CorProfilerCallbackFactory::Release()
{
    ULONG refCount = _refCount.fetch_sub(1) - 1;

    if (refCount == 0)
    {
        delete this;
    }

    return refCount;
}

ULONG STDMETHODCALLTYPE CorProfilerCallbackFactory::GetRefCount()
{
    ULONG refCount = _refCount.load();
    return refCount;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallbackFactory::LockServer(BOOL fLock)
{
    return E_NOTIMPL;
}

HRESULT STDMETHODCALLTYPE CorProfilerCallbackFactory::CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppvObject)
{
    if (ppvObject == nullptr)
    {
        return E_POINTER;
    }

    if (pUnkOuter != nullptr)
    {
        *ppvObject = nullptr;
        return CLASS_E_NOAGGREGATION;
    }

    // the scenario where different CLRs are loaded in the same process is not supported
    std::lock_guard<std::mutex> lock(CorProfilerCallbackFactory::_lock);

    auto currentProfiler = CorProfilerCallback::GetInstance();
    if (currentProfiler != nullptr)
    {
        Log::Error(
            "Impossible to initialize the Profiler a second time. The following runtime is already loaded: ",
            currentProfiler->GetRuntimeDescription());

        return E_INVALIDARG;
    }

    CorProfilerCallback* profiler = new (std::nothrow) CorProfilerCallback(_configuration);
    if (profiler == nullptr)
    {
        return E_OUTOFMEMORY;
    }

    HRESULT hr = profiler->QueryInterface(riid, ppvObject);
    if (hr != S_OK)
    {
        delete profiler;
    }

    return hr;
}
