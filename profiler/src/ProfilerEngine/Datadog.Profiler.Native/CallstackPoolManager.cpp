// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "CallstackPoolManager.h"

#include "shared/src/native-src/dd_span.hpp"

CallstackPool* CallstackPoolManager::Get(shared::pmr::memory_resource* allocator)
{
    auto pool = std::make_unique<CallstackPool>(allocator);
    _pools.push_back(std::move(pool));
    return _pools.back().get();
}

CallstackPool* CallstackPoolManager::GetDefault()
{
    static auto instance = std::make_unique<CallstackPool>(shared::pmr::get_default_resource());
    return instance.get();
}
