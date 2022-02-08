// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <atomic>
#include <cstdint>

// from dotnet coreclr includes
#include "cor.h"
// end

/// <summary>
/// Imlements basic COM-line reference counting.
/// However not exactly like COM, so do not use it to implement ref counting
/// for classes that actually implement COM interfaces.
/// You can copy and modify code. Differences:
///  - The typesed for ReferenceCount in COM would/should be ULONG, not uint32_t.
///    We use uint32_t for its well-definedness and we dont ned more.
///  - COM objects should count refs *per Interface* not *per Instance*.
///  - I.e. a class that exposes multiple COM ifaces should count refs for each separately.
///    We do not require and do notimplement this complexity here.
/// </summary>
class RefCountingObject
{
public:
    typedef std::uint32_t ReferenceCount;

public:
    virtual ~RefCountingObject();
    virtual ReferenceCount STDMETHODCALLTYPE AddRef(void);
    virtual ReferenceCount STDMETHODCALLTYPE Release(void);
    virtual ReferenceCount STDMETHODCALLTYPE GetRefCount(void) const;

    template <class T>
    T* STDMETHODCALLTYPE AddRef()
    {
        return AddRef<T>(nullptr);
    }

    template <class T>
    T* STDMETHODCALLTYPE Release()
    {
        return Release<T>(nullptr);
    }

    template <class T>
    T* STDMETHODCALLTYPE AddRef(ReferenceCount* refCountAfterAdd)
    {
        if (refCountAfterAdd == nullptr)
        {
            this->AddRef();
        }
        else
        {
            *refCountAfterAdd = this->AddRef();
        }

        return static_cast<T*>(this);
    }

    template <class T>
    T* STDMETHODCALLTYPE Release(ReferenceCount* refCountAfterRelease)
    {
        if (refCountAfterRelease == nullptr)
        {
            this->Release();
        }
        else
        {
            *refCountAfterRelease = this->Release();
        }

        return static_cast<T*>(refCountAfterRelease > (ReferenceCount*)0 ? this : nullptr);
    }

private:
    std::atomic<std::uint32_t> _instanceRefCount{0};
};
