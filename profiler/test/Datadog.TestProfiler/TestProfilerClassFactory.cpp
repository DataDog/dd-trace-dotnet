// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "TestProfilerClassFactory.h"
#include "TestProfilerCallback.h"
#include "Log.h"

// COM GUIDs needed for Linux
const IID IID_IUnknown = {0x00000000,
                          0x0000,
                          0x0000,
                          {0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46}};

const IID IID_IClassFactory = {
    0x00000001,
    0x0000,
    0x0000,
    {0xC0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x46}};

HRESULT STDMETHODCALLTYPE TestProfilerClassFactory::QueryInterface(REFIID riid, void** ppvObject)
{
    if (ppvObject == nullptr)
    {
        return E_POINTER;
    }

    if (riid == IID_IUnknown || riid == IID_IClassFactory)
    {
        *ppvObject = static_cast<IClassFactory*>(this);
        this->AddRef();
        return S_OK;
    }

    *ppvObject = nullptr;
    return E_NOINTERFACE;
}

ULONG STDMETHODCALLTYPE TestProfilerClassFactory::AddRef()
{
    return ++_refCount;
}

ULONG STDMETHODCALLTYPE TestProfilerClassFactory::Release()
{
    ULONG refCount = --_refCount;
    if (refCount == 0)
    {
        delete this;
    }
    return refCount;
}

HRESULT STDMETHODCALLTYPE TestProfilerClassFactory::CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppvObject)
{
    Log::Info("TestProfilerClassFactory::CreateInstance called");

    if (pUnkOuter != nullptr)
    {
        return CLASS_E_NOAGGREGATION;
    }

    Log::Info("TestProfilerClassFactory::CreateInstance: Creating TestProfilerCallback...");
    auto profiler = new TestProfilerCallback();
    if (profiler == nullptr)
    {
        return E_OUTOFMEMORY;
    }

    Log::Info("TestProfilerClassFactory::CreateInstance: Calling QueryInterface...");
    // QueryInterface will AddRef, so we start with refcount 0 and let QI make it 1
    HRESULT hr = profiler->QueryInterface(riid, ppvObject);
    Log::Info("TestProfilerClassFactory::CreateInstance: QueryInterface returned: 0x", std::hex, hr);
    return hr;
}

HRESULT STDMETHODCALLTYPE TestProfilerClassFactory::LockServer(BOOL fLock)
{
    return E_NOTIMPL;
}
