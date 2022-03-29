// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "RefCountingObject.h"
#include "Log.h"

RefCountingObject::~RefCountingObject()
{
}

RefCountingObject::ReferenceCount STDMETHODCALLTYPE RefCountingObject::AddRef(void)
{
    std::uint32_t newInstanceRefCount = _instanceRefCount.fetch_add(1) + 1;
    return static_cast<RefCountingObject::ReferenceCount>(newInstanceRefCount);
}

RefCountingObject::ReferenceCount STDMETHODCALLTYPE RefCountingObject::Release(void)
{
    std::uint32_t newInstanceRefCount = _instanceRefCount.fetch_sub(1) - 1;

    if (0 >= newInstanceRefCount)
    {
        if (0 > newInstanceRefCount)
        {
            Log::Debug("RefCountingObject::Release() encountered newInstanceRefCount=", newInstanceRefCount, ".",
                       " Instance was deleted correctly, but there is likely an AddRef/Release mismatch somewhere.");
        }

        delete this;
    }

    return static_cast<RefCountingObject::ReferenceCount>(newInstanceRefCount);
}

RefCountingObject::ReferenceCount STDMETHODCALLTYPE RefCountingObject::GetRefCount(void) const
{
    std::uint32_t currentInstanceRefCount = _instanceRefCount.load();
    return static_cast<RefCountingObject::ReferenceCount>(currentInstanceRefCount);
}
