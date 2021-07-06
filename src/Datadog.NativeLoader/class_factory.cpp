// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full
// license information.
#include "class_factory.h"

#include "cor_profiler.h"
#include "logging.h"
#include "proxy.h"

const IID IID_IUnknown = {0x00000000, 0x0000, 0x0000, {0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46}};
const IID IID_IClassFactory = {0x00000001, 0x0000, 0x0000, {0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46}};

ClassFactory::ClassFactory(datadog::nativeloader::DynamicDispatcher* dispatcher) : m_refCount(0), m_dispatcher(dispatcher)
{
    Debug("ClassFactory::.ctor");
}

ClassFactory::~ClassFactory()
{
}

HRESULT STDMETHODCALLTYPE ClassFactory::QueryInterface(REFIID riid, void** ppvObject)
{
    Debug("ClassFactory::QueryInterface");
    HRESULT res = m_dispatcher->LoadClassFactory(riid);

    if ((riid == IID_IUnknown || riid == IID_IClassFactory) && SUCCEEDED(res))
    {
        *ppvObject = this;
        this->AddRef();

        return S_OK;
    }

    *ppvObject = nullptr;
    return E_NOINTERFACE;
}

ULONG STDMETHODCALLTYPE ClassFactory::AddRef()
{
    Debug("ClassFactory::AddRef");
    return std::atomic_fetch_add(&this->m_refCount, 1) + 1;
}

ULONG STDMETHODCALLTYPE ClassFactory::Release()
{
    Debug("ClassFactory::Release");
    int count = std::atomic_fetch_sub(&this->m_refCount, 1) - 1;
    if (count <= 0)
    {
        delete this;
    }

    return count;
}

// profiler entry point
HRESULT STDMETHODCALLTYPE ClassFactory::CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppvObject)
{
    Debug("ClassFactory::CreateInstance");
    if (pUnkOuter != nullptr)
    {
        *ppvObject = nullptr;
        return CLASS_E_NOAGGREGATION;
    }

    auto profiler = new datadog::nativeloader::CorProfiler(m_dispatcher);
    HRESULT res = profiler->QueryInterface(riid, ppvObject);
    if (SUCCEEDED(res))
    {
        m_dispatcher->LoadInstance(pUnkOuter, riid);
    }
    Debug("ClassFactory::CreateInstance: ", res);
    return res;
}

HRESULT STDMETHODCALLTYPE ClassFactory::LockServer(BOOL fLock)
{
    Debug("ClassFactory::LockServer");
    return S_OK;
}
