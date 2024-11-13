// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full
// license information.
#include "cor_profiler_class_factory.h"

#include "cor_profiler.h"
#include "log.h"
#include "dynamic_dispatcher.h"

const IID IID_IUnknown = {0x00000000, 0x0000, 0x0000, {0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46}};
const IID IID_IClassFactory = {0x00000001, 0x0000, 0x0000, {0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46}};

CorProfilerClassFactory::CorProfilerClassFactory(datadog::shared::nativeloader::IDynamicDispatcher* dispatcher) :
    m_refCount(0), m_dispatcher(dispatcher)
{
    Log::Debug("CorProfilerClassFactory::.ctor");
}

CorProfilerClassFactory::~CorProfilerClassFactory()
{
}

HRESULT STDMETHODCALLTYPE CorProfilerClassFactory::QueryInterface(REFIID riid, void** ppvObject)
{
    Log::Debug("CorProfilerClassFactory::QueryInterface");
    if (riid == IID_IUnknown || riid == IID_IClassFactory)
    {
        *ppvObject = this;
        this->AddRef();

        // We try to load the class factory of all target cor profilers.
        if (FAILED(m_dispatcher->LoadClassFactory(riid)))
        {
            Log::Warn("Error loading all cor profiler class factories.");
        }

        return S_OK;
    }

    *ppvObject = nullptr;
    return E_NOINTERFACE;
}

ULONG STDMETHODCALLTYPE CorProfilerClassFactory::AddRef()
{
    Log::Debug("CorProfilerClassFactory::AddRef");
    return std::atomic_fetch_add(&this->m_refCount, 1) + 1;
}

ULONG STDMETHODCALLTYPE CorProfilerClassFactory::Release()
{
    Log::Debug("CorProfilerClassFactory::Release");
    int count = std::atomic_fetch_sub(&this->m_refCount, 1) - 1;
    if (count <= 0)
    {
        delete this;
    }

    return count;
}

// profiler entry point
HRESULT STDMETHODCALLTYPE CorProfilerClassFactory::CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppvObject)
{
    Log::Debug("CorProfilerClassFactory::CreateInstance");
    if (pUnkOuter != nullptr)
    {
        *ppvObject = nullptr;
        return CLASS_E_NOAGGREGATION;
    }

    auto profiler = new datadog::shared::nativeloader::CorProfiler(m_dispatcher);
    HRESULT res = profiler->QueryInterface(riid, ppvObject);
    if (SUCCEEDED(res))
    {
        m_dispatcher->LoadInstance(pUnkOuter, riid);
    }
    else
    {
        delete profiler;
    }

    Log::Debug("CorProfilerClassFactory::CreateInstance: ", res);
    return res;
}

HRESULT STDMETHODCALLTYPE CorProfilerClassFactory::LockServer(BOOL fLock)
{
    Log::Debug("CorProfilerClassFactory::LockServer");
    return E_NOTIMPL;
}
