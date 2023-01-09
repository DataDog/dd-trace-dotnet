// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "unknwn.h"
#include <atomic>

class CorProfilerCallbackFactory : public IClassFactory
{
public:
    virtual ~CorProfilerCallbackFactory();

    // use STDMETHODCALLTYPE macro to match the CLR declaration.
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppvObject) override;
    ULONG STDMETHODCALLTYPE AddRef() override;
    ULONG STDMETHODCALLTYPE Release() override;
    ULONG STDMETHODCALLTYPE GetRefCount();
    HRESULT STDMETHODCALLTYPE CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppvObject) override;
    HRESULT STDMETHODCALLTYPE LockServer(BOOL fLock) override;

private:
    std::atomic<ULONG> _refCount{0};
};
