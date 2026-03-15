// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "TestProfilerCallback.h"
#include "Validation.h"
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

#ifdef _WIN32
#include <windows.h>

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
    // Do not call logger or any file I/O functions from DllMain
    // This can cause deadlocks or crashes
    return TRUE;
}
#endif

// Class factory for creating the profiler
class ClassFactory : public IClassFactory
{
private:
    std::atomic<ULONG> _refCount{0};

public:
    ClassFactory() = default;
    virtual ~ClassFactory() = default;

    // IUnknown
    STDMETHOD(QueryInterface)(REFIID riid, void** ppvObject) override
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

    STDMETHOD_(ULONG, AddRef)() override
    {
        return ++_refCount;
    }

    STDMETHOD_(ULONG, Release)() override
    {
        ULONG refCount = --_refCount;
        if (refCount == 0)
        {
            delete this;
        }
        return refCount;
    }

    // IClassFactory
    STDMETHOD(CreateInstance)(IUnknown* pUnkOuter, REFIID riid, void** ppvObject) override
    {
        Log::Info("CreateInstance called");

        if (pUnkOuter != nullptr)
        {
            return CLASS_E_NOAGGREGATION;
        }

        Log::Info("Creating TestProfilerCallback...");
        auto profiler = new TestProfilerCallback();
        if (profiler == nullptr)
        {
            return E_OUTOFMEMORY;
        }

        Log::Info("Calling QueryInterface...");
        // QueryInterface will AddRef, so we start with refcount 0 and let QI make it 1
        HRESULT hr = profiler->QueryInterface(riid, ppvObject);
        Log::Info("QueryInterface returned: 0x", std::hex, hr);
        return hr;
    }

    STDMETHOD(LockServer)(BOOL fLock) override
    {
        return S_OK;
    }
};

// DLL export for getting the class object
extern "C" PROFILER_EXPORT HRESULT STDMETHODCALLTYPE DllGetClassObject(REFCLSID rclsid, REFIID riid, LPVOID* ppv)
{
    Log::Info("DllGetClassObject called");

    // Profiler GUID: {12345678-ABCD-1234-ABCD-123456789ABC}
    // This should match the GUID set in the environment variables
    const GUID CLSID_TestProfiler = {0x12345678, 0xABCD, 0x1234, {0xAB, 0xCD, 0x12, 0x34, 0x56, 0x78, 0x9A, 0xBC}};

    Log::Info("Checking CLSID...");
    if (rclsid != CLSID_TestProfiler)
    {
        Log::Warn("CLSID mismatch!");
        return CLASS_E_CLASSNOTAVAILABLE;
    }

    Log::Info("Creating ClassFactory...");
    auto factory = new ClassFactory();
    if (factory == nullptr)
    {
        return E_OUTOFMEMORY;
    }

    Log::Info("Calling factory->QueryInterface...");
    // QueryInterface will AddRef, making refcount = 1
    // The CLR will call Release() when it's done with the factory
    return factory->QueryInterface(riid, ppv);
}

#ifdef _WIN32
extern "C" PROFILER_EXPORT HRESULT STDMETHODCALLTYPE DllCanUnloadNow()
{
    return S_OK;
}
#endif
