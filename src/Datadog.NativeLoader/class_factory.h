#ifndef CLASS_FACTORY_H_
#define CLASS_FACTORY_H_

// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full
// license information.

#include <atomic>
#include "unknwn.h"
#include "proxy.h"

class ClassFactory : public IClassFactory
{
private:
    std::atomic<int> m_refCount;
    datadog::nativeloader::DynamicDispatcher* m_dispatcher;

public:
    ClassFactory(datadog::nativeloader::DynamicDispatcher* dispatcher);
    virtual ~ClassFactory();
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppvObject) override;
    ULONG STDMETHODCALLTYPE AddRef(void) override;
    ULONG STDMETHODCALLTYPE Release(void) override;
    HRESULT STDMETHODCALLTYPE CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppvObject) override;
    HRESULT STDMETHODCALLTYPE LockServer(BOOL fLock) override;
};

#endif